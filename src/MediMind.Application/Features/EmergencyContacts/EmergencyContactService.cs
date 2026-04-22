using AutoMapper;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.EmergencyContacts;

public interface IEmergencyContactService
{
    Task<IReadOnlyList<EmergencyContactResponseDto>> GetAllAsync(Guid patientId, CancellationToken ct = default);
    Task<EmergencyContactResponseDto> CreateAsync(Guid patientId, CreateEmergencyContactDto dto, CancellationToken ct = default);
    Task<EmergencyContactResponseDto> UpdateAsync(Guid patientId, Guid contactId, UpdateEmergencyContactDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid patientId, Guid contactId, CancellationToken ct = default);
    Task SetPrimaryAsync(Guid patientId, Guid contactId, CancellationToken ct = default);
}

public class EmergencyContactService(
    IEmergencyContactRepository repository,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IEmergencyContactService
{
    private const int MaxContacts = 3;

    public async Task<IReadOnlyList<EmergencyContactResponseDto>> GetAllAsync(Guid patientId, CancellationToken ct = default)
    {
        var contacts = await repository.GetByPatientIdAsync(patientId, ct);
        return mapper.Map<IReadOnlyList<EmergencyContactResponseDto>>(contacts);
    }

    public async Task<EmergencyContactResponseDto> CreateAsync(Guid patientId, CreateEmergencyContactDto dto, CancellationToken ct = default)
    {
        var count = await repository.CountByPatientAsync(patientId, ct);
        if (count >= MaxContacts)
            throw new DomainException($"A patient may have at most {MaxContacts} emergency contacts.");

        var existing = await repository.GetByPatientIdAsync(patientId, ct);
        bool isPrimary = dto.IsPrimary || !existing.Any();

        if (isPrimary)
            await repository.ClearPrimaryAsync(patientId, ct);

        var contact = EmergencyContact.Create(patientId, dto.FullName, dto.Relationship, dto.PhoneNumber, isPrimary);
        await repository.AddAsync(contact, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<EmergencyContactResponseDto>(contact);
    }

    public async Task<EmergencyContactResponseDto> UpdateAsync(Guid patientId, Guid contactId, UpdateEmergencyContactDto dto, CancellationToken ct = default)
    {
        var contact = await repository.GetByIdAsync(contactId, patientId, ct)
            ?? throw new NotFoundException(nameof(EmergencyContact), contactId);

        contact.Update(dto.FullName, dto.Relationship, dto.PhoneNumber);
        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<EmergencyContactResponseDto>(contact);
    }

    public async Task DeleteAsync(Guid patientId, Guid contactId, CancellationToken ct = default)
    {
        var contact = await repository.GetByIdAsync(contactId, patientId, ct)
            ?? throw new NotFoundException(nameof(EmergencyContact), contactId);

        await repository.DeleteAsync(contact, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // If the deleted contact was primary, promote the first remaining one
        if (contact.IsPrimary)
        {
            var remaining = await repository.GetByPatientIdAsync(patientId, ct);
            var first = remaining.FirstOrDefault();
            if (first is not null)
            {
                first.SetPrimary(true);
                await unitOfWork.SaveChangesAsync(ct);
            }
        }
    }

    public async Task SetPrimaryAsync(Guid patientId, Guid contactId, CancellationToken ct = default)
    {
        var contact = await repository.GetByIdAsync(contactId, patientId, ct)
            ?? throw new NotFoundException(nameof(EmergencyContact), contactId);

        await repository.ClearPrimaryAsync(patientId, ct);
        contact.SetPrimary(true);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
