using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MediMind.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediMind.UnitTests.Infrastructure;

/// <summary>
/// Verifies that <see cref="AgoraTokenService"/> generates AccessToken2 ("007")
/// tokens whose binary layout and signature exactly match the npm
/// <c>agora-token</c> spec — the implementation the Agora server actually
/// validates against. Tests use fixed issueTs/salt via the internal
/// <c>BuildInternal</c> hook so the assertion isn't subject to clock/random drift.
/// </summary>
public sealed class AgoraTokenServiceTests
{
    // Fixed test inputs — same shape as the real values in appsettings.json.
    private const string TestAppId = "0b95a2bb84eb478eb8fcaf67c2fda48f";
    private const string TestAppCert = "1e7454cb9b124fe88552f9e08f6fcc05";
    private const uint TestIssueTs = 1735689600;       // arbitrary fixed Unix ts
    private const uint TestSalt = 0x12345678;
    private const uint TestExpire = 3600;
    private const string TestChannel = "room_2acb37739d9e4e82b7506e3981346fbb";
    private const string TestUserId = "d42214cb-d851-494c-912a-c6c97c3e6521";

    private static AgoraTokenService BuildService() =>
        new(Options.Create(new AgoraOptions
        {
            AppId = TestAppId,
            AppCertificate = TestAppCert,
        }));

    [Fact]
    public void RtcToken_StartsWith_007()
    {
        var svc = BuildService();
        var token = svc.GenerateRtcToken(TestChannel);
        token.Should().StartWith("007");
    }

    [Fact]
    public void RtmToken_StartsWith_007()
    {
        var svc = BuildService();
        var token = svc.GenerateRtmToken(TestUserId);
        token.Should().StartWith("007");
    }

    [Fact]
    public void EmptyCredentials_YieldEmptyToken()
    {
        var svc = new AgoraTokenService(Options.Create(new AgoraOptions
        {
            AppId = "",
            AppCertificate = "",
        }));

        svc.GenerateRtcToken(TestChannel).Should().BeEmpty();
        svc.GenerateRtmToken(TestUserId).Should().BeEmpty();
    }

    /// <summary>
    /// The crucial test: decode our generated token and recompute the
    /// expected signature from its payload. If the algorithm is right, the
    /// signature embedded in the token matches HMAC-SHA256 over the
    /// reconstructed signingInfo using the derived signing key. If anything
    /// is off (endianness, HMAC algorithm, HMAC arg order, field layout)
    /// this fails.
    /// </summary>
    [Fact]
    public void RtcToken_Signature_VerifiesAgainstAlgorithm()
    {
        var svc = BuildService();

        // Build RTC service bytes for the fixed inputs (matches PackRtcService).
        var serviceBytes = BuildRtcServiceBytes(TestChannel, "", TestExpire);
        var services = new[] { ((ushort)1, serviceBytes) };

        var token = svc.BuildInternal(TestIssueTs, TestSalt, TestExpire, services);

        token.Should().StartWith("007");
        VerifyTokenSignature(token, TestAppId, TestAppCert, TestIssueTs, TestSalt, TestExpire, serviceBytes, serviceType: 1);
    }

    [Fact]
    public void RtmToken_Signature_VerifiesAgainstAlgorithm()
    {
        var svc = BuildService();

        var serviceBytes = BuildRtmServiceBytes(TestUserId, TestExpire);
        var services = new[] { ((ushort)2, serviceBytes) };

        var token = svc.BuildInternal(TestIssueTs, TestSalt, TestExpire, services);

        token.Should().StartWith("007");
        VerifyTokenSignature(token, TestAppId, TestAppCert, TestIssueTs, TestSalt, TestExpire, serviceBytes, serviceType: 2);
    }

    // -- Verification helpers (independent reconstruction of the spec) -------

