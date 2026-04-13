# MediMind — AI-Enhanced Hospital Appointment, Queue & Health Monitoring System
## Backend API (.NET 10 | Clean Architecture | PostgreSQL | SignalR | Hangfire)

> **Kombolcha Institute of Technology** — Final Year Project  
> **Team:** Daniel Abera Bogale · Zelalem Getachew · Meragiaw Biset Mekonen  
> **Advisor:** Tilahun Ayalew

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
GET    /api/v1/appointments/my                   — My appointments (Patient)
GET    /api/v1/appointments/doctors/{id}/slots   — Available time slots
POST   /api/v1/appointments                      — Book appointment (Patient)
PATCH  /api/v1/appointments/{id}/approve         — Approve (Admin)
PATCH  /api/v1/appointments/{id}/reject          — Reject with reason (Admin)
PATCH  /api/v1/appointments/{id}/cancel          — Cancel (Patient/Admin)
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
POST /api/v1/consultations/{apptId}/start         — Doctor starts call
POST /api/v1/consultations/{apptId}/join          — Patient joins
POST /api/v1/consultations/{apptId}/end           — Doctor ends call
POST /api/v1/consultations/{apptId}/prescriptions — Issue prescription
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
| SMS | Geez SMS Gateway | OTP, reminders |
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
