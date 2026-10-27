using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyOrdinaryAdmissionValidation
{
    internal static void ValidateStartupCharacter(CharacterSnapshot value)
    {
        ValidateCharacter(value);
        // Fresh ordinary startup emits LoaderHeroInfo.Level as a byte. Retained
        // journal validation and the non-emitting native prefix keep their wider domain.
        if (value.Level > byte.MaxValue)
            throw new InvalidDataException("Ordinary startup LoaderHeroInfo Level exceeds its byte field.");
    }

    internal static void ValidateCharacter(CharacterSnapshot value)
    {
        if (value.Uid is <= 0 or > uint.MaxValue || value.Class is < 0 or > uint.MaxValue ||
            value.FirstClass is < 0 or > uint.MaxValue || value.SecondClass is < 0 or > uint.MaxValue ||
            value.Reborn is < 0 or > byte.MaxValue || value.Level is < 1 or > ushort.MaxValue || value.Experience < 0 ||
            value.Money < 0 || value.ConquerPoints < 0 || value.BoundConquerPoints < 0 || value.BankConquerPoints < 0 ||
            value.PkPoints is < 0 or > ushort.MaxValue || value.Strength is < 0 or > ushort.MaxValue ||
            value.Agility is < 0 or > ushort.MaxValue || value.Vitality is < 0 or > ushort.MaxValue ||
            value.Spirit is < 0 or > ushort.MaxValue || value.AttributePoints is < 0 or > ushort.MaxValue ||
            value.MinHitPoints is < 0 or > uint.MaxValue || value.MinMana is < 0 or > uint.MaxValue ||
            value.MaxMana is < 0 or > uint.MaxValue)
            throw new InvalidDataException("Character state exceeds required startup numeric bounds.");
    }

    internal static void ValidateStarterReplacement(PlayerItemSnapshot saved, byte[]? raw)
    {
        if (raw is not null && !PlayerItemSnapshotLoader.ToLegacyBytes(saved).AsSpan().SequenceEqual(raw))
            throw new InvalidDataException("Starter replacement would discard unmodeled saved item state.");
        foreach (var row in saved.InventoryItems.Concat(saved.EquipmentItems))
        {
            if (row.StackSize > 1 || row.Position != 0 && (row.Durability == 0 || row.Durability != row.MaximumDurability))
                throw new InvalidDataException("Starter replacement would consume stacked or damaged owned items.");
            var neutral = new PlayerItemSnapshotItem { Uid = row.Uid, ItemId = row.ItemId, Position = row.Position,
                Durability = row.Durability, MaximumDurability = row.MaximumDurability, Color = row.Color, StackSize = row.StackSize };
            if (!PlayerItemSnapshotLoader.ToLegacyBytes(new([row], [], [], [], [])).AsSpan()
                    .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(new([neutral], [], [], [], []))))
                throw new InvalidDataException("Starter replacement would discard enhanced, locked or otherwise nonneutral owned state.");
        }
    }
}
