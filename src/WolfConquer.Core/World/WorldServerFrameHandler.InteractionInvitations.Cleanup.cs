using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal sealed class EndedInteractionCleanup
    {
        internal WorldSession Proposed => EndedInteractionCleanups.TryGetValue(this, out var data) ? data.Proposed :
            throw new InvalidOperationException("endedInteractionCleanupNotIssued");
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding, WorldSession expected) =>
            EndedInteractionCleanups.TryGetValue(this, out var data) && data.IsCurrentPlain(binding, expected);
        internal void PublishPrepared()
        {
            if (EndedInteractionCleanups.TryGetValue(this, out var data)) data.PublishPrepared();
        }
    }
    private static readonly ConditionalWeakTable<EndedInteractionCleanup, EndedInteractionCleanupData> EndedInteractionCleanups = new();

    internal bool HasEndedInteractionCleanup(LegacyOrdinarySpellBinding binding) =>
        ReferenceEquals(binding.Store, _sessions) && interactionInvitations.TryGetValue(binding.Connection, out var slot) &&
        Volatile.Read(ref slot.RetiredPair) is not null;

    internal bool TryPrepareEndedInteractionCleanup(WorldSessionStore store, WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession expected, out EndedInteractionCleanup? cleanup)
    {
        cleanup = null;
        if (!IsActionLifecycleCurrent(store, context) || !ReferenceEquals(binding.Store, store) ||
            !ReferenceEquals(context.Connection, binding.Connection) || !store.IsBoundSpellOwnerCurrent(binding) ||
            !store.TryGet(binding.Connection, out var current) || !ReferenceEquals(current, expected)) return false;
        if (!interactionInvitations.TryGetValue(binding.Connection, out var slot) || slot.RetiredPair is not { } state)
            return true;
        // Old evidence cannot clear a replacement owner or a newer transition.
        if (!state.Contains(binding) || slot.State is not null || slot.Generation != GenerationFor(state, binding)) return true;
        var ended = state.RetiredEndpoint;
        var other = ReferenceEquals(state.Requester, binding) ? state.Recipient : state.Requester;
        if (state.Phase != InvitationPaired || Volatile.Read(ref state.Retired) == 0 ||
            !ReferenceEquals(ended, other) || !other.IsRetired) return false;
        if (expected.InteractionPartnerUid != other.Uid || expected.ActiveInteractionDance != state.Dance) return true;
        var data = new EndedInteractionCleanupData(this, store, context, binding, expected, slot, state, other);
        var token = new EndedInteractionCleanup();
        EndedInteractionCleanups.Add(token, data);
        if (!token.IsCurrentPlain(binding, expected)) return false;
        cleanup = token; return true;
    }

    private static long GenerationFor(InvitationState state, LegacyOrdinarySpellBinding binding) =>
        ReferenceEquals(state.Requester, binding) ? state.RequesterGeneration : state.RecipientGeneration;

    private sealed class EndedInteractionCleanupData
    {
        private readonly WorldServerFrameHandler frame;
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyOrdinarySpellBinding binding, ended;
        private readonly WorldSession expected;
        private readonly InvitationSlot slot;
        private readonly InvitationState state;
        private readonly long before, after;
        private bool consumed;
        internal WorldSession Proposed { get; }
        internal EndedInteractionCleanupData(WorldServerFrameHandler frame, WorldSessionStore store, WorldPacketHandlerContext context,
            LegacyOrdinarySpellBinding binding, WorldSession expected, InvitationSlot slot, InvitationState state,
            LegacyOrdinarySpellBinding ended)
        {
            this.frame = frame; this.store = store; this.context = context; this.binding = binding;
            this.expected = expected; this.slot = slot; this.state = state; this.ended = ended;
            before = slot.Generation; after = checked(before + 1);
            Proposed = expected with { InteractionPartnerUid = null, ActiveInteractionDance = 0 };
        }
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding actual, WorldSession current) => !consumed &&
            ReferenceEquals(actual, binding) && ReferenceEquals(current, expected) && frame.IsActionLifecycleCurrent(store, context) &&
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
            store.IsSpellFileWriteOwnerCurrent(binding.Connection, expected, binding.NativeIdentity, binding) &&
            frame.interactionInvitations.TryGetValue(binding.Connection, out var actualSlot) && ReferenceEquals(slot, actualSlot) &&
            slot.State is null && ReferenceEquals(slot.RetiredPair, state) && slot.Generation == before &&
            before == GenerationFor(state, binding) && state.Phase == InvitationPaired && Volatile.Read(ref state.Retired) != 0 &&
            ReferenceEquals(state.RetiredEndpoint, ended) && ended.IsRetired &&
            expected.InteractionPartnerUid == ended.Uid && expected.ActiveInteractionDance == state.Dance;
        internal void PublishPrepared()
        {
            slot.Generation = after;
            slot.RetiredPair = null;
            consumed = true;
        }
    }
}
