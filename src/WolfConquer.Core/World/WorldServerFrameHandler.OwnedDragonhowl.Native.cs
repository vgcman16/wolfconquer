using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool TryReadDragonhowlStamp(OwnedDragonhowlRequest request, OwnedDragonhowlAttempt attempt,
        out LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? before, out long generation, out DateTime stamp)
    {
        before = null; generation = 0; stamp = default;
        if (!DragonhowlAttemptCurrent(attempt, request, out var hit) || hit.CommittedGeneration >= 0 ||
            !dragonhowlRequests.TryGetValue(request, out var source) ||
            !IsActionLifecycleCurrent(_sessions, source.Context) || !DragonhowlContextOwnersCurrent(source.Context) ||
            !_worldOperations.OwnsActiveExclusiveLease(source.Context.FrameOperationLease) ||
            !source.Wire.Slot.Native.IsCurrentPlain(source.Actor)) return false;
        before = source.Wire.Slot.Native; generation = hit.Generation; stamp = hit.Stamp; return true;
    }

    internal bool IsDragonhowlActive(OwnedDragonhowlRequest request, OwnedDragonhowlAttempt attempt,
        WorldSession actor, WorldSession? primary)
    {
        if (!dragonhowlRequests.TryGetValue(request, out var source) || !DragonhowlAttemptCurrent(attempt, request, out _) ||
            !IsActionLifecycleCurrent(_sessions, source.Context) || !DragonhowlContextOwnersCurrent(source.Context) ||
            !_worldOperations.OwnsActiveExclusiveLease(source.Context.FrameOperationLease) ||
            !source.Wire.Slot.Native.IsCurrentPlain(source.Actor) ||
            !_sessions.TryGet(source.Actor.Connection, out var current) || !ReferenceEquals(current, actor) ||
            !TryReadDragonhowlPrimary(source.Primary, primary, out var uid, out _, out _, out _) || uid != source.Wire.Query.OpponentUid ||
            !_sessions.IsBoundSpellOwnerCurrent(source.Actor) ||
            !source.Gear.IsCurrentPlain(actor) || !source.ActorMetadata.IsCurrentPlain() || source.TargetMetadata?.IsCurrentPlain() == false ||
            !TryCaptureNativeCombatGearMetadata(_sessions, out var tables) || !ReferenceEquals(tables, source.Tables)) return false;
        return SnapshotFileLockCoordinator.ExecuteLocked(source.GearPath, () =>
            CharacterSnapshotFileBaseline.TryReadBytes(source.GearPath, "ownedDragonhowlGear", out var bytes, out _) &&
            bytes.AsSpan().SequenceEqual(source.GearImage));
    }

    internal string RequireDragonhowlGearPath(OwnedDragonhowlRequest request) =>
        Monitor.IsEntered(_tradeStates.SyncRoot) && dragonhowlRequests.TryGetValue(request, out var state)
            ? state.GearPath : throw new InvalidOperationException("ownedDragonhowlRequestNotIssued");

    internal bool TryPrepareDragonhowlRepeat(OwnedDragonhowlRequest request, OwnedDragonhowlAttempt attempt,
        WorldSessionStore.OwnedDragonhowlCompletion completion, WorldSession actor, WorldSession? target,
        out LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? native, out long generation, out MsgAttackQuery query)
    {
        native = null; generation = 0; query = default;
        if (!IsDragonhowlActive(request, attempt, actor, target) ||
            !DragonhowlAttemptCurrent(attempt, request, out var hit) || hit.CommittedGeneration < 0 ||
            !_sessions.IsActualDragonhowlCompletion(completion, hit.Source.Actor, attempt) ||
            !TryReadDragonhowlPrimary(hit.Source.Primary, target, out _, out var x, out var y, out var alive)) return false;
        // The source permits the initial gear request at 9, but arms a player
        // repeat only at 3. Completion is real even when no repeat can be armed.
        if (!alive || actor.Character.MinHitPoints <= 0 || target is not null &&
            Math.Max(Math.Abs(actor.Character.X - x), Math.Abs(actor.Character.Y - y)) > 3)
            return false;
        hit.Source.Wire.Slot.DragonhowlCommitted = completion;
        native = hit.Source.Wire.Slot.Native; generation = hit.Source.Wire.Slot.Generation; query = hit.Source.Wire.Query;
        return true;
    }
}
