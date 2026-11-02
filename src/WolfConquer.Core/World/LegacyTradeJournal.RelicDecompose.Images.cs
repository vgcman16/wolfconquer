using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static void ValidateRelicDecomposeImages(LegacyTradeJournalChange[] changes,
        LegacyRelicDecomposeJournalOperation operation, uint highWater)
    {
        ArgumentNullException.ThrowIfNull(changes); ArgumentNullException.ThrowIfNull(operation);
        RequireCharacterItemsSpellsOrder(changes);
        var character = changes[0]; var items = changes[1]; var spells = changes[2];
        if (character.Before is null || items.Before is null || character.FileId != operation.Request.AccountId ||
            items.FileId != operation.Request.CharacterUid || spells.FileId != operation.Request.CharacterUid ||
            ReadCharacterUid(character.Before) != operation.Request.CharacterUid || ReadCharacterUid(character.After) != operation.Request.CharacterUid ||
            (ulong)operation.Allocation.First + (uint)operation.Allocation.Count - 1 > highWater ||
            !Matches(character.Before, character.After) ||
            !(spells.Before is null ? spells.After.AsSpan().SequenceEqual(new byte[sizeof(int)]) : Matches(spells.Before, spells.After)))
            throw new InvalidDataException("relicDecomposeJournalOwnerOrUnchangedImageInvalid");
        var frameBytes = operation.OriginalFrame; var selections = operation.Selections.ToArray();
        var digest = LegacyRelicDecomposeRecipeDigest.Compute(operation.Allocation.DomainId, operation.Request.AccountId,
            operation.Request.CharacterUid, frameBytes, selections, operation.RewardDurability,
            character.Before, items.Before, spells.Before);
        if (!StringComparer.Ordinal.Equals(digest, operation.Request.PayloadSha256))
            throw new InvalidDataException("relicDecomposeJournalRecipeDigestMismatch");
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(items.Before);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(items.After);
        if (LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.Before).Any(uid => uid > highWater) ||
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.After).Any(uid => uid > highWater))
            throw new InvalidDataException("relicDecomposeJournalUidOutsideCustody");
        var bytes = new ReadOnlySequence<byte>(frameBytes);
        var before = PlayerItemSnapshotLoader.Parse(items.Before);
        if (!PacketFrameDecoder.TryRead(bytes, out var frame, out var consumed, out _, ushort.MaxValue) || !bytes.Slice(consumed).IsEmpty ||
            !MsgItemUsagePacket.TryParse(frame, out var packet) ||
            !LegacyRelicDecomposeRequest.TryParse(packet, frame, out var request, out _) || request is null ||
            !request.CopyOriginalFrame().AsSpan().SequenceEqual(frameBytes) ||
            !LegacyRelicDecomposeRecipe.TryFromRecorded(request, selections, operation.RewardDurability, out var recipe, out _) || recipe is null ||
            !WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(before)) ||
            !recipe.TryProject(before, operation.Allocation.First, operation.Allocation.Count, out var projected, out _) || projected is null ||
            !projected.CopyAfterImage().AsSpan().SequenceEqual(items.After))
            throw new InvalidDataException("relicDecomposeJournalItemAfterimageMismatch");
    }
}
