using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real client side of the existing raw/DH transports. No Frame delegate,
// accepting sender, native owner injection or synthetic server response.
internal sealed class OwnedHostWireClient : IAsyncDisposable
{
    private readonly TcpClient socket = new();
    private NetworkStream Stream => socket.GetStream();
    private LegacyGameServerCipher? cipher;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    internal static async Task<OwnedHostWireClient> ConnectAsync(int port, bool encrypted)
    {
        var client = new OwnedHostWireClient();
        try
        {
            using var timeout = new CancellationTokenSource(Timeout);
            await client.socket.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            if (encrypted) await client.CompleteHandshakeAsync(timeout.Token);
            else
            {
                var serverFirst = new byte[256];
                await client.Stream.ReadExactlyAsync(serverFirst, timeout.Token);
                // This transport's genuine server-first material is 256 random
                // bytes, not a deterministic challenge golden.
                Assert.Equal(256, serverFirst.Length);
            }
            return client;
        }
        catch { client.socket.Dispose(); throw; }
    }

    internal async Task SendAsync(byte[] frame)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var bytes = cipher is null ? frame.ToArray() :
            LegacyGameServerFrameCodec.EncodeEncrypted(frame, cipher, LegacyGameServerFrameSeal.Client);
        await Stream.WriteAsync(bytes, timeout.Token);
    }

    internal async Task<byte[][]> ReadThroughHeroAsync(ushort throughOpcode = LegacyLoaderHeroInfoPacket.Opcode)
    {
        var frames = new List<byte[]>();
        using var timeout = new CancellationTokenSource(Timeout);
        for (var index = 0; index < 64; index++)
        {
            var header = new byte[PacketHeader.Size];
            await Stream.ReadExactlyAsync(header, timeout.Token);
            cipher?.DecryptInPlace(header);
            Assert.True(PacketHeader.TryRead(header, out var parsed));
            Assert.InRange(parsed.Length, PacketHeader.Size, PacketHeader.DefaultMaxFrameSize);
            var tail = new byte[parsed.Length - PacketHeader.Size + (cipher is null ? 0 : LegacyGameServerFrameCodec.SealSize)];
            await Stream.ReadExactlyAsync(tail, timeout.Token);
            cipher?.DecryptInPlace(tail);
            var bytes = header.Concat(tail).ToArray();
            if (cipher is not null)
            {
                Assert.True(LegacyGameServerFrameCodec.TryStripSeal(bytes, out var plain, out var consumed));
                Assert.Equal(bytes.Length, consumed);
                bytes = plain;
            }
            frames.Add(bytes);
            if (parsed.Opcode == throughOpcode) return frames.ToArray();
        }
        throw new InvalidDataException("Saved character hero was absent from bounded startup frames.");
    }

    internal static PacketFrame Decode(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private async Task CompleteHandshakeAsync(CancellationToken token)
    {
        var login = new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey));
        var header = new byte[LegacyGameServerHandshakePacket.LengthFieldOffset + sizeof(int)];
        await Stream.ReadExactlyAsync(header, token);
        login.DecryptInPlace(header);
        var length = checked(BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(
            LegacyGameServerHandshakePacket.LengthFieldOffset, sizeof(int))) + LegacyGameServerHandshakePacket.LengthFieldAdjustment);
        Assert.InRange(length, LegacyGameServerHandshakePacket.FixedLength + 1, PacketHeader.DefaultMaxFrameSize);
        var tail = new byte[length - header.Length];
        await Stream.ReadExactlyAsync(tail, token);
        login.DecryptInPlace(tail);
        var plain = header.Concat(tail).ToArray();
        Assert.Equal("TQServer", Encoding.ASCII.GetString(plain.AsSpan(length - 8)));
        Assert.Equal(LegacyGameServerHandshakePacket.HandshakeType, BinaryPrimitives.ReadInt32LittleEndian(plain.AsSpan(15, 4)));
        var offset = 53;
        var prime = ReadBlob(plain, ref offset);
        var generator = ReadBlob(plain, ref offset);
        var serverPublic = ReadBlob(plain, ref offset);
        var dh = new LegacyGameServerDiffieHellman(prime, generator);
        var publicKey = dh.GenerateRequest(); // Genuine client-side CSPRNG.
        dh.HandleResponse(serverPublic);
        var material = new byte[LegacyGameServerCipher.KeyMaterialSize];
        dh.DeriveKeyMaterial(material);
        cipher = new LegacyGameServerCipher(material);
        var key = Encoding.ASCII.GetBytes(publicKey);
        var keyOffset = 17 + LegacyGameServerHandshakeReply.KeyLengthFieldAdjustment;
        var bytesOffset = keyOffset + sizeof(int);
        var reply = new byte[bytesOffset + key.Length + 8];
        BinaryPrimitives.WriteInt32LittleEndian(reply.AsSpan(LegacyGameServerHandshakeReply.LengthFieldOffset, 4), 17);
        BinaryPrimitives.WriteInt32LittleEndian(reply.AsSpan(keyOffset, 4), key.Length);
        key.CopyTo(reply, bytesOffset);
        Encoding.ASCII.GetBytes(LegacyGameServerHandshakeReply.Seal).CopyTo(reply, bytesOffset + key.Length);
        new LegacyGameServerCipher(Encoding.ASCII.GetBytes(LegacyGameServerCipher.LoginKey)).EncryptInPlace(reply);
        await Stream.WriteAsync(reply, token);
    }

    private static string ReadBlob(byte[] bytes, ref int offset)
    {
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
        offset += 4;
        Assert.InRange(length, 1, 1024);
        var result = Encoding.ASCII.GetString(bytes.AsSpan(offset, length));
        offset += length;
        return result;
    }

    public ValueTask DisposeAsync() { socket.Dispose(); return ValueTask.CompletedTask; }
}
