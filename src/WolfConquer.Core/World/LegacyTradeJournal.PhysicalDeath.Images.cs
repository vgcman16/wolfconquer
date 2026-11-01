using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal const int PhysicalDeathMaximumReferenceBytes = MaxTargetBytes;

    private static void RequirePhysicalDeathOrder(LegacyTradeJournalChange[] changes)
    {
        ValidateCharacterSpellsChanges(changes);
        if (changes[0].Kind != LegacyTradeJournalFileKind.Character || changes[1].Kind != LegacyTradeJournalFileKind.Spells ||
            changes[0].Before is not { Length: > 0 }) throw new InvalidDataException("Physical death22 targets are not Character then Spells.");
    }

    private static void ValidateRecordedPhysicalDeathImages(JournalRecord record)
    {
        if (record.Version != 22 || record.PhysicalDeath is not { } operation)
            throw new InvalidDataException("Missing physical death22 intent.");
        RequirePhysicalDeathOrder(record.Changes);
        var character = record.Changes[0]; var spells = record.Changes[1];
        var before = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character.Before!, out _));
        if (before.Uid != operation.TargetUid || spells.FileId != operation.TargetUid ||
            !Matches(operation.ProjectCharacter(character.Before!, spells.Before), character.After) ||
            !Matches(operation.ProjectSpells(spells.Before, before), spells.After))
            throw new InvalidDataException("Physical death22 images differ from the complete fixed death/spell recipe.");
    }

    private static JournalTarget[] GetPhysicalDeathTargets(JournalRoots roots, JournalRecord record)
    {
        ValidateRecordedPhysicalDeathImages(record);
        var domain = record.CharacterItemsSpellsDomain;
        if (roots.Spells is null || domain is null || !SameQuestRoot(roots.Users, domain.Users) ||
            !SameQuestRoot(roots.Items, domain.Items) || !SameQuestRoot(roots.Spells, domain.Spells))
            throw new InvalidDataException("Physical death22 differs from its explicit Character/Items/Spells configuration.");
        var current = GetCharacterItemsSpellsRoots(roots.Users, roots.Items, roots.Spells);
        var targets = new[]
        {
            new JournalTarget(Path.Combine(current.Users, record.Changes[0].FileId.ToString(CultureInfo.InvariantCulture) + ".ini"), record.Changes[0]),
            new JournalTarget(Path.Combine(current.Spells!, record.Changes[1].FileId.ToString(CultureInfo.InvariantCulture) + ".bin"), record.Changes[1]),
        };
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }

    private static string PhysicalDeathItemsPath(JournalRoots roots, JournalRecord record) =>
        Path.Combine(roots.Items, record.PhysicalDeath!.TargetUid.ToString(CultureInfo.InvariantCulture) + ".bin");

    private static void RequirePhysicalDeathItemsCurrent(JournalRoots roots, JournalRecord record)
    {
        var path = PhysicalDeathItemsPath(roots, record); EnsureSafePath(path);
        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "physicalDeath22Items", out var current, out _) ||
            !current.AsSpan().SequenceEqual(record.PhysicalDeath!.ItemsBefore))
            throw new InvalidDataException("Physical death22 read-only item source changed.");
    }

    private static void PreparePhysicalDeathDirectories(JournalRoots roots, JournalRecord record, JournalTarget[] targets)
    {
        if (roots.Spells is null) throw new InvalidDataException("Missing physical death Spells root.");
        EnsureSafePath(roots.Spells);
        Directory.CreateDirectory(roots.Spells);
        var current = GetPhysicalDeathTargets(roots, record);
        if (targets.Length != current.Length || targets.Where((target, index) =>
                !JournalDomainComparer.Equals(target.Path, current[index].Path) ||
                !ReferenceEquals(target.Change, current[index].Change)).Any())
            throw new InvalidDataException("Physical death Character/Spells target ownership changed.");
        RequirePhysicalDeathItemsCurrent(roots, record);
        EnsureTargetsMatch(current, allowAfter: true);
    }
}
