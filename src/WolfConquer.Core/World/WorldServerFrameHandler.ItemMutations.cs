using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private static bool IsLegacyExclusiveItemMutationFrame(PacketFrame frame) =>
        IsLegacyDragonSkinMutationFrame(frame) ||
        frame.Header.Opcode == WorldPacketOpcodes.LegacyFloorItem ||
        frame.Header.Opcode == WorldPacketOpcodes.MsgItemUsage &&
        MsgItemUsagePacket.TryParse(frame, out var packet) &&
        packet.Action is MsgItemUsageAction.SellItem or MsgItemUsageAction.DropItem;
}
