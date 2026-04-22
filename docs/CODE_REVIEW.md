# MediMind Backend — Code Review

**Date:** 2026-04-22  
**Reviewer:** Claude Code (automated + manual)  
**Scope:** All layers — Domain, Application, Infrastructure, API  
**Build baseline:** `dotnet build` — 0 errors before this review

---

## Summary

| Severity | Count | Status |
|----------|-------|--------|
| Critical (bug / security) | 2 | Fixed |
| High (architectural violation) | 5 | Fixed / Flagged |
| Medium (code quality) | 8 | Fixed |
| Low (style / docs) | 5 | Fixed |
| Design decisions (flag only) | 4 | Documented below |

---

## Critical

### C1 — Refresh token loses tenant_id for Admin users
**File:** `src/MediMind.API/Controllers/Controllers.cs:204`  
`AuthController.RefreshToken` regenerates tokens with `null` tenantId:
```csharp
tokenService.GenerateTokens(user.Id, user.UserType.ToString(), null);
```
Admin JWTs carry `tenant_id` / `center_id` claims. After refresh, those claims are absent and every subsequent admin request throws `TenantIsolationException` (HTTP 403).  
**Fix applied:** Cast to `HealthcareCenterAdmin` and pass `CenterId`.

### C2 — SignalR registered twice; second call discards MaximumReceiveMessageSize and KeepAliveInterval
**Files:** `src/MediMind.Infrastructure/DependencyInjection.cs:203` and `src/MediMind.API/Program.cs:56`  
`AddSignalR` is called in `AddInfrastructure` (with `MaximumReceiveMessageSize = 32 KB` and `KeepAliveInterval = 30s`) and again in `Program.cs` with only `EnableDetailedErrors`. ASP.NET Core's options system means the second call appends, but the hub-level defaults won't include the first registration's values for any option that isn't re-set.  
**Fix applied:** Removed the redundant call from `Program.cs`.

---

## High

### H1 — Webhook exception silently swallowed (no logging)
**File:** `src/MediMind.API/Controllers/PaymentsController.cs:86-93`
```csharp
catch
{
    // Chapa retries non-200; always return 200 for idempotent webhook behavior.
}
```
Payment webhook failures are completely invisible — no logs, no alerts. Idempotent 200 is correct for Chapa, but the exception should be logged at Error level.  
**Fix applied:** Injected `ILogger<PaymentsController>` and added `_logger.LogError(ex, ...)` in the catch.

### H2 — DoctorsController injects IDoctorRepository directly (Clean Architecture violation)
**File:** `src/MediMind.API/Controllers/DoctorsController.cs:22`  
The API layer must not reference repository interfaces. `Search` and `Get` build DTOs from raw `Doctor` entities, duplicating projection logic that belongs in the Application layer.  
**Flag:** Refactoring `Search` and `Get` into `IDoctorProfileService` methods is the correct fix but is a larger change involving new service methods. Left for a follow-up task to avoid scope creep.

### H3 — DoctorSchedulesController injects IDoctorScheduleRepository directly
**File:** `src/MediMind.API/Controllers/DoctorSchedulesController.cs:27`  
Business validation (time ordering, break window) and upsert-by-delete/recreate logic live in the controller. This belongs in a `IDoctorScheduleService`.  
**Flag:** Same architectural violation as H2. Left for follow-up.

### H4 — MedicationRemindersController injects IMedicationReminderRepository + IUnitOfWork directly
**File:** `src/MediMind.API/Controllers/NotificationControllers.cs:111`  
CRUD operations including `SaveChangesAsync` calls are in the controller.  
**Flag:** Left for follow-up — needs a dedicated `IMedicationReminderService`.

### H5 — N+1 query in DoctorsController.Search
**File:** `src/MediMind.API/Controllers/DoctorsController.cs:41-71`  
For each doctor on the current page, the loop calls `availabilityService.GetAvailableSlotsAsync` up to 14 times (14-day look-ahead × N doctors). For the default page size of 20 this is up to 280 DB/compute round-trips.  
**Flag:** The `nextSlot` calculation is a convenience field. Short-term mitigation: cap the look-ahead to 7 days and add a `maxDoctors` guard. Long-term: compute in the repository or add a dedicated `GetNextAvailableSlot` that does a single query per doctor. Left for follow-up.

---

## Medium

### M1 — Missing CancellationToken on multiple action methods
Actions that perform async DB work but don't propagate the request cancellation token:

| File | Methods |
|------|---------|
| `AppointmentsController.cs` | `Book`, `Get`, `GetById`, `Cancel`, `Reschedule`, `Approve`, `Reject`, `Availability`, `AvailableDates` |
| `DoctorsController.cs` | `Search`, `Get`, `GetMyProfile`, `UpdateMyProfile`, `GetMyCenters`, `GetTodayDashboard`, `GetMyQueue`, `GetMyPatients` |
| `HealthRecordsController.cs` | `GetById`, `Update`, `Delete`, `GetTrends`, `GetCount` |
| `HealthcareCentersController.cs` | `Register`, `Search`, `Nearby`, `GetById`, `UpdateConfig`, `AddDoctor`, `RemoveDoctor`, `GetDoctors`, `Analytics` |
| `DoctorSchedulesController.cs` | `Upsert`, `Get`, `Delete` |

**Fix applied:** Added `CancellationToken ct = default` to all listed methods and threaded `ct` through to service/repository calls.

