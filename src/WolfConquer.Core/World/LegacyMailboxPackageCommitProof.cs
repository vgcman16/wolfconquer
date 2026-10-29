using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// An in-process publication witness issued only after the real four-file owner
// commits and all four exact postimages are observed under its shared lock domain.
internal sealed class LegacyMailboxPackageCommitProof
{
    private readonly uint uid;
    private readonly byte[] gearBefore;
    private readonly byte[] gearAfter;
    private readonly string[] roots;
    private readonly LegacyTradeJournalChange[] changes;

    private LegacyMailboxPackageCommitProof(uint uid, string[] roots, LegacyTradeJournalChange[] changes)
    { this.uid = uid; this.roots = roots; this.changes = changes; gearBefore = changes[3].Before!; gearAfter = changes[3].After; }

    internal bool MatchesPackageWork(LegacyMailboxPackageWorkStore.Record row) =>
        uid == row.CharacterUid && changes[0].FileId == row.AccountId &&
        roots.SequenceEqual(new[] { row.Users, row.Items, row.Mailbox, row.CombatGear },
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal) &&
        changes.Select(change => Hash(change.Before!)).SequenceEqual(row.Before, StringComparer.Ordinal) &&
        changes.Select(change => Hash(change.After)).SequenceEqual(row.After, StringComparer.Ordinal);

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    internal bool MatchesCombatGear(uint expectedUid, ReadOnlySpan<byte> before, ReadOnlySpan<byte> after) =>
        uid == expectedUid && before.SequenceEqual(gearBefore) && after.SequenceEqual(gearAfter);

    internal static bool TryCommit(string users, string items, string mailbox, string gear,
        IReadOnlyList<LegacyTradeJournalChange> changes, out LegacyMailboxPackageCommitProof? proof, out string reason)
        => TryCommit(users, items, mailbox, gear, changes, null, out proof, out reason);

    internal static bool TryCommit(string users, string items, string mailbox, string gear,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyJournalInterruption? interruption,
        out LegacyMailboxPackageCommitProof? proof, out string reason)
    {
        proof = null;
        reason = "mailboxClaimRecoveryRequired:packageJournalInvalid";
        if (changes.Count != 4 || changes.Any(change => change.Before is null || change.After is null)) return false;
        var copies = changes.Select(change => change with { Before = change.Before!.ToArray(), After = change.After.ToArray() }).ToArray();
        LegacyTradeJournalFileKind[] kinds = [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items,
            LegacyTradeJournalFileKind.Mailbox, LegacyTradeJournalFileKind.CombatGear];
        if (!copies.Select(change => change.Kind).SequenceEqual(kinds) || copies.Any(change => change.FileId <= 0) ||
            copies[1].FileId > uint.MaxValue || copies.Skip(1).Any(change => change.FileId != copies[1].FileId)) return false;
        string[] roots = [users, items, mailbox, gear];
        var paths = copies.Select((change, index) => Path.Combine(roots[index],
            change.FileId.ToString(CultureInfo.InvariantCulture) + (index is 0 or 3 ? ".ini" : ".bin"))).ToArray();
        LegacyMailboxPackageCommitProof? committedProof = null;
        var failure = reason;
        var result = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
        {
            if (!ExactImages(paths, copies, after: false))
            { failure = "mailboxClaimRecoveryRequired:packagePreimagesChanged"; return false; }
            if (!LegacyTradeJournal.TryCommitMailboxGearOwner(users, items, mailbox, gear, copies, interruption, out var journalReason))
            { failure = "mailboxClaimRecoveryRequired:packageJournal:" + journalReason; return false; }
            if (!ExactImages(paths, copies, after: true))
            { failure = "mailboxClaimRecoveryRequired:packagePostimagesUnproven"; return false; }
            committedProof = new(checked((uint)copies[1].FileId), roots.Select(Path.GetFullPath).ToArray(), copies);
            failure = string.Empty;
            return true;
        });
        proof = committedProof;
        reason = failure;
        return result;
    }

    private static bool ExactImages(string[] paths, LegacyTradeJournalChange[] changes, bool after)
    {
        for (var index = 0; index < paths.Length; index++)
            if (!CharacterSnapshotFileBaseline.TryReadBytes(paths[index], "mailboxPackage", out var bytes, out _) ||
                !bytes.AsSpan().SequenceEqual(after ? changes[index].After : changes[index].Before)) return false;
        return true;
    }
}
