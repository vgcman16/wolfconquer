using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// One character file owns BOTH purchased capacity and its CP cost. No item file changes.
internal static partial class LegacyInventorySashUpgradePersistence
{
    private const string Prefix = "sashUpgrade";
    internal const string RecoveryRequired = "sashUpgradeRecoveryRequired";

    internal static bool TryReadCount(IReadOnlyDictionary<string, string> fields, out ulong count)
    {
        count = 0;
        return !fields.TryGetValue("InventorySashCount", out var text) ||
            ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
    }

    internal static bool TryPersist(WorldSession expected, ulong count, long updatedCp, out string reason)
    {
        reason = Prefix + "BindingInvalid";
        if (expected.AccountId <= 0 || expected.Character.Uid is <= 0 or > uint.MaxValue ||
            expected.Character.ConquerPoints < 90 || count >= 300 || updatedCp != expected.Character.ConquerPoints - 90 ||
            string.IsNullOrWhiteSpace(expected.UsersRoot) || !Path.IsPathFullyQualified(expected.UsersRoot)) return false;
        try
        {
            var name = expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
            var path = Path.Combine(Path.GetFullPath(expected.UsersRoot), name);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var source = expected.Character.SourceFile;
            if (!string.Equals(source, name, comparison) && (string.IsNullOrWhiteSpace(source) ||
                !Path.IsPathFullyQualified(source) || !string.Equals(Path.GetFullPath(source), path, comparison))) return false;
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
                PersistLocked(path, expected, count, updatedCp, out failure));
            reason = failure;
            return saved;
        }
        catch (Exception error) when (IsBaselineFailure(error))
        {
            // Write failures are classified by the coordinator, never downgraded here.
            reason = Prefix + "Baseline:" + error.GetType().Name;
            return false;
        }
    }

    private static bool PersistLocked(string path, WorldSession expected, ulong count, long updatedCp, out string reason)
    {
        if (!CharacterSnapshotFileBaseline.TryRead(path, Prefix, out var baseline, out reason)) return false;
        var text = CharacterSnapshotFileEncoding.Decode(baseline!.Bytes, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(baseline.Bytes))
        { reason = Prefix + "EncodingUnproven"; return false; }
        // The actual complete character parser must accept the file, not merely a fabricated UID row.
        var parsed = CharacterSnapshotParser.ParseText(text, expected.Character.SourceFile);
        var liveHasCount = expected.Character.Attributes.Keys.Any(key => key.Equals("InventorySashCount", StringComparison.OrdinalIgnoreCase));
        if (parsed.Uid != expected.Character.Uid || parsed.Money != expected.Character.Money ||
            parsed.ConquerPoints != expected.Character.ConquerPoints ||
            !TryReadCount(parsed.Attributes, out var diskCount) || diskCount != count ||
            parsed.Attributes.ContainsKey("InventorySashCount") != liveHasCount ||
            !LegacyTradeEscrowClaims.TryRead(parsed.Attributes, out var claim, out _) || claim is not null)
        { reason = Prefix + "CharacterBaselineStaleOrEscrow"; return false; }
        if (!TryProject(text, baseline, expected.Character.Uid, updatedCp, count + 1, out var image, out reason)) return false;
        var saved = CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, baseline.Bytes, image,
            out var uncertain, out reason);
        if (!saved) reason = (uncertain ? RecoveryRequired : Prefix + "WriteRejected") + ":" + reason;
        return saved;
    }

    private static bool IsBaselineFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or System.Security.SecurityException or OverflowException or FormatException;
}
