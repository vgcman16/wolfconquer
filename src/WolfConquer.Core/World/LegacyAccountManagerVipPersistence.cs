using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyAccountManagerVipPersistence
{
    internal const string RecoveryRequired = "accountManagerVipRecoveryRequired";
    private const string Prefix = "accountManagerVip";
    internal enum Outcome { Rejected, Committed, Indeterminate }
    internal sealed record Prepared(string Users, string Path, byte[] Before, byte[] After);
    internal readonly record struct Values(ulong Donation, byte Level, long? Expiry);

    internal static bool TryReadValues(IReadOnlyDictionary<string, string> fields, out Values values)
    {
        values = default;
        ulong donation = 0, level = 0;
        if (fields.TryGetValue("DonatePoints", out var rawDonation) &&
            !ulong.TryParse(rawDonation, NumberStyles.Integer, CultureInfo.InvariantCulture, out donation)) return false;
        if (fields.TryGetValue("VipLevel", out var rawLevel) &&
            (!ulong.TryParse(rawLevel, NumberStyles.Integer, CultureInfo.InvariantCulture, out level) || level > byte.MaxValue)) return false;
        long? expiry = null;
        if (fields.TryGetValue("VipTime", out var rawExpiry) || fields.TryGetValue("ExpireVip", out rawExpiry))
        {
            if (!string.IsNullOrWhiteSpace(rawExpiry))
            {
                if (!long.TryParse(rawExpiry, NumberStyles.Integer, CultureInfo.InvariantCulture, out var binary)) return false;
                try { _ = DateTime.FromBinary(binary); }
                catch (ArgumentException) { return false; }
                expiry = binary;
            }
        }
        values = new(donation, (byte)level, expiry);
        return true;
    }

    internal static bool TryCapture(WorldSession expected, Values expectedValues,
        out CharacterSnapshotFileBaseline.Snapshot baseline, out string path, out string reason)
    {
        baseline = null!; path = string.Empty; reason = Prefix + "StorageBindingInvalid";
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
            if (!CharacterSnapshotFileBaseline.TryRead(actualPath, Prefix, out var current, out failure)) return false;
            var text = CharacterSnapshotFileEncoding.Decode(current!.Bytes, out var encoding);
            if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(current.Bytes))
            { failure = Prefix + "EncodingUnproven"; return false; }
            var parsed = CharacterSnapshotParser.ParseText(text, source);
            if (parsed.Uid != expected.Character.Uid || !TryReadValues(current.Fields, out var saved) || saved != expectedValues)
            { failure = Prefix + "EconomicBaselineStale"; return false; }
            captured = current;
            return true;
        });
        baseline = captured!; reason = failure;
        return accepted;
    }

    internal static Outcome Commit(Prepared prepared, out string reason)
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
