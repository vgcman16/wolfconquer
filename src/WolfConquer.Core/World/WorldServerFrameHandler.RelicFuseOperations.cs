using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // The registered handler retains Frame's genuine lease. It does not enter
    // a second exclusive operation while holding an ordinary shared lease.
    private static bool LegacyRelicFuseRequiresExclusiveOperation(PacketFrame frame) =>
        frame.Header.Opcode == WorldPacketOpcodes.LegacyRelicFuse &&
        LegacyRelicFusePacket.TryParse(frame, out var packet) &&
        packet.Type is LegacyRelicFusePacket.ComposeType or LegacyRelicFusePacket.ReturnType;
}
