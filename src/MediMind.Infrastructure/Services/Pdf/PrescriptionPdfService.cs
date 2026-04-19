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

        var qr = rx.QrCode ?? string.Empty;
        return await GeneratePrescriptionPdfAsync(rx, rx.Patient, rx.Doctor, rx.Center, qr, ct);
    }

    public Task<byte[]> GeneratePrescriptionPdfAsync(
        Prescription prescription,
        Patient patient,
        Doctor doctor,
        HealthcareCenter center,
        string qrCodeDataUrl,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var doc = BuildDocument(prescription, patient, doctor, center, qrCodeDataUrl);
        return Task.FromResult(doc.GeneratePdf());
    }

    private static IDocument BuildDocument(
        Prescription rx,
        Patient patient,
        Doctor doctor,
        HealthcareCenter center,
        string qrCodeDataUrl)
    {
        var shortRef = PrescriptionRefFormatter.Format(rx.Id);
        var expiryLine = rx.ExpiryDate is { } exp
            ? exp.ToString("dd MMMM yyyy")
            : "Valid for 30 days from issue date";

        byte[]? qrBytes = null;
        if (!string.IsNullOrWhiteSpace(qrCodeDataUrl) &&
            qrCodeDataUrl.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase))
        {
            var b64 = qrCodeDataUrl["data:image/png;base64,".Length..];
            try
            {
                qrBytes = Convert.FromBase64String(b64);
            }
            catch
            {
                qrBytes = null;
            }
        }

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.ConstantItem(60).Height(40).Background(Colors.Blue.Medium);
                        row.RelativeItem().AlignCenter().AlignMiddle()
                            .Text("MEDICAL PRESCRIPTION")
                            .Bold().FontSize(16);
                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item().Text(center.CenterName).FontSize(9);
                            right.Item().Text(center.Address).FontSize(8).FontColor(Colors.Grey.Darken2);
                        });
                    });
                    header.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().Column(main =>
                {
                    main.Spacing(12);

                    main.Item().Background(Colors.Grey.Lighten3).Padding(12).Column(box =>
                    {
                        box.Item().Text($"Prescription #: {shortRef}").SemiBold();
                        box.Item().Text($"Date: {rx.IssueDate:dd MMMM yyyy}");
                        box.Item().Text($"Expiry: {expiryLine}");
                    });

                    main.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("Patient Information").Bold().FontSize(11);
                            left.Item().Text($"Name: {patient.FullName}");
                            left.Item().Text($"Date of Birth: {patient.DateOfBirth:dd MMMM yyyy}");
                            left.Item().Text($"Phone: {patient.PhoneNumber}");
                        });
                        row.RelativeItem().Column(right =>
                        {
                            right.Item().Text("Doctor Information").Bold().FontSize(11);
                            right.Item().Text($"Dr. {doctor.FullName}");
                            right.Item().Text(doctor.Specialization);
                            right.Item().Text($"License: {doctor.LicenseNumber}");
                            right.Item().Text(center.CenterName);
                        });
                    });

                    main.Item().Column(d =>
                    {
                        d.Item().Text("DIAGNOSIS:").Bold();
                        d.Item().Text(rx.Diagnosis);
                    });

                    main.Item().Text("Medications").Bold().FontSize(11);
                    main.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn(2);
                        });

                        IContainer CellStyle(IContainer c, bool header, int index)
                        {
                            var bg = header
                                ? Colors.Blue.Lighten4
                                : (index % 2 == 0 ? Colors.White : Colors.Blue.Lighten5);
                            return c.Background(bg).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6);
                        }

                        table.Header(h =>
                        {
                            h.Cell().Element(c => CellStyle(c, true, 0)).Text("Medication").SemiBold();
                            h.Cell().Element(c => CellStyle(c, true, 1)).Text("Dosage").SemiBold();
                            h.Cell().Element(c => CellStyle(c, true, 2)).Text("Frequency").SemiBold();
                            h.Cell().Element(c => CellStyle(c, true, 3)).Text("Duration").SemiBold();
                            h.Cell().Element(c => CellStyle(c, true, 4)).Text("Instructions").SemiBold();
                        });

                        var i = 0;
                        foreach (var m in rx.Medications)
                        {
                            var idx = i++;
                            table.Cell().Element(c => CellStyle(c, false, idx)).Text(m.Name);
                            table.Cell().Element(c => CellStyle(c, false, idx)).Text(m.Dosage);
                            table.Cell().Element(c => CellStyle(c, false, idx)).Text(m.Frequency);
                            table.Cell().Element(c => CellStyle(c, false, idx)).Text(m.Duration);
                            table.Cell().Element(c => CellStyle(c, false, idx)).Text(m.Instructions ?? "—");
                        }
                    });

                    if (rx.LabTests.Count > 0)
                    {
                        main.Item().Text("RECOMMENDED LAB TESTS:").Bold().FontSize(11);
                        foreach (var lab in rx.LabTests)
                            main.Item().Text($"• {lab}");
                    }

                    if (!string.IsNullOrWhiteSpace(rx.FollowUpInstructions))
                    {
                        main.Item().Column(f =>
                        {
                            f.Item().Text("FOLLOW-UP INSTRUCTIONS:").Bold();
                            f.Item().Text(rx.FollowUpInstructions!);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(rx.SpecialInstructions))
                    {
                        main.Item().Text(rx.SpecialInstructions!);
                    }
                });

                page.Footer().Column(foot =>
                {
                    foot.Item().Row(row =>
                    {
                        if (qrBytes is not null)
                            row.ConstantItem(80).Height(80).Image(qrBytes);
                        else
                            row.ConstantItem(80).Height(80).Background(Colors.Grey.Lighten2);

                        row.RelativeItem().AlignMiddle().AlignCenter()
                            .Text("Scan QR to verify authenticity").FontSize(9);

                        row.RelativeItem().AlignRight().Column(sig =>
                        {
                            sig.Item().Text("_________________________").FontSize(9);
                            sig.Item().Text($"Dr. {doctor.FullName}").FontSize(9);
                        });
                    });
                    foot.Item().PaddingTop(8).AlignCenter().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(8));
                        t.Span("This prescription was generated by MediMind. For verification: ");
                        t.Span($"medimind.et/verify/{rx.Id}").FontColor(Colors.Blue.Medium);
                    });
                });
            });
        });
    }
}

internal static class PrescriptionRefFormatter
{
    public static string Format(Guid id) => "RX-" + id.ToString("N")[..8].ToUpperInvariant();
}
