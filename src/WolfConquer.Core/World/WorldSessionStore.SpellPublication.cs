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
        private readonly LegacyNativePlayerRuntime.OwnedHitpointsPublication? hitpoints;
        private readonly LegacyNativeEquipItemStorage.OwnedAlternatePublication? alternateEquipment;
        private readonly LegacyPreparedSpellFileWrite.RuneCharacterPublication? runePublication;
        private LegacyNativePlayerRuntime.OwnedMoneyPublication? money;
        private LegacyNativePlayerRuntime.OwnedCpsPublication? cps;
        private LegacyNativePlayerRuntime.OwnedWarehouseMoneyPublication? warehouseMoney;
        internal LegacyBoundHitpointsScalarWrite? HitpointsWrite { get; }
        internal bool HasCommittedHitpoints => HitpointsWrite?.IsCommitted == true;
        private readonly SpellActorScope? actors;
        private readonly LegacyNativeSpellStorage.PreparedSessionSpellMap? map;
        private readonly LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap? proficiencies;
        private LegacyOwnedNativeItemImage.Preparation? itemImage;
        private LegacyNativeOwnedItemGraph.PreparedPublication? items;
        private readonly bool deferItems;
        private bool deferredItemsPrepared;
        internal WorldSession FinalSession { get; private set; }
        internal LegacyOrdinarySpellBinding? Binding => binding;
        internal object PublishedIdentity => finalIdentity;

        internal PreparedSessionSpellPublication(WorldSessionStore store, ConquerTcpConnection connection,
            SessionHolder holder, WorldSession? expected, WorldSession proposed,
            WorldPacketHandlers.LegacyCapriceCharacterFields? caprice = null, bool deferItems = false,
            LegacyPreparedSpellFileWrite.RuneCharacterPublication? runePublication = null)
        {
            this.store = store; this.connection = connection; this.holder = holder; this.expected = expected;
            this.deferItems = deferItems;
            this.runePublication = runePublication;
            if (runePublication is not null)
            {
                if (expected is null || caprice is not null || deferItems) throw InvalidSpellPublication();
                runePublication.RequireCurrent(store, connection, expected, proposed);
            }
            binding = holder.SpellBinding;
            finalIdentity = expected is not null && expected.AccountId == proposed.AccountId &&
                expected.Character.Uid == proposed.Character.Uid ? holder.NativeClientIdentity : new object();
            if (binding is null)
            {
                // A retired/foreign attachment is never an unbound fallback.
                if (connection.NativeGameClient?.OrdinarySpellBinding is not null ||
                    proposed.NativeGameClient?.OrdinarySpellBinding is not null ||
                    connection.NativeGameClient?.Player?.HasOwnedVitalsAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedVitalsAttachment == true ||
                    connection.NativeGameClient?.Player?.HasOwnedMoneyAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedMoneyAttachment == true ||
                    connection.NativeGameClient?.Player?.HasOwnedCpsAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedCpsAttachment == true ||
                    connection.NativeGameClient?.Player?.HasOwnedWarehouseMoneyAttachment == true ||
                    proposed.NativeGameClient?.Player?.HasOwnedWarehouseMoneyAttachment == true) throw InvalidSpellPublication();
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
                binding.Items.RequireCurrentBeforePreparation();
                alternateEquipment = LegacyNativeEquipItemStorage.OwnedAlternatePublication.PrepareBound(binding, expected, proposed);
                map = LegacyNativeSpellStorage.PrepareSessionSnapshot(binding.Client, binding.Player,
                    expected.PlayerSpells, proposed.PlayerSpells);
                proficiencies = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(binding.Client, binding.Player,
                    expected.PlayerProficiencies, proposed.PlayerProficiencies);
                var final = ReferenceEquals(proposed.PlayerSpells, map.FinalSnapshot) &&
                    ReferenceEquals(proposed.PlayerProficiencies, proficiencies.FinalSnapshot) ? proposed :
                    proposed with { PlayerSpells = map.FinalSnapshot, PlayerProficiencies = proficiencies.FinalSnapshot };
                if (!deferItems)
                {
                    PrepareItems(final);
                    if (!ReferenceEquals(final.PlayerItems, itemImage!.FinalSnapshot))
                        final = final with { PlayerItems = itemImage.FinalSnapshot };
                }
                hitpoints = LegacyNativePlayerRuntime.OwnedHitpointsPublication.PrepareBound(binding, expected, final);
                if (expected.Character.MinHitPoints != final.Character.MinHitPoints ||
                    expected.Character.MinMana != final.Character.MinMana || caprice is not null)
                    HitpointsWrite = (store.framePersistenceOwner ?? throw InvalidSpellPublication())
                        .PrepareNativeHitpointsWrite(store, connection, binding, expected, final, caprice, runePublication);
                FinalSession = HitpointsWrite?.Proposed ?? final;
                money = LegacyNativePlayerRuntime.OwnedMoneyPublication.PrepareBound(binding, expected, FinalSession);
                cps = LegacyNativePlayerRuntime.OwnedCpsPublication.PrepareBound(binding, expected, FinalSession);
                warehouseMoney = LegacyNativePlayerRuntime.OwnedWarehouseMoneyPublication.PrepareBound(binding, expected, FinalSession);
                RequireCurrentBeforeWriter();
            }
            catch { items?.Dispose(); actors.Dispose(); throw; }
        }

        private void PrepareItems(WorldSession proposed)
        {
            if (binding is null || items is not null) throw InvalidSpellPublication();
            binding.Items.RequireCurrentBeforePreparation();
            var context = (store.framePersistenceOwner ?? throw InvalidSpellPublication()).RequireOwnedNativeItemContext();
            if (!ReferenceEquals(context.Catalog, binding.ItemCatalog) || context.Profile != binding.ItemProfile)
                throw InvalidSpellPublication();
            itemImage = LegacyOwnedNativeItemImage.Prepare(context.Catalog, proposed.PlayerItems,
                context.Profile, binding.Items.Image);
            if (!ReferenceEquals(itemImage.SourceSnapshot, proposed.PlayerItems) ||
                !ReferenceEquals(itemImage.BaseImage, binding.Items.Image)) throw InvalidSpellPublication();
            items = LegacyNativeOwnedItemGraph.PrepareRefresh(binding.Items, itemImage);
            if (!ReferenceEquals(items.Graph, binding.Items) ||
                !ReferenceEquals(items.FinalSnapshot, itemImage.FinalSnapshot)) throw InvalidSpellPublication();
        }

        // Only the actual Store-issued mailbox capability calls this before its
        // real joint writer. Spell/vital ownership cannot be replaced by it.
        internal WorldSession PrepareDeferredItems(WorldSession proposed)
        {
            if (!deferItems || deferredItemsPrepared || expected is null ||
                !ReferenceEquals(holder.Read(), expected) ||
                proposed.AccountId != expected.AccountId || proposed.Character.Uid != expected.Character.Uid ||
                proposed.Character.MinHitPoints != expected.Character.MinHitPoints || proposed.Character.MinMana != expected.Character.MinMana ||
                !ReferenceEquals(proposed.PlayerSpells, expected.PlayerSpells) ||
                !ReferenceEquals(proposed.PlayerProficiencies, expected.PlayerProficiencies) ||
                !ReferenceEquals(proposed.NativeGameClient, expected.NativeGameClient) ||
                !string.Equals(proposed.UsersRoot, expected.UsersRoot, StringComparison.Ordinal)) throw InvalidSpellPublication();
            deferredItemsPrepared = true;
            if (binding is not null)
            {
                PrepareItems(proposed);
                proposed = proposed with { PlayerItems = itemImage!.FinalSnapshot,
                    PlayerSpells = map!.FinalSnapshot, PlayerProficiencies = proficiencies!.FinalSnapshot };
                money = LegacyNativePlayerRuntime.OwnedMoneyPublication.PrepareBound(binding, expected, proposed);
                cps = LegacyNativePlayerRuntime.OwnedCpsPublication.PrepareBound(binding, expected, proposed);
                warehouseMoney = LegacyNativePlayerRuntime.OwnedWarehouseMoneyPublication.PrepareBound(binding, expected, proposed);
            }
            FinalSession = proposed;
            RequireCurrentBeforeWriter();
            return FinalSession;
        }

        // The actual deferred-mail capability calls this after recipe/catalog
        // preparation and before its producer can reserve any item identities.
        internal void RequireCurrenciesBeforeReservation(WorldSession proposed)
        {
            if (!deferItems || deferredItemsPrepared || expected is null ||
                !ReferenceEquals(holder.Read(), expected) || proposed.AccountId != expected.AccountId ||
                proposed.Character.Uid != expected.Character.Uid ||
                !ReferenceEquals(proposed.NativeGameClient, expected.NativeGameClient) ||
                !string.Equals(proposed.UsersRoot, expected.UsersRoot, StringComparison.Ordinal))
                throw InvalidSpellPublication();
            RequireCurrentBeforeWriter();
            if (binding is null) return; // Wholly managed path has no fabricated native limits/owner.
            LegacyNativePlayerRuntime.OwnedCpsPublication.RequireRepresentable(proposed.Character);
            var proposedWarehouseMoney = new LegacyWarehouseMoneyState.NativeValueProof(proposed.Character);
            // No callback-bearing read follows this complete native tuple proof.
            if (!store.BindingIsCurrentPlain(binding, holder, expected) ||
                !money!.IsCurrentPlain() || !cps!.IsCurrentPlain() || !warehouseMoney!.IsCurrentPlain() ||
                !proposedWarehouseMoney.IsCurrentPlain()) throw InvalidSpellPublication();
        }

        internal void RequireCurrentBeforeWriter()
        {
            if (!ReferenceEquals(holder.Read(), expected) || binding is not null &&
                !store.BindingIsCurrentPlain(binding, holder, expected!)) throw InvalidSpellPublication();
            map?.RequireCurrentBeforeWriter();
            proficiencies?.RequireCurrentBeforeWriter();
            items?.RequireCurrentBeforeWriter();
            if (hitpoints is not null && !hitpoints.IsCurrentPlain() || money is not null && !money.IsCurrentPlain() ||
                cps is not null && !cps.IsCurrentPlain() || warehouseMoney is not null && !warehouseMoney.IsCurrentPlain() ||
                alternateEquipment is not null && !alternateEquipment.IsCurrentPlain(FinalSession.AlternateEquipmentActive))
                throw InvalidSpellPublication();
            if (binding is not null && !store.BindingIsCurrentPlain(binding, holder, expected!)) throw InvalidSpellPublication();
        }

        internal void ArmFinalProof()
        {
            if (deferItems && !deferredItemsPrepared) throw InvalidSpellPublication();
            map?.ArmFinalProof();
            proficiencies?.ArmFinalProof();
            items?.ArmFinalProof();
            HitpointsWrite?.Inputs.Arm();
        }
        internal bool ConsumeBeforeWriterPlain() => ReferenceEquals(holder.Read(), expected) &&
            (binding is null || store.BindingIsCurrentPlain(binding, holder, expected!) && map!.ConsumeBeforeWriterPlain() &&
                proficiencies!.ConsumeBeforeWriterPlain() && items!.ConsumeBeforeWriterPlain() && hitpoints!.IsCurrentPlain() &&
                money!.IsCurrentPlain() && cps!.IsCurrentPlain() && warehouseMoney!.IsCurrentPlain() && alternateEquipment!.IsCurrentPlain(FinalSession.AlternateEquipmentActive) &&
                (HitpointsWrite is null || HitpointsWrite.Inputs.ConsumeBeforePlain()));

        // The raw map enumerator was allocated before the writer. Consume it
        // once, without a comparer or a fresh iterator at this final boundary.
        internal bool IsCurrentPlain() => ReferenceEquals(holder.Read(), expected) &&
            (binding is null || store.BindingIsCurrentPlain(binding, holder, expected!) && map!.ConsumeCurrentPlain() &&
                proficiencies!.ConsumeCurrentPlain() && items!.ConsumeCurrentPlain() && hitpoints!.IsCurrentPlain() &&
                money!.IsCurrentPlain() && cps!.IsCurrentPlain() && warehouseMoney!.IsCurrentPlain() && alternateEquipment!.IsCurrentPlain(FinalSession.AlternateEquipmentActive) &&
                (HitpointsWrite is null || HitpointsWrite.IsCommitted && HitpointsWrite.Inputs.ConsumeAfterPlain()));

        // Call only after the final plain proof and actual successful writer.
        internal void PublishNative()
        {
            if (binding is null) return;
            map!.PublishPreparedMap();
            proficiencies!.PublishPreparedMap();
            items!.PublishPrepared();
            alternateEquipment!.PublishPrepared();
            binding.PublishSnapshots(FinalSession.PlayerSpells, FinalSession.PlayerProficiencies);
            binding.PublishItemSnapshot(FinalSession.PlayerItems);
            hitpoints!.PublishPrepared();
            money!.PublishPrepared();
            cps!.PublishPrepared();
            warehouseMoney!.PublishPrepared();
        }

        internal bool AcceptExternallyPersistedHitpoints() => HitpointsWrite is not null
            ? HitpointsWrite.RequireExternallyCommitted() : runePublication?.RequireCommittedCharacter() ?? true;

        internal void PublishHolder() => holder.PublishPrepared(FinalSession, finalIdentity);

        public void Dispose()
        {
            items?.Dispose();
            actors?.Dispose();
        }
    }

    private bool BindingIsCurrentPlain(LegacyOrdinarySpellBinding binding, SessionHolder holder, WorldSession expected) =>
        ReferenceEquals(binding.Store, this) && _sessions.TryGetValue(binding.Connection, out var actual) &&
        ReferenceEquals(actual, holder) && ReferenceEquals(holder.SpellBinding, binding) &&
        ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) && ReferenceEquals(holder.Read(), expected) &&
        expected.AccountId == binding.AccountId && expected.Character.Uid == binding.Uid &&
        string.Equals(expected.UsersRoot, binding.SessionUsersRoot, StringComparison.Ordinal) &&
        ReferenceEquals(expected.NativeGameClient, binding.Client) && ReferenceEquals(expected.PlayerSpells, binding.Snapshot) &&
        ReferenceEquals(expected.PlayerProficiencies, binding.ProficiencySnapshot) &&
        ReferenceEquals(expected.PlayerItems, binding.ItemSnapshot) &&
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
