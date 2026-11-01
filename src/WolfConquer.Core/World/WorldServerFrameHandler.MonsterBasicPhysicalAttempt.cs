using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal MonsterRuntimeStateStore MonsterStatesForPhysicalDamage => _monsterStates;
    // The token is inert unless the issuing Frame has registered the real
    // request, actor readiness and target birth in its private table.
    internal sealed class MonsterPhysicalAttempt { }
    private sealed record MonsterPhysicalAttemptData(BasicPhysicalSlot Slot, BasicPhysicalAttempt Reservation,
        LegacyOrdinarySpellBinding Actor, WorldPacketHandlerContext Context, WorldSession Before,
        MsgAttackQuery Query, MonsterRuntimeStateStore.OwnedMonsterRepeatTarget Target, long Generation, DateTime Stamp)
    {
        internal long CommittedGeneration = -1;
    }
    private readonly ConditionalWeakTable<MonsterPhysicalAttempt, MonsterPhysicalAttemptData> monsterPhysicalAttempts = new();

    internal bool TryBeginMonsterPhysical(WorldPacketHandlerContext context, MsgAttackPacket packet,
        LegacyMonsterInstance monster, Monster family, out MonsterPhysicalAttempt? attempt)
    {
        attempt = null;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || context.BasicPhysicalRequest is not { } request ||
            !basicPhysicalRequests.TryGetValue(request, out var issued) || issued.Begun ||
            packet.Query != issued.Query || packet.Query.OpponentUid != monster.Uid || context.Session is not { } expected ||
            !ReferenceEquals(context.TradeOperations, _worldOperations) ||
            !_worldOperations.OwnsActiveExclusiveLease(context.FrameOperationLease) ||
            _worldData is not { } catalog || !TryPrepareActionContext(_sessions, context, expected, out var actual) ||
            !IsActionLifecycleCurrent(_sessions, actual)) return false;
        var slot = issued.Slot; var actor = slot.Readiness.Binding;
        if (!IsStaminaReadinessCurrent(slot.Readiness) || slot.Generation != issued.Generation || slot.Attempt is not null ||
            issued.Repeat is not null && (!ReferenceEquals(slot.Repeat, issued.Repeat) || issued.Repeat.Monster is null) ||
            !ReferenceEquals(actor.Connection, context.Connection) ||
            !ReferenceEquals(expected.NativeGameClient, actor.Client) ||
            !_sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
            !_sessions.IsBoundSpellOwnerCurrent(actor) ||
            !LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(actor, out var speed) || speed is null ||
            expected.Character.Agility is < 0 or > ushort.MaxValue) return false;
        using var nativeRead = BasicPhysicalReadScope.Enter(actor);
        if (!slot.Native.IsCurrentPlain(actor) || slot.Native.AttackStamp != slot.AttackStamp ||
            !TryReadBasicPhysicalOrdinaryFlags(actor, expected, out var flags) ||
            !LegacyBasicPhysicalRuneSelection.TryCapture(actor, expected, out var runes) || runes is null || runes.HasDeadlySight)
            return false;
        // This is only a complete currentness witness. Owned status/action
        // parsers retain their own ambiguity checks; unrelated keys stay raw.
        var attrs = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.Attributes);
        var stamp = slot.Readiness.ReadLocalNow();
        int delay = CalculateBasicPhysicalOrdinaryDelay(speed, checked((ushort)expected.Character.Agility));
        if (stamp < slot.AttackStamp || stamp < slot.AttackStamp.AddMilliseconds(delay)) return false;
        _ = checked(slot.Generation + 1);
        var target = issued.Repeat?.Monster ?? MonsterRuntimeStateStore.OwnedMonsterRepeatTarget.TryCapture(_monsterStates, this, catalog, monster, family);
        if (target is null || !target.TryReadCurrent(out var selected, out var selectedFamily, out _) ||
            selected != monster || !ReferenceEquals(selectedFamily, family) || !attrs.IsCurrentPlain() ||
            !speed.IsCurrentPlain(actor) || !flags.IsCurrentPlain() || !runes.IsCurrentPlain() ||
            !IsStaminaReadinessCurrent(slot.Readiness) || slot.Generation != issued.Generation ||
            !_sessions.TryGet(context.Connection, out current) || !ReferenceEquals(current, expected)) return false;
        var reservation = new BasicPhysicalAttempt();
        var token = new MonsterPhysicalAttempt();
        monsterPhysicalAttempts.Add(token, new(slot, reservation, actor, actual, expected, issued.Query, target, slot.Generation, stamp));
        issued.Begun = true; slot.Attempt = reservation; attempt = token;
        return true;
    }

    internal bool TryReadMonsterPhysicalAttempt(MonsterPhysicalAttempt attempt, LegacyOrdinarySpellBinding actor,
        WorldSession expected, out MonsterRuntimeStateStore.OwnedMonsterRepeatTarget? target,
        out LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? native, out long generation, out DateTime stamp)
    {
        target = null; native = null; generation = 0; stamp = default;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || !monsterPhysicalAttempts.TryGetValue(attempt, out var data) ||
            !ReferenceEquals(data.Actor, actor) || !ReferenceEquals(data.Before, expected) || data.CommittedGeneration >= 0 ||
            !ReferenceEquals(data.Slot.Attempt, data.Reservation) || data.Slot.Generation != data.Generation ||
            !ReferenceEquals(data.Context.TradeOperations, _worldOperations) ||
            !_worldOperations.OwnsActiveExclusiveLease(data.Context.FrameOperationLease) ||
            !IsStaminaReadinessCurrent(data.Slot.Readiness) || !IsActionLifecycleCurrent(_sessions, data.Context) ||
            !TryReadBasicPhysicalNative(actor, out native, out generation) || native is null || generation != data.Generation)
            return false;
        target = data.Target; stamp = data.Stamp;
        return true;
    }

    internal bool PublishMonsterPhysicalStamp(MonsterPhysicalAttempt attempt, LegacyOrdinarySpellBinding actor,
        WorldSession expected, LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication native)
    {
        if (!TryReadMonsterPhysicalAttempt(attempt, actor, expected, out _, out var before, out var generation, out _) ||
            !monsterPhysicalAttempts.TryGetValue(attempt, out var data) || !native.IsCurrentPlain() || before is null ||
            !ReferenceEquals(data.Slot.Native, before)) return false;
        long next = checked(generation + 1);
        native.PublishPrepared();
        data.Slot.Native = native.After; data.Slot.AttackStamp = native.After.AttackStamp;
        data.Slot.Generation = next; data.CommittedGeneration = next;
        data.Slot.Committed = null; data.Slot.Repeat = null;
        return true;
    }

    internal void AbandonMonsterPhysicalAttempt(MonsterPhysicalAttempt attempt)
    {
        if (Monitor.IsEntered(_tradeStates.SyncRoot) && monsterPhysicalAttempts.TryGetValue(attempt, out var data) &&
            data.Slot.Generation == (data.CommittedGeneration < 0 ? data.Generation : data.CommittedGeneration) &&
            ReferenceEquals(data.Slot.Attempt, data.Reservation)) data.Slot.Attempt = null;
    }

    internal bool TryGetMonsterPhysicalAttemptActor(MonsterPhysicalAttempt attempt, out LegacyOrdinarySpellBinding actor)
    {
        actor = null!;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || !monsterPhysicalAttempts.TryGetValue(attempt, out var data)) return false;
        actor = data.Actor; return true;
    }

    internal bool IsMonsterCommittedAttemptCurrent(MonsterPhysicalAttempt attempt, LegacyOrdinarySpellBinding actor) =>
        Monitor.IsEntered(_tradeStates.SyncRoot) && monsterPhysicalAttempts.TryGetValue(attempt, out var data) &&
        ReferenceEquals(data.Actor, actor) && data.CommittedGeneration >= 0 &&
        data.Slot.Generation == data.CommittedGeneration && ReferenceEquals(data.Slot.Attempt, data.Reservation) &&
        ReferenceEquals(data.Context.TradeOperations, _worldOperations) &&
        _worldOperations.OwnsActiveExclusiveLease(data.Context.FrameOperationLease) &&
        IsStaminaReadinessCurrent(data.Slot.Readiness) && data.Slot.Native.IsCurrentPlain(actor) &&
        IsActionLifecycleCurrent(_sessions, data.Context);

    internal bool TryReadMonsterArm(MonsterPhysicalAttempt attempt, LegacyOrdinarySpellBinding actor,
        out MonsterRuntimeStateStore.OwnedMonsterRepeatTarget? target,
        out LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? native, out long generation, out MsgAttackQuery query)
    {
        target = null; native = null; generation = 0; query = default;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || !monsterPhysicalAttempts.TryGetValue(attempt, out var data) ||
            !ReferenceEquals(data.Actor, actor) || data.CommittedGeneration < 0 ||
            data.Slot.Generation != data.CommittedGeneration || !ReferenceEquals(data.Slot.Attempt, data.Reservation) ||
            !ReferenceEquals(data.Context.TradeOperations, _worldOperations) ||
            !_worldOperations.OwnsActiveExclusiveLease(data.Context.FrameOperationLease) ||
            !IsStaminaReadinessCurrent(data.Slot.Readiness) || !IsActionLifecycleCurrent(_sessions, data.Context) ||
            !TryReadBasicPhysicalNative(actor, out native, out generation) || native is null || generation != data.CommittedGeneration ||
            !data.Target.TryReadCurrent(out _, out _, out _)) return false;
        target = data.Target; query = data.Query; return true;
    }

    internal bool PublishMonsterArm(MonsterPhysicalAttempt attempt, LegacyOrdinarySpellBinding actor,
        WorldSessionStore.BasicPhysicalNativeChange change, LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication native)
    {
        if (!TryReadMonsterArm(attempt, actor, out var target, out var before, out var generation, out _) || target is null ||
            before is null || !native.IsPreparedFor(actor, change) || !native.IsCurrentPlain() ||
            !monsterPhysicalAttempts.TryGetValue(attempt, out var data)) return false;
        native.PublishPrepared();
        data.Slot.Native = native.After;
        data.Slot.Repeat = new BasicPhysicalRepeat(null, native.After, generation, target);
        data.Slot.Attempt = null;
        return true;
    }
}
