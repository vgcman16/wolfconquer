using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Recovery recipe only; this does not authorize any Store or native write.
internal sealed record LegacyRelicFuseReturnJournalOperation(uint AccountId, uint CharacterUid, uint SelectedUid);

internal static partial class LegacyTradeJournal
{
    internal static void ValidateRelicFuseReturnImages(LegacyTradeJournalChange[] changes,
        LegacyRelicFuseReturnJournalOperation operation, uint highWater)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(operation);
        RequireCharacterItemsSpellsOrder(changes);
        var character = changes[0]; var items = changes[1]; var spells = changes[2];
        if (operation.AccountId == 0 || operation.CharacterUid == 0 || operation.SelectedUid == 0 ||
            operation.SelectedUid > highWater || character.Before is null || items.Before is null ||
            character.FileId != operation.AccountId || items.FileId != operation.CharacterUid ||
            spells.FileId != operation.CharacterUid || ReadCharacterUid(character.Before) != operation.CharacterUid ||
            ReadCharacterUid(character.After) != operation.CharacterUid || !Matches(character.Before, character.After) ||
            !(spells.Before is null ? spells.After.AsSpan().SequenceEqual(new byte[sizeof(int)]) : Matches(spells.Before, spells.After)))
            throw new InvalidDataException("relicFuseReturnJournalOwnerOrUnchangedImageInvalid");
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(items.Before);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(items.After);
        // At a later journal prefix the actual physical file can already be
        // the afterimage, so the startup scan alone cannot see discarded UIDs.
        // Both complete retained images must fit the real registry high-water.
        if (LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.Before).Any(uid => uid > highWater) ||
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.After).Any(uid => uid > highWater))
            throw new InvalidDataException("relicFuseReturnJournalUidExceedsGlobalOwner");
        var before = PlayerItemSnapshotLoader.Parse(items.Before);
        if (!WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(before)) ||
            !LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(before) ||
            !LegacyRelicFuseReturnPreparation.TryPrepare(before, operation.SelectedUid, out var preparation, out _) || preparation is null ||
            !preparation.TryProject(before, out var projected, out _) || projected is null ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items).AsSpan().SequenceEqual(items.After))
            throw new InvalidDataException("relicFuseReturnJournalItemAfterimageMismatch");
    }
}
