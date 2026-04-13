using MediMind.Domain.Common.Interfaces;
using Microsoft.Extensions.Hosting;

namespace MediMind.Infrastructure.Services.Storage;

/// <summary>
/// Stores uploads under ContentRootPath/uploads when Cloudinary credentials are not configured.
/// </summary>
public sealed class LocalUploadStorageService(IHostEnvironment env) : IStorageService
{
    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string containerName,
        CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        var dir = Path.Combine(env.ContentRootPath, "uploads", containerName);
        Directory.CreateDirectory(dir);
        var fullPath = Path.Combine(dir, safeName);
        await using var fs = File.Create(fullPath);
        await fileStream.CopyToAsync(fs, ct);
        return $"/uploads/{containerName}/{safeName}";
    }

    public Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(fileUrl) || !fileUrl.StartsWith("/uploads/", StringComparison.Ordinal))
            return Task.CompletedTask;

        var relative = fileUrl["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var path = Path.Combine(env.ContentRootPath, "uploads", relative);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }
}
