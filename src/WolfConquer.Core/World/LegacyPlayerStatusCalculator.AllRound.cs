using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class LegacyPlayerStatusCalculator
{
    private static void ApplyAllRoundRuneAttributes(uint[] fields, CharacterSnapshot character,
        PlayerItemSnapshot items, WorldDataCatalog? catalog, bool alternate,
        PlayerSpellSnapshot? spells, ConquerProtocolProfile? profile)
    {
        // This is a pure projection. The real caller obtains the profile from
        // its admitted item binding; the existing Store/native proof publishes.
        if (profile != ConquerProtocolProfile.VisibleDx8D13 || character.MinHitPoints <= 0 ||
            character.Reborn < 2 || character.Level < 15) return;

        var selected = SelectAllRoundRune(items, alternate);
        if (selected is null) return;
        RequireAllRoundMetadata(catalog, selected.ItemId);
        if (selected.StackSize > 1)
            throw new InvalidDataException("All-round requires one physical nonstackable Rune.");
        if (!WorldPacketHandlers.PassesOwnedRuneCastRequirements(character, 2000, 0)) return;
        var left = LegacyEquipmentQuerySelector.SelectLeftWeapon(character, items, alternate, spells);
        if (left is null || left.Durability == 0 || left.ItemId is < 900_000 or > 900_309) return;
        var bonus = LegacyAllRoundRuneBonus.ForItem(selected.ItemId);
        Add(fields, LegacyPlayerStatusPacket.MinAttackIndex, bonus.Attack);
        Add(fields, LegacyPlayerStatusPacket.MaxAttackIndex, bonus.Attack);
        Add(fields, LegacyPlayerStatusPacket.DefenceIndex, bonus.Defence);
        Add(fields, LegacyPlayerStatusPacket.MagicDefenceIndex, bonus.MagicDefence);
    }

    private static PlayerItemSnapshotItem? SelectAllRoundRune(PlayerItemSnapshot items, bool alternate)
    {
        // Native Rune.WornObjects uses +20 alternate slots, independently of
        // ordinary equipment's +50 policy. The first source row wins.
        var positions = items.RuneItems.Select(item => item.Position).ToHashSet();
        var activePositions = new HashSet<ushort>();
        foreach (var item in items.RuneItems)
            if (item.Position is not (0 or 211 or 212) && !activePositions.Add(item.Position))
                throw new InvalidDataException("All-round requires an unambiguous complete equipped Rune owner.");
        PlayerItemSnapshotItem? selected = null;
        foreach (var item in items.RuneItems)
        {
            if (item.ItemId is < 4_020_201 or > 4_020_227 || item.Position is 0 or 211 or 212 ||
                (!alternate && item.Position > 120) ||
                (alternate && item.Position < 120 && positions.Contains(checked((ushort)(item.Position + 20))))) continue;
            if (item.Position is not (102 or 122))
                throw new InvalidDataException("All-round active Rune position is ambiguous or invalid.");
            selected ??= item;
        }
        return selected;
    }

    private static void RequireAllRoundMetadata(WorldDataCatalog? catalog, uint itemId)
    {
        if (catalog is null || !catalog.ItemsById.TryGetValue(checked((int)itemId), out var indexed) ||
            indexed.Count != 1 || indexed[0] is not { } row || row.Id != itemId ||
            row.Fields.Count <= ItemType.StackSizeFieldIndex)
            throw new InvalidDataException("All-round requires unique adopted item metadata.");
        // Actual publication's current native binding already pins the unique
        // raw/index Rune row and complete raw-list identity. Do not rescan the
        // whole catalog for every combat status projection.
        uint Read(int index) => uint.TryParse(row.Fields[index], NumberStyles.None,
            CultureInfo.InvariantCulture, out var value) ? value :
            throw new InvalidDataException("All-round item metadata contains an invalid numeric field.");
        if (Read(0) != itemId || Read(ItemType.RequiredClassFieldIndex) != 2000 ||
            Read(ItemType.RequiredLevelFieldIndex) != 0 || Read(ItemType.RuneSpellIdFieldIndex) != itemId ||
            Read(ItemType.StackSizeFieldIndex) != 0)
            throw new InvalidDataException("All-round item metadata disagrees with the selected7899 passive.");
    }
}
