namespace WolfConquer.Core.World;

public static class LegacyGuildWarJumpRules
{
    public const int GuildWarMapId = 1_038;

    private const int GateTerrainMask = 3;
    private const int RightGateCenterX = 217;
    private const int RightGateCenterY = 177;
    private const int RightGateCenterResetRadius = 3;
    private const int LeftGateX = 164;
    private const int LeftGateY = 209;
    private const int LeftGateJumpRadius = 20;
    private const int RightGateX = 222;
    private const int RightGateY = 177;
    private const int RightGateJumpRadius = 15;

    // Mirrors MrRayzo.GUILDWAR.ValidJump; callers pass the map-1038 DMap FloorType surface value.
    public static bool TryGetNextTerrainMask(
        int currentTerrainMask,
        ushort targetX,
        ushort targetY,
        int floorType,
        bool leftGateOpen,
        bool rightGateOpen,
        out int nextTerrainMask)
    {
        if (GetLegacyDistance(RightGateCenterX, RightGateCenterY, targetX, targetY) <= RightGateCenterResetRadius)
        {
            nextTerrainMask = 0;
            return true;
        }

        nextTerrainMask = currentTerrainMask;
        if (currentTerrainMask == GateTerrainMask && IsGateSeamFloor(floorType))
        {
            if (leftGateOpen && GetLegacyDistance(targetX, targetY, LeftGateX, LeftGateY) <= LeftGateJumpRadius)
            {
                nextTerrainMask = floorType;
                return true;
            }

            if (rightGateOpen && GetLegacyDistance(targetX, targetY, RightGateX, RightGateY) <= RightGateJumpRadius)
            {
                nextTerrainMask = floorType;
                return true;
            }

            return false;
        }

        nextTerrainMask = floorType;
        return true;
    }

    private static bool IsGateSeamFloor(int floorType)
    {
        return floorType is 0 or 9 or 13;
    }

    private static long GetLegacyDistance(long x, long y, long x2, long y2)
    {
        return Math.Max(Math.Abs(x - x2), Math.Abs(y - y2));
    }
}
