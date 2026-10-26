using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyAccountManagerFullPromotionPersistence
{
    internal const string RecoveryRequired = "accountManagerFullPromotionRecoveryRequired";
    private const string Prefix = "accountManagerFullPromotion";
    internal enum Outcome { Rejected, Committed, Indeterminate }
    internal sealed record Prepared(string Users, string Path, byte[] Before, byte[] After);
    internal readonly record struct Values(ulong Donation, long Class, byte ChargePromotion);

    internal static bool TryReadValues(IReadOnlyDictionary<string, string> fields, out Values values)
    {
        values = default;
        ulong donation = 0, charge = 0;
        if (fields.TryGetValue("DonatePoints", out var rawDonation) &&
            !ulong.TryParse(rawDonation, NumberStyles.Integer, CultureInfo.InvariantCulture, out donation)) return false;
        if (!fields.TryGetValue("Class", out var rawClass) ||
            !long.TryParse(rawClass, NumberStyles.Integer, CultureInfo.InvariantCulture, out var classCode) ||
            classCode is <= 0 or > uint.MaxValue) return false;
        if (fields.TryGetValue("ChargePromotion", out var rawCharge) &&
            (!ulong.TryParse(rawCharge, NumberStyles.Integer, CultureInfo.InvariantCulture, out charge) || charge > byte.MaxValue)) return false;
        values = new(donation, classCode, (byte)charge);
        return true;
    }

    internal static bool TryCapture(WorldSession expected, Values expectedValues,
        out CharacterSnapshotFileBaseline.Snapshot? baseline, out string path, out string reason)
    {
        baseline = null; path = string.Empty; reason = Prefix + "StorageBindingInvalid";
        if (expected.AccountId is <= 0 or > uint.MaxValue || expected.Character.Uid is <= 0 or > uint.MaxValue ||
            string.IsNullOrWhiteSpace(expected.UsersRoot) || !Path.IsPathFullyQualified(expected.UsersRoot)) return false;
        var users = Path.GetFullPath(expected.UsersRoot);
        var name = expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
        path = Path.Combine(users, name);
        var source = expected.Character.SourceFile;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(source, name, comparison) && (string.IsNullOrWhiteSpace(source) || !Path.IsPathFullyQualified(source) ||
            !string.Equals(Path.GetFullPath(source), path, comparison))) return false;
        CharacterSnapshotFileBaseline.Snapshot? captured = null;
        var failure = reason;
        var actualPath = path;
        var accepted = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            if (LegacyTradeJournal.HasPending(users)) { failure = RecoveryRequired + ":pendingJournal"; return false; }
            if (!CharacterSnapshotFileBaseline.TryRead(actualPath, Prefix, out var current, out failure) || current is null) return false;
            var text = CharacterSnapshotFileEncoding.Decode(current.Bytes, out var encoding);
            if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(current.Bytes))
            { failure = Prefix + "EncodingUnproven"; return false; }
            var parsed = CharacterSnapshotParser.ParseText(text, source);
            if (parsed.Uid != expected.Character.Uid || parsed.Class != expected.Character.Class || !TryReadValues(current.Fields, out var saved) || saved != expectedValues)
            { failure = Prefix + "EconomicBaselineStale"; return false; }
            captured = current;
            return true;
        });
        baseline = captured; reason = failure;
        return accepted;
    }

    internal static Outcome Commit(Prepared prepared, Func<bool> stillCurrent, out string reason)
    {
        var failure = string.Empty;
        var phase = Outcome.Rejected;
        try
        {
            var outcome = SnapshotFileLockCoordinator.ExecuteLocked(prepared.Path, () =>
            {
                if (LegacyTradeJournal.HasPending(prepared.Users))
                { failure = RecoveryRequired + ":pendingJournal"; return Outcome.Indeterminate; }
                // This separate read is pre-write. A denied/missing baseline
                // here cannot be mistaken for a partial or successful write.
                if (!CharacterSnapshotFileBaseline.TryReadBytes(prepared.Path, Prefix, out var before, out failure) ||
                    !before.AsSpan().SequenceEqual(prepared.Before))
                { if (failure.Length == 0) failure = Prefix + "PreparedFileChanged"; return Outcome.Rejected; }
                // Read/metadata work may retire the real owner. Recheck under
                // the actual Character image gate immediately before replacement.
                if (!stillCurrent()) { failure = Prefix + "OwnerChanged"; return Outcome.Rejected; }
                phase = Outcome.Indeterminate;
                if (CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(prepared.Path, prepared.Before, prepared.After,
                        out var uncertain, out failure)) return Outcome.Committed;
                if (!uncertain) return Outcome.Rejected;
                failure = RecoveryRequired + ":" + failure;
                return Outcome.Indeterminate;
            });
            reason = failure;
            return outcome;
        }
        catch (Exception error) when (IsFailure(error))
        {
            reason = Prefix + "Write:" + error.GetType().Name;
            if (phase == Outcome.Rejected) return Outcome.Rejected;
            try
            {
                if (CharacterSnapshotFileBaseline.TryReadBytes(prepared.Path, Prefix, out var actual, out _))
                {
                    if (actual.AsSpan().SequenceEqual(prepared.After)) { reason = string.Empty; return Outcome.Committed; }
                    if (actual.AsSpan().SequenceEqual(prepared.Before)) return Outcome.Rejected;
                }
            }
            catch (Exception observation) when (IsFailure(observation)) { }
            reason = RecoveryRequired + ":writeOutcomeUnproven:" + error.GetType().Name;
            return Outcome.Indeterminate;
        }
    }

    internal static bool IsFailure(Exception error) => error is IOException or UnauthorizedAccessException or ArgumentException or
        NotSupportedException or FormatException or OverflowException or InvalidOperationException or System.Security.SecurityException;
}
