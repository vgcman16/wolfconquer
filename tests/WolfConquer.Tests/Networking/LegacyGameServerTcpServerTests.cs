using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class LegacyGameServerTcpServerTests
{
    private const int HandshakeDhBlobOffset = 53;
    private const int ClientHandshakeReplySegmentLength = 17;
    private static readonly byte[] LoginKey = Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey);

    [Fact]
    public async Task DefaultWireModeSendsConfiguredServerFirstPayloadAndReceivesPlainFrame()
    {
        var received = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.WorldServer,
                IPAddress.Loopback,
                ListenPort: 0,
                ServerFirstPayloadFactory: () => new byte[] { 0xC5, 0x48, 0x69, 0x12 }),
            (_, frame, _) =>
            {
                received.TrySetResult(frame.Header.Opcode);
                return ValueTask.CompletedTask;
            });

        server.Start();
        using var client = await ConnectAsync(server);
        var stream = client.GetStream();
        var serverFirst = new byte[4];
        await stream.ReadExactlyAsync(serverFirst);

        var packet = new PacketWriter(1010, payloadLength: 2);
        packet.WriteUInt16(0, 0x1234);
        await stream.WriteAsync(packet.ToArray());

        Assert.Equal([0xC5, 0x48, 0x69, 0x12], serverFirst);
        Assert.Equal(1010, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task LegacyGameServerWireModeSendsEncryptedDhHandshakeWithLegacyShape()
    {
        await using var server = CreateLegacyLoopbackServer((_, _, _) => ValueTask.CompletedTask);

        server.Start();
        using var client = await ConnectAsync(server);

        var handshake = await ReadEncryptedLegacyHandshakeAsync(client.GetStream());

        Assert.NotEqual(handshake.Plain, handshake.Encrypted);
        Assert.Equal("TQServer", Encoding.ASCII.GetString(handshake.Plain.AsSpan(handshake.Plain.Length - 8)));
        Assert.Equal(handshake.Plain.Length - LegacyGameServerHandshakePacket.LengthFieldAdjustment, ReadInt32(handshake.Plain, 11));
        Assert.Equal(LegacyGameServerHandshakePacket.HandshakeType, ReadInt32(handshake.Plain, 15));
    }

    [Fact]
    public async Task LegacyGameServerWireModeCompletesHandshakeAndDispatchesEncryptedSealedFrame()
    {
        var received = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = CreateLegacyLoopbackServer((_, frame, _) =>
        {
            received.TrySetResult(frame.Header.Opcode);
            return ValueTask.CompletedTask;
        });

        server.Start();
        using var client = await ConnectAsync(server);
        var stream = client.GetStream();
        var handshake = await ReadEncryptedLegacyHandshakeAsync(stream);
        var dhValues = ReadHandshakeDhValues(handshake.Plain);
        var clientDh = new LegacyGameServerDiffieHellman(dhValues.PrimeHex, dhValues.GeneratorHex);
        var clientPublicKey = clientDh.GenerateResponse(dhValues.ServerPublicHex, new BigInteger(15));

        await stream.WriteAsync(EncryptWithLoginKey(CreateClientHandshakeReply(clientPublicKey)));

        var keyMaterial = new byte[LegacyGameServerCipher.KeyMaterialSize];
        clientDh.DeriveKeyMaterial(keyMaterial);
        var packet = new PacketWriter(2065, payloadLength: sizeof(uint));
        packet.WriteUInt32(0, 0x0013BEED);
        var encryptedFrame = LegacyGameServerFrameCodec.EncodeEncrypted(
            packet.ToArray(),
            new LegacyGameServerCipher(keyMaterial),
            LegacyGameServerFrameSeal.Client);
        await stream.WriteAsync(encryptedFrame);

        Assert.Equal(2065, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task LegacyGameServerWireModeDispatchesFirstFrameCoalescedWithHandshakeReply()
    {
        var received = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = CreateLegacyLoopbackServer((_, frame, _) =>
        {
            received.TrySetResult(frame.Header.Opcode);
            return ValueTask.CompletedTask;
        });

        server.Start();
        using var client = await ConnectAsync(server);
        var stream = client.GetStream();
        var handshake = await ReadEncryptedLegacyHandshakeAsync(stream);
        var dhValues = ReadHandshakeDhValues(handshake.Plain);
        var clientDh = new LegacyGameServerDiffieHellman(dhValues.PrimeHex, dhValues.GeneratorHex);
        var clientPublicKey = clientDh.GenerateResponse(dhValues.ServerPublicHex, new BigInteger(15));
        var encryptedHandshake = EncryptWithLoginKey(CreateClientHandshakeReply(clientPublicKey));

        var keyMaterial = new byte[LegacyGameServerCipher.KeyMaterialSize];
        clientDh.DeriveKeyMaterial(keyMaterial);
        var packet = new PacketWriter(2065, payloadLength: sizeof(uint));
        packet.WriteUInt32(0, 0x0013BEED);
        var encryptedFrame = LegacyGameServerFrameCodec.EncodeEncrypted(
            packet.ToArray(),
            new LegacyGameServerCipher(keyMaterial),
            LegacyGameServerFrameSeal.Client);
        var coalesced = new byte[encryptedHandshake.Length + encryptedFrame.Length];
        encryptedHandshake.CopyTo(coalesced, 0);
        encryptedFrame.CopyTo(coalesced, encryptedHandshake.Length);

        await stream.WriteAsync(coalesced);

        Assert.Equal(2065, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task LegacyGameServerWireTraceLogsSkippedFrameAfterClientClose()
    {
        var previousTrace = Environment.GetEnvironmentVariable("WOLFCONQUER_TRACE_LEGACY_GAME_WIRE");
        Environment.SetEnvironmentVariable("WOLFCONQUER_TRACE_LEGACY_GAME_WIRE", "1");
        try
        {
            using var output = new StringWriter();
            var capturedConnection = new TaskCompletionSource<ConquerTcpConnection>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            await using var server = CreateLegacyLoopbackServer(
                (connection, _, _) =>
                {
                    capturedConnection.TrySetResult(connection);
                    return ValueTask.CompletedTask;
                },
                TextWriter.Synchronized(output));

            server.Start();
            using (var client = await ConnectAsync(server))
            {
                var stream = client.GetStream();
                var handshake = await ReadEncryptedLegacyHandshakeAsync(stream);
                var dhValues = ReadHandshakeDhValues(handshake.Plain);
                var clientDh = new LegacyGameServerDiffieHellman(dhValues.PrimeHex, dhValues.GeneratorHex);
                var clientPublicKey = clientDh.GenerateResponse(dhValues.ServerPublicHex, new BigInteger(15));

                await stream.WriteAsync(EncryptWithLoginKey(CreateClientHandshakeReply(clientPublicKey)));

                var keyMaterial = new byte[LegacyGameServerCipher.KeyMaterialSize];
                clientDh.DeriveKeyMaterial(keyMaterial);
                var packet = new PacketWriter(2065, payloadLength: sizeof(uint));
                packet.WriteUInt32(0, 0x0013BEED);
                var encryptedFrame = LegacyGameServerFrameCodec.EncodeEncrypted(
                    packet.ToArray(),
                    new LegacyGameServerCipher(keyMaterial),
                    LegacyGameServerFrameSeal.Client);
                await stream.WriteAsync(encryptedFrame);

                _ = await capturedConnection.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            var closedLog = await WaitForLogAsync(
                output,
                static text => text.Contains("WorldServer closed client", StringComparison.Ordinal));
            var connection = await capturedConnection.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await connection.SendAsync(new PacketWriter(22210, payloadLength: 0).ToArray());

            var skippedLog = await WaitForLogAsync(
                output,
                static text => text.Contains("LegacyGameWireTrace outbound-frame-skipped", StringComparison.Ordinal));
            Assert.Contains("WorldServer closed client", closedLog);
            Assert.Contains("reason=closed-before-gate", skippedLog);
            Assert.Contains("opcode=22210", skippedLog);
            Assert.Contains("payloadLength=0", skippedLog);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WOLFCONQUER_TRACE_LEGACY_GAME_WIRE", previousTrace);
        }
    }

    private static ConquerTcpServer CreateLegacyLoopbackServer(
        Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask> handleFrame,
        TextWriter? log = null)
    {
        return new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.WorldServer,
                IPAddress.Loopback,
                ListenPort: 0,
                WireMode: ConquerTcpWireMode.LegacyGameServer,
                Log: log),
            handleFrame);
    }

    private static async Task<TcpClient> ConnectAsync(ConquerTcpServer server)
    {
        var endpoint = server.BoundEndPoint;
        var client = new TcpClient();
        await client.ConnectAsync(endpoint.Address, endpoint.Port);
        return client;
    }

    private static async Task<LegacyHandshakeCapture> ReadEncryptedLegacyHandshakeAsync(NetworkStream stream)
    {
        var cipher = new LegacyGameServerCipher(LoginKey);
        using var encrypted = new MemoryStream();
        using var plain = new MemoryStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[128];

        while (true)
        {
            var read = await stream.ReadAsync(buffer, timeout.Token);
            if (read == 0)
            {
                throw new EndOfStreamException("Connection closed before the legacy GameServer handshake was complete.");
            }

            encrypted.Write(buffer.AsSpan(0, read));
            var decrypted = new byte[read];
            cipher.Decrypt(buffer.AsSpan(0, read), decrypted);
            plain.Write(decrypted);

            var plainBytes = plain.ToArray();
            if (TryGetCompleteLegacyHandshakeLength(plainBytes, out var length))
            {
                return new LegacyHandshakeCapture(
                    encrypted.ToArray().AsSpan(0, length).ToArray(),
                    plainBytes.AsSpan(0, length).ToArray());
            }
        }
    }

    private static bool TryGetCompleteLegacyHandshakeLength(byte[] plain, out int length)
    {
        length = 0;
        if (plain.Length < LegacyGameServerHandshakePacket.LengthFieldOffset + sizeof(int))
        {
            return false;
        }

        var adjustedLength = ReadInt32(plain, LegacyGameServerHandshakePacket.LengthFieldOffset);
        length = checked(adjustedLength + LegacyGameServerHandshakePacket.LengthFieldAdjustment);
        if (length <= LegacyGameServerHandshakePacket.FixedLength || length > PacketHeader.DefaultMaxFrameSize)
        {
            throw new InvalidDataException($"Legacy GameServer handshake length {length} is outside the expected range.");
        }

        if (plain.Length < length)
        {
            return false;
        }

        if (!plain.AsSpan(length - 8, 8).SequenceEqual(Encoding.ASCII.GetBytes(LegacyGameServerHandshakePacket.Seal)))
        {
            throw new InvalidDataException("Decrypted legacy GameServer handshake did not end with TQServer.");
        }

        return true;
    }

    private static async Task<string> WaitForLogAsync(StringWriter output, Func<string, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            var log = output.ToString();
            if (predicate(log))
            {
                return log;
            }

            await Task.Delay(25, CancellationToken.None);
        }

        return output.ToString();
    }

    private static LegacyHandshakeDhValues ReadHandshakeDhValues(byte[] packet)
    {
        var offset = HandshakeDhBlobOffset;
        return new LegacyHandshakeDhValues(
            Encoding.ASCII.GetString(ReadLengthPrefixedBlob(packet, ref offset)),
            Encoding.ASCII.GetString(ReadLengthPrefixedBlob(packet, ref offset)),
            Encoding.ASCII.GetString(ReadLengthPrefixedBlob(packet, ref offset)));
    }

    private static byte[] ReadLengthPrefixedBlob(byte[] packet, ref int offset)
    {
        var length = ReadInt32(packet, offset);
        offset += sizeof(int);
        var blob = packet.AsSpan(offset, length).ToArray();
        offset += length;
        return blob;
    }

    private static byte[] CreateClientHandshakeReply(string publicKey)
    {
        var publicKeyBytes = Encoding.ASCII.GetBytes(publicKey);
        var keyLengthOffset = ClientHandshakeReplySegmentLength + LegacyGameServerHandshakeReply.KeyLengthFieldAdjustment;
        var keyBytesOffset = keyLengthOffset + sizeof(int);
        var sealBytes = Encoding.ASCII.GetBytes(LegacyGameServerHandshakeReply.Seal);
        var reply = new byte[keyBytesOffset + publicKeyBytes.Length + sealBytes.Length];

        BinaryPrimitives.WriteInt32LittleEndian(
            reply.AsSpan(LegacyGameServerHandshakeReply.LengthFieldOffset, sizeof(int)),
            ClientHandshakeReplySegmentLength);
        BinaryPrimitives.WriteInt32LittleEndian(reply.AsSpan(keyLengthOffset, sizeof(int)), publicKeyBytes.Length);
        publicKeyBytes.CopyTo(reply.AsSpan(keyBytesOffset));
        sealBytes.CopyTo(reply.AsSpan(keyBytesOffset + publicKeyBytes.Length));
        return reply;
    }

    private static byte[] EncryptWithLoginKey(byte[] plain)
    {
        var encrypted = plain.ToArray();
        new LegacyGameServerCipher(LoginKey).EncryptInPlace(encrypted);
        return encrypted;
    }

    private static int ReadInt32(byte[] packet, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(offset, sizeof(int)));
    }

    private sealed record LegacyHandshakeCapture(byte[] Encrypted, byte[] Plain);

    private sealed record LegacyHandshakeDhValues(string PrimeHex, string GeneratorHex, string ServerPublicHex);
}
