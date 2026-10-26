using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private bool IsLegacyExclusiveItemMutationFrame(PacketFrame frame) =>
        IsLegacyDragonSkinMutationFrame(frame) ||
        IsLegacyVendorPurchaseFrame(frame) ||
        IsLegacyInventorySashUpgradeFrame(frame) ||
        frame.Header.Opcode == WorldPacketOpcodes.LegacyFloorItem ||
        frame.Header.Opcode == WorldPacketOpcodes.MsgItemUsage &&
        MsgItemUsagePacket.TryParse(frame, out var packet) &&
        packet.Action is MsgItemUsageAction.SellItem or MsgItemUsageAction.DropItem;

    // Both configured profiles dispatch MsgAction256 through this same registered owner.
    // Malformed actions retain Frame's ordinary shared lease and parse refusal.
    private static bool IsLegacyInventorySashUpgradeFrame(PacketFrame frame) =>
        frame.Header.Opcode == WorldPacketOpcodes.MsgAction &&
        MsgActionPacket.TryParse(frame, out var action) && action.Type == MsgActionType.UpdateInventorySash;

    // Only valid buys hand ownership to the vendor's exclusive async wrapper.
    // Profile collision/malformed branches keep the ordinary Frame lease and dispatch fences.
    private bool IsLegacyVendorPurchaseFrame(PacketFrame frame) =>
        _protocolProfile == ConquerProtocolProfile.VisibleDx8D13 &&
        frame.Header.Opcode == WorldPacketOpcodes.MsgItemUsage &&
        MsgItemUsagePacket.TryParse(frame, out var usage) &&
        usage.Action == MsgItemUsageAction.BuyVendingItem && usage.DwParam <= uint.MaxValue ||
        _protocolProfile == ConquerProtocolProfile.RootD11_7899 &&
        frame.Header.Opcode == WorldPacketOpcodes.MsgItemOperation &&
        MsgItemOperationPacket.TryParse(frame, out var operation) &&
        operation.Action == MsgItemOperationAction.BuyVendingItem &&
        !operation.HasPrice && operation.HasSecondaryValue;
}
