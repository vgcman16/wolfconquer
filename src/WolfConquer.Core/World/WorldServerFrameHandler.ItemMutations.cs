using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal static bool HandlerOwnsOperation(PacketFrame frame, ConquerProtocolProfile profile) =>
        frame.Header.Opcode is WorldPacketOpcodes.LegacyTrade or WorldPacketOpcodes.LegacySecondaryPassword ||
        IsLegacyExclusiveItemMutationFrame(frame, profile);

    private bool IsLegacyExclusiveItemMutationFrame(PacketFrame frame) =>
        IsLegacyExclusiveItemMutationFrame(frame, _protocolProfile);

    private static bool IsLegacyExclusiveItemMutationFrame(PacketFrame frame, ConquerProtocolProfile profile) =>
        IsLegacyDragonSkinMutationFrame(frame) ||
        IsLegacyVendorPurchaseFrame(frame, profile) ||
        IsLegacyMailboxClaimFrame(frame) ||
        IsLegacyInventorySashUpgradeFrame(frame) ||
        IsLegacyWarehouseItemTransferFrame(frame) ||
        IsLegacyWarehouseLockRefreshFrame(frame) ||
        frame.Header.Opcode == WorldPacketOpcodes.LegacyFloorItem ||
        frame.Header.Opcode == WorldPacketOpcodes.MsgItemUsage &&
        MsgItemUsagePacket.TryParse(frame, out var packet) &&
        packet.Action is MsgItemUsageAction.SellItem or MsgItemUsageAction.DropItem or
            MsgItemUsageAction.Equip or MsgItemUsageAction.Unequip;

    // Parsed claims own their actual Exclusive before resolving the current participant.
    private static bool IsLegacyMailboxClaimFrame(PacketFrame frame) =>
        frame.Header.Opcode == WorldPacketOpcodes.LegacyMailboxMode &&
        LegacyMailboxModePacket.TryParse(frame, out var packet) &&
        packet.Type is LegacyMailboxModeType.ClaimItem or LegacyMailboxModeType.ClaimMoney or LegacyMailboxModeType.ClaimAll or
            LegacyMailboxModeType.Delete or LegacyMailboxModeType.DeleteClaim or LegacyMailboxModeType.ClaimRecordType;

    // Registered LegacyWarehouse uses the same action IDs in D11 and D13.
    // Parsed item transfers own the inner Exclusive; physical-bag Show stays Shared.
    private static bool IsLegacyWarehouseItemTransferFrame(PacketFrame frame) =>
        frame.Header.Opcode == WorldPacketOpcodes.LegacyWarehouse &&
        LegacyWarehousePacket.TryParseRequest(frame, out var request) &&
        LegacyWarehouseItemTransferActions.IsMutation(request.Action);

    // Ordinary/house/ownUID Show owns its Shared probe and, only when needed,
    // a separately acquired Exclusive refresh. Frame must not wrap it in Shared.
    private static bool IsLegacyWarehouseLockRefreshFrame(PacketFrame frame) =>
        frame.Header.Opcode == WorldPacketOpcodes.LegacyWarehouse &&
        LegacyWarehousePacket.TryParseRequest(frame, out var request) &&
        LegacyWarehousePacket.IsShowAction(request.Action);

    // Both configured profiles dispatch MsgAction256 through this same registered owner.
    // Malformed actions retain Frame's ordinary shared lease and parse refusal.
    private static bool IsLegacyInventorySashUpgradeFrame(PacketFrame frame) =>
        frame.Header.Opcode == WorldPacketOpcodes.MsgAction &&
        MsgActionPacket.TryParse(frame, out var action) && action.Type == MsgActionType.UpdateInventorySash;

    // Only valid buys hand ownership to the vendor's exclusive async wrapper.
    // Profile collision/malformed branches keep the ordinary Frame lease and dispatch fences.
    private static bool IsLegacyVendorPurchaseFrame(PacketFrame frame, ConquerProtocolProfile profile) =>
        profile == ConquerProtocolProfile.VisibleDx8D13 &&
        frame.Header.Opcode == WorldPacketOpcodes.MsgItemUsage &&
        MsgItemUsagePacket.TryParse(frame, out var usage) &&
        usage.Action == MsgItemUsageAction.BuyVendingItem && usage.DwParam <= uint.MaxValue ||
        profile == ConquerProtocolProfile.RootD11_7899 &&
        frame.Header.Opcode == WorldPacketOpcodes.MsgItemOperation &&
        MsgItemOperationPacket.TryParse(frame, out var operation) &&
        operation.Action == MsgItemOperationAction.BuyVendingItem &&
        !operation.HasPrice && operation.HasSecondaryValue;
}
