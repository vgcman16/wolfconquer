using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // The model does not grant a target. Only this Frame's private registry
    // connects an actual captured Physical request to its real target owner.
    internal sealed class OwnedDragonhowlPrimary { }

    private sealed class DragonhowlPrimaryState
    {
        internal readonly WorldPacketHandlerContext Context;
        internal readonly BasicPhysicalRequest Request;
        internal readonly BasicPhysicalRequestState Wire;
        internal readonly WorldActiveSession? Player;
        internal readonly LegacyOrdinarySpellBinding? Binding;
        internal readonly MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget? Monster;
        internal readonly SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget? Sob;
        internal readonly LegacyMonsterInstance? OriginalMonster;
        internal readonly Monster? Family;
        internal readonly SobNpc? OriginalSob;

        internal DragonhowlPrimaryState(WorldPacketHandlerContext context, BasicPhysicalRequest request,
            BasicPhysicalRequestState wire, WorldActiveSession player, LegacyOrdinarySpellBinding binding)
        { Context = context; Request = request; Wire = wire; Player = player; Binding = binding; }
        internal DragonhowlPrimaryState(WorldPacketHandlerContext context, BasicPhysicalRequest request,
            BasicPhysicalRequestState wire, MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget monster,
            LegacyMonsterInstance original, Monster family)
        { Context = context; Request = request; Wire = wire; Monster = monster; OriginalMonster = original; Family = family; }
        internal DragonhowlPrimaryState(WorldPacketHandlerContext context, BasicPhysicalRequest request,
            BasicPhysicalRequestState wire, SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget sob, SobNpc original)
        { Context = context; Request = request; Wire = wire; Sob = sob; OriginalSob = original; }
    }

    private readonly ConditionalWeakTable<OwnedDragonhowlPrimary, DragonhowlPrimaryState> dragonhowlPrimaries = new();
    private readonly ConditionalWeakTable<BasicPhysicalRequest, OwnedDragonhowlPrimary> dragonhowlPrimaryByWire = new();

    private bool TryIssueDragonhowlPrimary(DragonhowlPrimaryState state, out OwnedDragonhowlPrimary? primary)
    {
        primary = null;
        if (dragonhowlPrimaryByWire.TryGetValue(state.Request, out var previous))
        {
            if (!dragonhowlPrimaries.TryGetValue(previous, out var old) || !ReferenceEquals(old.Wire, state.Wire) ||
                !ReferenceEquals(old.Player?.Connection, state.Player?.Connection) ||
                !ReferenceEquals(old.Player?.Session, state.Player?.Session) || old.OriginalMonster != state.OriginalMonster ||
                !ReferenceEquals(old.Family, state.Family) || old.OriginalSob != state.OriginalSob ||
                !DragonhowlPrimaryBeforeCurrent(previous, state.Request, out _, out _, out _)) return false;
            primary = previous; return true;
        }
        var token = new OwnedDragonhowlPrimary();
        dragonhowlPrimaries.Add(token, state);
        dragonhowlPrimaryByWire.Add(state.Request, token);
        primary = token; return true;
    }

    internal bool TryReadDragonhowlPrimaryPlayer(OwnedDragonhowlPrimary primary,
        out WorldActiveSession? player, out LegacyOrdinarySpellBinding? binding)
    {
        player = null; binding = null;
        if (!dragonhowlPrimaries.TryGetValue(primary, out var data)) return false;
        player = data.Player; binding = data.Binding; return true;
    }

    internal MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget? CaptureDragonhowlSelectedMonster(
        OwnedDragonhowlRequest request, LegacyMonsterInstance monster, Monster family)
    {
        if (!dragonhowlRequests.TryGetValue(request, out var source) || !DragonhowlBeforeCurrent(source) ||
            !dragonhowlPrimaries.TryGetValue(source.Primary, out var primary) || _worldData is null) return null;
        if (primary.OriginalMonster?.Uid == monster.Uid)
            return primary.OriginalMonster == monster && ReferenceEquals(primary.Family, family) ? primary.Monster : null;
        return MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget.TryCapture(_monsterStates, this, _worldData, monster, family);
    }

    internal bool TryCaptureDragonhowlSelectedSob(OwnedDragonhowlRequest request, SobNpc birth,
        out SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget? target, out SobNpc current)
    {
        target = null; current = null!;
        if (!dragonhowlRequests.TryGetValue(request, out var source) || !DragonhowlBeforeCurrent(source) ||
            !dragonhowlPrimaries.TryGetValue(source.Primary, out var primary) || _worldData is null) return false;
        if (primary.OriginalSob?.Id == birth.Id && primary.OriginalSob.MapId == birth.MapId)
        {
            if (!_worldData.SobNpcs.Any(row => ReferenceEquals(row, birth)) || primary.Sob is not { } actual ||
                !actual.TryReadCurrent(out current) || current != primary.OriginalSob) return false;
            target = actual; return true;
        }
        return SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget.TryCaptureRegisteredBirth(_sobNpcStates, _worldData, birth,
            out target, out current);
    }

    private bool DragonhowlPrimaryBeforeCurrent(OwnedDragonhowlPrimary primary, BasicPhysicalRequest request,
        out uint uid, out ushort x, out ushort y)
    {
        uid = 0; x = y = 0;
        if (!dragonhowlPrimaries.TryGetValue(primary, out var data) || !ReferenceEquals(data.Request, request) ||
            !basicPhysicalRequests.TryGetValue(request, out var wire) || !ReferenceEquals(wire, data.Wire) ||
            wire.Begun || !TryReadDragonhowlPrimary(primary, data.Player?.Session, out uid, out x, out y, out var alive) || !alive)
            return false;
        return data.Monster is not null ? data.Monster.TryReadCurrent(out var current, out _) && current == data.OriginalMonster :
            data.Sob is null || data.Sob.TryReadCurrent(out _);
    }

    private bool TryReadDragonhowlPrimaryIssuer(WorldPacketHandlerContext context, MsgAttackPacket packet,
        out BasicPhysicalRequest request, out BasicPhysicalRequestState wire)
    {
        request = null!; wire = null!;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || context.BasicPhysicalRequest is not { } token ||
            !basicPhysicalRequests.TryGetValue(token, out var actual) || actual.Query != packet.Query || actual.Begun ||
            context.Session is not { } expected || !IsActionLifecycleCurrent(_sessions, context) || !DragonhowlContextOwnersCurrent(context) ||
            !_worldOperations.OwnsActiveExclusiveLease(context.FrameOperationLease) ||
            !IsStaminaReadinessCurrent(actual.Slot.Readiness) || actual.Slot.Attempt is not null ||
            actual.Slot.Generation != actual.Generation ||
            actual.Repeat is not null && !ReferenceEquals(actual.Slot.Repeat, actual.Repeat)) return false;
        var actor = actual.Slot.Readiness.Binding;
        if (!ReferenceEquals(actor.Connection, context.Connection) || packet.Query.Uid != actor.Uid ||
            !ReferenceEquals(expected.NativeGameClient, actor.Client) ||
            !Monitor.IsEntered(actor.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(actor.Player.SpellAdmissionSyncRoot) ||
            !_sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
            !_sessions.IsBoundSpellOwnerCurrent(actor) || !actual.Slot.Native.IsCurrentPlain(actor)) return false;
        request = token; wire = actual; return true;
    }

    internal bool TryCaptureDragonhowlPlayerPrimary(WorldPacketHandlerContext context, MsgAttackPacket packet,
        WorldActiveSession player, out OwnedDragonhowlPrimary? primary)
    {
        primary = null;
        if (!TryReadDragonhowlPrimaryIssuer(context, packet, out var request, out var wire) ||
            player.Session.NativeGameClient?.OrdinarySpellBinding is not { IsRetired: false } binding ||
            ReferenceEquals(binding, wire.Slot.Readiness.Binding) || binding.Uid != packet.Query.OpponentUid ||
            wire.Repeat is { } repeat && (!ReferenceEquals(repeat.Target, binding) ||
                repeat.Monster is not null || repeat.Dragonhowl is not null) ||
            !DragonhowlPlayerPrimaryCurrent(binding, player.Session) ||
            !ReferenceEquals(binding.Connection, player.Connection)) return false;
        return TryIssueDragonhowlPrimary(new(context, request, wire, player, binding), out primary);
    }

    internal bool TryCaptureDragonhowlMonsterPrimary(WorldPacketHandlerContext context, MsgAttackPacket packet,
        LegacyMonsterInstance monster, Monster family, out OwnedDragonhowlPrimary? primary)
    {
        primary = null;
        if (!TryReadDragonhowlPrimaryIssuer(context, packet, out var request, out var wire) ||
            packet.Query.OpponentUid != monster.Uid || _worldData is not { } catalog) return false;
        MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget? target;
        if (wire.Repeat is { } repeat)
        {
            LegacyMonsterInstance next;
            Monster nextFamily;
            if (repeat.Target is not null) return false;
            if (repeat.Dragonhowl is { } proc)
            {
                if (repeat.Monster is not null || proc.Monster is not { } original ||
                    !original.TryCaptureRepeatSuccessor(out target, out next, out nextFamily)) return false;
            }
            else if (repeat.Monster is not { } ordinary ||
                !MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget.TryCaptureRepeatSuccessor(
                    _monsterStates, this, catalog, ordinary, out target, out next, out nextFamily)) return false;
            if (target is null || next != monster || !ReferenceEquals(nextFamily, family)) return false;
        }
        else target = MonsterRuntimeStateStore.OwnedDragonhowlMonsterTarget.TryCapture(_monsterStates, this, catalog, monster, family);
        if (target is null ||
            !target.TryReadCurrent(out var actual, out var actualFamily) || actual != monster || !ReferenceEquals(actualFamily, family)) return false;
        return TryIssueDragonhowlPrimary(new(context, request, wire, target, monster, family), out primary);
    }

    internal bool TryCaptureDragonhowlSobPrimary(WorldPacketHandlerContext context, MsgAttackPacket packet,
        SobNpc sob, out OwnedDragonhowlPrimary? primary)
    {
        primary = null;
        if (!TryReadDragonhowlPrimaryIssuer(context, packet, out var request, out var wire) ||
            packet.Query.OpponentUid != sob.Id || _worldData is not { } catalog) return false;
        SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget? target;
        if (wire.Repeat is { } repeat)
        {
            if (repeat.Target is not null || repeat.Monster is not null || repeat.Dragonhowl?.Sob is not { } original ||
                !original.TryCaptureRepeatSuccessor(out target, out var next) || target is null || next != sob)
                return false;
        }
        else target = SobNpcRuntimeStateStore.OwnedDragonhowlSobTarget.TryCapture(_sobNpcStates, catalog, sob);
        if (target is null ||
            !target.TryReadCurrent(out var actual) || actual != sob) return false;
        return TryIssueDragonhowlPrimary(new(context, request, wire, target, sob), out primary);
    }

    private bool DragonhowlPlayerPrimaryCurrent(LegacyOrdinarySpellBinding binding, WorldSession current) =>
        !binding.IsRetired && !_tradeStates.HasConnectionEnded(binding.Connection) &&
        ReferenceEquals(binding.Store, _sessions) && ReferenceEquals(binding.Recovery, _tradeRecovery) &&
        ReferenceEquals(binding.ItemCatalog, _worldData) && ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) &&
        ReferenceEquals(binding.Client.Player, binding.Player) && ReferenceEquals(current.NativeGameClient, binding.Client) &&
        Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
        _sessions.TryGet(binding.Connection, out var actual) && ReferenceEquals(actual, current) &&
        _sessions.IsBoundSpellOwnerCurrent(binding);

    private bool IsDragonhowlRepeatTargetCurrent(DragonhowlPrimaryState target, WorldSession actor, uint uid)
    {
        if (target.Binding is not null) return false; // Player repeats use the existing bound-player descriptor.
        if (target.Monster is { } monster)
            return monster.TryCaptureRepeatSuccessor(out _, out var current, out _) && current.Uid == uid &&
                CanSeeMonster(actor.Character, current);
        return target.Sob is { } sob && sob.TryCaptureRepeatSuccessor(out _, out var currentSob) && currentSob.Id == uid &&
            WorldPacketHandlers.IsDragonhowlSobRepeatVisible(actor, currentSob);
    }

    internal bool TryReadDragonhowlPrimary(OwnedDragonhowlPrimary primary, WorldSession? playerSuccessor,
        out uint uid, out ushort x, out ushort y, out bool alive)
    {
        uid = 0; x = y = 0; alive = false;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || !dragonhowlPrimaries.TryGetValue(primary, out var data) ||
            !IsActionLifecycleCurrent(_sessions, data.Context) ||
            !_worldOperations.OwnsActiveExclusiveLease(data.Context.FrameOperationLease)) return false;
        if (data.Binding is { } binding)
        {
            if (playerSuccessor is null || !DragonhowlPlayerPrimaryCurrent(binding, playerSuccessor) ||
                playerSuccessor.Character.Uid != binding.Uid || playerSuccessor.Character.X is < 0 or > ushort.MaxValue ||
                playerSuccessor.Character.Y is < 0 or > ushort.MaxValue) return false;
            uid = checked((uint)binding.Uid); x = (ushort)playerSuccessor.Character.X; y = (ushort)playerSuccessor.Character.Y;
            alive = playerSuccessor.Character.MinHitPoints > 0; return true;
        }
        if (playerSuccessor is not null) return false;
        if (data.Monster is { } monster)
        {
            if (!monster.TryReadCurrent(out var current, out var family))
            {
                if (!monster.TryReadCommitted(out current, out _)) return false;
            }
            else if (!ReferenceEquals(family, data.Family) || current != data.OriginalMonster) return false;
            uid = current.Uid; x = current.X; y = current.Y; alive = current.Hitpoints > 0; return true;
        }
        if (data.Sob is not { } sob) return false;
        if (!sob.TryReadCurrent(out var currentSob) && !sob.TryReadCommitted(out currentSob, out _)) return false;
        if (currentSob.Id < 1 || currentSob.X is < 0 or > ushort.MaxValue ||
            currentSob.Y is < 0 or > ushort.MaxValue) return false;
        uid = checked((uint)currentSob.Id); x = checked((ushort)currentSob.X); y = checked((ushort)currentSob.Y);
        alive = currentSob.Life > 0; return true;
    }
}
