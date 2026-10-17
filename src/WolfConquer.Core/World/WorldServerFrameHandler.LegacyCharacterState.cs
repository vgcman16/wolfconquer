using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private static uint ReadCharacterDynamicId(CharacterSnapshot character)
    {
        return ReadMonsterAttributeUInt32(character.Attributes, "DynamicID", "DynamicId");
    }

    private static bool CanSeePlayer(CharacterSnapshot character, CharacterSnapshot other)
    {
        return character.MapId == other.MapId &&
            character.MapId != LegacyAlienWorldMapId &&
            ReadCharacterDynamicId(character) == ReadCharacterDynamicId(other) &&
            LegacyGetDistance(character.X, character.Y, other.X, other.Y) <= LegacyMonsterViewThreshold;
    }

    private static bool CanSeeFloorItem(CharacterSnapshot character, long mapId, ushort x, ushort y)
    {
        return character.MapId == mapId &&
            LegacyGetDistance(character.X, character.Y, x, y) <= LegacyMonsterViewThreshold;
    }

    private static bool IsLegacyPlayerBlackSpotExpired(CharacterSnapshot character, DateTimeOffset now)
    {
        return character.Attributes.TryGetValue(RuntimeBlackSpotAttribute, out var active) &&
            active == "1" &&
            character.Attributes.TryGetValue(RuntimeBlackSpotExpiresAtUtcAttribute, out var rawExpiresAt) &&
            DateTimeOffset.TryParse(
                rawExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var expiresAt) &&
            expiresAt <= now;
    }

    private static CharacterSnapshot WithoutLegacyPlayerBlackSpot(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        attributes.Remove(RuntimeBlackSpotAttribute);
        attributes.Remove(RuntimeBlackSpotExpiresAtUtcAttribute);

        return character with { Attributes = attributes };
    }

    private static long LegacyGetDistance(long x, long y, long x2, long y2)
    {
        return Math.Max(Math.Abs(x - x2), Math.Abs(y - y2));
    }

    private static uint BuildLegacyCharacterMesh(CharacterSnapshot character)
    {
        var transformation = 0L;
        if (character.Attributes.TryGetValue("TransformationID", out var rawTransformation) &&
            !long.TryParse(rawTransformation, NumberStyles.Integer, CultureInfo.InvariantCulture, out transformation))
        {
            transformation = 0;
        }

        var mesh = checked((transformation * 10_000_000) + (character.Face * 10_000) + character.Body);
        return CheckedUInt32(mesh, "Mesh");
    }
}
