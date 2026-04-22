using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace MediMind.API.Authorization;

/// <summary>
/// FR-023: Doctor can access patient data only when there is an appointment row
/// with (DoctorId = me, PatientId = target) and Status IN (Confirmed, InProgress, Completed).
/// </summary>
public class DoctorPatientAccessRequirement : IAuthorizationRequirement { }

public class DoctorPatientAccessHandler(IAppointmentRepository appointmentRepository)
    : AuthorizationHandler<DoctorPatientAccessRequirement, Guid>
{
    private static readonly AppointmentStatus[] ValidStatuses =
        [AppointmentStatus.Confirmed, AppointmentStatus.InProgress, AppointmentStatus.Completed];

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DoctorPatientAccessRequirement requirement,
        Guid patientId)
    {
        var userType = context.User.FindFirst("user_type")?.Value;
        if (userType != "Doctor")
            return;

        var subClaim = context.User.FindFirst("sub")?.Value
                       ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(subClaim, out var doctorId))
            return;

        var appointments = await appointmentRepository.GetByPatientAsync(patientId, CancellationToken.None);
        if (appointments.Any(a => a.DoctorId == doctorId && ValidStatuses.Contains(a.Status)))
            context.Succeed(requirement);
    }
}
