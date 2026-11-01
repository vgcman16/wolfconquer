namespace WolfConquer.Core.World;

internal static partial class LegacyNativePerfectionClient
{
    internal sealed class OwnedPhysicalDeathPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation;
        private readonly ClientState? state;
        private readonly bool admitted, beforePublished;
        private readonly uint beforePrestige, afterPrestige;
        private readonly LegacyNativePerfectionSnapshot? beforeSnapshot, afterSnapshot;
        private bool published;

        private OwnedPhysicalDeathPublication(LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation,
            WorldPacketHandlers.OwnedPhysicalDeathQueryValues values)
        {
            this.binding = binding; this.preparation = preparation; admitted = values.Admitted;
            if (admitted)
            {
                ArgumentNullException.ThrowIfNull(values.Perfection);
                // A shell grants no old read/mutation authority. Its private
                // Published flag stays false until the real Store commit.
                state = Clients.GetValue(binding.NativeIdentity, static _ => new ClientState());
                afterPrestige = values.PerfectionPrestige; afterSnapshot = values.Perfection;
            }
            else
            {
                Clients.TryGetValue(binding.NativeIdentity, out var existing);
                state = existing;
            }
            if (state is not null)
            {
                beforePublished = state.Published;
                beforePrestige = state.PrestigeLevel;
                beforeSnapshot = state.Snapshot;
            }
        }

        internal static OwnedPhysicalDeathPublication Prepare(LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonicalTarget)
        {
            ArgumentNullException.ThrowIfNull(binding);
            ArgumentNullException.ThrowIfNull(preparation);
            ArgumentNullException.ThrowIfNull(canonicalTarget);
            if (!DeathPerfectionOwnerCurrentPlain(binding) || !preparation.IsCurrentPlain(binding) ||
                !preparation.TryReadNativeQueryProposal(binding, canonicalTarget, out var values)) throw InvalidDeathPerfection();
            var result = new OwnedPhysicalDeathPublication(binding, preparation, values);
            if (!result.IsCurrentPlain()) throw InvalidDeathPerfection();
            return result;
        }

        internal bool IsCurrentPlain()
        {
            if (published || !DeathPerfectionOwnerCurrentPlain(binding) || !preparation.IsCurrentPlain(binding)) return false;
            if (state is null) return !Clients.TryGetValue(binding.NativeIdentity, out _);
            return Clients.TryGetValue(binding.NativeIdentity, out var actual) && ReferenceEquals(actual, state) &&
                state.Published == beforePublished && state.PrestigeLevel == beforePrestige &&
                ReferenceEquals(state.Snapshot, beforeSnapshot);
        }

        internal bool IsPublishedCurrentPlain()
        {
            if (!published || !DeathPerfectionOwnerCurrentPlain(binding)) return false;
            if (state is null) return !Clients.TryGetValue(binding.NativeIdentity, out _);
            return Clients.TryGetValue(binding.NativeIdentity, out var actual) && ReferenceEquals(actual, state) &&
                state.Published == (admitted || beforePublished) &&
                state.PrestigeLevel == (admitted ? afterPrestige : beforePrestige) &&
                ReferenceEquals(state.Snapshot, admitted ? afterSnapshot : beforeSnapshot);
        }

        // The sole caller is the outer query participant after the actual
        // Store's final proof. No callbacks, allocations or cache lookup here.
        internal void PublishPrepared()
        {
            if (admitted)
            {
                state!.PrestigeLevel = afterPrestige;
                state.Snapshot = afterSnapshot!;
                state.Published = true;
            }
            published = true;
        }
    }

    private static bool DeathPerfectionOwnerCurrentPlain(LegacyOrdinarySpellBinding binding) =>
        WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
        Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
        !binding.IsRetired && ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) &&
        ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, binding.Player) &&
        ReferenceEquals(binding.Player.Owner, binding.Client) && binding.Player.UID == binding.Uid &&
        binding.Store.IsBoundSpellOwnerCurrent(binding);

    private static InvalidOperationException InvalidDeathPerfection() => new("ordinaryPhysicalDeathPerfectionOwnerChanged");
}
