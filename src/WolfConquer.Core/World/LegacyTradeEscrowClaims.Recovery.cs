using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeEscrowClaims
{
    // Run only before accepting game sessions, and after replaying committed pair journals. A claim
    // on disk is a debit not yet settled by a journal, so startup refunds its owner, not its partner.
    internal static bool TryRecoverCharacter(string path, out bool recovered, out string reason)
    {
        recovered = false;
        reason = string.Empty;
        try
        {
            if (!TryValidatePath(path, directory: false, out reason)) return false;
            var failure = string.Empty;
            var didRecover = false;
            var result = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
            {
                if (!TryReadRecoveryDisk(path, out var disk, out failure)) return false;
                if (disk is null || disk.Claim is null) return true;
                if (!TryGetRefundWallet(disk, out var money, out var cp, out failure)) return false;
                if (!CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(path, disk.Uid,
                    WalletValues(money, cp, claim: null), detailedIoFailures: false, out failure)) return false;
                didRecover = true;
                return true;
            });
            recovered = didRecover;
            reason = failure;
            return result;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        { reason = "tradeEscrowRecovery:" + ex.GetType().Name; return false; }
    }

    internal static bool TryRecoverAll(string usersRoot, out int recovered, out string reason)
    {
        recovered = 0;
        reason = string.Empty;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(usersRoot);
            if (!Directory.Exists(usersRoot) && !File.Exists(usersRoot)) return true;
            if (!TryValidatePath(usersRoot, directory: true, out reason)) return false;
            // No recursive enumeration and no traversal through reparse points. Numeric snapshots
            // are the only candidates; adjacent backups, journal files and nested roots are ignored.
            var paths = Directory.EnumerateFiles(Path.GetFullPath(usersRoot), "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetExtension(path).Equals(".ini", StringComparison.OrdinalIgnoreCase) &&
                    IsNumericName(Path.GetFileNameWithoutExtension(path)))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (paths.Length == 0) return true;
            var failure = string.Empty;
            var count = 0;
            var result = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                // Validate the entire root before refunding anything. A corrupt later record must
                // not cause a partially recovered startup; committed journal replay precedes this.
                foreach (var path in paths)
                    if (!TryValidatePath(path, directory: false, out failure) ||
                        !TryReadRecoveryDisk(path, out var disk, out failure) ||
                        (disk is not null && !TryGetRefundWallet(disk, out _, out _, out failure))) return false;
                foreach (var path in paths)
                {
                    if (!TryRecoverCharacter(path, out var changed, out failure)) return false;
                    if (changed) count++;
                }
                return true;
            });
            recovered = count;
            reason = failure;
            return result;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        { reason = "tradeEscrowRecoveryEnumeration:" + ex.GetType().Name; return false; }
    }

    private static bool IsNumericName(string name) => name.Length != 0 &&
        name.All(static c => c is >= '0' and <= '9');

    private static bool TryGetRefundWallet(DiskWallet disk, out long money, out long cp, out string reason)
    {
        money = disk.Money;
        cp = disk.ConquerPoints;
        reason = string.Empty;
        if (disk.Claim is null) return true;
        try
        {
            money = checked(disk.Money + disk.Claim.Money);
            cp = checked(disk.ConquerPoints + disk.Claim.ConquerPoints);
            if (cp <= uint.MaxValue) return true;
        }
        catch (OverflowException) { }
        reason = "tradeEscrowRecoveryOverflow";
        return false;
    }
}
