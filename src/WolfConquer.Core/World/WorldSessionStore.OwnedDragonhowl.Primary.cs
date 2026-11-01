using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDragonhowlSobSelection
    {
        private readonly WorldSessionStore store;
        internal OwnedDragonhowlSobSelection(WorldSessionStore store) => this.store = store;
        internal SobNpc Current => store.dragonhowlSobSelections.TryGetValue(this, out var selected) ? selected.Current :
            throw new InvalidOperationException("ownedDragonhowlSobSelectionNotIssued");
    }
    private sealed record DragonhowlSobSelection(DragonhowlOwnerState Owner,
        SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget Target, SobNpc Current);
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<OwnedDragonhowlSobSelection, DragonhowlSobSelection>
        dragonhowlSobSelections = new();

    internal bool TrySelectDragonhowlSob(OwnedDragonhowlOwner purpose, SobNpc birth, out OwnedDragonhowlSobSelection? selection)
    {
        selection = null;
        if (!dragonhowlOwners.TryGetValue(purpose, out var owner) || owner.Plan is not null ||
            !IsOwnedDragonhowlBeforeCurrent(purpose) || !owner.Frame.TryCaptureDragonhowlSelectedSob(owner.Request, birth,
                out var target, out var current) || target is null) return false;
        var token = new OwnedDragonhowlSobSelection(this);
        dragonhowlSobSelections.Add(token, new(owner, target, current));
        selection = token; return true;
    }

    // Initial selection DATA only. Each branch must capture the real target
    // owner inside the Frame's actual lifecycle/connection/native scope.
    private sealed class DragonhowlInitialTarget
    {
        internal readonly WorldActiveSession? Player;
        private readonly LegacyMonsterInstance? monster;
        private readonly Monster? family;
        private readonly SobNpc? sob;
        internal DragonhowlInitialTarget(WorldActiveSession player) => Player = player;
        internal DragonhowlInitialTarget(LegacyMonsterInstance monster, Monster family)
        { this.monster = monster; this.family = family; }
        internal DragonhowlInitialTarget(SobNpc sob) => this.sob = sob;

        internal bool TryCapture(WorldServerFrameHandler frame, WorldPacketHandlerContext context, MsgAttackPacket packet,
            out WorldServerFrameHandler.OwnedDragonhowlPrimary? primary)
        {
            primary = null;
            if (context.Session is not { } actor) return false;
            if (Player is not null) return frame.TryCaptureDragonhowlPlayerPrimary(context, packet, Player, out primary);
            if (monster is not null && family is not null)
                return WorldPacketHandlers.CanPrepareDragonhowlMonsterPrimary(context, actor, monster, family) &&
                    frame.TryCaptureDragonhowlMonsterPrimary(context, packet, monster, family, out primary);
            return sob is not null && WorldPacketHandlers.CanPrepareDragonhowlSobPrimary(context, actor, sob) &&
                frame.TryCaptureDragonhowlSobPrimary(context, packet, sob, out primary);
        }
    }

    internal bool TryPrepareOwnedDragonhowlMonster(WorldPacketHandlerContext context, MsgAttackPacket packet,
        LegacyMonsterInstance monster, Monster family, out WorldPacketHandlers.OwnedDragonhowlPreparation? prepared, out string reason) =>
        TryPrepareOwnedDragonhowlCore(context, packet, new DragonhowlInitialTarget(monster, family), out prepared, out reason);

    internal bool TryPrepareOwnedDragonhowlSob(WorldPacketHandlerContext context, MsgAttackPacket packet,
        SobNpc sob, out WorldPacketHandlers.OwnedDragonhowlPreparation? prepared, out string reason) =>
        TryPrepareOwnedDragonhowlCore(context, packet, new DragonhowlInitialTarget(sob), out prepared, out reason);

    private static WorldSession? DragonhowlCurrentPrimary(DragonhowlOwnerState owner) =>
        owner.PrimaryPlayer is { } player ? owner.Current[player.Connection] : null;
}

public static partial class WorldPacketHandlers
{
    internal static bool IsDragonhowlSobRepeatVisible(WorldSession actor, SobNpc current) => CanSeeSobNpc(actor.Character, current);

    internal static bool CanPrepareDragonhowlMonsterPrimary(WorldPacketHandlerContext context, WorldSession actor,
        LegacyMonsterInstance monster, Monster family)
    {
        var targets = ReadOwnedPhysicalNonPlayerTargets(context, actor, monster.Uid);
        return targets.NamespaceCount == 1 && targets.Monster == monster && ReferenceEquals(targets.Family, family) &&
            context.Sessions is { } store && !store.Snapshot().Any(row => row.Session.Character.Uid == monster.Uid) &&
            CanEnterOwnedPhysicalNonPlayer(context, actor, targets);
    }

    internal static bool CanPrepareDragonhowlSobPrimary(WorldPacketHandlerContext context, WorldSession actor, SobNpc sob)
    {
        if (sob.Id < 1) return false;
        var targets = ReadOwnedPhysicalNonPlayerTargets(context, actor, checked((uint)sob.Id));
        return targets.NamespaceCount == 1 && targets.Sob == sob && context.Sessions is { } store &&
            !store.Snapshot().Any(row => row.Session.Character.Uid == sob.Id) && CanEnterOwnedPhysicalNonPlayer(context, actor, targets);
    }
}
