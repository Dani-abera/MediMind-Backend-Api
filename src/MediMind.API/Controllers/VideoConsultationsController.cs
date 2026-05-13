using MediMind.Application.Features.VideoConsultations;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// Telemedicine — video consultation sessions, real-time WebRTC signaling, in-session chat, and connection quality reporting.
/// </summary>
/// <remarks>
/// ## Full Session Lifecycle
///
/// ```
/// Doctor                                     Patient
///   │                                           │
///   │  1. POST /initiate  ──────────────────────┤ (push notification sent)
///   │  ← 201 ConsultationSessionDto             │
///   │                                           │  2. receives FCM push
///   │  3. Connect SignalR /hubs/video            │  3. Connect SignalR /hubs/video
///   │  4. hub → JoinConsultationRoom(id)         │  4. POST /{id}/join  → 200 ConsultationJoinDto
///   │                                           │  5. hub → JoinConsultationRoom(id)
///   │  ← hub event: UserJoined                  │
///   │                                           │
///   │  ── WebRTC handshake (offer/answer/ICE) ──│
///   │  6. hub → SendOffer(targetConnectionId)   │
///   │                                           │  ← hub event: ReceiveOffer
///   │                                           │  7. hub → SendAnswer(connectionId)
///   │  ← hub event: ReceiveAnswer               │
///   │  8. hub → SendIceCandidate(...)           │  8. hub → SendIceCandidate(...)
///   │                                           │
///   │  ── Live call ────────────────────────────│
///   │  hub → BroadcastMediaState(muted, cam)    │  ← hub event: ParticipantMediaStateChanged
///   │  hub → SendChatMessage(content)           │  ← hub event: ReceiveChatMessage
///   │                                           │
///   │  ── Quality monitoring ───────────────────│
///   │                                           │  POST /{id}/quality-report (every 10s)
///   │  ← hub event: QualityAlert (if bw &lt; 500)  │  ← hub event: QualityAlert
///   │                                           │
///   │  9. POST /{id}/end                        │
///   │  ← hub event: ConsultationEnded           │  ← hub event: ConsultationEnded
/// ```
///
/// ## SignalR Hub
/// Connect to **`/hubs/video?access_token=&lt;JWT&gt;`** (WebSocket or Long-Polling).
///
/// ### Hub methods the client calls
/// | Method | Parameters | Description |
/// |---|---|---|
/// | `JoinConsultationRoom` | `consultationId: string` | Must be called after connecting. Adds client to the room group and records participation. |
/// | `LeaveConsultationRoom` | `consultationId: string` | Graceful leave. Also fires on disconnect. |
/// | `SendOffer` | `consultationId, targetConnectionId, sdpOffer` | Routes SDP offer to specific peer during WebRTC handshake. |
/// | `SendAnswer` | `consultationId, targetConnectionId, sdpAnswer` | Routes SDP answer to specific peer. |
/// | `SendIceCandidate` | `consultationId, targetConnectionId, candidate` | Routes ICE candidate to specific peer. |
/// | `BroadcastMediaState` | `consultationId, isMuted: bool, isCameraEnabled: bool` | Broadcasts local mute/camera state to all other participants so their UI can update. Call whenever user toggles mic or camera. |
/// | `SendChatMessage` | `consultationId, content: string` | Persists and broadcasts a chat message (1–2000 chars). |
/// | `GetRoomParticipants` | `consultationId: string` | Returns list of currently connected participants with their connectionIds. |
///
/// ### Hub events the client receives
/// | Event | Payload | Description |
/// |---|---|---|
/// | `UserJoined` | `{ connectionId, userId, userType, userName }` | Another participant joined the room. Use `connectionId` as `targetConnectionId` when sending WebRTC offer. |
/// | `UserLeft` | `{ connectionId, userId, userType, reason? }` | Participant left or disconnected. Tear down peer connection. |
/// | `ReceiveOffer` | `senderConnectionId, sdpOffer` | Incoming SDP offer. Respond with `SendAnswer`. |
/// | `ReceiveAnswer` | `senderConnectionId, sdpAnswer` | Incoming SDP answer. Set as remote description. |
/// | `ReceiveIceCandidate` | `senderConnectionId, candidate` | Incoming ICE candidate. Add to peer connection. |
/// | `ParticipantMediaStateChanged` | `{ connectionId, isMuted, isCameraEnabled }` | Other participant toggled mic or camera. Update their video tile UI. |
/// | `ReceiveChatMessage` | `{ messageId, senderId, senderName, content, sentAt }` | New chat message. Display in chat panel. |
/// | `MessageSendFailed` | `{ content }` | Chat message failed to save. Show "Message not sent" UI with retry. |
/// | `QualityAlert` | `{ userId, message, audioOnlyRecommended }` | Triggered when reported bandwidth is below 500 Kbps. If `audioOnlyRecommended` is `true`, offer the user an option to disable their video track. |
/// | `ConsultationEnded` | `{ reason }` | Session terminated (by doctor, admin, or timeout). Navigate away from call screen. |
/// </remarks>
[ApiController]
[Route("api/v1/video-consultations")]
[Authorize]
[Tags("Telemedicine — Video Consultations")]
public sealed class VideoConsultationsController(IVideoConsultationService service, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Initiate a new video consultation session for a confirmed appointment.</summary>
    /// <remarks>
    /// **Doctor only.** Creates the consultation room and sends an FCM push notification to the patient.
    /// The appointment must be in **Confirmed** status. Only one active session per appointment is allowed.
    ///
    /// Use the returned `iceServers` array when creating the client-side `RTCPeerConnection`.
    ///
    /// **Example request:**
    /// ```json
    /// { "appointmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6" }
    /// ```
    ///
    /// **Example response (201):**
    /// ```json
    /// {
    ///   "consultationId": "a1b2c3d4-...",
    ///   "appointmentId": "3fa85f64-...",
    ///   "roomId": "room_a1b2c3d4e5f6...",
    ///   "status": "Scheduled",
    ///   "joinUrl": "wss://api.medimind.et/hubs/video",
    ///   "iceServers": [{ "urls": ["stun:stun.l.google.com:19302"], "username": null, "credential": null }],
    ///   "doctorInfo": { "userId": "...", "fullName": "Dr. Abebe Bekele", "specialization": "General Medicine", "profileImageUrl": null },
    ///   "patientInfo": { "userId": "...", "fullName": "Sara Haile", "profileImageUrl": null },
    ///   "appointmentDate": "2026-05-15",
    ///   "appointmentTime": "09:00:00"
    /// }
    /// ```
    ///
    /// **Error cases:**
    /// - `400` — Appointment not confirmed, or an active consultation already exists for this appointment.
    /// - `403` — Caller is not the doctor assigned to this appointment.
    /// - `404` — Appointment not found.
    /// </remarks>
    /// <param name="request">The appointment to start a video session for.</param>
    [HttpPost("initiate")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(ConsultationSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsultationSessionDto>> Initiate([FromBody] InitiateConsultationRequest request, CancellationToken ct)
    {
        var result = await service.InitiateConsultationAsync(request.AppointmentId, currentUser.UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.ConsultationId }, result);
    }

    /// <summary>Join an existing video consultation session and receive connection bootstrapping data.</summary>
    /// <remarks>
    /// Available to both the **Doctor** and the **Patient** assigned to the appointment.
    /// Call this via REST first to obtain `signalRHubUrl`, `yourConnectionInfo`, and `chatHistory`,
    /// then connect to the SignalR hub and call `JoinConsultationRoom(consultationId)`.
    ///
    /// The session status transitions **Scheduled → InProgress** on the first join.
    ///
    /// **Example response (200):**
    /// ```json
    /// {
    ///   "consultationId": "a1b2c3d4-...",
    ///   "roomId": "room_a1b2c3d4...",
    ///   "signalRHubUrl": "/hubs/video",
    ///   "yourConnectionInfo": { "userId": "...", "userType": "Patient", "displayName": "Sara Haile" },
    ///   "otherParticipants": [],
    ///   "chatHistory": []
    /// }
    /// ```
    ///
    /// After receiving this response:
    /// 1. Connect to `wss://&lt;host&gt;/hubs/video?access_token=&lt;JWT&gt;`
    /// 2. Call hub method `JoinConsultationRoom(consultationId)`
    /// 3. On receiving `UserJoined` event, initiate WebRTC by calling `SendOffer(consultationId, event.connectionId, sdpOffer)`
    ///
    /// **Error cases:**
    /// - `400` — Consultation is already completed or cancelled.
    /// - `403` — Caller is not the assigned doctor or patient.
    /// - `404` — Consultation not found.
    /// </remarks>
    /// <param name="id">The consultation ID returned by the Initiate endpoint.</param>
    [HttpPost("{id:guid}/join")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(ConsultationJoinDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsultationJoinDto>> Join(Guid id, CancellationToken ct)
    {
        var result = await service.JoinConsultationAsync(id, currentUser.UserId, currentUser.UserType, null, ct);
        return Ok(result);
    }

    /// <summary>Terminate an active video consultation session.</summary>
    /// <remarks>
    /// **Doctor or Admin only.** Marks the consultation as **Completed**, records leave timestamps for both
    /// participants, and broadcasts a `ConsultationEnded` SignalR event to the room.
    /// If called by the doctor, the linked appointment is also marked **Completed**.
    ///
    /// This call is idempotent — calling it on an already-ended session returns `204` with no side effects.
    ///
    /// **After receiving `ConsultationEnded` on the client:**
    /// 1. Close all `RTCPeerConnection` instances.
    /// 2. Stop all local `MediaStream` tracks.
    /// 3. Disconnect from the SignalR hub.
    /// 4. Navigate to the post-call summary screen.
    ///
    /// **Error cases:**
    /// - `404` — Consultation not found.
    /// </remarks>
    /// <param name="id">The consultation ID to end.</param>
    [HttpPost("{id:guid}/end")]
    [Authorize(Policy = "DoctorOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> End(Guid id, CancellationToken ct)
    {
        var endedByDoctor = string.Equals(currentUser.UserType, "Doctor", StringComparison.OrdinalIgnoreCase);
        await service.EndConsultationAsync(id, currentUser.UserId, endedByDoctor, ct);
        return NoContent();
    }

    /// <summary>Get full session details for a video consultation.</summary>
    /// <remarks>
    /// Returns ICE server configuration, doctor and patient info, and appointment metadata.
    /// Use this to reconstruct the call screen after an app backgrounding/restore event.
    ///
    /// **Error cases:**
    /// - `403` — Caller is not the assigned doctor or patient.
    /// - `404` — Consultation not found.
    /// </remarks>
    /// <param name="id">The consultation ID.</param>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(ConsultationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsultationSessionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, currentUser.UserId, currentUser.UserType, ct);
        return Ok(result);
    }

    /// <summary>Retrieve paginated in-session chat history for a consultation.</summary>
    /// <remarks>
    /// Messages are returned in chronological order (oldest first). The first page (50 messages) is
    /// already included in the `chatHistory` field of the Join response — call this endpoint only when
    /// the user scrolls up to load older messages.
    ///
    /// **Example response (200):**
    /// ```json
    /// [
    ///   {
    ///     "messageId": "...",
    ///     "consultationId": "...",
    ///     "senderId": "...",
    ///     "senderName": "Dr. Abebe Bekele",
    ///     "senderType": "Doctor",
    ///     "content": "How are you feeling today?",
    ///     "sentAt": "2026-05-15T09:05:00Z",
    ///     "isRead": true
    ///   }
    /// ]
    /// ```
    /// </remarks>
    /// <param name="id">The consultation ID.</param>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Messages per page, max 50 (default: 50).</param>
    [HttpGet("{id:guid}/chat")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(IReadOnlyList<ChatMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> Chat(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await service.GetChatHistoryAsync(id, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Get the video consultation associated with a specific appointment.</summary>
    /// <remarks>
    /// Convenience lookup — equivalent to `GET /{id}` when you have the appointment ID but not the
    /// consultation ID (e.g. navigating from the appointment detail screen).
    ///
    /// **Error cases:**
    /// - `403` — Caller is not the assigned doctor or patient.
    /// - `404` — No consultation found for this appointment.
    /// </remarks>
    /// <param name="appointmentId">The appointment ID.</param>
    [HttpGet("appointment/{appointmentId:guid}")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(ConsultationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsultationSessionDto>> ByAppointment(Guid appointmentId, CancellationToken ct)
    {
        var result = await service.GetByAppointmentAsync(appointmentId, currentUser.UserId, currentUser.UserType, ct);
        return Ok(result);
    }

    /// <summary>Submit a real-time connection quality report (bandwidth, packet loss, frame rate).</summary>
    /// <remarks>
    /// Call this endpoint **every 10 seconds** during an active call using stats from
    /// `RTCPeerConnection.getStats()`. The server persists metrics and triggers a `QualityAlert`
    /// SignalR event to the room if bandwidth drops below **500 Kbps**.
    ///
    /// **How to read stats from WebRTC (JavaScript/TypeScript):**
    /// ```typescript
    /// const stats = await peerConnection.getStats();
    /// stats.forEach(report => {
    ///   if (report.type === 'inbound-rtp' &amp;&amp; report.kind === 'video') {
    ///     const bandwidth = report.bytesReceived / report.timestamp * 8; // approximate Kbps
    ///     const packetsLost = report.packetsLost;
    ///     const frameRate = report.framesPerSecond;
    ///   }
    /// });
    /// ```
    ///
    /// **Example request:**
    /// ```json
    /// { "bandwidth": 320, "packetsLost": 12, "frameRate": 15 }
    /// ```
    ///
    /// On `QualityAlert` event with `audioOnlyRecommended: true`, the client should:
    /// 1. Display toast: **"Poor connection. Audio-only mode recommended"**
    /// 2. Show a button "Disable video" that calls `videoTrack.enabled = false` on the local stream.
    ///
    /// **Error cases:**
    /// - `404` — Consultation not found.
    /// </remarks>
    /// <param name="id">The consultation ID.</param>
    /// <param name="request">Bandwidth (Kbps), packets lost count, and current frame rate.</param>
    [HttpPost("{id:guid}/quality-report")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QualityReport(Guid id, [FromBody] QualityReportRequest request, CancellationToken ct)
    {
        await service.ReportQualityAsync(id, currentUser.UserId, request.Bandwidth, request.PacketsLost, request.FrameRate, ct);
        return Accepted();
    }
}

/// <summary>Request body for initiating a new video consultation.</summary>
/// <param name="AppointmentId">ID of the confirmed appointment to start the session for.</param>
public sealed record InitiateConsultationRequest(Guid AppointmentId);

/// <summary>Real-time WebRTC connection quality metrics collected from RTCPeerConnection.getStats().</summary>
/// <param name="Bandwidth">Current estimated receive bandwidth in Kbps. Triggers a quality alert when below 500.</param>
/// <param name="PacketsLost">Total RTP packets lost since the last report.</param>
/// <param name="FrameRate">Current video frame rate in frames per second.</param>
public sealed record QualityReportRequest(int Bandwidth, int PacketsLost, int FrameRate);
