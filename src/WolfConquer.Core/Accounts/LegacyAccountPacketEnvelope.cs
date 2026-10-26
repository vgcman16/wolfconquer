using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Accounts;

internal static class LegacyAccountPacketEnvelope
{
    public static bool TryRead(
        PacketFrame frame,
        ushort opcode,
        int frameLength,
        out PacketReader reader)
    {
        reader = default;
        return frameLength <= LegacyAccountAuthenticationPacket.FrameLength &&
            PacketFrameEnvelope.TryRead(frame, opcode, frameLength, out reader);
    }
}
