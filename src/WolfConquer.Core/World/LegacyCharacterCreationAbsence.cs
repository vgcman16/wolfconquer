using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Exact seven-leaf absence/membership evidence, never creation entitlement.
internal sealed partial class LegacyCharacterCreationAbsence
{
    private readonly LegacyOwnedAdmissionOptions configuration;
    private readonly uint account;
    private readonly Dictionary<LegacyTradeJournalFileKind, string> targets;
    private readonly RootMembership[] membership;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private LegacyCharacterCreationAbsence(LegacyOwnedAdmissionOptions configuration, uint account,
        Dictionary<LegacyTradeJournalFileKind, string> targets, RootMembership[] membership)
    { this.configuration = configuration; this.account = account; this.targets = targets; this.membership = membership; }

    internal static LegacyCharacterCreationAbsence Capture(LegacyOwnedAdmissionOptions configuration, uint account)
    {
        var targets = CreateTargets(configuration, account);
        var candidate = new LegacyCharacterCreationAbsence(configuration, account, targets, []);
        foreach (var target in targets.Values) RequireAbsent(target);
        var roots = targets.Values.Select(path => Path.GetDirectoryName(path)!).Distinct(PathComparer);
        var memberships = roots.Select(root => candidate.CaptureMembership(root)).ToArray();
        foreach (var target in targets.Values) RequireAbsent(target);
        return new(configuration, account, targets, memberships);
    }

    internal static LegacyCharacterCreationAbsence CaptureRecorded(LegacyOwnedAdmissionOptions configuration,
        uint account, IReadOnlyList<LegacyTradeJournalChange> changes, string transactionId)
    {
        var targets = CreateTargets(configuration, account);
        var candidate = new LegacyCharacterCreationAbsence(configuration, account, targets, []);
        var byKind = candidate.RequireCreationChanges(changes, transactionId);
        candidate.RequireCurrentAtWriteBoundary(byKind, transactionId);
        var roots = targets.Values.Select(path => Path.GetDirectoryName(path)!).Distinct(PathComparer);
        var captured = roots.Select(root => candidate.CaptureMembership(root, byKind, transactionId)).Select(root =>
            root with { Entries = candidate.UnrelatedEntries(root, byKind, transactionId) }).ToArray();
        var result = new LegacyCharacterCreationAbsence(configuration, account, targets, captured);
        result.RequireCurrentAtWriteBoundary(byKind, transactionId);
        return result;
    }

    private static Dictionary<LegacyTradeJournalFileKind, string> CreateTargets(
        LegacyOwnedAdmissionOptions configuration, uint account)
    {
        if (account == 0) throw new InvalidDataException("Creation requires a positive account/Character UID.");
        var targets = configuration.Roots.Kinds.ToDictionary(kind => kind,
            kind => configuration.Roots.PathFor(kind, account));
        if (targets.Count != 6 || targets.ContainsKey(LegacyTradeJournalFileKind.Mailbox))
            throw new InvalidDataException("Creation requires all six configured ordinary participants.");
        targets.Add(LegacyTradeJournalFileKind.Mailbox, Path.Combine(configuration.MailboxRoot,
            account.ToString(CultureInfo.InvariantCulture) + ".bin"));
        return targets;
    }

    internal void RequireCurrentBefore()
    {
        foreach (var target in targets.Values) RequireAbsent(target);
        foreach (var root in membership) RequireMembership(root, null, null);
        foreach (var target in targets.Values) RequireAbsent(target);
    }

    internal Dictionary<LegacyTradeJournalFileKind, LegacyTradeJournalChange> RequireCreationChanges(
        IReadOnlyList<LegacyTradeJournalChange> changes, string transactionId)
    {
        if (!Guid.TryParseExact(transactionId, "N", out var transaction) || transaction == Guid.Empty ||
            transaction.ToString("N") != transactionId || changes.Count != 6)
            throw new InvalidDataException("Creation boundary identity/participant count is invalid.");
        var result = new Dictionary<LegacyTradeJournalFileKind, LegacyTradeJournalChange>();
        foreach (var row in changes)
            if (row is null || row.Kind == LegacyTradeJournalFileKind.Mailbox || !targets.ContainsKey(row.Kind) ||
                row.FileId != account || row.Before is not null || row.After is null ||
                row.After.Length is < 1 or > CharacterSnapshotFileBaseline.MaximumBytes ||
                !result.TryAdd(row.Kind, row))
                throw new InvalidDataException("Creation boundary differs from its six absent account participants.");
        return result;
    }

    internal void RequireCurrentAtWriteBoundary(
        IReadOnlyDictionary<LegacyTradeJournalFileKind, LegacyTradeJournalChange> changes, string transactionId)
    {
        RequireAbsent(targets[LegacyTradeJournalFileKind.Mailbox]);
        foreach (var row in changes)
        {
            var path = targets[row.Key]; RequireSafePath(path);
            if (PathExists(path))
            {
                if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "creationTarget", out var bytes, out var reason) ||
                    !bytes.AsSpan().SequenceEqual(row.Value.After))
                    throw new InvalidDataException("Creation target is neither absent nor its exact afterimage: " + reason);
            }
        }
        foreach (var root in membership) RequireMembership(root, changes, transactionId);
        RequireAbsent(targets[LegacyTradeJournalFileKind.Mailbox]);
    }

    private void RequireMembership(RootMembership captured,
        IReadOnlyDictionary<LegacyTradeJournalFileKind, LegacyTradeJournalChange>? changes, string? transactionId)
    {
        var current = CaptureMembership(captured.Root, changes, transactionId);
        var entries = changes is null ? current.Entries : UnrelatedEntries(current, changes, transactionId!);
        if (changes is not null && PathComparer.Equals(captured.Root, configuration.UsersRoot) &&
            !captured.Entries.ContainsKey(".managed-trade-journal"))
            entries.Remove(".managed-trade-journal"); // Real journal owner validates exact contents.
        if ((!current.Exists && captured.Exists) || (!captured.Exists && current.Exists && changes is null) ||
            entries.Count != captured.Entries.Count || captured.Entries.Any(pair =>
                !entries.TryGetValue(pair.Key, out var attributes) || attributes != pair.Value))
            throw new InvalidDataException("Creation participant root membership changed.");
    }

    private Dictionary<string, FileAttributes> UnrelatedEntries(RootMembership current,
        IReadOnlyDictionary<LegacyTradeJournalFileKind, LegacyTradeJournalChange> changes, string transactionId)
    {
        var entries = new Dictionary<string, FileAttributes>(current.Entries, PathComparer);
        foreach (var row in changes)
        {
            var path = targets[row.Key];
            if (!PathComparer.Equals(Path.GetDirectoryName(path), current.Root)) continue;
            entries.Remove(Path.GetFileName(path));
            var stage = path + ".managed-trade-" + transactionId + ".tmp";
            if (!entries.Remove(Path.GetFileName(stage))) continue;
            RequireSafePath(stage);
            if (!CharacterSnapshotFileBaseline.TryReadBytes(stage, "creationStage", out var bytes, out var reason) ||
                bytes.Length > row.Value.After.Length ||
                !bytes.AsSpan().SequenceEqual(row.Value.After.AsSpan(0, bytes.Length)))
                throw new InvalidDataException("Creation stage is not its exact bounded afterimage: " + reason);
        }
        return entries;
    }

    private sealed record RootMembership(string Root, bool Exists, Dictionary<string, FileAttributes> Entries);
}
