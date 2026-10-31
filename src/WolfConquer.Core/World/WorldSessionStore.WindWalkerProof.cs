using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Opaque native transition acceptance. Only the actual held Store/actors,
    // completed query (or due-only expiry token) can produce a current proof.
    internal sealed class WindWalkerPublicationProof
    {
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly SessionHolder holder;
        private readonly WorldSession expected;
        private readonly object identity;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private readonly LegacyNativeStatusFlagsBigVector32.WindWalkerChange change;
        private readonly LegacyNativeMagicQueryClient.WindWalkerQuery? query;
        private readonly JoinedWindWalker? joined;
        private readonly LegacyEquipmentQueryPublicationScope? scope;
        private readonly WorldPacketHandlers.WindWalkerStatusInputs inputs;
        private readonly WorldSession proposed;
        private readonly WindWalkerPublication notification;

        private WindWalkerPublicationProof(WorldSessionStore store, WorldPacketHandlerContext context,
            SessionHolder holder, WorldSession expected, object identity, LegacyOrdinarySpellBinding binding,
            LegacyNativeStatusFlagsBigVector32 vector, LegacyNativeStatusFlagsBigVector32.WindWalkerChange change,
            LegacyNativeMagicQueryClient.WindWalkerQuery? query, LegacyEquipmentQueryPublicationScope? scope,
            WorldPacketHandlers.WindWalkerStatusInputs inputs, WorldSession proposed, WindWalkerPublication notification)
        {
            this.store = store; this.context = context; this.holder = holder; this.expected = expected;
            this.identity = identity; this.binding = binding; this.vector = vector; this.change = change;
            this.query = query; this.scope = scope;
            this.inputs = inputs;
            this.proposed = proposed; this.notification = notification;
            joined = WindWalkerOwners.TryGetValue(binding.Player, out var slot) ? slot.Owner : null;
        }

        internal static WindWalkerPublicationProof Capture(WorldSessionStore store, WorldPacketHandlerContext context,
            WorldSession expected, object identity, LegacyNativeStatusFlagsBigVector32.WindWalkerChange change,
            LegacyNativeMagicQueryClient.WindWalkerQuery? query, LegacyEquipmentQueryPublicationScope? scope,
            WorldPacketHandlers.WindWalkerStatusInputs inputs, WorldSession proposed, WindWalkerPublication notification)
        {
            if (!store._connectionGates.TryGetValue(context.Connection, out var gate) || !Monitor.IsEntered(gate) ||
                !store._sessions.TryGetValue(context.Connection, out var holder) || holder.SpellBinding is not { } binding)
                throw InvalidSpellPublication();
            var proof = new WindWalkerPublicationProof(store, context, holder, expected, identity, binding,
                binding.Player.BitVector, change, query, scope, inputs, proposed, notification);
            if (!proof.IsCurrentPlain(binding.Player.BitVector, change)) throw InvalidSpellPublication();
            return proof;
        }

        internal bool IsCurrentPlain(LegacyNativeStatusFlagsBigVector32 actual,
            LegacyNativeStatusFlagsBigVector32.WindWalkerChange actualChange)
        {
            if (!ReferenceEquals(vector, actual) || !ReferenceEquals(change, actualChange) ||
                !Monitor.IsEntered(vector.SyncRoot) || !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
                !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !store._connectionGates.TryGetValue(context.Connection, out var gate) || !Monitor.IsEntered(gate) ||
                !store._sessions.TryGetValue(context.Connection, out var actualHolder) || !ReferenceEquals(actualHolder, holder) ||
                !ReferenceEquals(holder.Read(), expected) || !ReferenceEquals(holder.NativeClientIdentity, identity) ||
                !store.BindingIsCurrentPlain(binding, holder, expected) || !binding.Items.Image.IsCurrentPlain() ||
                !ReferenceEquals(binding.Player.BitVector, vector) || !ReferenceEquals(binding.Player.Owner, binding.Client) ||
                binding.Player.View is not { } view || !ReferenceEquals(view.Owner, binding.Client) || !inputs.IsCurrentPlain() ||
                store.framePersistenceOwner?.IsWindWalkerContext(store, context, expected) != true) return false;
            if (query is not null)
                return !vector.IsWindWalkerExpiryChange(change) && scope is not null &&
                    scope.OwnerUid == expected.Character.Uid && scope.OwnerNativeClientIdentity is { } scopedIdentity &&
                    ReferenceEquals(scopedIdentity, identity) && RequiredParticipantCurrentPlain(scope) &&
                    (scope.ExpectedItems is null || ReferenceEquals(scope.ExpectedItems, expected.PlayerItems)) &&
                    LegacyNativeMagicQueryClient.TryGetWindWalkerDesiredPlain(query, store, context.Connection, identity, expected, out var desired) &&
                    change.Proposed.Present == desired;
            return vector.IsWindWalkerExpiryChange(change) && joined is not null &&
                WindWalkerBindingCurrentPlain(joined, binding.Player) &&
                WindWalkerOwners.TryGetValue(binding.Player, out var slot) && ReferenceEquals(slot.Owner, joined);
        }

        private bool RequiredParticipantCurrentPlain(LegacyEquipmentQueryPublicationScope captured)
        {
            if (captured.RequiredParticipant is not { } participant) return true;
            // Atomic holder reads only: never acquire another connection gate
            // while this actual owner's actor/vector/connection gates are held.
            return captured.RequiredNativeClientIdentity is { } requiredIdentity &&
                store._sessions.TryGetValue(participant.Connection, out var required) && required.Read() is { } current &&
                current.Character.Uid == participant.Session.Character.Uid &&
                ReferenceEquals(required.NativeClientIdentity, requiredIdentity);
        }

        internal bool IsCommittedPublication(WindWalkerPublication actual) => ReferenceEquals(notification, actual) &&
            Monitor.IsEntered(vector.SyncRoot) && vector.IsWindWalkerCommitted(change) &&
            ReferenceEquals(holder.Read(), proposed) && ReferenceEquals(holder.NativeClientIdentity, identity) &&
            store.BindingIsCurrentPlain(binding, holder, proposed) && inputs.IsCurrentPlain() &&
            actual.MatchesCommitted(store, context.Connection, identity, binding, vector, change.Proposed, proposed);
    }
}
