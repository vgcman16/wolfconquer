using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal sealed class OwnedDragonhowlFlashingExpiry { }
    private sealed record DragonhowlFlashingExpiryState(WorldPacketHandlerContext Context,
        LegacyOrdinarySpellBinding Binding, WorldSession Before, LegacyCanAttackFlashingRegistration Registration)
    { internal bool Consumed; }
    private readonly ConditionalWeakTable<OwnedDragonhowlFlashingExpiry, DragonhowlFlashingExpiryState> dragonhowlFlashingExpiries = new();

    // Called only by the real global worker, with its existing retained shared
    // lease. No nested lease, supplied due boolean, or UID refresh is accepted.
    private async ValueTask TryExpireOwnedDragonhowlFlashingAsync(WorldActiveSession active,
        LegacyCanAttackFlashingRegistration registration, IDisposable operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_worldOperations.OwnsActiveLease(operation) || active.Session.NativeGameClient?.OrdinarySpellBinding is not { } binding)
            return;
        var context = CreateMonsterRuntimeContext(active) with { TradeOperations = _worldOperations, FrameOperationLease = operation };
        var purpose = new OwnedDragonhowlFlashingExpiry();
        dragonhowlFlashingExpiries.Add(purpose, new(context, binding, active.Session, registration));
        if (_sessions.TryCommitDragonhowlFlashingExpiry(this, purpose, out var output) && output is not null)
            await output.PublishAsync(cancellationToken).ConfigureAwait(false);
    }

    internal bool TryReadDragonhowlFlashingExpiry(OwnedDragonhowlFlashingExpiry purpose,
        out WorldPacketHandlerContext context, out LegacyOrdinarySpellBinding binding, out WorldSession expected)
    {
        context = null!; binding = null!; expected = null!;
        if (!dragonhowlFlashingExpiries.TryGetValue(purpose, out var state) || state.Consumed ||
            !IsActionLifecycleCurrent(_sessions, state.Context) ||
            !_worldOperations.OwnsActiveLease(state.Context.FrameOperationLease) ||
            !ReferenceEquals(state.Context.TimeProvider, _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System) ||
            !ReferenceEquals(state.Binding.Connection, state.Context.Connection) ||
            !ReferenceEquals(state.Before.CanAttackFlashingRegistration, state.Registration) ||
            !_sessions.TryGet(state.Context.Connection, out var current) || !ReferenceEquals(current, state.Before) ||
            !_sessions.IsBoundSpellOwnerCurrent(state.Binding)) return false;
        context = state.Context; binding = state.Binding; expected = state.Before; return true;
    }

    // Participant addresses only; this does not authorize removal. The Store
    // acquires these real gates and then requires the complete proof above.
    internal bool TryReadDragonhowlFlashingExpiryParticipants(OwnedDragonhowlFlashingExpiry purpose,
        WorldSessionStore store, out WorldPacketHandlerContext context,
        out LegacyOrdinarySpellBinding binding, out WorldSession expected)
    {
        context = null!; binding = null!; expected = null!;
        if (!ReferenceEquals(store, _sessions) || !dragonhowlFlashingExpiries.TryGetValue(purpose, out var state) || state.Consumed)
            return false;
        context = state.Context; binding = state.Binding; expected = state.Before; return true;
    }

    internal bool IsActualDragonhowlFlashingExpiry(OwnedDragonhowlFlashingExpiry purpose,
        LegacyOrdinarySpellBinding binding, WorldSession expected,
        LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease lease) =>
        TryReadDragonhowlFlashingExpiry(purpose, out var context, out var actual, out var before) &&
        ReferenceEquals(binding, actual) && ReferenceEquals(before, expected) &&
        ReferenceEquals(expected.CanAttackFlashingRegistration, lease.Registration) &&
        lease.UsesClock(context.TimeProvider!) && lease.IsCurrentPlain(expected) && lease.IsDueNow;

    internal bool ConsumeDragonhowlFlashingExpiry(OwnedDragonhowlFlashingExpiry purpose,
        LegacyOrdinarySpellBinding binding, WorldSession expected,
        LegacyNativeStatusFlagsBigVector32.OwnedDragonhowlFlashingLease lease)
    {
        if (!IsActualDragonhowlFlashingExpiry(purpose, binding, expected, lease) ||
            !dragonhowlFlashingExpiries.TryGetValue(purpose, out var state)) return false;
        state.Consumed = true; return true;
    }
}
