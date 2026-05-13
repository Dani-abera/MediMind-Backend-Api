# MediMind Telemedicine — Frontend Implementation Guide

**Covers:** FR-017 (Video Consultation) · FR-018 (Text Chat)  
**Backend:** ASP.NET Core 10 · SignalR · WebRTC signaling  
**Applies to:** Patient mobile app · Doctor role-based portal

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Prerequisites & Dependencies](#2-prerequisites--dependencies)
3. [Doctor Portal — Full Workflow](#3-doctor-portal--full-workflow)
4. [Patient App — Full Workflow](#4-patient-app--full-workflow)
5. [WebRTC Signaling Reference](#5-webrtc-signaling-reference)
6. [Media Controls Implementation](#6-media-controls-implementation)
7. [In-Session Chat Implementation](#7-in-session-chat-implementation)
8. [Connection Quality Monitoring](#8-connection-quality-monitoring)
9. [Error Handling Reference](#9-error-handling-reference)
10. [SignalR Events Quick Reference](#10-signalr-events-quick-reference)

---

## 1. Architecture Overview

```
Patient App / Doctor Portal
        │
        ├── REST API (HTTP)          ← Session lifecycle, chat history, quality reports
        │   POST /api/v1/video-consultations/initiate
        │   POST /api/v1/video-consultations/{id}/join
        │   POST /api/v1/video-consultations/{id}/end
        │   POST /api/v1/video-consultations/{id}/quality-report
        │   GET  /api/v1/video-consultations/{id}/chat
        │
        ├── SignalR Hub (WebSocket)   ← Real-time signaling + chat
        │   wss://api.medimind.et/hubs/video?access_token=<JWT>
        │
        └── WebRTC (P2P)             ← Actual audio/video media (peer-to-peer)
            STUN: stun.l.google.com:19302
```

**Key principle:** REST manages session state. SignalR carries WebRTC signaling messages and chat. The actual media (video/audio) streams are peer-to-peer WebRTC — the server never sees the media.

---

## 2. Prerequisites & Dependencies

### Web / React Portal (Doctor)

```bash
npm install @microsoft/signalr
# No extra WebRTC library needed — it is native in all modern browsers
```

### Flutter / Mobile (Patient App)

```yaml
dependencies:
  signalr_netcore: ^1.3.5      # SignalR client
  flutter_webrtc: ^0.9.x       # WebRTC wrapper (uses native APIs)
```

### Auth requirement

Every REST call and the SignalR connection require a valid **Bearer JWT** with `user_type` claim set to `Doctor` or `Patient`. Obtain the token from the OTP verification flow before entering the call screen.

---

## 3. Doctor Portal — Full Workflow

### 3.1 Screen: Appointment Detail → "Start Video Call" Button

The button appears only when:
- `appointment.status === "Confirmed"`
- No active consultation exists yet for this appointment

```typescript
// Check if a consultation already exists before showing "Start" vs "Rejoin"
const res = await fetch(`/api/v1/video-consultations/appointment/${appointmentId}`, {
  headers: { Authorization: `Bearer ${token}` }
});
if (res.ok) {
  // Consultation exists → show "Rejoin" button
} else if (res.status === 404) {
  // No consultation yet → show "Start Video Call" button
}
```

### 3.2 Step 1 — Initiate Session (Doctor only)

```typescript
const res = await fetch('/api/v1/video-consultations/initiate', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
  body: JSON.stringify({ appointmentId })
});

if (!res.ok) {
  const problem = await res.json();
  // Handle: 400 = appointment not confirmed or duplicate session
  // Handle: 403 = not the assigned doctor
  showError(problem.detail);
  return;
}

const session = await res.json();
// session: ConsultationSessionDto
// Store: session.consultationId, session.iceServers
```

A push notification is automatically sent to the patient at this point.

### 3.3 Step 2 — Request Camera & Microphone

Always request media **before** connecting to SignalR. If permission is denied, there is no point joining the room.

```typescript
let localStream: MediaStream;
try {
  localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
  localVideoElement.srcObject = localStream;
} catch (err) {
  if (err instanceof DOMException && err.name === 'NotAllowedError') {
    showError('Please enable camera and microphone permissions to join video call');
    return;
  }
  showError('Could not access camera or microphone.');
  return;
}
```

### 3.4 Step 3 — Connect to SignalR Hub

```typescript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`wss://api.medimind.et/hubs/video`, {
    accessTokenFactory: () => token
  })
  .withAutomaticReconnect()
  .build();

// Register all event handlers BEFORE starting
registerSignalRHandlers(connection, session.consultationId, session.iceServers, localStream);

await connection.start();
await connection.invoke('JoinConsultationRoom', session.consultationId);
```

### 3.5 Step 4 — Call Screen UI

The doctor call screen must have:

| Control | Action |
|---|---|
| Mute / Unmute mic | `localStream.getAudioTracks()[0].enabled = !muted` + `BroadcastMediaState` |
| Enable / Disable camera | `localStream.getVideoTracks()[0].enabled = !cameraOn` + `BroadcastMediaState` |
| End Call button | `POST /api/v1/video-consultations/{id}/end` then cleanup |
| Chat panel | SignalR `SendChatMessage` / `ReceiveChatMessage` events |
| Quality indicator | Driven by `QualityAlert` SignalR event |

### 3.6 Step 5 — End Call

```typescript
async function endCall(consultationId: string) {
  await fetch(`/api/v1/video-consultations/${consultationId}/end`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` }
  });
  // Cleanup is handled when ConsultationEnded SignalR event fires (see Section 9)
}
```

---

## 4. Patient App — Full Workflow

### 4.1 Entry Point: Push Notification

The patient receives an FCM notification when the doctor initiates the session:

```json
{
  "title": "Dr. Abebe Bekele is ready",
  "body": "Dr. has started your video consultation. Tap to join.",
  "data": {
    "consultationId": "a1b2c3d4-...",
    "roomId": "room_a1b2c3d4..."
  }
}
```

On tap, navigate to the Video Call screen with `consultationId` from the data payload.

### 4.2 Flutter: Join Flow

```dart
// Step 1 — Call REST join endpoint
final response = await http.post(
  Uri.parse('$baseUrl/api/v1/video-consultations/$consultationId/join'),
  headers: {'Authorization': 'Bearer $token'},
);

if (response.statusCode != 200) {
  _showError('Could not join consultation. Please try again.');
  return;
}

final joinData = ConsultationJoinDto.fromJson(jsonDecode(response.body));
// joinData.chatHistory → render existing messages
// joinData.yourConnectionInfo → your display name/role

// Step 2 — Request permissions
final cameraStatus = await Permission.camera.request();
final micStatus = await Permission.microphone.request();

if (cameraStatus.isDenied || micStatus.isDenied) {
  _showError('Please enable camera and microphone permissions to join video call');
  return;
}

// Step 3 — Open local media
final localStream = await navigator.mediaDevices.getUserMedia({
  'video': true,
  'audio': true,
});

// Step 4 — Connect SignalR
final hubConnection = HubConnectionBuilder()
  .withUrl('$baseUrl/hubs/video',
    options: HttpConnectionOptions(accessTokenFactory: () async => token))
  .withAutomaticReconnect()
  .build();

_registerHandlers(hubConnection, consultationId, iceServers, localStream);
await hubConnection.start();
await hubConnection.invoke('JoinConsultationRoom', [consultationId]);
```

### 4.3 Flutter: Call Screen Widgets

```
┌─────────────────────────────────┐
│  Remote Video (full screen)      │
│  ┌────────┐                      │
│  │ Local  │  (PiP, top-right)    │
│  │ Video  │                      │
│  └────────┘                      │
│                                  │
│  Doctor info: name + status bar  │
│  Quality indicator (if poor)     │
├─────────────────────────────────┤
│  [Mute] [Camera] [Chat] [End]   │
└─────────────────────────────────┘
```

### 4.4 Flutter: Media Control Buttons

```dart
// Mute/Unmute
void toggleMute() {
  final track = localStream.getAudioTracks().first;
  track.enabled = !track.enabled;
  setState(() => isMuted = !isMuted);
  hubConnection.invoke('BroadcastMediaState', [consultationId, isMuted, isCameraEnabled]);
}

// Camera On/Off
void toggleCamera() {
  final track = localStream.getVideoTracks().first;
  track.enabled = !track.enabled;
  setState(() => isCameraEnabled = !isCameraEnabled);
  hubConnection.invoke('BroadcastMediaState', [consultationId, isMuted, isCameraEnabled]);
}
```

---

## 5. WebRTC Signaling Reference

This section applies to both Doctor Portal (TypeScript) and Patient App (Dart/flutter_webrtc). The logic is identical — only the API syntax differs.

### 5.1 Setup RTCPeerConnection

```typescript
function createPeerConnection(iceServers: IceServerDto[]): RTCPeerConnection {
  return new RTCPeerConnection({
    iceServers: iceServers.map(s => ({
      urls: s.urls,
      username: s.username ?? undefined,
      credential: s.credential ?? undefined,
    }))
  });
}
```

### 5.2 Full Signaling Flow

```typescript
const peerConnections = new Map<string, RTCPeerConnection>(); // keyed by connectionId

function registerSignalRHandlers(
  connection: signalR.HubConnection,
  consultationId: string,
  iceServers: IceServerDto[],
  localStream: MediaStream
) {
  // ── Peer joins: we initiate the offer ──────────────────────────────────────
  connection.on('UserJoined', async ({ connectionId }) => {
    const pc = createPeerConnection(iceServers);
    peerConnections.set(connectionId, pc);

    // Add our local tracks to the peer connection
    localStream.getTracks().forEach(track => pc.addTrack(track, localStream));

    // Forward ICE candidates to the new peer via SignalR
    pc.onicecandidate = ({ candidate }) => {
      if (candidate) {
        connection.invoke('SendIceCandidate', consultationId, connectionId, JSON.stringify(candidate));
      }
    };

    // Render remote video when tracks arrive
    pc.ontrack = ({ streams }) => {
      remoteVideoElement.srcObject = streams[0];
    };

    // Create and send the SDP offer
    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);
    await connection.invoke('SendOffer', consultationId, connectionId, JSON.stringify(offer));
  });

  // ── We receive an offer: create answer ─────────────────────────────────────
  connection.on('ReceiveOffer', async (senderConnectionId: string, sdpOffer: string) => {
    const pc = createPeerConnection(iceServers);
    peerConnections.set(senderConnectionId, pc);

    localStream.getTracks().forEach(track => pc.addTrack(track, localStream));

    pc.onicecandidate = ({ candidate }) => {
      if (candidate) {
        connection.invoke('SendIceCandidate', consultationId, senderConnectionId, JSON.stringify(candidate));
      }
    };

    pc.ontrack = ({ streams }) => {
      remoteVideoElement.srcObject = streams[0];
    };

    await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(sdpOffer)));
    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);
    await connection.invoke('SendAnswer', consultationId, senderConnectionId, JSON.stringify(answer));
  });

  // ── Receive answer ──────────────────────────────────────────────────────────
  connection.on('ReceiveAnswer', async (senderConnectionId: string, sdpAnswer: string) => {
    const pc = peerConnections.get(senderConnectionId);
    if (!pc) return;
    await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(sdpAnswer)));
  });

  // ── Receive ICE candidate ───────────────────────────────────────────────────
  connection.on('ReceiveIceCandidate', async (senderConnectionId: string, candidate: string) => {
    const pc = peerConnections.get(senderConnectionId);
    if (!pc) return;
    await pc.addIceCandidate(new RTCIceCandidate(JSON.parse(candidate)));
  });

  // ── Peer leaves: tear down ──────────────────────────────────────────────────
  connection.on('UserLeft', ({ connectionId }) => {
    const pc = peerConnections.get(connectionId);
    pc?.close();
    peerConnections.delete(connectionId);
    remoteVideoElement.srcObject = null;
  });
}
```

---

## 6. Media Controls Implementation

### 6.1 Mute / Unmute Microphone

```typescript
function toggleMute(consultationId: string, connection: signalR.HubConnection) {
  const track = localStream.getAudioTracks()[0];
  track.enabled = !track.enabled;         // WebRTC: mutes locally
  isMuted = !track.enabled;
  updateMuteButton(isMuted);

  // Tell other participants so their UI can show a muted indicator
  connection.invoke('BroadcastMediaState', consultationId, isMuted, isCameraEnabled);
}
```

### 6.2 Enable / Disable Camera

```typescript
function toggleCamera(consultationId: string, connection: signalR.HubConnection) {
  const track = localStream.getVideoTracks()[0];
  track.enabled = !track.enabled;         // WebRTC: blacks out the video track
  isCameraEnabled = track.enabled;
  updateCameraButton(isCameraEnabled);

  connection.invoke('BroadcastMediaState', consultationId, isMuted, isCameraEnabled);
}
```

### 6.3 Receiving Other Participant's Media State

```typescript
connection.on('ParticipantMediaStateChanged', ({ connectionId, isMuted, isCameraEnabled }) => {
  // Update the video tile for that participant
  updateParticipantTile(connectionId, { isMuted, isCameraEnabled });
  // e.g. show a mic-off icon overlay on their video tile
});
```

### 6.4 Audio-Only Fallback (on poor connection)

```typescript
connection.on('QualityAlert', ({ userId, message, audioOnlyRecommended }) => {
  showToast(message); // "Poor connection. Audio-only mode recommended"

  if (audioOnlyRecommended && userId === currentUserId) {
    showAudioOnlyPrompt({
      onConfirm: () => {
        // Disable the local video track
        localStream.getVideoTracks().forEach(t => t.enabled = false);
        isCameraEnabled = false;
        connection.invoke('BroadcastMediaState', consultationId, isMuted, false);
      }
    });
  }
});
```

---

## 7. In-Session Chat Implementation

### 7.1 Sending a Message

```typescript
async function sendMessage(consultationId: string, content: string) {
  if (!content.trim() || content.length > 2000) return;

  // Optimistically add to UI
  const tempId = crypto.randomUUID();
  addMessageToUI({ id: tempId, content, status: 'sending', senderName: 'You' });

  try {
    await connection.invoke('SendChatMessage', consultationId, content.trim());
    updateMessageStatus(tempId, 'sent');
  } catch {
    // MessageSendFailed event handles this — see below
  }
}
```

### 7.2 Receiving Messages

```typescript
connection.on('ReceiveChatMessage', ({ messageId, senderId, senderName, content, sentAt }) => {
  addMessageToUI({ id: messageId, senderId, senderName, content, sentAt, status: 'delivered' });
  scrollChatToBottom();
});
```

### 7.3 Handling Send Failure (FR-018)

```typescript
connection.on('MessageSendFailed', ({ content }) => {
  // Find the optimistic message and mark it failed
  markLastPendingMessageFailed(content);

  // Show "Message not sent" UI with a retry button
  showRetryBanner({
    message: 'Message not sent',
    onRetry: () => sendMessage(consultationId, content)
  });
});
```

### 7.4 Loading Older Messages (scroll-up pagination)

```typescript
async function loadOlderMessages(consultationId: string, currentPage: number) {
  const res = await fetch(
    `/api/v1/video-consultations/${consultationId}/chat?page=${currentPage + 1}&pageSize=50`,
    { headers: { Authorization: `Bearer ${token}` } }
  );
  const messages: ChatMessageDto[] = await res.json();
  prependMessagesToUI(messages);
}
```

---

## 8. Connection Quality Monitoring

Poll every 10 seconds using `RTCPeerConnection.getStats()` and POST to the quality-report endpoint.

```typescript
function startQualityMonitoring(
  consultationId: string,
  peerConnections: Map<string, RTCPeerConnection>,
  token: string
) {
  return setInterval(async () => {
    for (const [, pc] of peerConnections) {
      const stats = await pc.getStats();
      let bandwidth = 0, packetsLost = 0, frameRate = 0;

      stats.forEach(report => {
        if (report.type === 'inbound-rtp' && report.kind === 'video') {
          bandwidth = Math.round((report.bytesReceived * 8) / 1000); // Kbps approx
          packetsLost = report.packetsLost ?? 0;
          frameRate = Math.round(report.framesPerSecond ?? 0);
        }
      });

      if (bandwidth > 0) {
        fetch(`/api/v1/video-consultations/${consultationId}/quality-report`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
          body: JSON.stringify({ bandwidth, packetsLost, frameRate })
        });
      }
    }
  }, 10_000);
}
```

**Note for Flutter (flutter_webrtc):**

```dart
final stats = await peerConnection.getStats(null);
stats.forEach((report) {
  if (report.type == 'inbound-rtp' && report.values['kind'] == 'video') {
    final bandwidth = ((report.values['bytesReceived'] as int) * 8 / 1000).round();
    final packetsLost = report.values['packetsLost'] as int? ?? 0;
    final frameRate = (report.values['framesPerSecond'] as double? ?? 0).round();
    _postQualityReport(consultationId, bandwidth, packetsLost, frameRate);
  }
});
```

---

## 9. Error Handling Reference

### Camera / Microphone Permission Denied

Handle the `DOMException` / Flutter `PlatformException` **before** connecting to SignalR:

```typescript
// Web
try {
  localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
} catch (err) {
  if (err.name === 'NotAllowedError' || err.name === 'PermissionDeniedError') {
    showError('Please enable camera and microphone permissions to join video call');
    return; // Do NOT proceed to SignalR or REST
  }
}
```

```dart
// Flutter
try {
  localStream = await navigator.mediaDevices.getUserMedia({'video': true, 'audio': true});
} catch (e) {
  showErrorDialog('Please enable camera and microphone permissions to join video call');
  return;
}
```

### Session Ended Remotely

```typescript
connection.on('ConsultationEnded', async ({ reason }) => {
  // 1. Close all peer connections
  peerConnections.forEach(pc => pc.close());
  peerConnections.clear();

  // 2. Stop all local media tracks
  localStream.getTracks().forEach(t => t.stop());

  // 3. Disconnect hub
  await connection.stop();

  // 4. Navigate to summary screen
  router.push('/appointments');
  showToast(reason ?? 'Consultation has ended');
});
```

### SignalR Disconnection / Reconnect

```typescript
connection.onreconnecting(() => showConnectionBanner('Reconnecting...'));
connection.onreconnected(async () => {
  hideConnectionBanner();
  // Rejoin the room group after reconnect
  await connection.invoke('JoinConsultationRoom', consultationId);
});
connection.onclose(() => showConnectionBanner('Connection lost. Please rejoin.'));
```

### REST API Errors

| HTTP Status | Meaning | UI action |
|---|---|---|
| `400` | Appointment not confirmed / session already active / message too long | Show `problem.detail` to user |
| `401` | JWT expired | Redirect to login / refresh token |
| `403` | Wrong doctor or patient for this appointment | "You do not have access to this consultation" |
| `404` | Consultation or appointment not found | "Consultation not found" + back button |

---

## 10. SignalR Events Quick Reference

### Hub URL
```
wss://api.medimind.et/hubs/video?access_token=<JWT>
```

### Methods the client **invokes** (sends to server)

| Method | Parameters | When to call |
|---|---|---|
| `JoinConsultationRoom` | `consultationId: string` | Immediately after hub connection starts |
| `LeaveConsultationRoom` | `consultationId: string` | On graceful leave (not needed if calling End) |
| `SendOffer` | `consultationId, targetConnectionId, sdpOffer` | When `UserJoined` fires and you are the caller |
| `SendAnswer` | `consultationId, senderConnectionId, sdpAnswer` | When `ReceiveOffer` fires |
| `SendIceCandidate` | `consultationId, targetConnectionId, candidate` | When `pc.onicecandidate` fires |
| `BroadcastMediaState` | `consultationId, isMuted: bool, isCameraEnabled: bool` | Whenever user toggles mic or camera |
| `SendChatMessage` | `consultationId, content: string` | When user sends a chat message |
| `GetRoomParticipants` | `consultationId: string` | To list currently connected peers |

### Events the client **listens for** (receives from server)

| Event | Payload | Action |
|---|---|---|
| `UserJoined` | `{ connectionId, userId, userType, userName }` | Create RTCPeerConnection, add tracks, send SDP offer |
| `UserLeft` | `{ connectionId, userId, userType, reason? }` | Close and remove the peer connection, clear remote video |
| `ReceiveOffer` | `senderConnectionId: string, sdpOffer: string` | Set remote description, create and send SDP answer |
| `ReceiveAnswer` | `senderConnectionId: string, sdpAnswer: string` | Set remote description |
| `ReceiveIceCandidate` | `senderConnectionId: string, candidate: string` | Add ICE candidate to peer connection |
| `ParticipantMediaStateChanged` | `{ connectionId, isMuted, isCameraEnabled }` | Update mic/camera icon on that participant's video tile |
| `ReceiveChatMessage` | `{ messageId, senderId, senderName, content, sentAt }` | Append to chat panel |
| `MessageSendFailed` | `{ content }` | Show "Message not sent" with retry button |
| `QualityAlert` | `{ userId, message, audioOnlyRecommended }` | Show toast; if `audioOnlyRecommended`, offer to disable video |
| `ConsultationEnded` | `{ reason }` | Close peer connections, stop media, disconnect hub, navigate away |
