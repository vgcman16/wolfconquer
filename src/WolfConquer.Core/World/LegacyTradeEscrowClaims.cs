using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyTradeEscrowClaim(Guid TradeId, long Money, uint ConquerPoints);

// Managed crash-safety adaptation: native Trade debits immediately but has no durable escrow owner.
// A claim lives in the SAME atomic character replacement as its debit. Pair settlement must clear it
// through the trade journal; it must never clear the claim independently of transferring/refunding it.
internal static partial class LegacyTradeEscrowClaims
{
    internal const string IdKey = "ManagedTradeEscrowId";
    internal const string MoneyKey = "ManagedTradeEscrowMoney";
    internal const string CpKey = "ManagedTradeEscrowCP";

    internal static IReadOnlyDictionary<string, string> ClearValues { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            [IdKey] = string.Empty, [MoneyKey] = "0", [CpKey] = "0",
        });

    internal static bool TryRead(IReadOnlyDictionary<string, string> attrs,
        out LegacyTradeEscrowClaim? claim, out string reason)
    {
        ArgumentNullException.ThrowIfNull(attrs);
        claim = null;
        reason = string.Empty;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in attrs)
        {
            if (!IsClaimKey(key)) continue;
            if (value is null || !fields.TryAdd(key, value))
            { reason = "tradeEscrowFieldsAmbiguous"; return false; }
        }
        if (fields.Count == 0) return true;
        if (fields.Count != 3 || !fields.TryGetValue(IdKey, out var idText) ||
            !fields.TryGetValue(MoneyKey, out var moneyText) || !fields.TryGetValue(CpKey, out var cpText))
        { reason = "tradeEscrowFieldsPartial"; return false; }
        if (!TryCanonicalLong(moneyText, out var money) ||
            !TryCanonicalLong(cpText, out var cp) || cp > uint.MaxValue)
        { reason = "tradeEscrowAmountInvalid"; return false; }
        if (idText.Length == 0 && money == 0 && cp == 0) return true;
        if (!Guid.TryParseExact(idText, "N", out var id) || id == Guid.Empty ||
            idText != id.ToString("N") || (money == 0 && cp == 0))
        { reason = "tradeEscrowIdOrEmptyClaimInvalid"; return false; }
        claim = new(id, money, (uint)cp);
        return true;
    }

    internal static WorldSession WithClaim(WorldSession session, LegacyTradeEscrowClaim? claim)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (claim is { } validatedClaim && (validatedClaim.TradeId == Guid.Empty || validatedClaim.Money < 0 ||
            (validatedClaim.Money == 0 && validatedClaim.ConquerPoints == 0)))
            throw new ArgumentException("An escrow claim needs a trade id and positive assets.", nameof(claim));
        var attrs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in session.Character.Attributes)
            if (!IsClaimKey(key)) attrs.Add(key, value);
        foreach (var (key, value) in Values(claim)) attrs.Add(key, value);
        return session with
        {
            Character = session.Character with
            { Attributes = new ReadOnlyDictionary<string, string>(attrs) },
        };
    }

    internal static bool TryPersistWallet(WorldSession original, WorldSession updated, out string reason)
    {
        reason = string.Empty;
        if (original.AccountId <= 0 || original.AccountId != updated.AccountId ||
            original.Character.Uid != updated.Character.Uid ||
            string.IsNullOrWhiteSpace(original.UsersRoot) || original.UsersRoot != updated.UsersRoot)
        { reason = "tradeEscrowWalletBindingInvalid"; return false; }
        if (!TryRead(original.Character.Attributes, out var before, out reason) ||
            !TryRead(updated.Character.Attributes, out var after, out reason) ||
            !TryValidateDebit(original.Character.Money, original.Character.ConquerPoints, before,
                updated.Character.Money, updated.Character.ConquerPoints, after, out reason)) return false;
        try
        {
            var path = Path.Combine(original.UsersRoot, original.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            if (!TryValidatePath(path, directory: false, out reason)) return false;
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!TryReadDisk(path, out var disk, out failure)) return false;
                if (disk.Uid != original.Character.Uid || disk.Money != original.Character.Money ||
                    disk.ConquerPoints != original.Character.ConquerPoints || disk.Claim != before)
                { failure = "tradeEscrowWalletOrClaimStale"; return false; }
                try
                {
                    if (CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(path, disk.Uid,
                        WalletValues(updated.Character.Money, updated.Character.ConquerPoints, after),
                        detailedIoFailures: false, out failure)) return true;
                }
                catch (Exception ex) when (IsFileFailure(ex))
                { failure = "tradeEscrowWalletPersistence:" + ex.GetType().Name; }
                // An IO error is not proof that replacement never happened. Fence the host if the
                // old wallet/claim cannot be re-established; do not silently orphan a durable debit.
                if (!TryValidateBaseline(path, original, out _))
                    failure = "tradeEscrowRecoveryRequired:" + failure;
                return false;
            });
            reason = failure;
            return saved;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        { reason = "tradeEscrowWalletPersistence:" + ex.GetType().Name; return false; }
    }

    // The pair journal calls this inside its already ordered character/item file lock set.
    internal static bool TryValidateBaseline(string characterPath, WorldSession expected, out string reason)
    {
        reason = string.Empty;
        try
        {
            if (!TryRead(expected.Character.Attributes, out var claim, out reason) ||
                !TryValidatePath(characterPath, directory: false, out reason) ||
                !TryReadDisk(characterPath, out var disk, out reason)) return false;
            if (disk.Uid == expected.Character.Uid && disk.Money == expected.Character.Money &&
                disk.ConquerPoints == expected.Character.ConquerPoints && disk.Claim == claim) return true;
            reason = "tradeEscrowWalletOrClaimStale";
            return false;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        { reason = "tradeEscrowBaseline:" + ex.GetType().Name; return false; }
    }

    private static bool TryValidateDebit(long oldMoney, long oldCp, LegacyTradeEscrowClaim? before,
        long newMoney, long newCp, LegacyTradeEscrowClaim? after, out string reason)
    {
        reason = "tradeEscrowDebitInvalid";
        if (oldMoney < 0 || newMoney < 0 || oldCp is < 0 or > uint.MaxValue ||
            newCp is < 0 or > uint.MaxValue || newMoney > oldMoney || newCp > oldCp || after is null ||
            (before is not null && before.TradeId != after.TradeId)) return false;
        var oldClaimMoney = before?.Money ?? 0;
        var oldClaimCp = before?.ConquerPoints ?? 0;
        if (after.Money < oldClaimMoney || after.ConquerPoints < oldClaimCp ||
            after.Money - oldClaimMoney != oldMoney - newMoney ||
            (long)after.ConquerPoints - oldClaimCp != oldCp - newCp) return false;
        try
        {
            _ = checked(newMoney + after.Money);
            if (newCp + after.ConquerPoints > uint.MaxValue) return false;
        }
        catch (OverflowException) { return false; }
        reason = string.Empty;
        return true;
    }

    private static IReadOnlyDictionary<string, string> Values(LegacyTradeEscrowClaim? claim) =>
        claim is null ? ClearValues : new Dictionary<string, string>
        {
            [IdKey] = claim.TradeId.ToString("N"),
            [MoneyKey] = claim.Money.ToString(CultureInfo.InvariantCulture),
            [CpKey] = claim.ConquerPoints.ToString(CultureInfo.InvariantCulture),
        };

    private static IReadOnlyDictionary<string, string> WalletValues(long money, long cp, LegacyTradeEscrowClaim? claim)
    {
        var values = new Dictionary<string, string>(Values(claim), StringComparer.OrdinalIgnoreCase)
        {
            ["Money"] = money.ToString(CultureInfo.InvariantCulture),
            ["ConquerPoints"] = cp.ToString(CultureInfo.InvariantCulture),
        };
        return values;
    }

    private static bool IsClaimKey(string key) => key.Equals(IdKey, StringComparison.OrdinalIgnoreCase) ||
        key.Equals(MoneyKey, StringComparison.OrdinalIgnoreCase) || key.Equals(CpKey, StringComparison.OrdinalIgnoreCase);

    private static bool TryCanonicalLong(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
        value >= 0 && text == value.ToString(CultureInfo.InvariantCulture);

    private static bool IsFileFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or System.Security.SecurityException;
}
