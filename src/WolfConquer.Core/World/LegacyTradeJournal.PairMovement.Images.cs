using System.Globalization;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidatePairMovementChanges(LegacyTradeJournalChange[] changes)
    {
        ValidateCharacterResourcesChanges(changes);
        if (changes.Any(change => change.Before!.Length == 0 || change.After.Length == 0))
            throw new InvalidDataException("Pair movement requires two complete existing Character images.");
    }

    private static void RequirePairMovementOrder(LegacyTradeJournalChange[] changes)
    {
        ValidatePairMovementChanges(changes);
        if (changes[0].FileId >= changes[1].FileId)
            throw new InvalidDataException("Pair movement account target order is not canonical.");
    }

    private static void ValidateRecordedPairMovementImages(JournalRecord record)
    {
        if (record.Version != 21 || record.PairMovement is not { } operation)
            throw new InvalidDataException("Missing pair movement21 intent.");
        operation.Validate(); RequirePairMovementOrder(record.Changes);
        foreach (var change in record.Changes)
        {
            var uid = operation.UidFor(change.FileId);
            if (!LegacyPairMovementCharacterProjection.TryProject(change.Before!, operation, uid,
                    out var expected, out var reason) || !Matches(expected, change.After))
                throw new InvalidDataException("Pair movement image differs from its exact coordinate intent: " + reason);
        }
    }

    private static JournalTarget[] GetPairMovementTargets(JournalRoots roots, JournalRecord record)
    {
        ValidateRecordedPairMovementImages(record);
        var current = GetCharacterResourcesRoots(roots.Users);
        var targets = record.Changes.Select(change => new JournalTarget(Path.Combine(current.Users,
            change.FileId.ToString(CultureInfo.InvariantCulture) + ".ini"), change)).ToArray();
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }
}
