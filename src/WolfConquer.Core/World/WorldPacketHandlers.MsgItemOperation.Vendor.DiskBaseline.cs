using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool ValidateLegacyVendorDiskBaseline(WorldSession expected, LegacyItemSaveGraph items,
        string? itemsRoot, out string reason)
    {
        reason = "vendorPersistenceRootMissing";
        if (string.IsNullOrWhiteSpace(expected.UsersRoot) || string.IsNullOrWhiteSpace(itemsRoot)) return false;
        try
        {
            var characterPath = Path.Combine(expected.UsersRoot,
                expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            // Exact UID/wallet/canonical escrow baseline, not a permissive loader parse.
            if (!LegacyTradeEscrowClaims.TryValidateBaseline(characterPath, expected, out reason)) return false;
            var itemPath = Path.Combine(itemsRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var bytes = PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(itemPath);
            // Preserve the existing Trade absent-empty-file compatibility only.
            // Missing nonempty inventory never establishes a matching baseline.
            bytes ??= PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty);
            if (items.HasBytes(bytes)) { reason = string.Empty; return true; }
            reason = "vendorItemFileStale";
            return false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or OverflowException or System.Security.SecurityException)
        {
            reason = "vendorBaselineRead:" + error.GetType().Name;
            return false;
        }
    }
}
