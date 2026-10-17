using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ulong ReadRacePoints(CharacterSnapshot character)
    {
        return ReadAttributeUInt64(character.Attributes, "RacePoints");
    }

    private static bool TryApplyNewGuildWarItemPickups(
        CharacterSnapshot character,
        out CharacterSnapshot updatedCharacter,
        out IReadOnlyList<NewGuildWarItemPickupResult> pickups)
    {
        updatedCharacter = character;
        pickups = [];

        if (character.MapId != LegacyNewGuildWarMapId ||
            ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId") == 0)
        {
            return false;
        }

        var items = ReadNewGuildWarItems(character);
        if (items.Count == 0)
        {
            return false;
        }

        var remaining = new List<NewGuildWarItemProjection>(items.Count);
        var picked = new List<NewGuildWarItemPickupResult>();
        var totalAwarded = 0UL;
        var currentPoints = ReadAttributeUInt64(
            character.Attributes,
            "GuildWarPoints",
            "NewGuildWarPoints",
            "NewGuildWarUserPoints");
        foreach (var item in items)
        {
            if (!item.Viable ||
                LegacyGetDistance(character.X, character.Y, item.X, item.Y) > 1 ||
                !TryGetNewGuildWarItemPoints(item.ItemId, out var points))
            {
                remaining.Add(item);
                continue;
            }

            totalAwarded += points;
            var totalPoints = currentPoints + totalAwarded;
            picked.Add(new NewGuildWarItemPickupResult(
                item.Uid,
                item.X,
                item.Y,
                item.ItemId,
                points,
                totalPoints,
                item.Name.Length == 0 ? FormatNewGuildWarItemName(item.ItemId) : item.Name));
        }

        if (picked.Count == 0)
        {
            return false;
        }

        updatedCharacter = WithCharacterAttribute(character, "GuildWarPoints", currentPoints + totalAwarded);
        updatedCharacter = WithNewGuildWarItemsAttribute(updatedCharacter, remaining);
        pickups = picked;
        return true;
    }

    private static bool TryApplySteedRaceItemPickup(
        CharacterSnapshot character,
        out CharacterSnapshot updatedCharacter,
        out RaceItemPickupResult? pickup)
    {
        updatedCharacter = character;
        pickup = null;

        if (!IsSteedRaceMap(character.MapId))
        {
            return false;
        }

        var roles = ReadRaceStaticRoles(character);
        if (roles.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < roles.Count; index++)
        {
            var role = roles[index];
            if (!role.Viable ||
                LegacyGetDistance(character.X, character.Y, role.X, role.Y) > 1 ||
                !TryGetRaceItemType(role.Mesh, out var type))
            {
                continue;
            }

            if (type == LegacyRaceItemType.FrozenTrap)
            {
                if (role.SetBy == character.Uid)
                {
                    continue;
                }

                var remainingTrapRoles = roles
                    .Where((_, roleIndex) => roleIndex != index)
                    .ToArray();
                var trapRaceStaticRoles = WriteRaceStaticRoles(remainingTrapRoles);
                updatedCharacter = WithLegacySourceAddFlag(character, LegacyFreezeFlag, removeOnDead: true);
                updatedCharacter = trapRaceStaticRoles.Length == 0
                    ? WithoutCharacterAttribute(updatedCharacter, "RaceStaticRoles")
                    : WithCharacterAttribute(updatedCharacter, "RaceStaticRoles", trapRaceStaticRoles);
                pickup = new RaceItemPickupResult(
                    role.Uid,
                    type,
                    Potion: null,
                    LegacyGameUpdatePacket.Freeze(
                        CheckedUInt32(character.Uid, nameof(character.Uid)),
                        LegacySteedRaceFrozenTrapSeconds));
                return true;
            }

            var potions = ReadRacePotionSlots(character);
            if (!TryAddRacePotionSlot(potions, type, role.Level, out var potion))
            {
                return false;
            }

            var remainingRoles = roles
                .Where((_, roleIndex) => roleIndex != index)
                .ToArray();
            updatedCharacter = WithCharacterAttribute(character, "RacePotions", WriteRacePotionSlots(potions));
            var raceStaticRoles = WriteRaceStaticRoles(remainingRoles);
            updatedCharacter = raceStaticRoles.Length == 0
                ? WithoutCharacterAttribute(updatedCharacter, "RaceStaticRoles")
                : WithCharacterAttribute(updatedCharacter, "RaceStaticRoles", raceStaticRoles);

            pickup = new RaceItemPickupResult(role.Uid, type, potion, GameUpdate: null);
            return true;
        }

        return false;
    }

    private static bool IsSteedRaceMap(long mapId)
    {
        return mapId is 1950 or 2061 or 2062 or 2063 or 2064;
    }

    private static bool TryAddRacePotionSlot(
        List<RacePotionSlot> potions,
        LegacyRaceItemType type,
        ushort amount,
        out LegacyRacePotionPacket packet)
    {
        packet = default;

        for (var index = 0; index < potions.Count; index++)
        {
            var slot = potions[index];
            if (slot.Type != type)
            {
                continue;
            }

            var updatedAmount = slot.Count + amount;
            if (updatedAmount > ushort.MaxValue)
            {
                return false;
            }

            var updated = slot with { Count = (ushort)updatedAmount };
            potions[index] = updated;
            packet = new LegacyRacePotionPacket(updated.Count, updated.Type, updated.Location, DwParam: 0);
            return true;
        }

        for (var location = 1; location <= 5; location++)
        {
            if (potions.Any(slot => slot.Location == location))
            {
                continue;
            }

            var slot = new RacePotionSlot(location, type, amount);
            potions.Add(slot);
            packet = new LegacyRacePotionPacket(slot.Count, slot.Type, slot.Location, DwParam: 0);
            return true;
        }

        return false;
    }

    private static List<NewGuildWarItemProjection> ReadNewGuildWarItems(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, RuntimeNewGuildWarItemsAttribute, "GuildWarItems");
        if (raw.Length == 0)
        {
            return [];
        }

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var items = new List<NewGuildWarItemProjection>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(
                [',', '|'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 4 ||
                !uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) ||
                !ushort.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !ushort.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                !uint.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId))
            {
                continue;
            }

            var viable = fields.Length < 5 || TryParseLegacyBool(fields[4], out var parsedViable) && parsedViable;
            var name = fields.Length > 5 ? fields[5] : FormatNewGuildWarItemName(itemId);
            items.Add(new NewGuildWarItemProjection(uid, x, y, itemId, viable, name));
        }

        return items;
    }

    private static List<RaceStaticRoleProjection> ReadRaceStaticRoles(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, "RaceStaticRoles", "SteedRaceItems");
        if (raw.Length == 0)
        {
            return [];
        }

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var roles = new List<RaceStaticRoleProjection>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(
                [',', '|'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 7 ||
                !uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) ||
                !ushort.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !ushort.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                !uint.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mesh) ||
                !ushort.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ||
                !uint.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var setBy) ||
                !TryParseLegacyBool(fields[6], out var viable))
            {
                continue;
            }

            roles.Add(new RaceStaticRoleProjection(uid, x, y, mesh, level, setBy, viable));
        }

        return roles;
    }

    private static List<RacePotionSlot> ReadRacePotionSlots(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, "RacePotions", "SteedRacePotions");
        if (raw.Length == 0)
        {
            return [];
        }

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var slots = new List<RacePotionSlot>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(
                [',', '|'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 3 ||
                !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var location) ||
                location is < 1 or > 5 ||
                !ushort.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var typeValue) ||
                !Enum.IsDefined(typeof(LegacyRaceItemType), typeValue) ||
                !ushort.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                continue;
            }

            slots.Add(new RacePotionSlot(location, (LegacyRaceItemType)typeValue, count));
        }

        return slots
            .GroupBy(slot => slot.Location)
            .Select(group => group.First())
            .OrderBy(slot => slot.Location)
            .ToList();
    }

    private static string WriteRaceStaticRoles(IReadOnlyList<RaceStaticRoleProjection> roles)
    {
        return string.Join(
            ';',
            roles.Select(role => string.Join(
                ',',
                role.Uid.ToString(CultureInfo.InvariantCulture),
                role.X.ToString(CultureInfo.InvariantCulture),
                role.Y.ToString(CultureInfo.InvariantCulture),
                role.Mesh.ToString(CultureInfo.InvariantCulture),
                role.Level.ToString(CultureInfo.InvariantCulture),
                role.SetBy.ToString(CultureInfo.InvariantCulture),
                role.Viable ? "1" : "0")));
    }

    private static CharacterSnapshot WithNewGuildWarItemsAttribute(
        CharacterSnapshot character,
        IReadOnlyList<NewGuildWarItemProjection> items)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(RuntimeNewGuildWarItemsAttribute);
        attributes.Remove("GuildWarItems");
        if (items.Count > 0)
        {
            attributes[RuntimeNewGuildWarItemsAttribute] = WriteNewGuildWarItems(items);
        }

        return character with { Attributes = attributes };
    }

    private static string WriteNewGuildWarItems(IReadOnlyList<NewGuildWarItemProjection> items)
    {
        return string.Join(
            ';',
            items.Select(item => string.Join(
                ',',
                item.Uid.ToString(CultureInfo.InvariantCulture),
                item.X.ToString(CultureInfo.InvariantCulture),
                item.Y.ToString(CultureInfo.InvariantCulture),
                item.ItemId.ToString(CultureInfo.InvariantCulture),
                item.Viable ? "1" : "0",
                item.Name)));
    }

    private static string WriteRacePotionSlots(IReadOnlyList<RacePotionSlot> slots)
    {
        return string.Join(
            ';',
            slots
                .OrderBy(slot => slot.Location)
                .Select(slot => string.Join(
                    ',',
                    slot.Location.ToString(CultureInfo.InvariantCulture),
                    ((ushort)slot.Type).ToString(CultureInfo.InvariantCulture),
                    slot.Count.ToString(CultureInfo.InvariantCulture))));
    }

    private static bool TryGetNewGuildWarItemPoints(uint itemId, out uint points)
    {
        points = itemId switch
        {
            2_619 => 10,
            2_620 => 5,
            2_621 => 3,
            2_622 => 1,
            _ => 0,
        };

        return points != 0;
    }

    private static string FormatNewGuildWarItemName(uint itemId)
    {
        return "STR_TRAP_ID_" + itemId.ToString(CultureInfo.InvariantCulture) + "@@";
    }

    private static bool TryGetRaceItemType(uint mesh, out LegacyRaceItemType type)
    {
        type = mesh switch
        {
            760 => LegacyRaceItemType.GuardPotion,
            761 => LegacyRaceItemType.FrozenTrap,
            762 => LegacyRaceItemType.SluggishPotion,
            763 => LegacyRaceItemType.DizzyHammer,
            764 => LegacyRaceItemType.RestorePotion,
            765 => LegacyRaceItemType.ScreamBomb,
            766 => LegacyRaceItemType.TransformItem,
            768 => LegacyRaceItemType.SpiritPotion,
            769 => LegacyRaceItemType.ExcitementPotion,
            770 => LegacyRaceItemType.ChaosBomb,
            _ => LegacyRaceItemType.Null,
        };

        return type != LegacyRaceItemType.Null;
    }
}
