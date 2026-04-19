using MediMind.Domain.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MediMind.Infrastructure.Services.Prescriptions;

public class PrescriptionLocalFileStorage(
    IHostEnvironment environment,
    IOptions<PrescriptionStorageOptions> options) : IPrescriptionFileStorage
{
    private readonly PrescriptionStorageOptions _options = options.Value;

    public async Task<string> SavePrescriptionPdfAsync(Guid prescriptionId, byte[] pdfBytes, CancellationToken ct = default)
    {
        var dir = ResolveDirectory();
        Directory.CreateDirectory(dir);
        var fileName = $"{prescriptionId:N}.pdf";
        var fullPath = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(fullPath, pdfBytes, ct);

        var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";
        var relative = $"prescriptions/{fileName}";
        return baseUrl + relative;
    }

    public async Task<byte[]?> GetPrescriptionPdfAsync(Guid prescriptionId, CancellationToken ct = default)
    {
        var path = Path.Combine(ResolveDirectory(), $"{prescriptionId:N}.pdf");
        if (!File.Exists(path))
            return null;
        return await File.ReadAllBytesAsync(path, ct);
    }

    private string ResolveDirectory()
    {
        var relative = (_options.PrescriptionPdfPath ?? "storage/prescriptions/").Trim().TrimStart('/');
        return Path.Combine(environment.ContentRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
