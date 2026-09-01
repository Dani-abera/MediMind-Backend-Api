# MediMind — AI-Enhanced Hospital Appointment, Queue & Health Monitoring System
## Backend API (.NET 10 | Clean Architecture | PostgreSQL | SignalR | Hangfire)

> Part of the **MediMind platform** → [Overview](https://github.com/Dani-abera/MediMind-Platform) · [Patient App](https://github.com/Dani-abera/MediMind_Patient_App) · [Staff Portal](https://github.com/Dani-abera/MediMind_Portal) · [ML Service](https://github.com/Dani-abera/MediMind-Disease-Prediction-ML)
>
> 
---

## Architecture Overview

```
MediMind/
├── src/
│   ├── MediMind.Domain/            # Zero-dependency pure domain
│   │   ├── Entities/               # User, Patient, Doctor, Appointment, Queue...
│   │   ├── Enums/                  # All domain enums (AppointmentStatus, etc.)
│   │   ├── Events/                 # Domain events (AppointmentBooked, etc.)
│   │   └── Exceptions/             # DomainException, NotFoundException...
│   │
│   ├── MediMind.Application/       # CQRS + MediatR (business use cases)
│   │   ├── Common/
│   │   │   ├── Behaviors/          # Validation + Logging pipeline behaviors
│   │   │   └── Interfaces/         # IRepositories + IServices (contracts)
│   │   └── Features/
│   │       ├── Auth/               # Register, Login OTP, Refresh Token
│   │       ├── Appointments/       # Book, Approve, Cancel, GetSlots
│   │       ├── Queue/              # GenerateDailyQueues, CallNext, MyStatus
│   │       ├── HealthRecords/      # LogVitals, RequestPrediction
│   │       ├── HealthcareCenters/  # Register, AddDoctor, ConfigSchedule
│   │       ├── Payments/           # Initiate, Webhook, Receipt
│   │       ├── VideoConsultations/ # Start, Join, End
│   │       ├── Prescriptions/      # Issue, GetByAppointment
│   │       └── Analytics/          # Dashboard, WeeklyTrend
│   │
│   ├── MediMind.Infrastructure/    # EF Core, Repositories, Services
│   │   ├── Data/
│   │   │   ├── MediMindDbContext.cs
│   │   │   ├── Configurations/     # All 16 IEntityTypeConfiguration classes
│   │   │   └── Repositories/       # All repository implementations
│   │   ├── Services/
│   │   │   ├── Auth/               # JWT, BCrypt, OTP
│   │   │   ├── ML/                 # HTTP client → Python Flask
│   │   │   ├── Payment/            # Chapa gateway integration
│   │   │   └── Notifications/      # Geez SMS, Firebase FCM, SendGrid
│   │   └── SignalR/                # QueueHub + QueueHubService
│   │
│   └── MediMind.API/              # ASP.NET Core 10 Web API
│       ├── Controllers/            # Auth, Appointments, Queue, Health...
│       ├── Program.cs              # Full startup configuration
│       └── appsettings.json
│
└── tests/
    ├── MediMind.UnitTests/         # xUnit + FluentAssertions + NSubstitute
    └── MediMind.IntegrationTests/
```

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | **10.0** | `dotnet --version` |
| PostgreSQL | 15+ | Or use Docker Compose |
| Docker Desktop | Latest | For full stack |
| Python | 3.10+ | For ML microservice |
| Node.js | 18 LTS | For frontend |

---

## Quick Start (5 minutes)

### Option A: Docker Compose (Recommended)
```bash
# Clone and start everything
git clone <your-repo-url>
cd MediMind

# Start PostgreSQL + API + ML Service
docker-compose up -d

# API:     http://localhost:5000
# Swagger: http://localhost:5000 (root)
# pgAdmin: http://localhost:5050
```

### Option B: Local Development
```bash
# 1. Clone repo
git clone <your-repo-url> && cd MediMind

# 2. Start PostgreSQL (via Docker)
docker run -d --name medimind_postgres \
  -e POSTGRES_DB=medimind_db \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=medimind_dev_password \
  -p 5432:5432 postgres:16-alpine

# 3. Update connection string in appsettings.json

# 4. Apply EF Core migrations
cd src/MediMind.API
dotnet ef migrations add InitialCreate --project ../MediMind.Infrastructure
dotnet ef database update

# 5. Start the API
dotnet run

# 6. Start Python ML service (separate terminal)
cd ../../ml
pip install -r requirements.txt
python app.py
```

---

## EF Core Migrations

```bash
cd src/MediMind.API

# Create a new migration after changing Domain entities
dotnet ef migrations add <MigrationName> \
  --project ../MediMind.Infrastructure \
  --startup-project .

# Apply to database
dotnet ef database update \
  --project ../MediMind.Infrastructure \
  --startup-project .

# Generate SQL script (for production review)
dotnet ef migrations script \
  --project ../MediMind.Infrastructure \
  --startup-project . \
  --output migration.sql
```

---

## Key API Endpoints

### Auth
```
POST /api/v1/auth/register/patient       — Register new patient
POST /api/v1/auth/verify-otp             — Verify OTP (returns JWT)
POST /api/v1/auth/login/patient/send-otp — Patient login OTP
POST /api/v1/auth/login/doctor/send-otp  — Doctor login (badge number)
POST /api/v1/auth/refresh-token          — Refresh JWT
```

### Appointments
```
POST   /api/v1/appointments                    — Book appointment (Patient)
GET    /api/v1/appointments                    — List appointments (Patient/Doctor/Admin)
GET    /api/v1/appointments/{id}               — Appointment detail (scoped)
POST   /api/v1/appointments/{id}/cancel        — Cancel (Patient/Admin)
POST   /api/v1/appointments/{id}/reschedule    — Reschedule (Patient)
POST   /api/v1/appointments/{id}/approve       — Approve (Admin)
POST   /api/v1/appointments/{id}/reject        — Reject (Admin)
GET    /api/v1/appointments/availability       — Available slots for date
GET    /api/v1/appointments/available-dates    — Dates with at least one slot
GET    /api/v1/legacy-appointments/my          — Legacy endpoint (kept for compatibility)
```

### Queue (Real-Time)
```
GET    /api/v1/queue/centers/{id}         — Full queue dashboard (Admin)
POST   /api/v1/queue/centers/{id}/call-next — Call next patient (Admin)
PATCH  /api/v1/queue/{entryId}/no-show    — Mark no-show (Admin)
GET    /api/v1/queue/my-status/{apptId}   — My queue position (Patient)

# SignalR WebSocket
ws://localhost:5000/hubs/queue?access_token=<JWT>
Events: QueueUpdated, PatientCalled
```

### Health Monitoring
```
POST /api/v1/health/records              — Log vital signs
GET  /api/v1/health/records?days=30      — Health history
POST /api/v1/health/predictions/request  — Request AI prediction
```

### Health Predictions (New Service-Based)
```
POST /api/v1/health-predictions/request  — Request ML prediction (Patient)
GET  /api/v1/health-predictions          — Paginated prediction history
GET  /api/v1/health-predictions/latest   — Latest prediction (Patient/Doctor scoped)
GET  /api/v1/health-predictions/{id}     — Prediction details
GET  /api/v1/health-predictions/status   — Prediction readiness status
```

### Doctor Schedules
```
POST   /api/v1/doctor-schedules                      — Create/Update (Admin)
GET    /api/v1/doctor-schedules/{doctorId}/{centerId} — Get schedule
DELETE /api/v1/doctor-schedules/{id}                — Delete schedule (Admin)
```

### Healthcare Centers
```
GET    /api/v1/healthcare-centers                         — Search centers
GET    /api/v1/healthcare-centers/{id}/doctors            — List doctors
POST   /api/v1/healthcare-centers/register                — Register center
POST   /api/v1/healthcare-centers/{id}/doctors            — Add doctor (Admin)
POST   /api/v1/healthcare-centers/{id}/doctors/{doctorId}/schedule — Set schedule
PATCH  /api/v1/healthcare-centers/{id}/configuration      — Update config
```

### Payments
```
POST /api/v1/payments/initiate           — Initiate Chapa payment
POST /api/v1/payments/webhook            — Chapa webhook (no auth)
GET  /api/v1/payments/{ref}/receipt      — Get receipt
```

### Video Consultations
```
POST /api/v1/video-consultations/initiate              — Doctor initiates consultation session
POST /api/v1/video-consultations/{id}/join             — Patient/Doctor joins consultation
POST /api/v1/video-consultations/{id}/end              — Doctor/Admin ends consultation
GET  /api/v1/video-consultations/{id}                  — Consultation details
GET  /api/v1/video-consultations/{id}/chat             — Chat history (paged)
GET  /api/v1/video-consultations/appointment/{apptId}  — Resolve consultation by appointment
POST /api/v1/video-consultations/{id}/quality-report   — Report bandwidth/packet-loss/fps

# SignalR WebSocket for WebRTC signaling + chat
ws://localhost:5000/hubs/video?access_token=<JWT>
Hub methods: JoinConsultationRoom, LeaveConsultationRoom, SendOffer, SendAnswer, SendIceCandidate, SendChatMessage
Server events: UserJoined, UserLeft, ReceiveOffer, ReceiveAnswer, ReceiveIceCandidate, ReceiveChatMessage, ConsultationEnded, QualityAlert
```

### Analytics
```
GET /api/v1/analytics/{centerId}/dashboard        — Today's dashboard
GET /api/v1/analytics/{centerId}/trends           — Weekly trends
```

---

## Authorization Matrix

| Endpoint | Patient | Doctor | Admin | SuperAdmin |
|----------|---------|--------|-------|------------|
| Book appointment | ✅ | ❌ | ❌ | ✅ |
| Approve appointment | ❌ | ❌ | ✅ | ✅ |
| View queue dashboard | ❌ | ❌ | ✅ | ✅ |
| Call next patient | ❌ | ❌ | ✅ | ✅ |
| Log health data | ✅ | ❌ | ❌ | ✅ |
| Request AI prediction | ✅ | ❌ | ❌ | ✅ |
| Issue prescription | ❌ | ✅ | ❌ | ✅ |
| Start video call | ❌ | ✅ | ❌ | ✅ |
| Register doctor | ❌ | ❌ | ✅ | ✅ |
| Analytics dashboard | ❌ | ❌ | ✅ | ✅ |

---

## Multi-Tenant Data Isolation

Every Admin's JWT contains `tenant_id` = their `center_id`.  
All queries are automatically scoped to that center:

```csharp
// In every admin query handler:
if (currentUser.TenantId != request.CenterId)
    throw new TenantIsolationException(); // HTTP 403
```

---

## Background Jobs (Hangfire)

| Job | Schedule | Description |
|-----|----------|-------------|
| `GenerateDailyQueues` | 06:00 AM EAT daily | Creates queue entries for all confirmed appointments |
| `SendAppointmentReminders` | Every 30 minutes | 24h + 2h reminders (SMS + Push) |

Dashboard: `http://localhost:5000/hangfire`

---

## Appointment Management Implementation

### 1) Availability Engine
- `AppointmentAvailabilityService` loads doctor schedule by `(doctorId, centerId)`.
- Validates `WorkingDays` includes requested day, then generates slots from `StartTime` to `EndTime`.
- Excludes slots inside `BreakStart`–`BreakEnd`.
- Marks booked slots unavailable using non-cancelled appointments for same doctor/date/center.
- Removes past slots for today using UTC time.
- Supports `GetAvailableDatesAsync` by scanning forward and selecting dates with at least one available slot.

### 2) Booking Validation
- `BookingValidationService` enforces:
  - date must be today or future
  - date within center `AdvanceBookingDays`
  - for same-day booking, at least 2 hours ahead
  - one appointment per patient/doctor/day (non-cancelled)
  - selected slot is still available
- User-facing validation messages are aligned with product text for time and slot conflicts.

### 3) Booking / Approval / Cancel / Reschedule
- `AppointmentService.BookAppointmentAsync`:
  - validates rules
  - starts transaction
  - re-checks conflict atomically
  - creates `Pending` appointment (or `Confirmed` when center auto-approves)
  - commits, then triggers fire-and-forget notifications
- `CancelAppointmentAsync`:
  - verifies ownership/authority by role
  - allows only `Pending`/`Confirmed`
  - enforces cancellation window for confirmed appointments
- `RescheduleAppointmentAsync`:
  - max 1 reschedule
  - cancels old appointment for history
  - creates new linked appointment via `OriginalAppointmentId`
- `ApproveAppointmentAsync`:
  - validates admin-center scope + `Pending` status
  - confirms and optionally creates queue record

### 4) Reminder Automation
- `AppointmentReminderService` runs every 5 minutes as hosted service.
- Sends:
  - 24h reminder (`SMS + push`) for confirmed appointments in ±5m window
  - 2h reminder (`push`) for confirmed appointments in ±5m window
- Tracks delivery with:
  - `Reminder24hSentAt`
  - `Reminder2hSentAt`
- Uses `INotificationService` abstraction to isolate transport providers.

### 5) Concurrency Strategy
- Booking re-checks slot conflicts inside transaction boundary to prevent race conditions.
- Infrastructure note: can be upgraded to strict pessimistic locking with `SELECT ... FOR UPDATE`
  on candidate slot rows for high contention environments.

---

## Telemedicine (WebRTC + SignalR) Implementation

### 1) Data Model
- `video_consultations` stores room session lifecycle (`Scheduled`, `InProgress`, `Completed`, `Cancelled`) and timing metadata.
- `video_consultation_participants` tracks doctor/patient join and leave timestamps with DB checks enforcing valid identity shape.
- `chat_messages` persists in-call text chat (content max length 2000, sent time, read flag) and is indexed by `(consultation_id, sent_at DESC)`.
- `video_quality_metrics` stores bandwidth, packet loss, and frame rate reports for operational monitoring.

### 2) Signaling Flow
- Clients connect to `/hubs/video` using JWT query token.
- Both patient and doctor call `JoinConsultationRoom(consultationId)` before exchanging SDP/ICE.
- WebRTC SDP offer/answer and ICE candidates are forwarded peer-to-peer by connection ID via SignalR methods.
- Chat is persisted through application service and then broadcast to room participants for consistency.

### 3) Consultation Lifecycle
- `InitiateConsultationAsync` verifies appointment + doctor ownership, creates room ID, and pushes join notification to patient.
- `JoinConsultationAsync` validates participant identity against appointment and transitions status to `InProgress` on first join.
- `EndConsultationAsync` marks consultation complete, computes duration, marks participant leave, and emits `ConsultationEnded`.
- When ended by doctor, appointment is also transitioned to `Completed` (when currently `InProgress`).

### 4) Quality Monitoring
- API endpoint `/api/v1/video-consultations/{id}/quality-report` accepts client metrics.
- Bandwidth below `500 kbps` triggers real-time `QualityAlert` event recommending audio-only mode.
- Metrics are persisted in `video_quality_metrics` for later analytics and incident review.

### 5) CORS & Environments
- Development mode allows dynamic origins for local Flutter/React websocket testing.
- Non-development environments use explicit origins from `Cors:AllowedOrigins`.
- SignalR websocket auth uses bearer token from `access_token` query parameter.

---

## Running Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test tests/MediMind.UnitTests/

# With coverage
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage"
```

---

## Environment Variables (Production)

```bash
ConnectionStrings__DefaultConnection="Host=...;Database=medimind_db;..."
Jwt__SecretKey="minimum-32-char-production-secret-key"
Chapa__SecretKey="CHASECK-live-..."
GeezsMS__ApiKey="your-live-key"
MlService__BaseUrl="http://ml-service:5001"
ASPNETCORE_ENVIRONMENT="Production"
```

---

## Technology Stack Summary

| Layer | Technology | Purpose |
|-------|-----------|---------|
| API Framework | ASP.NET Core 10 | REST API + SignalR |
| Architecture | Clean Architecture + CQRS | MediatR, separation of concerns |
| ORM | EF Core 10 + Npgsql | PostgreSQL, TPT inheritance |
| Auth | JWT Bearer + BCrypt | 15-min access, 7-day refresh |
| Real-time | SignalR (WebSockets) | Queue position updates |
| Background Jobs | Hangfire + PostgreSQL | Queue generation, reminders |
| Validation | FluentValidation | Pipeline behaviors |
| ML Client | HttpClient | Calls Python Flask microservice |
| Payment | Chapa Gateway | ETB payments, HMAC webhooks |
| SMS | SMS Gateway | OTP, reminders |
| Push | Firebase FCM | Android/iOS notifications |
| Storage | Cloudinary (optional; local /uploads fallback) | Prescription PDFs, profile images |
| Logging | Serilog | Console + File + PostgreSQL |
| Testing | xUnit + FluentAssertions | Domain unit tests |
| Database | PostgreSQL 15 | 16 tables, 68 indexes |

---

## SRS Functional Requirements Coverage

All 30 functional requirements from the SRS are implemented:

- **FR-001 to FR-004**: Auth (Register, Login, OAuth, RBAC) ✅
- **FR-005 to FR-007**: Health Monitoring (Vitals, AI Prediction, Reminders) ✅
- **FR-008 to FR-014**: Appointment Management ✅
- **FR-015 to FR-016**: Queue Management (Real-time, Notifications) ✅
- **FR-017 to FR-018**: Telemedicine (Video, Chat) ✅
- **FR-019 to FR-024**: Healthcare Center Management ✅
- **FR-025 to FR-028**: Payment & Billing (Chapa, Receipts) ✅
- **FR-029 to FR-030**: Multi-Tenant Architecture ✅

---

*MediMind — The future of Ethiopian healthcare is digital.*
