


# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MediMind is an AI-enhanced hospital appointment, queue management, and health monitoring backend API built with ASP.NET Core 10 and Clean Architecture. It is a final-year project targeting Ethiopian healthcare (Geez SMS, Chapa payments, Ethiopian phone numbers).

## Commands

```bash
# Run the API (from repo root)
cd src/MediMind.API && dotnet run

# Build entire solution
dotnet build MediMind.sln

# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/MediMind.UnitTests/

# Run a single test (filter by class or method name)
dotnet test --filter "FullyQualifiedName~AppointmentTests"

# EF Core migrations (run from src/MediMind.API)
dotnet ef migrations add <MigrationName> --project ../MediMind.Infrastructure --startup-project .
dotnet ef database update --project ../MediMind.Infrastructure --startup-project .
dotnet ef migrations script --project ../MediMind.Infrastructure --startup-project . --output migration.sql
```

**API docs** (Scalar UI, no Swashbuckle): `http://localhost:5000` when running locally.  
**Hangfire dashboard**: `http://localhost:5000/hangfire`

## Architecture

The solution uses Clean Architecture with strict dependency direction:

```
Domain → Application → Infrastructure → API
```

- **`MediMind.Domain`** — Zero external dependencies. Contains `User` (abstract, TPT base), `Patient`, `Doctor`, `HealthcareCenterAdmin`, `Appointment`, `HealthRecord`, `HealthPrediction`, `Queue`, `VideoConsultation`, etc. All entities extend `BaseEntity`. Interfaces live in `Domain/Common/Interfaces/` (`IRepository<T>`, `IUnitOfWork`, `ICurrentUser`, per-entity repositories).

- **`MediMind.Application`** — Business logic only. Uses MediatR CQRS: commands/queries are records in `Features/<Feature>/XxxCommands.cs` and their handlers call application service interfaces (`IAppointmentService`, `IQueueService`, etc.) defined alongside them in `XxxServices.cs`. FluentValidation runs as a MediatR pipeline behavior (`Common/Behaviors/PipelineBehaviors.cs`) — `ValidationException` → HTTP 400. AutoMapper profiles sit in the feature folder they belong to.

- **`MediMind.Infrastructure`** — EF Core 10 + Npgsql. `MediMindDbContext` in `Data/`. Entity configurations in `Data/Configurations/` (one file per entity implementing `IEntityTypeConfiguration<T>`). Repository implementations in `Data/Repositories/`. Services (Auth/JWT/BCrypt, Notifications, ML client, Chapa, PDF, Cloudinary) in `Services/`. SignalR hubs in `SignalR/`.

- **`MediMind.API`** — ASP.NET Core 10. All controllers extend `BaseController(IMediator mediator)` and use `[Route("api/v1/[controller]")]`. Controllers live in multiple files under `Controllers/` (grouped by domain area). `Program.cs` wires all DI registrations and middleware.

## Key Patterns

### CQRS without a Dispatcher Layer
Controllers call `Mediator.Send(command)` directly. Application service interfaces (`IAppointmentService`, `IQueueService`, `IVideoConsultationService`, etc.) are defined in the Application layer and implemented in Infrastructure — the handler calls the service, never the DB directly.

### Multi-Tenancy
Every Admin JWT contains `tenant_id` = their `center_id` (via `ICurrentUser.TenantId`). All admin query/command handlers enforce: `if (currentUser.TenantId != request.CenterId) throw new TenantIsolationException()` → HTTP 403.

### User Inheritance (TPT)
`User` is abstract. `Patient`, `Doctor`, and `HealthcareCenterAdmin` map to their own tables via Table-Per-Type (TPT). When writing EF configurations, follow the existing pattern in `Data/Configurations/`.

### Auth Flow
OTP-based login: register → OTP sent via Geez SMS → verify OTP → receive 15-min JWT + 7-day refresh token. Doctor login uses badge number + OTP. `ICurrentUser` is resolved from JWT claims by the Infrastructure layer.

### Real-Time (SignalR)
- `QueueHub` / `QueueHubService` — queue position updates; clients connect to `/hubs/queue?access_token=<JWT>`
- `VideoConsultationHub` — WebRTC signaling (offer/answer/ICE) + chat; `/hubs/video?access_token=<JWT>`

### Background Jobs (Hangfire + PostgreSQL)
- `GenerateDailyQueues` — 06:00 AM EAT daily
- `SendAppointmentReminders` — every 30 min (24h + 2h reminders via SMS + FCM)

### ML Integration
The Python Flask ML microservice is called via `IMlServiceClient` (HTTP, Polly retry). Default URL: `http://localhost:5001`. Feature engineering is in `Application/Features/HealthPredictions/HealthFeatureEngineeringService.cs`.

### Notifications
`INotificationService` → `MediMindNotificationService` fan-out to: Geez SMS (`GeezSmsClient`), Firebase FCM (`FcmClient`), and in-app (`NotificationServices.cs`).

### Payment
Chapa gateway: initiate → redirect → HMAC-verified webhook → receipt. Webhook endpoint is `[AllowAnonymous]`.

## Testing

Tests use xUnit + FluentAssertions + NSubstitute + Bogus. Only the Unit test project exists (no integration tests yet). Tests reference `MediMind.Application` and `MediMind.Domain` only — no Infrastructure.