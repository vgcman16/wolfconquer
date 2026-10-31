using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyAuthenticatedOrdinaryCreationRecipe
{
    internal PlayerItemSnapshot Materialize(LegacyItemUidReservationReceipt receipt)
    {
        var request = CreateRequest();
        if (receipt.DomainId == Guid.Empty || receipt.OperationId != request.OperationId || receipt.Writer != request.Writer ||
            receipt.AccountId != Account || receipt.CharacterUid != Account || receipt.PayloadSha256 != request.PayloadSha256 ||
            receipt.Count != request.Count || receipt.First == 0 || receipt.Count is < 1 or > 4 ||
            (ulong)receipt.First + (uint)receipt.Count - 1 > uint.MaxValue)
            throw new InvalidDataException("Authenticated creation range does not match its exact recipe.");
        var templates = Projection.Items;
        var items = templates.Select((row, index) =>
        {
            var uid = checked(receipt.First + (uint)index);
            return new PlayerItemSnapshotItem { Uid = uid, ItemId = row.ItemId, Position = row.Position,
                Durability = row.Durability, MaximumDurability = row.MaximumDurability,
                Color = 3U + unchecked(uid + row.ItemId) % 6U };
        }).ToArray();
        return new([], [], [], items, []);
    }

    internal Dictionary<LegacyTradeJournalFileKind, byte[]?> AfterImages(LegacyItemUidReservationReceipt receipt) => new()
    {
        [LegacyTradeJournalFileKind.Character] = Projection.CopyCharacterImage(),
        [LegacyTradeJournalFileKind.Items] = PlayerItemSnapshotLoader.ToLegacyBytes(Materialize(receipt)),
        [LegacyTradeJournalFileKind.Spells] = Projection.CopySpellsImage(),
        [LegacyTradeJournalFileKind.Proficiencies] = Projection.CopyProficienciesImage(),
        [LegacyTradeJournalFileKind.Quests] = Projection.CopyQuestsImage(),
        [LegacyTradeJournalFileKind.CombatGear] = Projection.CopyCombatGearImage(),
        [LegacyTradeJournalFileKind.Mailbox] = null,
    };

    internal bool IsBefore(IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> images) =>
        images.Count == TargetKinds.Count + 1 && TargetKinds.Append(LegacyTradeJournalFileKind.Mailbox)
            .All(kind => images.TryGetValue(kind, out var image) && image is null);

    internal bool IsAfter(IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> images,
        LegacyItemUidReservationReceipt receipt)
    {
        var after = AfterImages(receipt);
        return images.Count == after.Count && after.All(pair => images.TryGetValue(pair.Key, out var actual) &&
            (actual is null ? pair.Value is null : pair.Value is not null && actual.AsSpan().SequenceEqual(pair.Value)));
    }
}
