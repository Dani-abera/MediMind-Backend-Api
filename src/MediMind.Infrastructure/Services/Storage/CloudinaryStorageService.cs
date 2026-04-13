using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MediMind.Domain.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace MediMind.Infrastructure.Services.Storage;

/// <summary>Uploads assets to Cloudinary (raw for PDFs, image for photos). Returns HTTPS URLs.</summary>
public sealed class CloudinaryStorageService(IOptions<CloudinaryOptions> options) : IStorageService
{
    private readonly Cloudinary _cloudinary = CreateClient(options.Value);

    private static Cloudinary CreateClient(CloudinaryOptions o)
    {
        var account = new Account(o.CloudName, o.ApiKey, o.ApiSecret);
        var cloudinary = new Cloudinary(account) { Api = { Secure = true } };
        return cloudinary;
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string containerName,
        CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName.Replace('\\', '/'));
        var ext = Path.GetExtension(safeName).ToLowerInvariant();
        var asRaw = containerName.Equals("prescriptions", StringComparison.OrdinalIgnoreCase)
                    || ext == ".pdf";

        if (asRaw)
        {
            var rawParams = new RawUploadParams
            {
                File = new FileDescription(safeName, fileStream),
                Folder = containerName,
                UseFilename = true,
                UniqueFilename = true
            };
            var rawResult = await _cloudinary.UploadAsync(rawParams, "raw", ct).ConfigureAwait(false);
            EnsureSuccess(rawResult.Error, rawResult.StatusCode, "raw");
            return rawResult.SecureUrl?.AbsoluteUri
                   ?? throw new InvalidOperationException("Cloudinary raw upload returned no URL.");
        }

        var imageParams = new ImageUploadParams
        {
            File = new FileDescription(safeName, fileStream),
            Folder = containerName,
            UseFilename = true,
            UniqueFilename = true
        };
        var imageResult = await _cloudinary.UploadAsync(imageParams, ct).ConfigureAwait(false);
        EnsureSuccess(imageResult.Error, imageResult.StatusCode, "image");
        return imageResult.SecureUrl?.AbsoluteUri
               ?? throw new InvalidOperationException("Cloudinary image upload returned no URL.");
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)
            || !TryParsePublicId(fileUrl, out var publicId, out var resourceType))
            return;

        var delParams = new DeletionParams(publicId) { ResourceType = resourceType };
        await _cloudinary.DestroyAsync(delParams).ConfigureAwait(false);
    }

    private static void EnsureSuccess(CloudinaryDotNet.Actions.Error? error, System.Net.HttpStatusCode status, string kind)
    {
        if (error != null)
            throw new InvalidOperationException($"Cloudinary {kind} upload failed: {error.Message}");
        if (status != System.Net.HttpStatusCode.OK)
            throw new InvalidOperationException($"Cloudinary {kind} upload failed with status {status}.");
    }

    /// <summary>Extracts public_id and resource type from a delivered Cloudinary HTTPS URL.</summary>
    internal static bool TryParsePublicId(string url, out string publicId, out ResourceType resourceType)
    {
        publicId = "";
        resourceType = ResourceType.Image;

        if (!url.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return false;

        resourceType = url.Contains("/raw/", StringComparison.OrdinalIgnoreCase)
            ? ResourceType.Raw
            : ResourceType.Image;

        const string marker = "/upload/";
        var markerIndex = url.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return false;

        var tail = url[(markerIndex + marker.Length)..];

        // Optional version segment: v1234567890/
        if (tail.Length > 1
            && tail[0] == 'v'
            && tail[1] is >= '0' and <= '9')
        {
            var slash = tail.IndexOf('/');
            if (slash > 0)
                tail = tail[(slash + 1)..];
        }

        var q = tail.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
            tail = tail[..q];

        var lastDot = tail.LastIndexOf('.');
        if (lastDot > 0)
            tail = tail[..lastDot];

        if (string.IsNullOrWhiteSpace(tail))
            return false;

        publicId = tail;
        return true;
    }
}
