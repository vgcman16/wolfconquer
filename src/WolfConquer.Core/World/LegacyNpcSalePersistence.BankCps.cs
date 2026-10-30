namespace WolfConquer.Core.World;

internal static partial class LegacyNpcSalePersistence
{
    internal static bool TryPersistBankCps(WorldSession original, WorldSession updated, string? playerItemsRoot,
        bool deposit, uint amount, out string reason) =>
        TryPersistBankCps(original, updated, playerItemsRoot, deposit, amount, null, out reason);

    internal static bool TryPersistBankCps(WorldSession original, WorldSession updated, string? playerItemsRoot,
        bool deposit, uint amount, LegacyJournalInterruption? interruption, out string reason) =>
        TryPersistCore(original, updated, playerItemsRoot, PersistenceMode.BankCps, interruption, out reason,
            transferDeposit: deposit, transferAmount: amount);

    private static bool TryValidateBankCpsSessions(WorldSession original, WorldSession updated,
        bool deposit, uint amount, out long warehouse, out string reason)
    {
        warehouse = 0; reason = "bankCpsTransferBindingInvalid";
        if (original is null || updated is null || amount == 0 || original.AccountId <= 0 ||
            original.Character.Uid is <= 0 or > uint.MaxValue || original.AccountId != updated.AccountId ||
            original.Character.Uid != updated.Character.Uid || original.LegacyTradeActive || updated.LegacyTradeActive ||
            original.Character.Money < 0 || original.Character.ConquerPoints < 0 || original.Character.BankConquerPoints < 0 ||
            updated.Character.ConquerPoints < 0 || updated.Character.BankConquerPoints < 0 ||
            original.PendingBankConquerPoints != amount || original.PendingBankConquerPointsIsDeposit != deposit ||
            updated.PendingBankConquerPoints is not null || updated.PendingBankConquerPointsIsDeposit ||
            (original with { Character = updated.Character, PendingBankConquerPoints = null,
                PendingBankConquerPointsIsDeposit = false }) != updated) return false;
        if ((original.Character with { ConquerPoints = updated.Character.ConquerPoints,
                BankConquerPoints = updated.Character.BankConquerPoints, Attributes = updated.Character.Attributes }) != updated.Character)
        { reason = "bankCpsUnrelatedCharacterMutation"; return false; }
        if (!TryFields(original.Character.Attributes, out var before, out reason) ||
            !TryFields(updated.Character.Attributes, out var after, out reason) ||
            !TryWarehouse(before, out warehouse, out reason) || !TryWarehouse(after, out var afterWarehouse, out reason)) return false;
        if (afterWarehouse != warehouse) { reason = "bankCpsUnrelatedWarehouseMutation"; return false; }
        if (!BankCpsProjectionMatches(before, original.Character.ConquerPoints, original.Character.BankConquerPoints) ||
            !BankCpsProjectionMatches(after, updated.Character.ConquerPoints, updated.Character.BankConquerPoints))
        { reason = "bankCpsWalletProjectionInvalid"; return false; }
        if (deposit ? original.Character.ConquerPoints < amount : original.Character.BankConquerPoints < amount)
        { reason = "bankCpsFundsChanged"; return false; }
        decimal nextCps = (decimal)original.Character.ConquerPoints + (deposit ? -(decimal)amount : amount);
        decimal nextBank = (decimal)original.Character.BankConquerPoints + (deposit ? amount : -(decimal)amount);
        if (updated.Character.ConquerPoints != nextCps || updated.Character.BankConquerPoints != nextBank)
        { reason = "bankCpsTransferDeltaInvalid"; return false; }
        if (!LegacyTradeEscrowClaims.TryRead(before, out var firstClaim, out reason) || firstClaim is not null ||
            !LegacyTradeEscrowClaims.TryRead(after, out var lastClaim, out reason) || lastClaim is not null ||
            !LegacySecondaryPasswordState.TryRead(before, out _, out reason) ||
            !LegacySecondaryPasswordState.TryRead(after, out _, out reason))
        { if (reason.Length == 0) reason = "bankCpsTradeOrPinInvalid"; return false; }
        foreach (var key in new[] { "ConquerPoints", "BankCps" }) { before.Remove(key); after.Remove(key); }
        if (before.Count != after.Count || before.Any(pair => !after.TryGetValue(pair.Key, out var value) || value != pair.Value))
        { reason = "bankCpsUnrelatedAttributeMutation"; return false; }
        reason = string.Empty; return true;
    }

    private static bool BankCpsProjectionMatches(IReadOnlyDictionary<string, string> fields, long cps, long bank) =>
        fields.TryGetValue("ConquerPoints", out var cpText) && TryNonnegative(cpText, out var parsedCps) && parsedCps == cps &&
        fields.TryGetValue("BankCps", out var bankText) && TryNonnegative(bankText, out var parsedBank) && parsedBank == bank;

    private static bool SameBankCpsProtectedField(IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual, string key)
    {
        var hasExpected = expected.TryGetValue(key, out var before);
        var hasActual = actual.TryGetValue(key, out var saved);
        return hasExpected == hasActual && (!hasExpected || before == saved);
    }
}
