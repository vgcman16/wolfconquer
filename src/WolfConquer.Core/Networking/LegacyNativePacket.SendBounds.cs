using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativePacket
{
    // Only official send capture and Dispose cooperate here. Public pointer
    // writes/other codecs do not acquire this gate and are not made concurrent-safe.
    private readonly object sendStorageGate = new();

    internal unsafe (int Length, ushort Opcode) DescribeSendStorage()
    {
        lock (sendStorageGate)
        {
            int length = ValidateSendStorage();
            ushort opcode = length >= 4
                ? BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(Memory + 2, 2))
                : (ushort)0;
            return (length, opcode);
        }
    }

    internal unsafe byte[] CaptureSendBytes(int maximumLength = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        lock (sendStorageGate)
        {
            int length = ValidateSendStorage();
            if (length > maximumLength)
                throw new InvalidOperationException("Packet exceeds the selected send limit.");
            byte[] snapshot = new byte[length];
            if (length > 0) Marshal.Copy((nint)Memory, snapshot, 0, length);
            return snapshot;
        }
    }

    private unsafe int ValidateSendStorage()
    {
        ObjectDisposedException.ThrowIf(IsDisposed || Memory == null, this);
        int length = Size;
        if (length < 0 || length > Capacity)
            throw new InvalidOperationException("Packet send Size is outside its actual allocation.");
        return length;
    }
}
