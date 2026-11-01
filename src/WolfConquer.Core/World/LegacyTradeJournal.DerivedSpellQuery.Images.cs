using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal const int DerivedSpellQueryMaximumReferenceBytes = MaxTargetBytes;

    private static void RequireDerivedSpellQueryOrder(LegacyTradeJournalChange[] changes)
    {
        ValidateCharacterSpellsChanges(changes);
        if (changes[0].Kind != LegacyTradeJournalFileKind.Character || changes[1].Kind != LegacyTradeJournalFileKind.Spells ||
            changes[0].Before is not { Length: > 0 }) throw new InvalidDataException("Derived spell query23 targets are not Character then Spells.");
    }

    private static void ValidateRecordedDerivedSpellQueryImages(JournalRecord record)
    {
        if (record.Version != 23 || record.DerivedSpellQuery is not { } operation)
            throw new InvalidDataException("Missing derived spell query23 intent.");
        RequireDerivedSpellQueryOrder(record.Changes);
        var character = record.Changes[0]; var spells = record.Changes[1];
        _ = LegacyDerivedSpellContinuationImages.RequirePreserved(character.FileId, operation.TargetUid,
            character.Before!, character.After);
        var before = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character.Before!, out _));
        var projected = operation.Project(character.Before!, spells.Before);
        if (before.Uid != operation.TargetUid || spells.FileId != operation.TargetUid ||
            !Matches(projected.CopyCharacterAfter(), character.After) ||
            !Matches(projected.CopySpellsAfter(), spells.After))
            throw new InvalidDataException("Derived spell query23 images differ from the complete fixed derived-spell recipe.");
    }

    private static JournalTarget[] GetDerivedSpellQueryTargets(JournalRoots roots, JournalRecord record)
    {
        ValidateRecordedDerivedSpellQueryImages(record);
        var domain = record.CharacterItemsSpellsDomain;
        if (roots.Spells is null || domain is null || !SameQuestRoot(roots.Users, domain.Users) ||
            !SameQuestRoot(roots.Items, domain.Items) || !SameQuestRoot(roots.Spells, domain.Spells))
            throw new InvalidDataException("Derived spell query23 differs from its explicit Character/Items/Spells configuration.");
        var current = GetCharacterItemsSpellsRoots(roots.Users, roots.Items, roots.Spells);
        var targets = new[]
        {
            new JournalTarget(Path.Combine(current.Users, record.Changes[0].FileId.ToString(CultureInfo.InvariantCulture) + ".ini"), record.Changes[0]),
            new JournalTarget(Path.Combine(current.Spells!, record.Changes[1].FileId.ToString(CultureInfo.InvariantCulture) + ".bin"), record.Changes[1]),
        };
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }

    private static string DerivedSpellQueryItemsPath(JournalRoots roots, JournalRecord record) =>
        Path.Combine(roots.Items, record.DerivedSpellQuery!.TargetUid.ToString(CultureInfo.InvariantCulture) + ".bin");

    private static void RequireDerivedSpellQueryItemsCurrent(JournalRoots roots, JournalRecord record)
    {
        var path = DerivedSpellQueryItemsPath(roots, record); EnsureSafePath(path);
        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "derivedSpellQuery23Items", out var current, out _) ||
            !current.AsSpan().SequenceEqual(record.DerivedSpellQuery!.ItemsBefore))
            throw new InvalidDataException("Derived spell query23 read-only item source changed.");
    }

    private static void PrepareDerivedSpellQueryDirectories(JournalRoots roots, JournalRecord record, JournalTarget[] targets)
    {
        if (roots.Spells is null) throw new InvalidDataException("Missing derived spell query Spells root.");
        EnsureSafePath(roots.Spells);
        Directory.CreateDirectory(roots.Spells);
        var current = GetDerivedSpellQueryTargets(roots, record);
        if (targets.Length != current.Length || targets.Where((target, index) =>
                !JournalDomainComparer.Equals(target.Path, current[index].Path) ||
                !ReferenceEquals(target.Change, current[index].Change)).Any())
            throw new InvalidDataException("Derived spell query Character/Spells target ownership changed.");
        RequireDerivedSpellQueryItemsCurrent(roots, record);
        EnsureTargetsMatch(current, allowAfter: true);
    }
}
