using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class PreparedSessionSpellPublication : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly SessionHolder holder;
        private readonly WorldSession? expected;
        private readonly LegacyOrdinarySpellBinding? binding;
        private readonly object finalIdentity;
        private readonly SpellActorScope? actors;
        private readonly LegacyNativeSpellStorage.PreparedSessionSpellMap? map;
        private readonly LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap? proficiencies;
        internal WorldSession FinalSession { get; }
        internal LegacyOrdinarySpellBinding? Binding => binding;
        internal object PublishedIdentity => finalIdentity;

        internal PreparedSessionSpellPublication(WorldSessionStore store, ConquerTcpConnection connection,
            SessionHolder holder, WorldSession? expected, WorldSession proposed)
        {
            this.store = store; this.connection = connection; this.holder = holder; this.expected = expected;
            binding = holder.SpellBinding;
            finalIdentity = expected is not null && expected.AccountId == proposed.AccountId &&
                expected.Character.Uid == proposed.Character.Uid ? holder.NativeClientIdentity : new object();
            if (binding is null)
            {
                // A retired/foreign attachment is never an unbound fallback.
                if (connection.NativeGameClient?.OrdinarySpellBinding is not null ||
                    proposed.NativeGameClient?.OrdinarySpellBinding is not null) throw InvalidSpellPublication();
                FinalSession = proposed;
                return;
            }
            if (expected is null || proposed.AccountId != binding.AccountId || proposed.Character.Uid != binding.Uid ||
                !string.Equals(proposed.UsersRoot, binding.SessionUsersRoot, StringComparison.Ordinal) ||
                !ReferenceEquals(proposed.NativeGameClient, binding.Client) ||
                !store.BindingIsCurrentPlain(binding, holder, expected)) throw InvalidSpellPublication();
            actors = new(binding);
            try
            {
                map = LegacyNativeSpellStorage.PrepareSessionSnapshot(binding.Client, binding.Player,
                    expected.PlayerSpells, proposed.PlayerSpells);
                proficiencies = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(binding.Client, binding.Player,
                    expected.PlayerProficiencies, proposed.PlayerProficiencies);
                FinalSession = ReferenceEquals(proposed.PlayerSpells, map.FinalSnapshot) &&
                    ReferenceEquals(proposed.PlayerProficiencies, proficiencies.FinalSnapshot) ? proposed :
                    proposed with { PlayerSpells = map.FinalSnapshot, PlayerProficiencies = proficiencies.FinalSnapshot };
                RequireCurrentBeforeWriter();
            }
            catch { actors.Dispose(); throw; }
        }

        internal void RequireCurrentBeforeWriter()
        {
            if (!ReferenceEquals(holder.Read(), expected) || binding is not null &&
                !store.BindingIsCurrentPlain(binding, holder, expected!)) throw InvalidSpellPublication();
            map?.RequireCurrentBeforeWriter();
            proficiencies?.RequireCurrentBeforeWriter();
            if (binding is not null && !store.BindingIsCurrentPlain(binding, holder, expected!)) throw InvalidSpellPublication();
        }

        internal void ArmFinalProof()
        {
            map?.ArmFinalProof();
            proficiencies?.ArmFinalProof();
        }
        internal bool ConsumeBeforeWriterPlain() => ReferenceEquals(holder.Read(), expected) &&
            (binding is null || store.BindingIsCurrentPlain(binding, holder, expected!) && map!.ConsumeBeforeWriterPlain() &&
                proficiencies!.ConsumeBeforeWriterPlain());

        // The raw map enumerator was allocated before the writer. Consume it
        // once, without a comparer or a fresh iterator at this final boundary.
        internal bool IsCurrentPlain() => ReferenceEquals(holder.Read(), expected) &&
            (binding is null || store.BindingIsCurrentPlain(binding, holder, expected!) && map!.ConsumeCurrentPlain() &&
                proficiencies!.ConsumeCurrentPlain());

        // Call only after the final plain proof and actual successful writer.
        internal void PublishNative()
        {
            if (binding is null) return;
            map!.PublishPreparedMap();
            proficiencies!.PublishPreparedMap();
            binding.PublishSnapshots(FinalSession.PlayerSpells, FinalSession.PlayerProficiencies);
        }

        internal void PublishHolder() => holder.PublishPrepared(FinalSession, finalIdentity);

        public void Dispose() => actors?.Dispose();
    }

    private bool BindingIsCurrentPlain(LegacyOrdinarySpellBinding binding, SessionHolder holder, WorldSession expected) =>
        ReferenceEquals(binding.Store, this) && _sessions.TryGetValue(binding.Connection, out var actual) &&
        ReferenceEquals(actual, holder) && ReferenceEquals(holder.SpellBinding, binding) &&
        ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) && ReferenceEquals(holder.Read(), expected) &&
        expected.AccountId == binding.AccountId && expected.Character.Uid == binding.Uid &&
        string.Equals(expected.UsersRoot, binding.SessionUsersRoot, StringComparison.Ordinal) &&
        ReferenceEquals(expected.NativeGameClient, binding.Client) && ReferenceEquals(expected.PlayerSpells, binding.Snapshot) &&
        ReferenceEquals(expected.PlayerProficiencies, binding.ProficiencySnapshot) &&
        binding.PhysicalOwnerIsCurrentPlain();

    internal bool IsBoundSpellOwnerCurrent(LegacyOrdinarySpellBinding binding) =>
        _sessions.TryGetValue(binding.Connection, out var holder) && holder.Read() is { } current &&
        BindingIsCurrentPlain(binding, holder, current);

    private static InvalidOperationException InvalidSpellPublication() =>
        new("ordinarySpellPublicationRequiresCurrentCompleteOwner");

    private static void RefuseBoundSpellReentry(ConquerTcpConnection connection)
    {
        // A comparer/serializer on any holder may not acquire another Store
        // writer gate, including an otherwise unbound target connection.
        if (IsCurrentThreadInsideAnyConnectionGate || LegacyRuneRankingState.IsCurrentThreadInsideLease)
            throw InvalidSpellPublication();
        if (connection.NativeGameClient is { } native &&
            (Monitor.IsEntered(native.SpellAdmissionSyncRoot) ||
             native.Player is { } player && Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
             native.MySpells is { } storage && Monitor.IsEntered(storage.MutationGate) ||
             native.MyProfs is { } profs && Monitor.IsEntered(profs.MutationGate) ||
             native.OrdinarySpellBinding is { } binding &&
                (Monitor.IsEntered(binding.Storage.MutationGate) || Monitor.IsEntered(binding.Proficiencies.MutationGate)) ||
             LegacyNativeNinja.HasActiveOwnedPreparation(native))) throw InvalidSpellPublication();
    }

    private void FailCommittedSpellPublication(SessionHolder holder, LegacyOrdinarySpellBinding? binding)
    {
        binding?.Recovery.Block("ordinarySpellRecoveryRequired:durableCommitPublicationUnproven");
        holder.Clear();
        throw new LegacyCommittedSpellPublicationException();
    }
}
