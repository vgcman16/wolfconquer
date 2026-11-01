using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void RequireHitProgressionOrder(LegacyTradeJournalChange[] changes)
    {
        if (changes.Length is < 2 or > 3 || changes[0].Kind != LegacyTradeJournalFileKind.Character ||
            changes[^1].Kind != LegacyTradeJournalFileKind.Proficiencies ||
            changes.Length == 3 && changes[1].Kind != LegacyTradeJournalFileKind.Spells ||
            changes[0].Before is not { Length: > 0 } || changes.Any(change =>
                change.FileId is <= 0 or > uint.MaxValue || change.After is not { Length: > 0 and <= MaxTargetBytes } ||
                change.Before is { Length: > MaxTargetBytes }))
            throw new InvalidDataException("Invalid Character/optional Spells/Proficiencies hit progression order.");
    }

    private static void ValidateRecordedHitProgressionImages(JournalRecord record)
    {
        if (record.Version != 24 || record.PhysicalHitProgression is not { } operation)
            throw new InvalidDataException("Missing hit progression24 intent.");
        RequireHitProgressionOrder(record.Changes);
        RequireHitProgressionEnvelopeBudget(record.Changes, operation, record.HitProgressionDomain);
        var character = record.Changes[0]; var proficiencies = record.Changes[^1];
        bool death = operation.Death is not null;
        if (character.FileId != operation.TargetAccountId || proficiencies.FileId != operation.ActorUid ||
            record.Changes.Length != (death ? 3 : 2) || death && record.Changes[1].FileId != operation.TargetUid)
            throw new InvalidDataException("Hit progression account/player identities differ.");
        var projection = operation.Project(character.Before!, proficiencies.Before, death ? record.Changes[1].Before : null);
        if (!Matches(character.After, projection.CopyCharacterAfter()) ||
            !Matches(proficiencies.After, projection.CopyProficienciesAfter()) ||
            death && !Matches(record.Changes[1].After, projection.CopySpellsAfter()))
            throw new InvalidDataException("Hit progression images differ from their complete historical recipe.");
    }

    private static void RequireHitProgressionReferencesCurrent(JournalRoots roots, JournalRecord record)
    {
        var operation = record.PhysicalHitProgression!;
        var paths = HitProgressionReferencePaths(roots, record);
        foreach (var path in paths) EnsureSafePath(path);
        if (!Matches(ReadTarget(paths[1]), operation.ActorItems) || operation.Death is not null &&
            !Matches(ReadTarget(paths[2]), operation.Death.ItemsBefore))
            throw new InvalidDataException("Hit progression read-only Items changed.");
        RequireHitProgressionAccountMapping(roots, record);
    }

    // Existence of one legal prefix is explicit when an image is a no-op.
    // It never guesses which equal-image replacement physically occurred.
    private static void RequireHitProgressionPrefix(JournalTarget[] targets)
    {
        var images = targets.Select(target => ReadTarget(target.Path)).ToArray();
        for (var prefix = 0; prefix <= targets.Length; prefix++)
            if (targets.Select((target, index) => Matches(images[index],
                    index < prefix ? target.Change.After : target.Change.Before)).All(equal => equal)) return;
        throw new InvalidDataException("Hit progression current images are not a legal replacement prefix.");
    }
}
