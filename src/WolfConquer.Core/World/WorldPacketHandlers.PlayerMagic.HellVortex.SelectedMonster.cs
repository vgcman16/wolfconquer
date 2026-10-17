namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryRefreshLegacyHellVortexMonsterRecipient(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint selectedUid,
        LegacyPlayerMonsterMagicTarget candidate,
        out LegacyPlayerMonsterMagicTarget target)
    {
        target = default;
        var selected = FindLegacyHellVortexSelectedMonster(context, session, selectedUid);
        if (selected is null ||
            context.MonsterStates is not { } states ||
            !states.TryGetVisibleInstance(candidate.Monster, DateTimeOffset.UtcNow, out var current) ||
            !CanSeeMonster(session.Character, current) ||
            LegacyGetDistance(selected.X, selected.Y, current.X, current.Y) > LegacyHellVortexFanoutRadius)
        {
            return false;
        }

        // Source resolves the raw center inside each recipient's receive turn.
        // A normal dead center remains valid; a RemoveOnDead role has left the map.
        target = candidate with { Monster = current };
        return true;
    }

    private static LegacyMonsterInstance? FindLegacyHellVortexSelectedMonster(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint opponentUid)
    {
        if (session.Character.MapId is < int.MinValue or > int.MaxValue)
        {
            return null;
        }

        var mapId = (int)session.Character.MapId;
        IEnumerable<LegacyMonsterInstance> staticInstances = [];
        if (context.WorldData is { } worldData &&
            worldData.MonsterSpawnsByMapId.TryGetValue(mapId, out var spawns))
        {
            staticInstances = LegacyStaticMonsterMaterializer.Enumerate(worldData, spawns)
                .Select(static entry => entry.Instance);
        }

        var selected = context.MonsterStates is { } monsterStates
            ? monsterStates.FindMapRoleMonsterByUid(mapId, opponentUid, staticInstances)
            : staticInstances.FirstOrDefault(instance => instance.Uid == opponentUid);

        // RoleView.TryGetValue -> MapView.TryGetObject does not apply CanSee or alive filters.
        return selected is not null &&
            Math.Abs(selected.X / 18 - session.Character.X / 18) <= 1 &&
            Math.Abs(selected.Y / 18 - session.Character.Y / 18) <= 1
            ? selected
            : null;
    }
}
