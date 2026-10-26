using System.Globalization;
using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Closed package work metadata. It cannot write game files or allocate item IDs.
internal sealed partial class LegacyMailboxPackageWorkStore
{
    internal sealed record Record(int Version, string Domain, string Operation, string PayloadSha256,
        uint AccountId, uint CharacterUid, uint First, int Count, string Users, string Items, string Mailbox,
        string CombatGear, string[] Before, string[] After, bool Completed,
        string? AllocationKind = null, string? PayloadBase64 = null);

    private readonly string users, items, mailbox, gear, directory;
    private readonly Guid domain;
    private readonly LegacyMailboxPackageProducer owner;

    internal LegacyMailboxPackageWorkStore(LegacyMailboxPackageProducer owner)
    {
        this.owner = owner;
        domain = owner.Domain; users = owner.UsersRoot; items = owner.ItemsRoot; mailbox = owner.MailboxRoot; gear = owner.CombatGearRoot;
        directory = Path.Combine(users, ".mailbox-package-work", domain.ToString("N"));
    }

    internal string[] TargetPaths(Record row) =>
    [
        Path.Combine(users, row.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
        Path.Combine(items, row.CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin"),
        Path.Combine(mailbox, row.CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin"),
        Path.Combine(gear, row.CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini"),
    ];

    private Record CreateRecord(LegacyItemUidProducerIntentSnapshot intent, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        var receipt = intent.Allocation ?? throw new InvalidOperationException("Package allocation is missing.");
        LegacyTradeJournalFileKind[] kinds = [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items,
            LegacyTradeJournalFileKind.Mailbox, LegacyTradeJournalFileKind.CombatGear];
        if (intent.DomainId != domain || intent.Request.Writer != LegacyMailboxPackageProducer.Writer ||
            changes.Count != 4 || !changes.Select(change => change.Kind).SequenceEqual(kinds) ||
            changes[0].FileId != intent.Request.AccountId || changes.Skip(1).Any(change => change.FileId != intent.Request.CharacterUid) ||
            changes.Any(change => change.Before is null || change.After is null))
            throw new InvalidOperationException("Package work differs from its real intent and targets.");
        return new(1, domain.ToString("N"), intent.Request.OperationId.ToString("N"), intent.Request.PayloadSha256,
            intent.Request.AccountId, intent.Request.CharacterUid, receipt.First, receipt.Count,
            users, items, mailbox, gear, changes.Select(change => Digest(change.Before!)).ToArray(),
            changes.Select(change => Digest(change.After)).ToArray(), false);
    }

    internal bool MatchesIntent(Record row, LegacyItemUidProducerIntentSnapshot intent) =>
        row.Version == 1 && row.Domain == domain.ToString("N") && intent.DomainId == domain && row.Operation == intent.Request.OperationId.ToString("N") &&
        row.PayloadSha256 == intent.Request.PayloadSha256 && row.AccountId == intent.Request.AccountId &&
        row.CharacterUid == intent.Request.CharacterUid && intent.Request.Writer == LegacyMailboxPackageProducer.Writer &&
        intent.Allocation is { } allocation && row.First == allocation.First && row.Count == allocation.Count &&
        row.Users == users && row.Items == items && row.Mailbox == mailbox && row.CombatGear == gear &&
        owner.TryDescribe(intent, out var recipe) && row.Before.SequenceEqual(recipe!.Before, StringComparer.Ordinal) &&
        row.After[0] == recipe.After[0] && row.After[3] == recipe.After[1] && row.After[2] == recipe.After[2] &&
        (recipe.ItemsAfterSha256 is null || row.After[1] == recipe.ItemsAfterSha256);

    internal bool ImagesMatch(Record row, bool after)
    {
        var paths = TargetPaths(row);
        var hashes = after ? row.After : row.Before;
        for (var index = 0; index < paths.Length; index++)
            if (!CharacterSnapshotFileBaseline.TryReadBytes(paths[index], "mailboxPackageWork", out var bytes, out _) ||
                Digest(bytes) != hashes[index]) return false;
        return true;
    }

    internal static bool Same(Record left, Record right)
    {
        var blank = Array.Empty<string>();
        return (left with { Before = blank, After = blank, Completed = false }) ==
            (right with { Before = blank, After = blank, Completed = false }) &&
            left.Before.SequenceEqual(right.Before, StringComparer.Ordinal) && left.After.SequenceEqual(right.After, StringComparer.Ordinal);
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private string WorkPath(Guid operation) => Path.Combine(directory, operation.ToString("N") + ".json");
}
