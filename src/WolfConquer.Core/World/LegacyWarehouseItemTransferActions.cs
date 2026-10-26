using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

internal static class LegacyWarehouseItemTransferActions
{
    internal static bool IsPhysicalSash(LegacyWarehouseAction action) =>
        action is LegacyWarehouseAction.ShashDepositItem or LegacyWarehouseAction.ShashWithdrawItem;

    internal static bool IsDeposit(LegacyWarehouseAction action) =>
        action is LegacyWarehouseAction.DepositItem or LegacyWarehouseAction.DepositItem_WH_House or
            LegacyWarehouseAction.InventorySashDepositItem or LegacyWarehouseAction.ShashDepositItem;

    internal static bool IsMutation(LegacyWarehouseAction action) =>
        IsDeposit(action) || action is LegacyWarehouseAction.WithdrawItem or
            LegacyWarehouseAction.WithdrawItem_WH_House or LegacyWarehouseAction.InventorySashWithdrawItem or
            LegacyWarehouseAction.ShashWithdrawItem;
}
