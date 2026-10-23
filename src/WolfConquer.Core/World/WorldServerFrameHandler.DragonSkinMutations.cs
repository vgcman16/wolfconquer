using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private static bool IsLegacyDragonSkinMutationFrame(PacketFrame frame) =>
        frame.Header.Opcode == WorldPacketOpcodes.LegacyAnimaSkin && LegacyDragonSkinPacket.TryParse(frame, out var packet) &&
        packet.Type is LegacyDragonSkinType.Unlock or LegacyDragonSkinType.Retrieve;
}
