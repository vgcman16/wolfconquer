using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace WolfConquer.Tests;

internal sealed partial class OwnedSqlCommitAckDropProxy
{
    // Narrow real-fixture limits, not a general MySQL proxy or large-blob benchmark.
    private const int MaximumPacketPayload = 1024 * 1024;
    private const int MaximumDirectionBytes = 16 * 1024 * 1024;
    private const int MaximumDirectionPackets = 4096;

    private sealed class Packet(byte[] bytes) : IDisposable
    {
        internal byte Sequence => bytes[3];
        internal int PayloadLength => bytes.Length - 4;
        internal ReadOnlySpan<byte> Payload => bytes.AsSpan(4);
        internal ReadOnlyMemory<byte> Wire => bytes;
        public void Dispose() => CryptographicOperations.ZeroMemory(bytes);
    }

    private sealed class PacketBudget
    {
        private int packets, bytes;
        internal void Admit(int payloadLength)
        {
            if (payloadLength > MaximumPacketPayload || ++packets > MaximumDirectionPackets ||
                (bytes = checked(bytes + payloadLength + 4)) > MaximumDirectionBytes)
                throw new InvalidDataException("Owned SQL proxy packet budget exceeded.");
        }
    }

    private static async ValueTask<Packet> ReadPacketAsync(NetworkStream source, PacketBudget budget, CancellationToken token, CommitAckGate? gate = null, bool serverDirection = false)
    {
        var header = new byte[4];
        if (gate is null) await ReadPacketBytesAsync(source, header, token);
        else
        {
            int first;
            try { first = await source.ReadAsync(header.AsMemory(0, 1), token); }
            catch (Exception error) when (token.IsCancellationRequested && IsOwnedReadCancellation(error))
            { throw new OperationCanceledException("Owned SQL proxy packet read was canceled.", error, token); }
            token.ThrowIfCancellationRequested();
            if (first == 0)
            {
                if (Volatile.Read(ref gate.Forwarding) != 0 && (!serverDirection || Volatile.Read(ref gate.QuitForwarded) != 0))
                {
                    await gate.Forwarded.Task.WaitAsync(token); // Only a successfully forwarded actual OK permits clean boundary EOF.
                    throw new GatedClientEofException();
                }
                throw new EndOfStreamException("Owned SQL proxy peer ended inside the expected packet.");
            }
            await ReadPacketBytesAsync(source, header.AsMemory(1), token);
        }
        int length = header[0] | (header[1] << 8) | (header[2] << 16);
        budget.Admit(length); // Before payload allocation; 0xffffff continuation chunks exceed this fixture's cap.
        var bytes = new byte[checked(length + 4)]; header.CopyTo(bytes, 0);
        try { await ReadPacketBytesAsync(source, bytes.AsMemory(4), token); return new(bytes); }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }

    private static async ValueTask ReadPacketBytesAsync(NetworkStream source, Memory<byte> destination, CancellationToken token)
    {
        try
        {
            while (!destination.IsEmpty)
            {
                int count = await source.ReadAsync(destination, token);
                token.ThrowIfCancellationRequested(); // Before EOF classification, never post-hoc reset suppression.
                if (count == 0) throw new EndOfStreamException("Owned SQL proxy peer ended inside the expected packet.");
                destination = destination[count..];
            }
        }
        catch (Exception error) when (token.IsCancellationRequested && IsOwnedReadCancellation(error))
        { throw new OperationCanceledException("Owned SQL proxy packet read was canceled.", error, token); }
    }

    private static bool IsOwnedReadCancellation(Exception error) => error is ObjectDisposedException ||
        error is SocketException { SocketErrorCode: SocketError.OperationAborted or SocketError.Interrupted } ||
        error is IOException { InnerException: ObjectDisposedException } ||
        error is IOException { InnerException: SocketException { SocketErrorCode: SocketError.OperationAborted or SocketError.Interrupted } };

    private static void RequirePlainProtocol41(Packet firstClientPacket)
    {
        if (firstClientPacket.Sequence != 1 || firstClientPacket.PayloadLength < 32)
            throw new InvalidDataException("Owned SQL proxy requires a protocol41 handshake response.");
        uint capabilities = BinaryPrimitives.ReadUInt32LittleEndian(firstClientPacket.Payload);
        const uint protocol41 = 0x0200, ssl = 0x0800, compress = 0x0020, queryAttributes = 0x08000000;
        if ((capabilities & protocol41) == 0 || (capabilities & (ssl | compress | queryAttributes)) != 0)
            throw new InvalidDataException("Owned SQL proxy requires unencrypted, uncompressed MariaDB text queries.");
    }

    private static bool IsExactCommitQuery(Packet packet)
    {
        if (packet.Sequence != 0 || packet.PayloadLength != 7 || packet.Payload[0] != 0x03) return false;
        ReadOnlySpan<byte> expected = "COMMIT"u8;
        for (int index = 0; index < expected.Length; index++)
        {
            byte actual = packet.Payload[index + 1];
            if (actual is >= (byte)'a' and <= (byte)'z') actual -= (byte)('a' - 'A');
            if (actual != expected[index]) return false;
        }
        return true;
    }

    private static ushort RequireActualCommitOk(Packet packet)
    {
        // COM_QUERY commit is one seq0 packet. Its real server success must be seq1,
        // 0x00 OK, zero affected rows/insert ID and no remaining transaction/results.
        var payload = packet.Payload;
        if (packet.Sequence != 1 || payload.Length < 7 || payload[0] != 0x00 || payload[1] != 0 || payload[2] != 0)
            throw new InvalidDataException("Actual successful COMMIT response was not observed.");
        ushort status = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(3, 2));
        if ((status & (0x0001 | 0x0008)) != 0)
            throw new InvalidDataException("COMMIT response still reports a transaction or more results.");
        return status;
    }
}
