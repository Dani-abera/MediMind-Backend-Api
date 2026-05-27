using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MediMind.Application.Features.VideoConsultations;
using Microsoft.Extensions.Options;

namespace MediMind.Infrastructure.Services;

public sealed class AgoraOptions
{
    public const string SectionName = "Agora";
    public string AppId { get; init; } = string.Empty;
    public string AppCertificate { get; init; } = string.Empty;
}

/// <summary>
/// Agora AccessToken2 generator (token version "007"). Mirrors the algorithm
/// in Agora's official Node.js `agora-token` npm package exactly.
///
/// Spec (all integers little-endian, all HMACs SHA-256):
///   signingKey  = HMAC-SHA256(key = PackUint32(issueTs), msg = appCert_utf8)
///   signingKey  = HMAC-SHA256(key = PackUint32(salt),    msg = signingKey)
///   signingInfo = PackString(appId) | PackUint32(issueTs) | PackUint32(expire) | PackUint32(salt) | servicesMap
///   signature   = HMAC-SHA256(key = signingKey, msg = signingInfo)
///   content     = PackBytes(signature) | signingInfo
///   token       = "007" + base64(zlib_deflate(content))
///
///   service     = uint16 type | uint16 privCount | sorted[uint16 key | uint32 expire] | extras
///       RTC extras: PackString(channelName) | PackString(uidString)
///       RTM extras: PackString(userIdString)
///   servicesMap = uint16 count | sorted[service_bytes] (each service self-prefixes its type)
/// </summary>
public sealed class AgoraTokenService(IOptions<AgoraOptions> options) : IAgoraTokenService
{
    private const string Version = "007";
    private const ushort ServiceTypeRtc = 1;
    private const ushort ServiceTypeRtm = 2;

    // RTC privilege keys
    private const ushort RtcPrivilegeJoinChannel = 1;
    private const ushort RtcPrivilegePublishAudioStream = 2;
    private const ushort RtcPrivilegePublishVideoStream = 3;
    private const ushort RtcPrivilegePublishDataStream = 4;

    // RTM privilege keys
    private const ushort RtmPrivilegeLogin = 1;

    private readonly AgoraOptions _opts = options.Value;

    public string AppId => _opts.AppId;

    public string GenerateRtcToken(string channelName, string userIdString = "", int expirationSeconds = 3600)
    {
        if (!HasCredentials()) return string.Empty;

        var privileges = new SortedDictionary<ushort, uint>
        {
            [RtcPrivilegeJoinChannel] = (uint)expirationSeconds,
            [RtcPrivilegePublishAudioStream] = (uint)expirationSeconds,
            [RtcPrivilegePublishVideoStream] = (uint)expirationSeconds,
            [RtcPrivilegePublishDataStream] = (uint)expirationSeconds,
        };

        var serviceBytes = PackRtcService(channelName, userIdString, privileges);
        return Build(expirationSeconds, [(ServiceTypeRtc, serviceBytes)]);
    }

    public string GenerateRtmToken(string userIdString, int expirationSeconds = 3600)
    {
        if (!HasCredentials() || string.IsNullOrEmpty(userIdString)) return string.Empty;

        var privileges = new SortedDictionary<ushort, uint>
        {
            [RtmPrivilegeLogin] = (uint)expirationSeconds,
        };

        var serviceBytes = PackRtmService(userIdString, privileges);
        return Build(expirationSeconds, [(ServiceTypeRtm, serviceBytes)]);
    }

    private bool HasCredentials() =>
        !string.IsNullOrWhiteSpace(_opts.AppId) && !string.IsNullOrWhiteSpace(_opts.AppCertificate);

    private string Build(int expirationSeconds, IReadOnlyList<(ushort Type, byte[] Bytes)> services)
    {
        var issueTs = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var salt = (uint)Random.Shared.NextInt64(1, uint.MaxValue);
        return BuildInternal(issueTs, salt, (uint)expirationSeconds, services);
    }

