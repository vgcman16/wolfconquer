using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static WorldSession WithLegacyTradeWallet(WorldSession session, long money, long conquerPoints)
    {
        var character = WithCharacterAttribute(session.Character, "Money", money.ToString(CultureInfo.InvariantCulture));
        character = WithCharacterAttribute(character, "ConquerPoints", conquerPoints.ToString(CultureInfo.InvariantCulture));
        return session with { Character = character with { Money = money, ConquerPoints = conquerPoints } };
    }

    private static bool TryPersistLegacyTradeWallet(WorldPacketHandlerContext context,
        WorldSession original, WorldSession updated, out string reason)
    {
        var saved = LegacyTradeEscrowClaims.TryPersistWallet(original, updated, out reason);
        if (!saved && IsLegacyTradeRecoveryRequired(reason)) context.TradeRecovery?.Block(reason);
        return saved;
    }

    private static bool IsLegacyTradeRecoveryRequired(string reason) =>
        reason.StartsWith("tradeJournalRecoveryRequired", StringComparison.Ordinal) ||
        reason.StartsWith("tradeJournalPending", StringComparison.Ordinal) ||
        reason.StartsWith("tradeEscrowRecoveryRequired", StringComparison.Ordinal);

    private static bool LegacyTradeClaimsEqual(WorldSession first, WorldSession second) =>
        LegacyTradeEscrowClaims.TryRead(first.Character.Attributes, out var a, out _) &&
        LegacyTradeEscrowClaims.TryRead(second.Character.Attributes, out var b, out _) && Equals(a, b);

    private static bool TryPersistLegacyTradePair(WorldPacketHandlerContext context,
        WorldSession first, WorldSession updatedFirst, WorldSession second, WorldSession updatedSecond,
        out string reason)
    {
        reason = string.Empty;
        if (first.Character.Money == updatedFirst.Character.Money &&
            first.Character.ConquerPoints == updatedFirst.Character.ConquerPoints &&
            second.Character.Money == updatedSecond.Character.Money &&
            second.Character.ConquerPoints == updatedSecond.Character.ConquerPoints &&
            ReferenceEquals(first.PlayerItems, updatedFirst.PlayerItems) &&
            ReferenceEquals(second.PlayerItems, updatedSecond.PlayerItems) &&
            LegacyTradeClaimsEqual(first, updatedFirst) && LegacyTradeClaimsEqual(second, updatedSecond)) return true;

        // Preserve the existing vendor gate -> ordered file-lock order. Never take its file locks first.
        // The validator executes inside those locks, before any staged/committed write.
        var baselineReason = string.Empty;
        var persisted = LegacyVendorPurchasePersistence.TryPersistWithBaselineValidation(
            first, updatedFirst, second, updatedSecond, context.PlayerItemsRoot,
            () => ValidateLegacyTradeDiskBaseline(first, context.PlayerItemsRoot, out baselineReason) &&
                  ValidateLegacyTradeDiskBaseline(second, context.PlayerItemsRoot, out baselineReason), out reason);
        if (!persisted && baselineReason.Length != 0) reason = baselineReason;
        if (!persisted && IsLegacyTradeRecoveryRequired(reason)) context.TradeRecovery?.Block(reason);
        return persisted;
    }

    private static bool ValidateLegacyTradeDiskBaseline(WorldSession expected, string? playerItemsRoot, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(expected.UsersRoot) || string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            reason = "tradePersistenceRootMissing";
            return false;
        }
        try
        {
            var characterPath = Path.Combine(expected.UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            if (!ValidateLegacyTradeDiskWallet(characterPath, expected, out reason)) return false;
            var itemPath = Path.Combine(playerItemsRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var actual = PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(itemPath);
            var want = PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems);
            if (actual is null) actual = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty);
            if (actual.AsSpan().SequenceEqual(want)) return true;
            reason = "tradeItemFileStale";
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
        {
            reason = "tradeBaselineRead:" + ex.GetType().Name;
            return false;
        }
    }

    private static bool ValidateLegacyTradeDiskWallet(string path, WorldSession expected, out string reason)
    {
        return LegacyTradeEscrowClaims.TryValidateBaseline(path, expected, out reason);
    }

    private static byte[] LegacyTradeItemBytes(PlayerItemSnapshotItem item) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([item], [], [], [], [], []));
}
