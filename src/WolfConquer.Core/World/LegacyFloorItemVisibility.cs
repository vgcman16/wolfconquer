using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Current managed-character projection only, NOT native physical Player.DynamicID
// adoption. Native Player's plain uint property is zero at birth; malformed stored
// values are not that default. Birth captures floor scope independently of viewers.
internal static class LegacyFloorItemVisibility
{
    internal static bool TryReadDynamicId(CharacterSnapshot character, out uint dynamicId)
    {
        ArgumentNullException.ThrowIfNull(character);
        dynamicId = 0;
        var found = false;
        foreach (var attribute in character.Attributes)
        {
            if (!string.Equals(attribute.Key, "DynamicID", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!uint.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
                found && dynamicId != value)
            {
                dynamicId = 0;
                return false;
            }

            dynamicId = value;
            found = true;
        }

        return true;
    }

    // MsgItem.CanSee, used by its initial and Remove SendAll broadcasts.
    internal static bool CanBroadcast(CharacterSnapshot character, FloorItemRuntimeEntry entry)
    {
        return MatchesScope(character, entry) &&
            character.X > (long)entry.Packet.X - 18 && character.X < (long)entry.Packet.X + 18 &&
            character.Y > (long)entry.Packet.Y - 18 && character.Y < (long)entry.Packet.Y + 18;
    }

    // RoleView.CanSee eligibility only. Its CanAdd/Player.InView previous-position
    // edge, Force, SpawinEffect and map-4006 rules remain owned by the refresh caller.
    internal static bool CanRefresh(CharacterSnapshot character, FloorItemRuntimeEntry entry)
    {
        return MatchesScope(character, entry) && entry.Packet.Uid != character.Uid &&
            character.X >= (long)entry.Packet.X - 18 && character.X <= (long)entry.Packet.X + 18 &&
            character.Y >= (long)entry.Packet.Y - 18 && character.Y <= (long)entry.Packet.Y + 18;
    }

    private static bool MatchesScope(CharacterSnapshot character, FloorItemRuntimeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(entry);
        return entry.DynamicId is { } floorDynamicId && character.MapId == entry.MapId &&
            TryReadDynamicId(character, out var viewerDynamicId) && viewerDynamicId == floorDynamicId;
    }
}
