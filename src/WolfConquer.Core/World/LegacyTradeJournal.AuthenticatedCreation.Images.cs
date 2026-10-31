using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateAuthenticatedCreationChanges(LegacyTradeJournalChange[] changes)
    {
        if (changes.Length != 6 || changes.Any(row => row.FileId is <= 0 or > int.MaxValue || row.Before is not null ||
                row.After is null || row.After.Length is < 1 or > MaxTargetBytes) ||
            changes.Select(row => row.FileId).Distinct().Count() != 1 ||
            !changes.Select(row => row.Kind).SequenceEqual(LegacyAuthenticatedOrdinaryCreationRecipe.TargetKinds))
            throw new InvalidDataException("Authenticated creation requires exactly six absent account participants.");
        foreach (var row in changes) ValidateAdmissionSavedImage(row, row.After);
    }

    private static LegacyAuthenticatedOrdinaryCreationRecipe ValidateAuthenticatedCreationImages(JournalRecord record,
        uint highWater)
    {
        ValidateAuthenticatedCreationChanges(record.Changes);
        var roots = record.AdmissionDomain ?? throw new InvalidDataException("Creation domain is missing.");
        var mailbox = record.CreationMailbox ?? throw new InvalidDataException("Creation mailbox is missing.");
        var operation = record.OrdinaryCreation ?? throw new InvalidDataException("Creation operation is missing.");
        var recipe = LegacyAuthenticatedOrdinaryCreationRecipe.Decode(operation.Payload, roots, mailbox);
        var snapshot = new LegacyItemUidProducerIntentSnapshot(operation.Allocation.DomainId, recipe.CreateRequest(),
            operation.PreparedGeneration, operation.Payload, operation.Allocation);
        _ = LegacyOrdinaryProducerRecipe.Read(snapshot, operation.Allocation.DomainId, roots, mailbox);
        if ((ulong)operation.Allocation.First + (uint)operation.Allocation.Count - 1 > highWater)
            throw new InvalidDataException("Authenticated creation exceeds the pinned registered range.");
        var images = recipe.AfterImages(operation.Allocation);
        foreach (var change in record.Changes)
            if (change.FileId != recipe.Account || !images.TryGetValue(change.Kind, out var expected) ||
                !Matches(change.After, expected))
                throw new InvalidDataException("Authenticated creation image differs from its exact source projection.");
        return recipe;
    }

    private static JournalRoots GetAuthenticatedCreationRoots(LegacyOrdinaryAdmissionRoots roots, string mailbox)
    {
        var result = GetAdmissionRoots(roots);
        ValidateAuthenticatedCreationMailboxRoute(roots, mailbox);
        return result with { Mailbox = mailbox, Spells = roots.Spells, Quests = roots.Quests, CombatGear = roots.CombatGear };
    }

    private static JournalTarget[] GetAuthenticatedCreationTargets(JournalRoots roots, JournalRecord record)
    {
        var recipe = ValidateAuthenticatedCreationImages(record, uint.MaxValue);
        if (roots.Admission != recipe.Roots || !SameQuestRoot(roots.Mailbox!, recipe.MailboxRoot))
            throw new InvalidDataException("Creation journal differs from the actual configured participant roots.");
        var paths = AuthenticatedCreationPaths(recipe.Roots, recipe.MailboxRoot, recipe.Account);
        return record.Changes.Select(change => new JournalTarget(paths[change.Kind], change)).ToArray();
    }

    private static void EnsureAuthenticatedCreationInputs(JournalRoots roots, JournalRecord record)
    {
        if (record.Version != 19) return;
        var recipe = ValidateAuthenticatedCreationImages(record, uint.MaxValue);
        var paths = AuthenticatedCreationPaths(roots.Admission!, roots.Mailbox!, recipe.Account);
        if (ReadTarget(paths[LegacyTradeJournalFileKind.Mailbox]) is not null)
            throw new InvalidDataException("Authenticated creation mailbox absence changed.");
    }

    private static void PrepareAuthenticatedCreationDirectories(JournalRoots roots, JournalRecord record,
        JournalTarget[] targets)
    {
        foreach (var target in targets) Directory.CreateDirectory(Path.GetDirectoryName(target.Path)!);
        _ = GetAuthenticatedCreationRoots(roots.Admission!, record.CreationMailbox!);
    }
}
