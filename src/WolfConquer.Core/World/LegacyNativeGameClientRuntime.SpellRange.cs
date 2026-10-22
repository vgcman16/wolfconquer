namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Complete native GameClient828..843, retaining first-found category priority.
    public bool IsInSpellRange(uint UID, byte range)
    {
        if (Player.View.TryGetValue(UID, out var obj, LegacyNativeMapObjectType.Monster))
        {
            return LegacyNativeRoleCore.GetDistance(Player.X, Player.Y, obj.X, obj.Y) <= range;
        }
        if (Player.View.TryGetValue(UID, out obj, LegacyNativeMapObjectType.Player))
        {
            return LegacyNativeRoleCore.GetDistance(Player.X, Player.Y, obj.X, obj.Y) <= range;
        }
        if (Player.View.TryGetValue(UID, out obj, LegacyNativeMapObjectType.SobNpc))
        {
            return LegacyNativeRoleCore.GetDistance(Player.X, Player.Y, obj.X, obj.Y) <= range;
        }
        return false;
    }
}
