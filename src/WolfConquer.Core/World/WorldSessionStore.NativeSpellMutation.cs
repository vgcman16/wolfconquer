using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal NativeSpellMutationLease EnterBoundSpellMutation(LegacyOrdinarySpellBinding binding) => new(this, binding);

    // Native typed writers enter this real holder gate BEFORE their actor gates.
    // It accepts no persistence/acceptance callback and never publishes packets.
    internal sealed class NativeSpellMutationLease : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly SessionHolder holder;
        private readonly WorldSession expected;
        private readonly object gate;
        private readonly IDisposable packageScope;
        private bool entered, gateHeld, active, disposed;
        private WorldSession? prepared;
        internal WorldSession FinalSession => prepared ?? throw InvalidSpellPublication();
        internal WorldSession ExpectedSession => expected;
        internal PlayerProficiencySnapshot ExpectedProficiencies => expected.PlayerProficiencies;

        internal NativeSpellMutationLease(WorldSessionStore store, LegacyOrdinarySpellBinding binding)
        {
            this.store = store; this.binding = binding;
            // This check precedes package and connection acquisition, avoiding
            // actor -> Store inversion even when a real comparer calls back.
            if (!ReferenceEquals(binding.Store, store) || binding.IsRetired ||
                IsCurrentThreadInsideAnyConnectionGate || LegacyRuneRankingState.IsCurrentThreadInsideLease ||
                Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
                Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                Monitor.IsEntered(binding.Storage.MutationGate) ||
                Monitor.IsEntered(binding.Proficiencies.MutationGate) ||
                LegacyNativeNinja.HasActiveOwnedPreparation(binding.Client)) throw InvalidSpellPublication();
            packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages,
                store.PeekMailboxSession(binding.Connection));
            try
            {
                if (!store._connectionGates.TryGetValue(binding.Connection, out gate!)) throw InvalidSpellPublication();
                Monitor.Enter(gate, ref gateHeld);
                _enteredConnectionGateDepth = checked(_enteredConnectionGateDepth + 1); entered = true;
                if (!store._sessions.TryGetValue(binding.Connection, out holder!) || holder.Read() is not { } current ||
                    !store.BindingIsCurrentPlain(binding, holder, current)) throw InvalidSpellPublication();
                expected = current;
                if (!LegacyMailboxPackageMutationFence.CanMutateSession(store.MailboxPackages, expected, expected))
                    throw InvalidSpellPublication();
                active = true;
            }
            catch { Dispose(); throw; }
        }

        internal LegacyNativeOwnedItemGraph.TrainingWeaponsLease PrepareTrainingWeapons()
        {
            if (!HasTrainingOwnerPlain(binding, binding.Items))
                throw InvalidSpellPublication();
            return binding.Items.CaptureTrainingWeapons(this, binding);
        }

        // A graph projection cannot mint Store authority from a UID or supplied
        // session. Only this exact still-active holder capability can issue it.
        internal bool HasTrainingOwnerPlain(
            LegacyOrdinarySpellBinding expectedBinding, LegacyNativeOwnedItemGraph graph) =>
            active && !disposed && gateHeld && Monitor.IsEntered(gate) &&
            ReferenceEquals(binding, expectedBinding) && ReferenceEquals(binding.Items, graph) &&
            ReferenceEquals(expected.PlayerItems, graph.Image.FinalSnapshot) &&
            store.BindingIsCurrentPlain(binding, holder, expected);

        internal void PreparePublication(PlayerSpellSnapshot snapshot)
        {
            if (!active || prepared is not null || !store.BindingIsCurrentPlain(binding, holder, expected))
                throw InvalidSpellPublication();
            var frozen = LegacyNativeSpellStorage.FreezeSessionSnapshot(snapshot);
            prepared = ReferenceEquals(frozen, expected.PlayerSpells) ? expected : expected with { PlayerSpells = frozen };
            if (!IsCurrentPlain()) throw InvalidSpellPublication();
        }

        internal void PreparePublication(PlayerSpellSnapshot snapshot, PlayerProficiencySnapshot proficiencies)
        {
            if (!active || prepared is not null || !store.BindingIsCurrentPlain(binding, holder, expected))
                throw InvalidSpellPublication();
            var frozenSpells = LegacyNativeSpellStorage.FreezeSessionSnapshot(snapshot);
            var frozenProficiencies = LegacyNativeProficiencyStorage.FreezeSessionSnapshot(proficiencies);
            prepared = ReferenceEquals(frozenSpells, expected.PlayerSpells) &&
                ReferenceEquals(frozenProficiencies, expected.PlayerProficiencies) ? expected :
                expected with { PlayerSpells = frozenSpells, PlayerProficiencies = frozenProficiencies };
            if (!IsCurrentPlain()) throw InvalidSpellPublication();
        }

        internal bool IsCurrentPlain() => active && prepared is not null && Monitor.IsEntered(gate) &&
            store.BindingIsCurrentPlain(binding, holder, expected);

        // The native writer has finished its own current-map/rank/frame proof.
        // These stores have no allocation, comparer or external publication.
        internal void PublishPrepared()
        {
            binding.PublishSnapshots(prepared!.PlayerSpells, prepared.PlayerProficiencies);
            holder.PublishPrepared(prepared, binding.NativeIdentity);
        }

        internal LegacyPreparedSpellFileWrite PrepareSpellFileWrite() => store.PrepareSpellFileWrite(binding.Connection,
            expected, binding.Recovery, binding.UsersRoot, binding.SpellsRoot);

        internal void FailCommittedPublication() => store.FailCommittedSpellPublication(holder, binding);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            active = false;
            if (entered) { entered = false; _enteredConnectionGateDepth--; }
            if (gateHeld) { gateHeld = false; Monitor.Exit(gate); }
            packageScope.Dispose();
        }
    }
}
