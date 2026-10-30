using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Holds one genuinely prepared native frame, not an accepting transport/owner.
// Actual Send may filter or fail after commitment; this does not acknowledge delivery.
internal sealed unsafe class LegacyNativePreparedItemOutput : IDisposable
{
    private readonly LegacyNativePacket frame;
    private readonly byte[] body;
    private readonly ushort opcode;
    private readonly string? capturedSeal;
    private bool disposed;
    internal bool IsUsable => !disposed && frame.Memory != null;

    private LegacyNativePreparedItemOutput(LegacyNativePacket frame, byte[] body, ushort opcode, string? capturedSeal)
    {
        this.frame = frame; this.body = body; this.opcode = opcode; this.capturedSeal = capturedSeal;
    }

    internal static LegacyNativePreparedItemOutput Prepare(int capacity, Action<LegacyNativePacket> producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        string? captured = LegacyNativePacket.SealString;
        var prepared = new LegacyNativePacket(capacity);
        try
        {
            producer(prepared);
            if (!string.Equals(captured, LegacyNativePacket.SealString, StringComparison.Ordinal))
                throw new InvalidOperationException("Packet seal changed during item output preparation.");
            if (prepared.Memory == null || prepared.stream != prepared.Memory + 4 ||
                prepared.Size < 4 || prepared.Size > prepared.Capacity)
                throw new InvalidOperationException("Item output has no complete prepared native frame.");
            byte[] bytes = new ReadOnlySpan<byte>(prepared.Memory, prepared.Size).ToArray();
            int declared = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            byte[] seal = captured is null ? [] : Encoding.UTF8.GetBytes(captured).AsSpan(0, 8).ToArray();
            if (declared < 4 || declared + seal.Length != bytes.Length ||
                !bytes.AsSpan(declared).SequenceEqual(seal))
                throw new InvalidOperationException("Prepared item output does not match its actual framing state.");
            // This is the actual GameClient.Send frontier. Do not silently drop
            // rows or mark a bank shown when that sender would refuse the frame.
            if (bytes.Length > 1032)
                throw new InvalidOperationException("Prepared item output exceeds the native sender's 1032-byte limit.");
            return new(prepared, bytes.AsSpan(4, declared - 4).ToArray(),
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)), captured);
        }
        catch { prepared.Dispose(); throw; }
    }

    internal void PublishTo(LegacyNativePacket target)
    {
        ObjectDisposedException.ThrowIf(!IsUsable, this);
        ArgumentNullException.ThrowIfNull(target);
        target.PrepareFrame(opcode, prepared =>
        {
            if (!string.Equals(capturedSeal, LegacyNativePacket.SealString, StringComparison.Ordinal))
                throw new InvalidOperationException("Packet seal changed before item output publication.");
            fixed (byte* source = body) prepared.WriteUnsafe(source, body.Length);
        });
    }

    internal void Send(LegacyNativeGameClientRuntime client)
    {
        ObjectDisposedException.ThrowIf(!IsUsable, this);
        client.Send(frame);
    }

    public void Dispose() { disposed = true; frame.Dispose(); }
}
