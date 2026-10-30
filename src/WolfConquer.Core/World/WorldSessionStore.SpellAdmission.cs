using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class PreparedOrdinarySpellAdmission : IDisposable
    {
        private readonly ConquerTcpConnection connection;
        private readonly SessionHolder holder;
        private readonly LegacyNativeSpellStorage.PreparedDetachedSessionSpells detached;
        private readonly LegacyNativeProficiencyStorage.PreparedDetachedSessionProficiencies detachedProficiencies;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner admissionOwner;
        private readonly LegacyNativePlayerRuntime.OwnedHitpointsPublication hitpoints;
        private readonly LegacyNativePlayerRuntime.OwnedMoneyPublication money;
        private readonly LegacyNativePlayerRuntime.OwnedCpsPublication cps;
        private readonly LegacyNativePlayerRuntime.OwnedWarehouseMoneyPublication warehouseMoney;
        private readonly LegacyOwnedNativeItemImage.Preparation itemImage;
        private readonly LegacyNativeOwnedItemGraph.PreparedPublication? items;
        private bool spellGateHeld, proficiencyGateHeld;
        internal WorldSession FinalSession { get; }

        internal PreparedOrdinarySpellAdmission(WorldSessionStore store, ConquerTcpConnection connection,
            SessionHolder holder, WorldSession proposed, LegacyTradeRecoveryState recovery, string? spellsRoot, byte[]? savedSpellImage,
            LegacyNativeExistingCharacterPrefix prefix)
        {
            this.connection = connection; this.holder = holder;
            if (!connection.HasSendRoute || !connection.HasDisconnectRoute) throw InvalidSpellPublication();
            var client = connection.NativeGameClient ?? throw InvalidSpellPublication();
            var player = client.Player ?? throw InvalidSpellPublication();
            if (string.IsNullOrWhiteSpace(spellsRoot) || !Path.IsPathFullyQualified(spellsRoot) ||
                string.IsNullOrWhiteSpace(proposed.UsersRoot) || !Path.IsPathFullyQualified(proposed.UsersRoot) ||
                !recovery.IsReadyForCharacterSpells(proposed.UsersRoot, spellsRoot)) throw InvalidSpellPublication();
            admissionOwner = prefix.PrepareSpellAdmissionOwner(connection, proposed);
            var canonicalSpells = Path.TrimEndingDirectorySeparator(Path.GetFullPath(spellsRoot));
            if (holder.Read() is not null || holder.SpellBinding is not null || proposed.AccountId <= 0 ||
                proposed.Character.Uid != admissionOwner.PreparedUid || !ReferenceEquals(proposed.NativeGameClient, client) ||
                !ReferenceEquals(player.Owner, client) || recovery.IsBlocked) throw InvalidSpellPublication();
            detached = LegacyNativeSpellStorage.PrepareDetachedSessionSnapshot(client, player, proposed.PlayerSpells, admissionOwner);
            detachedProficiencies = LegacyNativeProficiencyStorage.PrepareDetachedSessionSnapshot(client, player, proposed.PlayerProficiencies, admissionOwner);
            var context = (store.framePersistenceOwner ?? throw InvalidSpellPublication()).RequireOwnedNativeItemContext();
            itemImage = LegacyOwnedNativeItemImage.Prepare(context.Catalog, proposed.PlayerItems, context.Profile);
            if (!ReferenceEquals(itemImage.SourceSnapshot, proposed.PlayerItems) || itemImage.BaseImage is not null)
                throw InvalidSpellPublication();
            FinalSession = proposed with { PlayerSpells = detached.Plan.FinalSnapshot,
                PlayerProficiencies = detachedProficiencies.Plan.FinalSnapshot, PlayerItems = itemImage.FinalSnapshot };
            var savedRows = savedSpellImage is null ? WolfConquer.Core.Data.PlayerSpellSnapshot.Empty :
                WolfConquer.Core.Data.PlayerSpellSnapshotLoader.Parse(savedSpellImage);
            if (!WolfConquer.Core.Data.PlayerSpellSnapshotLoader.ToLegacyBytes(savedRows).AsSpan().SequenceEqual(
                    WolfConquer.Core.Data.PlayerSpellSnapshotLoader.ToLegacyBytes(FinalSession.PlayerSpells))) throw InvalidSpellPublication();
            binding = new(store, connection, holder.NativeClientIdentity, FinalSession, client, player,
                detached.Storage, detachedProficiencies.Storage, recovery, canonicalSpells, savedSpellImage);
            hitpoints = LegacyNativePlayerRuntime.OwnedHitpointsPublication.PrepareAdmission(binding, admissionOwner, FinalSession);
            money = LegacyNativePlayerRuntime.OwnedMoneyPublication.PrepareAdmission(binding, admissionOwner, FinalSession);
            cps = LegacyNativePlayerRuntime.OwnedCpsPublication.PrepareAdmission(binding, admissionOwner, FinalSession);
            warehouseMoney = LegacyNativePlayerRuntime.OwnedWarehouseMoneyPublication.PrepareAdmission(binding, admissionOwner, FinalSession);
            try
            {
                Monitor.Enter(detached.Storage.MutationGate, ref spellGateHeld);
                Monitor.Enter(detachedProficiencies.Storage.MutationGate, ref proficiencyGateHeld);
                items = LegacyNativeOwnedItemGraph.PrepareAdmission(client, player, admissionOwner.PreparedUid, itemImage);
                if (!ReferenceEquals(items.FinalSnapshot, FinalSession.PlayerItems)) throw InvalidSpellPublication();
                binding.InitializePreparedItems(items.Graph, context.Catalog, context.Profile, FinalSession.PlayerItems);
                RequireCurrentBeforeWriter();
            }
            catch { Dispose(); throw; }
        }

        private bool OwnerIsCurrentPlain() => holder.Read() is null && holder.SpellBinding is null && !binding.IsRetired &&
            !binding.Recovery.IsBlocked && ReferenceEquals(connection.NativeGameClient, binding.Client) &&
            ReferenceEquals(binding.Client.Player, binding.Player) && ReferenceEquals(binding.Player.Owner, binding.Client) &&
            admissionOwner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) && binding.Client.MySpells is null && binding.Client.MyProfs is null &&
            binding.Client.OrdinarySpellBinding is null && binding.Storage.OrdinaryBinding is null && ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) &&
            binding.Storage.CanAttachOrdinaryBinding(binding);

        internal void RequireCurrentBeforeWriter()
        {
            detached.Plan.RequireCurrentBeforeWriter();
            detachedProficiencies.Plan.RequireCurrentBeforeWriter();
            items!.RequireCurrentBeforeWriter();
            if (!OwnerIsCurrentPlain() || !hitpoints.IsCurrentPlain() || !money.IsCurrentPlain() || !cps.IsCurrentPlain() ||
                !warehouseMoney.IsCurrentPlain())
                throw InvalidSpellPublication();
        }

        internal bool ConsumeCurrentPlain() => hitpoints.IsCurrentPlain() && money.IsCurrentPlain() && cps.IsCurrentPlain() &&
            warehouseMoney.IsCurrentPlain() &&
            OwnerIsCurrentPlain() && admissionOwner.ConsumeCurrentPlain() &&
            detached.Plan.ConsumeCurrentPlain() &&
            detachedProficiencies.Plan.ConsumeCurrentPlain() && items!.ConsumeCurrentPlain();
        internal bool ArmPreparedProof()
        {
            detached.Plan.ArmFinalProof();
            detachedProficiencies.Plan.ArmFinalProof();
            items!.ArmFinalProof();
            admissionOwner.ArmAdmissionProof();
            return hitpoints.IsCurrentPlain() && money.IsCurrentPlain() && cps.IsCurrentPlain() && warehouseMoney.IsCurrentPlain() &&
                OwnerIsCurrentPlain() &&
                admissionOwner.ConsumeBeforeWriterPlain() && detached.Plan.ConsumeBeforeWriterPlain() &&
                detachedProficiencies.Plan.ConsumeBeforeWriterPlain() && items.ConsumeBeforeWriterPlain();
        }

        internal void PublishPrepared()
        {
            detached.Plan.PublishPreparedMap();
            detachedProficiencies.Plan.PublishPreparedMap();
            items!.PublishPrepared();
            binding.Client.MySpells = detached.Storage;
            binding.Client.MyProfs = detachedProficiencies.Storage;
            detached.Storage.AttachOrdinaryBinding(binding);
            holder.SpellBinding = binding;
            hitpoints.PublishPrepared();
            money.PublishPrepared();
            cps.PublishPrepared();
            warehouseMoney.PublishPrepared();
        }

        public void Dispose()
        {
            items?.Dispose();
            if (proficiencyGateHeld) { proficiencyGateHeld = false; Monitor.Exit(detachedProficiencies.Storage.MutationGate); }
            if (spellGateHeld) { spellGateHeld = false; Monitor.Exit(detached.Storage.MutationGate); }
        }
    }
}
