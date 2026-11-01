using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal sealed class OwnedDragonhowlRequest { }

    private sealed class DragonhowlRequestState(BasicPhysicalRequest request,
        BasicPhysicalRequestState wire, WorldPacketHandlerContext context, OwnedDragonhowlPrimary primary,
        LegacyOrdinarySpellBinding actor, LegacyOrdinarySpellBinding? target,
        LegacyNativeMyCombatGear.PhysicalProcInputs gear, LegacyNativeCombatGearTable.MailboxLoadProof tables,
        LegacyOwnedPhysicalMetadataWitness actorMetadata, LegacyOwnedPhysicalMetadataWitness? targetMetadata,
        string gearPath, byte[] gearImage)
    {
        internal readonly BasicPhysicalRequest Request = request;
        internal readonly BasicPhysicalRequestState Wire = wire;
        internal readonly WorldPacketHandlerContext Context = context;
        internal readonly OwnedDragonhowlPrimary Primary = primary;
        internal readonly LegacyOrdinarySpellBinding Actor = actor;
        internal readonly LegacyOrdinarySpellBinding? Target = target;
        internal readonly LegacyNativeMyCombatGear.PhysicalProcInputs Gear = gear;
        internal readonly LegacyNativeCombatGearTable.MailboxLoadProof Tables = tables;
        internal readonly LegacyOwnedPhysicalMetadataWitness ActorMetadata = actorMetadata;
        internal readonly LegacyOwnedPhysicalMetadataWitness? TargetMetadata = targetMetadata;
        internal readonly string GearPath = gearPath;
        internal readonly byte[] GearImage = gearImage;
        internal WorldPacketHandlers.LegacyPhysicalCombatGearSelection? Selection;
        internal bool Evaluated;
    }

    private readonly ConditionalWeakTable<BasicPhysicalRequest, OwnedDragonhowlRequest> dragonhowlByWire = new();
    private readonly ConditionalWeakTable<OwnedDragonhowlRequest, DragonhowlRequestState> dragonhowlRequests = new();

    private bool DragonhowlContextOwnersCurrent(WorldPacketHandlerContext context) =>
        ReferenceEquals(context.TeamStates, _teamStates) && ReferenceEquals(context.MonsterStates, _monsterStates) &&
        ReferenceEquals(context.SobNpcStates, _sobNpcStates) && ReferenceEquals(context.FloorItems, _floorItems) &&
        ReferenceEquals(context.GuildWarStates, _guildWarStates) &&
        ReferenceEquals(context.Terrain, _terrain) && ReferenceEquals(context.ClientCompatibility, _clientCompatibility) &&
        ReferenceEquals(context.TimeProvider, _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System) &&
        ReferenceEquals(context.LegacyClock, _runtimeUpdateLoop.LegacyClock ?? LegacySignedMillisecondClock.System);

    internal bool AreDragonhowlDropOwnersCurrent(OwnedDragonhowlRequest request,
        FloorItemRuntimeStateStore floor, LegacyGameMapTerrainQuery terrain) =>
        Monitor.IsEntered(_tradeStates.SyncRoot) && dragonhowlRequests.TryGetValue(request, out var state) &&
        ReferenceEquals(floor, _floorItems) && ReferenceEquals(terrain, _terrain) &&
        ReferenceEquals(state.Context.FloorItems, floor) && ReferenceEquals(state.Context.Terrain, terrain) &&
        IsActionLifecycleCurrent(_sessions, state.Context) &&
        _worldOperations.OwnsActiveExclusiveLease(state.Context.FrameOperationLease);

    internal bool TryReadOwnedDragonhowl(OwnedDragonhowlRequest request,
        out WorldPacketHandlers.LegacyPhysicalCombatGearSelection? selection)
    {
        selection = null;
        if (!dragonhowlRequests.TryGetValue(request, out var state) || !state.Evaluated ||
            !DragonhowlBeforeCurrent(state) || !state.Gear.IsCurrentPlain(state.Context.Session!) ||
            !state.ActorMetadata.IsCurrentPlain() || state.TargetMetadata?.IsCurrentPlain() == false) return false;
        selection = state.Selection;
        return true;
    }

    internal bool OwnedDragonhowlSavedGearCurrent(OwnedDragonhowlRequest request) =>
        dragonhowlRequests.TryGetValue(request, out var state) &&
        SnapshotFileLockCoordinator.ExecuteLocked(state.GearPath, () =>
            TryReadOwnedDragonhowl(request, out _) &&
            CharacterSnapshotFileBaseline.TryReadBytes(state.GearPath, "ownedDragonhowlGear", out var image, out _) &&
            image.AsSpan().SequenceEqual(state.GearImage) && TryReadOwnedDragonhowl(request, out _));

    private bool DragonhowlBeforeCurrent(DragonhowlRequestState state)
    {
        var wire = state.Wire;
        var slot = wire.Slot;
        var actor = state.Actor;
        return Monitor.IsEntered(_tradeStates.SyncRoot) && WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
            Monitor.IsEntered(actor.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(actor.Player.SpellAdmissionSyncRoot) &&
            basicPhysicalRequests.TryGetValue(state.Request, out var actualWire) && ReferenceEquals(actualWire, wire) &&
            IsActionLifecycleCurrent(_sessions, state.Context) && DragonhowlContextOwnersCurrent(state.Context) &&
            _worldOperations.OwnsActiveExclusiveLease(state.Context.FrameOperationLease) && IsStaminaReadinessCurrent(slot.Readiness) &&
            !wire.Begun && slot.Attempt is null && slot.Generation == wire.Generation &&
            (wire.Repeat is null || ReferenceEquals(slot.Repeat, wire.Repeat)) &&
            ReferenceEquals(slot.Readiness.Binding, actor) && ReferenceEquals(actor.Connection, state.Context.Connection) &&
            DragonhowlPrimaryBeforeCurrent(state.Primary, state.Request, out var targetUid, out _, out _) &&
            targetUid == wire.Query.OpponentUid &&
            _sessions.TryGet(actor.Connection, out var current) && ReferenceEquals(current, state.Context.Session) &&
            _sessions.IsBoundSpellOwnerCurrent(actor) &&
            slot.Native.IsCurrentPlain(actor) && slot.Native.AttackStamp == slot.AttackStamp &&
            TryCaptureNativeCombatGearMetadata(_sessions, out var tables) && ReferenceEquals(tables, state.Tables);
    }
}
