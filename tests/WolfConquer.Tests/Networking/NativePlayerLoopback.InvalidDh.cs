using System.Globalization;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

internal sealed partial class NativePlayerLoopback
{
    internal async Task AssertInvalidDhPeerClosesBeforeTransportDispatchAsync(string kind)
    {
        if (_mode != ConquerTcpWireMode.LegacyGameServer) throw new InvalidOperationException("Actual legacy game listener required.");
        var socket = await ConnectAsync(); // Existing helper owns the socket before its first await.
        var stream = socket.GetStream();
        var handshake = await ReadServerHandshakeAsync(stream, Token);
        int offset = 53;
        var prime = ReadBlob(handshake, ref offset);
        _ = ReadBlob(handshake, ref offset); // Preserve actual packet extraction; no fabricated server parameters.
        _ = ReadBlob(handshake, ref offset);
        var modulus = BigInteger.Parse("0" + prime, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var key = kind switch
        {
            "empty" => string.Empty,
            "zero" => "0",
            "one" => "1",
            "p-minus-one" => (modulus - BigInteger.One).ToString("X", CultureInfo.InvariantCulture).TrimStart('0'),
            "p" => prime,
            "nonhex" => "not-hex",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var reply = ClientHandshakeReply(key);
        new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey)).EncryptInPlace(reply);
        byte[] transmitted = reply;
        if (kind == "one")
        {
            // Independent mathematical shared secret1, through the unchanged real packet-key derivation/cipher.
            // Never ask the hardened DH object to accept1 just to manufacture a passing client.
            var material = new byte[LegacyGameServerCipher.KeyMaterialSize];
            Assert.Equal(LegacyGameServerCipher.KeyMaterialSize,
                LegacyGameServerCipher.DeriveKeyMaterialFromDiffieHellmanSharedSecret(new byte[] { 1 }, material));
            var probe = LegacyGameServerFrameCodec.EncodeEncrypted(ProbeFrame(), new LegacyGameServerCipher(material), LegacyGameServerFrameSeal.Client);
            transmitted = new byte[reply.Length + probe.Length]; reply.CopyTo(transmitted, 0); probe.CopyTo(transmitted, reply.Length);
        }

        var eof = ReadRejectedDhEofAsync(stream, Token);
        _drains.Add(eof); // Same index as _clients; this is the ACTUAL owned EOF read, not a completed placeholder.
        await stream.WriteAsync(transmitted, Token); // One real login-cipher envelope, coalesced with the key1 probe when applicable.
        await eof; // Client send-side stays open: peer/server closure must supply real EOF.
        Token.ThrowIfCancellationRequested();
        Assert.False(_seen.Reader.TryRead(out _)); // Actual registered transport callback, not World/authentication acceptance.
        AssertNoTransportDiagnostics();
        Assert.Contains("rejected legacy DH peer key", _transportLog.Text, StringComparison.Ordinal);
        socket.Dispose(); // Retained in _clients for idempotent fixture settlement as well.
    }

    private static async Task ReadRejectedDhEofAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new byte[1];
        var count = await stream.ReadAsync(buffer, token);
        token.ThrowIfCancellationRequested();
        Assert.Equal(0, count); // Cancellation/reset/timeout/nonempty data is not successful EOF evidence.
    }
}