    // Exposed to MediMind.UnitTests via InternalsVisibleTo so the algorithm can
    // be verified against fixed inputs without bleeding live time/random into
    // the assertion.
    internal string BuildInternal(uint issueTs, uint salt, uint expire,
        IReadOnlyList<(ushort Type, byte[] Bytes)> services)
    {
        // ── signing key ──
        // HMAC-SHA256 chain: packed uint32 is the *key*, cert/prior-HMAC is the *message*.
        // This matches the JS: encodeHMac(packUint32(issueTs), appCertificate)
        //                      encodeHMac(packUint32(salt), signingKey)
        var appCertBytes = Encoding.UTF8.GetBytes(_opts.AppCertificate);
        var signingKey = HMACSHA256.HashData(PackUint32(issueTs), appCertBytes);
        signingKey = HMACSHA256.HashData(PackUint32(salt), signingKey);

        // ── signing_info ──
        // JS: putString(appId) + putUint32(issueTs) + putUint32(expire) + putUint32(salt)
        //     + putUint16(serviceCount) + forEach(service.pack())
        // NOTE: expire IS included in signingInfo (the old C# code was missing it!)
        byte[] signingInfo;
        using (var buf = new MemoryStream())
        {
            WriteString(buf, _opts.AppId);
            WriteUint32(buf, issueTs);
            WriteUint32(buf, expire);   // ← was missing before!
            WriteUint32(buf, salt);

            // inline services map (sorted by type)
            WriteUint16(buf, (ushort)services.Count);
            foreach (var (type, bytes) in services.OrderBy(s => s.Type))
            {
                // Each service is already fully packed (type + privileges + extras)
                // BUT: the JS packs service.pack() which starts with the type inside
                // Service.pack(). We must NOT write the type again here.
                // Actually, looking at JS: the services dict is keyed by type, and
                // service.pack() = __pack_type() + __pack_privileges() + extras.
                // The outer loop just does: signing_info = concat(signing_info, service.pack())
                // So service bytes already contain the type prefix.
                // But our C# PackRtcService/PackRtmService do NOT include the type prefix!
                // So we must write it here.
                WriteUint16(buf, type);
                buf.Write(bytes, 0, bytes.Length);
            }
            signingInfo = buf.ToArray();
        }

        // ── signature ──
        var signature = HMACSHA256.HashData(signingKey, signingInfo);

        // ── content ──
        // JS: content = concat(putString(signature).pack(), signing_info)
        // i.e. content = PackBytes(signature) + signingInfo (reused verbatim)
        byte[] content;
        using (var buf = new MemoryStream())
        {
            WriteBytes(buf, signature);
            buf.Write(signingInfo, 0, signingInfo.Length);
            content = buf.ToArray();
        }

        // ── zlib compress ──
        byte[] compressedContent;
        using (var output = new MemoryStream())
        {
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
            {
                zlib.Write(content, 0, content.Length);
            }
            compressedContent = output.ToArray();
        }

        return Version + Convert.ToBase64String(compressedContent);
    }

    private static byte[] PackRtcService(string channelName, string uid, SortedDictionary<ushort, uint> privileges)
    {
        using var buf = new MemoryStream();

        WritePrivileges(buf, privileges);
        WriteString(buf, channelName);
        WriteString(buf, uid);
        return buf.ToArray();
    }

    private static byte[] PackRtmService(string userId, SortedDictionary<ushort, uint> privileges)
    {
        using var buf = new MemoryStream();

        WritePrivileges(buf, privileges);
        WriteString(buf, userId);
        return buf.ToArray();
    }

    private static void WritePrivileges(Stream s, SortedDictionary<ushort, uint> privileges)
    {
        WriteUint16(s, (ushort)privileges.Count);
        foreach (var kv in privileges)
        {
            WriteUint16(s, kv.Key);
            WriteUint32(s, kv.Value);
        }
    }

    // All packed little-endian to match the npm agora-token spec
    // (Buffer.writeUInt32LE / writeUInt16LE).
    private static byte[] PackUint32(uint v) =>
    [
        (byte)(v & 0xFF),
        (byte)((v >> 8) & 0xFF),
        (byte)((v >> 16) & 0xFF),
        (byte)((v >> 24) & 0xFF),
    ];

    private static void WriteUint16(Stream s, ushort v)
    {
        s.WriteByte((byte)(v & 0xFF));
        s.WriteByte((byte)((v >> 8) & 0xFF));
    }

    private static void WriteUint32(Stream s, uint v)
    {
        s.WriteByte((byte)(v & 0xFF));
        s.WriteByte((byte)((v >> 8) & 0xFF));
        s.WriteByte((byte)((v >> 16) & 0xFF));
        s.WriteByte((byte)((v >> 24) & 0xFF));
    }

    private static void WriteString(Stream s, string v)
    {
        var bytes = Encoding.UTF8.GetBytes(v);
        WriteUint16(s, (ushort)bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static void WriteBytes(Stream s, byte[] bytes)
    {
        WriteUint16(s, (ushort)bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }
}
