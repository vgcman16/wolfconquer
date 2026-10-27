using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// These checks use the physical allocation, never mutable Size as capacity.
// Cooperating owners must retain the packet lifetime across validation/write.
internal static class LegacyNativePreparedPacketStorage
{
    internal static unsafe void Validate(LegacyNativePacket packet, int required)
    {
        if (packet is null) throw new InvalidOperationException("Prepared packet requires a real storage owner.");
        if (required is < 4 or > 1032) throw new InvalidDataException("Prepared packet length is not sendable.");
        if (packet.Memory == null || packet.Capacity < required)
            throw new InvalidOperationException("Prepared packet requires live storage with complete capacity.");
    }

    internal static unsafe nint Address(LegacyNativePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return (nint)packet.Memory;
    }

    internal static unsafe void Write(LegacyNativePacket packet, byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Validate(packet, frame.Length);
        Marshal.Copy(frame, 0, (nint)packet.Memory, frame.Length);
        packet.Size = frame.Length;
        packet.Seek(4);
    }

    internal static unsafe bool Matches(LegacyNativePacket? packet, byte[]? frame, nint expectedAddress) =>
        packet is not null && frame is not null && frame.Length is >= 4 and <= 1032 &&
        expectedAddress != 0 && (nint)packet.Memory == expectedAddress && packet.Memory != null &&
        packet.Capacity >= frame.Length && packet.Size == frame.Length && packet.Position == 4 &&
        new ReadOnlySpan<byte>(packet.Memory, frame.Length).SequenceEqual(frame);
}