    private static void VerifyTokenSignature(
        string token, string appId, string appCert,
        uint issueTs, uint salt, uint expire,
        byte[] singleServiceBytes, ushort serviceType)
    {
        // Strip "007", base64-decode
        var content = Convert.FromBase64String(token.Substring(3));

        // Read embedded signature
        var sigLen = ReadUInt16LE(content, 0);
        sigLen.Should().Be(32, "HMAC-SHA256 outputs 32 bytes");
        var embeddedSig = content.AsSpan(2, sigLen).ToArray();

        // Re-derive the signing key
        var firstHmac = HMACSHA256.HashData(PackUint32LE(issueTs), Encoding.UTF8.GetBytes(appCert));
        var signingKey = HMACSHA256.HashData(PackUint32LE(salt), firstHmac);

        // Rebuild services treemap (only one service in these tests)
        using var servicesMap = new MemoryStream();
        WriteUInt16LE(servicesMap, 1);                   // service count
        WriteUInt16LE(servicesMap, serviceType);
        servicesMap.Write(singleServiceBytes, 0, singleServiceBytes.Length);
        var servicesBytes = servicesMap.ToArray();

        // Reconstruct signingInfo = PackString(appId) | uint32(issueTs) | uint32(salt) | services
        using var info = new MemoryStream();
        WriteStringLE(info, appId);
        WriteUInt32LE(info, issueTs);
        WriteUInt32LE(info, salt);
        info.Write(servicesBytes, 0, servicesBytes.Length);

        var expectedSig = HMACSHA256.HashData(signingKey, info.ToArray());
        embeddedSig.Should().BeEquivalentTo(expectedSig,
            "the signature embedded in the token must match HMAC-SHA256(signingKey, signingInfo)");

        // Also check the trailing issueTs / expire / salt are positioned correctly.
        var cursor = 2 + sigLen;
        ReadUInt32LE(content, cursor).Should().Be(issueTs);
        ReadUInt32LE(content, cursor + 4).Should().Be(expire);
        ReadUInt32LE(content, cursor + 8).Should().Be(salt);
    }

    private static byte[] BuildRtcServiceBytes(string channelName, string uid, uint expire)
    {
        using var buf = new MemoryStream();
        WriteUInt16LE(buf, 1);                           // service type
        // privileges (sorted by key): 1=JoinChannel, 2=PubAudio, 3=PubVideo, 4=PubData
        WriteUInt16LE(buf, 4);
        foreach (var key in new ushort[] { 1, 2, 3, 4 })
        {
            WriteUInt16LE(buf, key);
            WriteUInt32LE(buf, expire);
        }
        WriteStringLE(buf, channelName);
        WriteStringLE(buf, uid);
        return buf.ToArray();
    }

    private static byte[] BuildRtmServiceBytes(string userId, uint expire)
    {
        using var buf = new MemoryStream();
        WriteUInt16LE(buf, 2);                           // service type
        WriteUInt16LE(buf, 1);                           // privCount
        WriteUInt16LE(buf, 1);                           // priv key = Login
        WriteUInt32LE(buf, expire);
        WriteStringLE(buf, userId);
        return buf.ToArray();
    }

    private static byte[] PackUint32LE(uint v) =>
    [
        (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF),
    ];

    private static void WriteUInt16LE(Stream s, ushort v)
    {
        s.WriteByte((byte)(v & 0xFF));
        s.WriteByte((byte)((v >> 8) & 0xFF));
    }

    private static void WriteUInt32LE(Stream s, uint v)
    {
        s.WriteByte((byte)(v & 0xFF));
        s.WriteByte((byte)((v >> 8) & 0xFF));
        s.WriteByte((byte)((v >> 16) & 0xFF));
        s.WriteByte((byte)((v >> 24) & 0xFF));
    }

    private static void WriteStringLE(Stream s, string v)
    {
        var bytes = Encoding.UTF8.GetBytes(v);
        WriteUInt16LE(s, (ushort)bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static ushort ReadUInt16LE(byte[] data, int offset) =>
        (ushort)(data[offset] | (data[offset + 1] << 8));

    private static uint ReadUInt32LE(byte[] data, int offset) =>
        (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}
