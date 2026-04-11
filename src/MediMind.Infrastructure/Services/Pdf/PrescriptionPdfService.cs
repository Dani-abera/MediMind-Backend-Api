using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MediMind.Infrastructure.Services.Pdf;

public sealed class PrescriptionPdfService(MediMindDbContext db) : IPdfService
{
    static PrescriptionPdfService() =>
        QuestPDF.Settings.License = LicenseType.Community;

    public async Task<byte[]> GeneratePrescriptionPdfAsync(Guid prescriptionId, CancellationToken ct = default)
    {
        var rx = await db.Prescriptions
            .AsNoTracking()
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .Include(p => p.Center)
            .FirstOrDefaultAsync(p => p.Id == prescriptionId, ct)
            ?? throw new InvalidOperationException($"Prescription {prescriptionId} not found.");

        return BuildDocument(rx).GeneratePdf();
    }

    private static IDocument BuildDocument(Prescription rx)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Text("MediMind — Prescription").SemiBold().FontSize(18);
                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Issued: {rx.IssueDate:yyyy-MM-dd}");
                    if (rx.ExpiryDate is { } exp)
                        col.Item().Text($"Expires: {exp:yyyy-MM-dd}");
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().Text($"Patient: {rx.Patient.FullName}");
                    col.Item().Text($"Doctor: {rx.Doctor.FullName}");
                    col.Item().Text($"Center: {rx.Center.CenterName}");
                    col.Item().Text($"Diagnosis: {rx.Diagnosis}").Bold();
                    col.Item().Text("Medications:").SemiBold();
                    foreach (var m in rx.Medications)
                    {
                        col.Item().Text($"• {m.Name} — {m.Dosage}, {m.Frequency}, {m.Duration}");
                    }
                    if (rx.LabTests.Count > 0)
                    {
                        col.Item().Text("Lab tests:").SemiBold();
                        foreach (var lab in rx.LabTests)
                            col.Item().Text($"• {lab}");
                    }
                    if (!string.IsNullOrWhiteSpace(rx.FollowUpInstructions))
                        col.Item().Text($"Follow-up: {rx.FollowUpInstructions}");
                    if (!string.IsNullOrWhiteSpace(rx.SpecialInstructions))
                        col.Item().Text($"Notes: {rx.SpecialInstructions}");
                });
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Prescription ID: ");
                    x.Span(rx.Id.ToString()).FontFamily(Fonts.CourierNew);
                });
            });
        });
    }
}
