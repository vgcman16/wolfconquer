using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static IReadOnlyList<LegacyHairFaceStorageItem> ReadLegacyHairFaceStorageItems(
            WorldDataCatalog? worldData,
            CharacterSnapshot character)
    {
        if (worldData is null ||
            !character.Attributes.TryGetValue("HairfaceStorage", out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var fields = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0 ||
            !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ||
            count < 0)
        {
            return [];
        }

        var items = new List<LegacyHairFaceStorageItem>(count);
        var offset = 1;
        for (var index = 0; index < count; index++)
        {
            if (offset + 3 > fields.Length ||
                !uint.TryParse(fields[offset++], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
                !byte.TryParse(fields[offset++], NumberStyles.Integer, CultureInfo.InvariantCulture, out var equippedColor) ||
                !int.TryParse(fields[offset++], NumberStyles.Integer, CultureInfo.InvariantCulture, out var colorCount) ||
                colorCount < 0 ||
                offset + colorCount > fields.Length)
            {
                break;
            }

            var colors = new byte[colorCount];
            for (var colorIndex = 0; colorIndex < colorCount; colorIndex++)
            {
                if (byte.TryParse(fields[offset++], NumberStyles.Integer, CultureInfo.InvariantCulture, out var color))
                {
                    colors[colorIndex] = color;
                }
            }

            if (!TryFindLegacyHairFaceStorageTypeById(worldData, id, out var definition))
            {
                continue;
            }

            var type = ToLegacyHairFaceStorageType(definition.CategoryType);
            items.Add(new LegacyHairFaceStorageItem(
                type,
                id,
                IsLegacyHairFaceEquipped(character, type, id),
                Unknown4: 0,
                [equippedColor, CreateLegacyHairFaceColorMask(colors)]));
        }

        return items
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    private static CharacterSnapshot WithLegacyHairFaceStorageItems(
            CharacterSnapshot character,
            IReadOnlyList<LegacyHairFaceStorageItem> items)
    {
        var parts = new List<string>(capacity: 1 + items.Count * 10)
        {
            items.Count.ToString(CultureInfo.InvariantCulture),
        };

        foreach (var item in items)
        {
            var equippedColor = item.Colors.Count > 0 ? checked((byte)item.Colors[0]) : (byte)0;
            var colorMask = item.Colors.Count > 1 ? item.Colors[1] : 1;
            var colors = ExpandLegacyHairFaceColorMask(colorMask, minLength: 7);
            parts.Add(item.Id.ToString(CultureInfo.InvariantCulture));
            parts.Add(equippedColor.ToString(CultureInfo.InvariantCulture));
            parts.Add(colors.Length.ToString(CultureInfo.InvariantCulture));
            foreach (var color in colors)
            {
                parts.Add(color.ToString(CultureInfo.InvariantCulture));
            }
        }

        return WithCharacterAttribute(character, "HairfaceStorage", string.Join('/', parts) + "/");
    }

    private static LegacyHairFaceStorageItem CreateLegacyHairFaceStorageItem(HairFaceStorageType definition)
    {
        return new LegacyHairFaceStorageItem(
            ToLegacyHairFaceStorageType(definition.CategoryType),
            checked((uint)definition.Id),
            Equipped: false,
            Unknown4: 0,
            [0, 1]);
    }

    private static LegacyHairFaceStoragePacket CreateLegacyHairFaceColorResponse(
            LegacyHairFaceStorageAction action,
            uint id,
            byte colorIndex)
    {
        return new LegacyHairFaceStoragePacket(
            action,
            Hair: null,
            HairColor: new LegacyHairFaceStorageOperation(id, colorIndex),
            AvatarsOrHairTap: [],
            Items: []);
    }

    private static bool TryReadLegacyHairFaceOperation(
            LegacyHairFaceStorageOperation? operation,
            out LegacyHairFaceStorageType type,
            out uint id)
    {
        type = default;
        id = 0;
        if (operation is not { } value ||
            value.Id > byte.MaxValue)
        {
            return false;
        }

        type = (LegacyHairFaceStorageType)(byte)value.Id;
        id = value.DwParam;
        return type is LegacyHairFaceStorageType.Hairstyle or LegacyHairFaceStorageType.Avatar;
    }

    private static bool TryReadLegacyHairFaceColorOperation(
            LegacyHairFaceStorageOperation? operation,
            out uint id,
            out byte colorIndex)
    {
        id = 0;
        colorIndex = 0;
        if (operation is not { } value ||
            value.DwParam > byte.MaxValue)
        {
            return false;
        }

        id = value.Id;
        colorIndex = (byte)value.DwParam;
        return true;
    }

    private static bool TryFindLegacyHairFaceStorageType(
            WorldDataCatalog worldData,
            LegacyHairFaceStorageType type,
            uint id,
            out HairFaceStorageType definition)
    {
        definition = default!;
        if (id > int.MaxValue ||
            !worldData.HairFaceStorageTypeByCategoryAndId.TryGetValue(((int)type, (int)id), out definition!))
        {
            return false;
        }

        return true;
    }

    private static bool TryFindLegacyHairColorType(
            WorldDataCatalog worldData,
            uint id,
            byte colorIndex,
            out HairColorType definition)
    {
        definition = default!;
        if (id > int.MaxValue ||
            !worldData.HairColorTypeByHairColorIdAndColorIndex.TryGetValue(((int)id, colorIndex), out definition!))
        {
            return false;
        }

        return true;
    }

    private static bool TryFindLegacyHairFaceStorageTypeById(
            WorldDataCatalog worldData,
            uint id,
            out HairFaceStorageType definition)
    {
        definition = default!;
        if (id > int.MaxValue ||
            !worldData.HairFaceStorageTypesById.TryGetValue((int)id, out var definitions) ||
            definitions.Count == 0)
        {
            return false;
        }

        definition = definitions[0];
        return true;
    }

    private static bool CanEquipLegacyHairFaceStorageItem(
            CharacterSnapshot character,
            HairFaceStorageType definition)
    {
        var sex = definition.Sort;
        if (sex == 1 && !IsLegacyBoyBody(character.Body) ||
            sex == 2 && !IsLegacyGirlBody(character.Body))
        {
            return false;
        }

        var vipLevel = ReadAttributeUInt64(character.Attributes, "VIPLevel", "VipLevel");
        if (definition.RequiredValue > 0 && vipLevel < (ulong)definition.RequiredValue)
        {
            return false;
        }

        var classes = ParseLegacyHairFaceClassList(definition.LinkedColorIdsRaw);
        return classes.Count == 0 || classes.Contains((byte)(character.Class / 1000));
    }

    private static IReadOnlyList<LegacyHairFaceStorageItem> ReplaceLegacyHairFaceStorageItem(
            IReadOnlyList<LegacyHairFaceStorageItem> items,
            LegacyHairFaceStorageItem updatedItem)
    {
        return items
            .Select(item => item.Type == updatedItem.Type && item.Id == updatedItem.Id ? updatedItem : item)
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    private static LegacyHairFaceStorageItem SetLegacyHairFaceColorFlag(
            LegacyHairFaceStorageItem item,
            byte colorIndex)
    {
        var colors = ExpandLegacyHairFaceColorMask(
            item.Colors.Count > 1 ? item.Colors[1] : 1,
            Math.Max(7, colorIndex + 1));
        colors[colorIndex] = 1;
        return item with
        {
            Colors = [GetLegacyHairFaceEquippedColor(item), CreateLegacyHairFaceColorMask(colors)],
        };
    }

    private static LegacyHairFaceStorageItem SetLegacyHairFaceEquippedColor(
            LegacyHairFaceStorageItem item,
            byte colorIndex)
    {
        return item with
        {
            Colors = [colorIndex, item.Colors.Count > 1 ? item.Colors[1] : 1],
        };
    }

    private static bool HasLegacyHairFaceColor(LegacyHairFaceStorageItem item, byte colorIndex)
    {
        var colors = ExpandLegacyHairFaceColorMask(
            item.Colors.Count > 1 ? item.Colors[1] : 1,
            Math.Max(7, colorIndex + 1));
        return colors[colorIndex] != 0;
    }

    private static byte GetLegacyHairFaceEquippedColor(LegacyHairFaceStorageItem item)
    {
        if (item.Colors.Count == 0)
        {
            return 0;
        }

        var raw = item.Colors[0];
        return raw is >= byte.MinValue and <= byte.MaxValue ? (byte)raw : (byte)0;
    }

    private static long CreateLegacyHairValue(uint id, byte colorIndex)
    {
        return checked((colorIndex * 1000L) + id);
    }

    private static IReadOnlyList<byte> ParseLegacyHairFaceClassList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var classes = new List<byte>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (byte.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var classId))
            {
                classes.Add(classId);
            }
        }

        return classes;
    }

    private static LegacyHairFaceStorageType ToLegacyHairFaceStorageType(int categoryType)
    {
        return categoryType == (int)LegacyHairFaceStorageType.Avatar
            ? LegacyHairFaceStorageType.Avatar
            : LegacyHairFaceStorageType.Hairstyle;
    }

    private static bool IsLegacyHairFaceEquipped(CharacterSnapshot character, LegacyHairFaceStorageType type, uint id)
    {
        return type == LegacyHairFaceStorageType.Hairstyle
            ? (ulong)character.Hair % 1000UL == id
            : character.Face == id;
    }

    private static long CreateLegacyHairFaceColorMask(IReadOnlyList<byte> colors)
    {
        long mask = 0;
        for (var index = 0; index < colors.Count && index < 62; index++)
        {
            if (colors[index] != 0)
            {
                mask |= 1L << index;
            }
        }

        return mask;
    }

    private static byte[] ExpandLegacyHairFaceColorMask(long colorMask, int minLength)
    {
        var colors = new byte[Math.Max(minLength, 1)];
        for (var index = 0; index < colors.Length; index++)
        {
            colors[index] = (colorMask & (1L << index)) != 0 ? (byte)1 : (byte)0;
        }

        return colors;
    }
}
