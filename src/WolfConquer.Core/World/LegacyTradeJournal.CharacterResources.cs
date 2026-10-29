using WolfConquer.Core.Data;
using System.Globalization;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal enum LegacyCharacterResourcesJournalInterruption { None, AfterPending, AfterFirstCharacter, AfterSecondCharacter }

internal static partial class LegacyTradeJournal
{
    // Version11 deliberately leaves held/unpublished version10 untouched. This
    // is a Users-only two-Character domain in the existing single pending slot.
    private sealed record CharacterResourcesJournalDomain(string Users);

    internal static bool TryCommitCharacterResources(string usersRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, out string reason,
        LegacyCharacterResourcesJournalInterruption interruption = LegacyCharacterResourcesJournalInterruption.None)
    {
        reason = "characterResourcesJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        if (!Enum.IsDefined(interruption)) return false;
        try
        {
            var roots = GetCharacterResourcesRoots(usersRoot);
            var record = CreateRecord(changes, 11) with { CharacterResourcesDomain = new(roots.Users) };
            var targets = GetTargets(roots, record);
            var stop = interruption == LegacyCharacterResourcesJournalInterruption.None ? null :
                LegacyJournalInterruption.StopAfter((int)interruption - 1);
            var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(),
                () => CommitLocked(roots, record, targets, stop, out failure));
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "characterResourcesJournalInvalid:" + error.GetType().Name; return false; }
    }

    private static JournalRoots GetCharacterResourcesRoots(string usersRoot)
    {
        var users = NormalizeQuestRoot(usersRoot);
        if (!string.Equals(usersRoot, users, StringComparison.Ordinal) || !Directory.Exists(users))
            throw new ArgumentException("Character resource root must be an existing canonical absolute directory.");
        var journal = Path.Combine(users, JournalDirectoryName);
        EnsureSafePath(users); EnsureSafePath(journal); RequireAdmissionPhysicalRoots([users]);
        return new(users, string.Empty, journal);
    }

    private static CharacterResourcesJournalDomain ReadCharacterResourcesDomain(JsonElement element)
    {
        var fields = GetProperties(element, "Users");
        if (fields["Users"].ValueKind != JsonValueKind.String)
            throw new JsonException("Invalid Character resource domain.");
        var users = fields["Users"].GetString()!;
        if (!string.Equals(users, NormalizeQuestRoot(users), StringComparison.Ordinal))
            throw new JsonException("Character resource root is not canonical.");
        return new(users);
    }

    private static void ValidateCharacterResourcesChanges(LegacyTradeJournalChange[] changes)
    {
        if (changes.Length != 2 || changes.Any(change => change.Kind != LegacyTradeJournalFileKind.Character ||
                change.FileId <= 0 || change.Before is null || change.After is null ||
                change.Before.Length > MaxTargetBytes || change.After.Length > MaxTargetBytes) ||
            changes[0].FileId == changes[1].FileId)
            throw new ArgumentException("Character resources require two distinct complete Character images.");
        var uids = new HashSet<long>();
        foreach (var change in changes)
            if (!uids.Add(ReadCharacterUid(change.Before!)) || ReadCharacterUid(change.Before!) != ReadCharacterUid(change.After))
                throw new InvalidDataException("Character resources cannot replace or duplicate character identity.");
    }

    private static JournalTarget[] GetCharacterResourcesTargets(JournalRoots roots, JournalRecord record)
    {
        if (record.CharacterResourcesDomain is not { } domain || !SameQuestRoot(roots.Users, domain.Users))
            throw new ArgumentException("Character resource domain differs from configured Users root.");
        if (record.Changes[0].FileId >= record.Changes[1].FileId)
            throw new ArgumentException("Character resource target order is not canonical.");
        var current = GetCharacterResourcesRoots(roots.Users);
        var targets = record.Changes.Select(change => new JournalTarget(Path.Combine(current.Users,
            change.FileId.ToString(CultureInfo.InvariantCulture) + ".ini"), change)).ToArray();
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }
}
