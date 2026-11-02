using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Fixed recipe/afterimage validation for a future typed relic journal.
    // A caller-supplied high-water or matching bytes alone grant no authority;
    // the not-yet-written journal owner must first obtain actual UID custody.
    internal static void ValidateRelicFuseImages(LegacyTradeJournalChange[] changes,
        LegacyRelicFuseJournalOperation operation, uint highWater)
    {
        ArgumentNullException.ThrowIfNull(changes); ArgumentNullException.ThrowIfNull(operation);
        RequireCharacterItemsSpellsOrder(changes);
        var character = changes[0]; var items = changes[1]; var spells = changes[2];
        if (character.Before is null || items.Before is null || character.FileId != operation.Request.AccountId ||
            items.FileId != operation.Request.CharacterUid || spells.FileId != operation.Request.CharacterUid ||
            ReadCharacterUid(character.Before) != operation.Request.CharacterUid ||
            ReadCharacterUid(character.After) != operation.Request.CharacterUid ||
            operation.Allocation.First > highWater || !Matches(character.Before, character.After) ||
            !(spells.Before is null ? spells.After.AsSpan().SequenceEqual(new byte[sizeof(int)]) : Matches(spells.Before, spells.After)))
            throw new InvalidDataException("relicFuseJournalOwnerOrUnchangedImageInvalid");
        var words = operation.Words.ToArray();
        var digest = LegacyRelicFuseRecipeDigest.Compute(operation.Allocation.DomainId,
            operation.Request.AccountId, operation.Request.CharacterUid, operation.FirstUid, operation.SecondUid,
            operation.Bound, operation.Durability, words, character.Before, items.Before, spells.Before);
        if (!StringComparer.Ordinal.Equals(digest, operation.Request.PayloadSha256))
            throw new InvalidDataException("relicFuseJournalRecipeDigestMismatch");
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(items.Before);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(items.After);
        var before = PlayerItemSnapshotLoader.Parse(items.Before);
        if (!WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(before)) ||
            !LegacyRelicFuseRollData.TryFromRecorded(operation.FirstUid, operation.SecondUid,
                operation.ResultItemId, operation.Bound, operation.Durability,
                words[0], words[1], words[2], words[3], words[4], out var roll) || roll is null ||
            !LegacyRelicFuseProjection.TryCreate(before, roll, operation.Allocation.First, out var projected, out _) || projected is null ||
            !projected.CopyAfterImage().AsSpan().SequenceEqual(items.After))
            throw new InvalidDataException("relicFuseJournalItemAfterimageMismatch");
    }
}