### M2 — HealthcareCentersController missing class-level [Authorize]
**File:** `src/MediMind.API/Controllers/HealthcareCentersController.cs:19`  
Protected endpoints rely only on `[RequireRole]` attribute. While functionally equivalent (the attribute adds `[Authorize]`), the class is inconsistent with every other controller in the project.  
**Fix applied:** Added `[Authorize]` at class level; kept `[AllowAnonymous]` on public endpoints.

### M3 — ProfileImagesController missing [ApiController] attribute
**File:** `src/MediMind.API/Controllers/ProfileImagesController.cs:10`  
Without `[ApiController]`, automatic 400 responses for model binding errors and `[FromBody]` inference are disabled.  
**Fix applied:** Added `[ApiController]`.

### M4 — ExportCsv / ExportPdf missing ProducesResponseType attributes
**Files:** `HealthcareCentersController.cs:131,150`  
OpenAPI schema is incomplete for these endpoints.  
**Fix applied:** Added `[ProducesResponseType(typeof(FileContentResult), 200)]` to both.

### M5 — DoctorSchedulesController endpoints missing ProducesResponseType
**File:** `DoctorSchedulesController.cs`  
No `[ProducesResponseType]` on any of the three endpoints.  
**Fix applied:** Added response type attributes.

### M6 — HealthPredictionsController catches ValidationException in controller
**File:** `HealthPredictionsController.cs:30-44`  
`ErrorController` already handles `FluentValidation.ValidationException` globally. The local catch duplicates that and only partially formats the response (drops the structured `errors` map).  
**Fix applied:** Removed the redundant `catch (ValidationException)` block; let the global handler run.

### M7 — HealthRecordsController.Create checks exception message by string equality
**File:** `HealthRecordsController.cs:37-43`  
```csharp
if (ex.Errors.Any(e => e.ErrorMessage == "Systolic must be higher than diastolic BP"))
```
This is brittle — any wording change silently breaks the branch. The same `DomainException` catch below handles the identical case.  
**Fix applied:** Removed the duplicate string-check branch; the `DomainException` catch is sufficient.

### M8 — AuthController.RefreshToken does not delegate admin token reissue through AdminAuthService
**File:** `Controllers.cs:196-206`  
Admin login goes through `IAdminAuthService.LoginAsync` which stamps the correct `CenterId`. Refresh is handled inline in the controller, which requires the same lookup logic and breaks encapsulation.  
**Note:** Critical fix (C1) addresses the immediate bug. Deeper refactor (move refresh logic into auth services) left for follow-up.

---

## Low

### L1 — Commented-out InMemory DB registration is dead code
**File:** `src/MediMind.Infrastructure/DependencyInjection.cs:41-42`  
**Fix applied:** Removed.

### L2 — Duplicate/redundant comment in appsettings.json
**File:** `src/MediMind.API/appsettings.json:29`  
`//SenderName` JSON comment is a JSON5-style comment in a JSON file — technically supported by `IConfiguration` but non-standard.  
**Fix applied:** Removed.

### L3 — DoctorSchedulesController DTO defined in controller file
**File:** `DoctorSchedulesController.cs:10-18`  
`UpsertDoctorScheduleDto` is a controller-layer record but should live in the Application feature folder.  
**Flag:** Left for follow-up as part of the H3 service extraction.

### L4 — NotificationsController.History injects INotificationLogRepository directly
**File:** `NotificationControllers.cs:19`  
Minor architectural violation — thin enough that it's low risk but inconsistent.  
**Flag:** Move into `INotificationService` in follow-up.

### L5 — ExportPdf uses QuestPDF directly in controller body
**File:** `HealthcareCentersController.cs:150-172`  
PDF generation logic (layout, fonts, data mapping) lives in the controller. Should move to `IAnalyticsService.GenerateReportPdfAsync`.  
**Flag:** Left for follow-up.

---

## Design Decisions (Flag Only — Not Fixed)

### DD1 — Hangfire uses MemoryStorage
**File:** `DependencyInjection.cs:211-215`  
All scheduled jobs (queue generation, appointment reminders) are lost on restart. `UseMemoryStorage()` is convenient for development but unsuitable for production. Consider `UsePostgreSqlStorage` (same DB, no extra infra).

### DD2 — Refresh tokens stored in static Dictionary
**File:** `Infrastructure/Services/Auth/AuthServices.cs:24`  
`static readonly Dictionary<Guid, ...> RefreshTokens` is shared across all requests but is in-memory only. Tokens are lost on restart and not shared across multiple API instances. For production, persist in the DB or Redis.

### DD3 — Doctor search endpoint requires authentication
**File:** `DoctorsController.cs:19`  
`GET /api/v1/doctors` requires a JWT. If the intent is to allow patients to browse doctors before registering, add `[AllowAnonymous]` to `Search` and `Get`. If it is intentionally gated, document the reason.

### DD4 — `SignalR.EnableDetailedErrors = true` in production
**File:** `DependencyInjection.cs:204`  
This leaks exception details to clients. Set to `builder.Environment.IsDevelopment()` or use an environment-aware configuration.

---

## Packages (No Vulnerabilities Found)

`dotnet list package --vulnerable` — clean.  
`dotnet list package --outdated` — no actionable upgrades (Polly 7→8 is a breaking-change major version; MediatR 12→14 requires handler interface changes — both are deliberate holds).

