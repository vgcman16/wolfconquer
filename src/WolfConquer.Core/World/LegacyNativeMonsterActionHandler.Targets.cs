namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterActionHandler
{
    public void CheckTarget(LegacyNativePlayerRuntime client, LegacyNativeMonsterRole? monster)
    {
        if (monster == null || !monster.Alive) return;
        if (client is null)
        {
            InvalidateUnavailableTarget(monster);
            return;
        }
        var selected = monster.Target;
        var state = monster.State;
        try
        {
            ResolveTarget(client, monster, ref selected, ref state);
            monster.Target = selected;
            monster.State = state;
        }
        catch (InvalidDataException)
        {
            InvalidateUnavailableTarget(monster);
        }
    }

    // Entire native CheckTarget selection algorithm, with its writes staged locally.
    // Valid ordering, highest-index selection, last nonflying fallback and boss gates
    // are unchanged; malformed membership never publishes an intermediate candidate.
    private static void ResolveTarget(LegacyNativePlayerRuntime client, LegacyNativeMonsterRole monster,
        ref LegacyNativePlayerRuntime? selected, ref LegacyNativeMobStatus state)
    {
        if (selected == null && !TargetIsFlying(client))
        {
            short distance = LegacyNativeMonsterView.GetDistance(client.X, client.Y, monster.X, monster.Y);
            if (distance > monster.Family.ViewRange || !client.Alive) return;
            ILegacyNativeMapObj? candidate = monster.View.GetTarget(client.Owner.Map!, LegacyNativeMapObjectType.Player);
            if (candidate == null) return;
            selected = candidate as LegacyNativePlayerRuntime;
            if (selected == null) return;
            if (!TargetIsFlying(selected))
            {
                state = LegacyNativeMobStatus.SearchTarget;
                return;
            }
            foreach (ILegacyNativeMapObj item in monster.View.Roles(client.Owner.Map!, LegacyNativeMapObjectType.Player))
            {
                LegacyNativePlayerRuntime? player = item as LegacyNativePlayerRuntime;
                if (!TargetIsFlying(player))
                {
                    selected = player;
                    state = LegacyNativeMobStatus.SearchTarget;
                }
            }
            return;
        }
        short distance2 = LegacyNativeMonsterView.GetDistance(client.X, client.Y, monster.X, monster.Y);
        if (selected == null)
        {
            ILegacyNativeMapObj? candidate = monster.View.GetTarget(client.Owner.Map!, LegacyNativeMapObjectType.Player);
            if (candidate != null)
            {
                selected = candidate as LegacyNativePlayerRuntime;
                if (!TargetIsFlying(selected) || monster.Boss == 1)
                {
                    state = LegacyNativeMobStatus.SearchTarget;
                }
                else
                {
                    foreach (ILegacyNativeMapObj item in monster.View.Roles(client.Owner.Map!, LegacyNativeMapObjectType.Player))
                    {
                        LegacyNativePlayerRuntime? player = item as LegacyNativePlayerRuntime;
                        if (!TargetIsFlying(player))
                        {
                            selected = player;
                            state = LegacyNativeMobStatus.SearchTarget;
                        }
                    }
                }
            }
        }
        if (selected != null && (distance2 > monster.Family.ViewRange || TargetIsFlying(selected) ||
            selected.Owner.Socket == null || (selected.Owner.Socket != null && !selected.Owner.Socket.Alive)))
        {
            selected = null;
        }
        else if (selected == null || selected.Alive)
        {
            state = LegacyNativeMobStatus.SearchTarget;
        }
    }
}
