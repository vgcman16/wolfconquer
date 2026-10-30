namespace WolfConquer.Core.World;

internal static partial class LegacyNpcSalePersistence
{
    // The actual Store owner prepares native publication and holds its current
    // session gate. This writer then commits the exact Character + unchanged
    // Items images through the existing single-owner v2 journal before replies.
    internal static bool TryPersistWarehouseMoney(WorldSession original, WorldSession updated,
        string? playerItemsRoot, bool deposit, long amount, out string reason) =>
        TryPersistWarehouseMoney(original, updated, playerItemsRoot, deposit, amount, null, out reason);

    internal static bool TryPersistWarehouseMoney(WorldSession original, WorldSession updated,
        string? playerItemsRoot, bool deposit, long amount, LegacyJournalInterruption? interruption, out string reason) =>
        TryPersistCore(original, updated, playerItemsRoot, PersistenceMode.WarehouseMoney, interruption, out reason,
            transferDeposit: deposit, transferAmount: amount);

    private static bool TryValidateWarehouseMoneySessions(WorldSession original, WorldSession updated,
        bool deposit, long amount, out long warehouse, out string reason)
    {
        warehouse = 0;
        reason = "warehouseMoneyTransferBindingInvalid";
        if (original is null || updated is null || amount <= 0 ||
            (original with { Character = updated.Character }) != updated) return false;
        if (!TryValidateSessions(original, updated, out warehouse, out reason) ||
            !TryWarehouse(original.Character.Attributes, out var beforeWarehouse, out reason)) return false;
        if (deposit ? original.Character.Money <= amount : beforeWarehouse < amount)
        { reason = "warehouseMoneyTransferFundsChanged"; return false; }

        // Decimal holds both signed-64 wallets plus the requested amount exactly.
        // It avoids overflowing an intermediate sum before the established
        // inventory cap returns excess silver to the actual warehouse balance.
        const long inventoryCap = 500_000_000_000;
        var expectedMoney = deposit ? (decimal)original.Character.Money - amount :
            Math.Min((decimal)original.Character.Money + amount, inventoryCap);
        var total = (decimal)original.Character.Money + beforeWarehouse;
        var expectedWarehouse = total - expectedMoney;
        if (updated.Character.Money != expectedMoney || warehouse != expectedWarehouse)
        { reason = "warehouseMoneyTransferDeltaInvalid"; return false; }
        reason = string.Empty;
        return true;
    }
}
