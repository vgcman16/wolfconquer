using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static CharacterSnapshot ApplyLegacyMonsterPlayerDeath(
        CharacterSnapshot character,
        LegacyMonsterInstance monster)
    {
        return LegacyPlayerDeathState.ApplyDeath(
                character,
                DateTimeOffset.UtcNow,
                CheckedUInt16(character.X, nameof(character.X)),
                CheckedUInt16(character.Y, nameof(character.Y)),
                monster.Uid,
                monster.Name)
            .Character;
    }

    private static bool CheckLegacyMonsterCounterPunchRateRoll(CharacterSnapshot character)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyMonsterCounterPunchRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < 80;
    }

    private static bool IsLegacyEagleEyeCountDownActive(CharacterSnapshot character)
    {
        return ReadAttributeBool(
            character.Attributes,
            RuntimeLegacyEagleEyeCountDownAttribute,
            "EagleEyeCountDown",
            "EagleEyeCountdown");
    }

    private static bool CheckLegacyEagleEyeResponseRateRoll(CharacterSnapshot character, byte rate)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyEagleEyeResponseRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < rate;
    }

    private static bool TryCalculateLegacyMonsterPushbackPosition(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        uint angle,
        int distance,
        out ushort x,
        out ushort y)
    {
        x = monster.X;
        y = monster.Y;
        if (distance <= 0)
        {
            return false;
        }

        var (deltaX, deltaY) = GetLegacyWalkDelta(angle % 8);
        if (deltaX == 0 && deltaY == 0)
        {
            return false;
        }

        for (var step = 0; step < distance; step++)
        {
            var nextX = x + deltaX;
            var nextY = y + deltaY;
            if (nextX is < 0 or > ushort.MaxValue ||
                nextY is < 0 or > ushort.MaxValue ||
                !TryValidateStaticTerrainLocation(context, monster.MapId, nextX, nextY, out _))
            {
                break;
            }

            x = checked((ushort)nextX);
            y = checked((ushort)nextY);
        }

        return true;
    }

    private static uint ReadLegacyMagicUInt32FieldOrDefault(
        WorldDataCatalog worldData,
        ushort spellId,
        ushort spellLevel,
        int fieldIndex)
    {
        return TryReadLegacyMagicUInt32Field(worldData, spellId, spellLevel, fieldIndex, out var value)
            ? value
            : 0;
    }

    private static bool TryReadLegacyMagicUInt32Field(
        WorldDataCatalog worldData,
        ushort spellId,
        ushort spellLevel,
        int fieldIndex,
        out uint value)
    {
        value = 0;
        if (!worldData.MagicByTypeId.TryGetValue(spellId, out var rows))
        {
            return false;
        }

        MagicType? fallback = null;
        foreach (var row in rows)
        {
            if (TryReadByte(row.Fields, 8, out var rowLevel) && rowLevel == spellLevel)
            {
                return fieldIndex >= 0 &&
                    fieldIndex < row.Fields.Count &&
                    uint.TryParse(row.Fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            fallback ??= row;
        }

        return fallback is not null &&
            fieldIndex >= 0 &&
            fieldIndex < fallback.Fields.Count &&
            uint.TryParse(fallback.Fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadLegacyMagicByteField(
        WorldDataCatalog worldData,
        ushort spellId,
        ushort spellLevel,
        int fieldIndex,
        out byte value)
    {
        value = 0;
        if (!worldData.MagicByTypeId.TryGetValue(spellId, out var rows))
        {
            return false;
        }

        MagicType? fallback = null;
        foreach (var row in rows)
        {
            if (TryReadByte(row.Fields, 8, out var rowLevel) && rowLevel == spellLevel)
            {
                return TryReadByte(row.Fields, fieldIndex, out value);
            }

            fallback ??= row;
        }

        return fallback is not null && TryReadByte(fallback.Fields, fieldIndex, out value);
    }

    private static bool TryReadByte(IReadOnlyList<string> fields, int index, out byte value)
    {
        value = 0;
        return index >= 0 &&
            index < fields.Count &&
            byte.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
