namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool TryPrepareBasicPhysicalEntityContext(WorldSessionStore store, WorldPacketHandlerContext context,
        WorldSession expected, out WorldPacketHandlerContext actual)
    {
        actual = null!;
        var clock = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;
        if (!TryPrepareActionContext(store, context, expected, out var prepared) ||
            !ReferenceEquals(context.MonsterStates, _monsterStates) || !ReferenceEquals(context.SobNpcStates, _sobNpcStates) ||
            !ReferenceEquals(context.GuildWarStates, _guildWarStates) ||
            context.TeamStates is not null && !ReferenceEquals(context.TeamStates, _teamStates) ||
            context.TimeProvider is not null && !ReferenceEquals(context.TimeProvider, clock)) return false;
        actual = prepared with { TeamStates = _teamStates, TimeProvider = clock };
        return true;
    }

    internal bool IsBasicPhysicalContextCurrentPlain(WorldSessionStore store, WorldPacketHandlerContext context) =>
        IsActionLifecycleCurrent(store, context) && ReferenceEquals(context.MonsterStates, _monsterStates) &&
        ReferenceEquals(context.SobNpcStates, _sobNpcStates) && ReferenceEquals(context.GuildWarStates, _guildWarStates) &&
        ReferenceEquals(context.TeamStates, _teamStates) &&
        ReferenceEquals(context.TimeProvider, _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System);
}

public sealed partial class WorldSessionStore
{
    internal bool TryPrepareBasicPhysicalEntityContext(WorldPacketHandlerContext context,
        WorldSession expected, out WorldPacketHandlerContext actual)
    {
        actual = null!;
        return framePersistenceOwner is { } frame && frame.TryPrepareBasicPhysicalEntityContext(this, context, expected, out actual);
    }
}
