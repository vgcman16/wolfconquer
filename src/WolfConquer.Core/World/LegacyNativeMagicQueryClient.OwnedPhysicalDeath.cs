namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    // An inert prepared tuple, not a query completion or a replacement Entry.
    // The actual Store consumes its complete writer/final proof before the
    // sole plain publication, alongside its other native participants.
    internal sealed class OwnedPhysicalDeathPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation;
        private readonly WorldSession canonicalTarget;
        private readonly Entry entry;
        private readonly LegacyNativeMsgStatus status;
        private readonly uint beforeMaximumHitpoints, beforeMaximumMana, afterMaximumHitpoints, afterMaximumMana;
        private readonly WorldPacketHandlers.CombatFields beforeCombat, afterCombat;
        private readonly bool beforeInitialized, admitted;
        private readonly LegacyNativeMagicQueryFields beforeFields, beforeJiangHu, afterFields, afterJiangHu;
        private readonly QueryEpoch? beforeEpoch, afterEpoch;
        private readonly MonkCache? beforeMonk, afterMonk;
        private readonly AttackSpeedCache? beforeAttackSpeed, afterAttackSpeed;
        private readonly LegacyAttackSpeedMetadata? afterMetadata;
        private readonly LegacyNativePerfectionClient.OwnedPhysicalDeathPublication perfection;
        private bool published;

        private OwnedPhysicalDeathPublication(LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonicalTarget,
            WorldPacketHandlers.OwnedPhysicalDeathQueryValues values, Entry entry)
        {
            this.binding = binding; this.preparation = preparation; this.canonicalTarget = canonicalTarget; this.entry = entry;
            beforeInitialized = entry.Initialized; beforeFields = entry.Fields; beforeJiangHu = entry.JiangHu;
            beforeEpoch = entry.Epoch; beforeMonk = entry.Monk; beforeAttackSpeed = entry.AttackSpeed;
            admitted = values.Admitted;
            status = binding.Client.Status ?? throw InvalidDeathQuery();
            beforeMaximumHitpoints = status.MaxHitpoints; beforeMaximumMana = status.MaxMana;
            beforeCombat = WorldPacketHandlers.CombatFields.Read(status);
            if (admitted)
            {
                // The same complete query that rebuilds Entry also owns these
                // native maxima. The scalar/HP publisher does not rebuild them.
                // Capture only the actual canonical death result, never an alias
                // answer or a late recomputation after holder publication.
                if (canonicalTarget.EquipmentHitpointsStatus is not { } hitpoints || hitpoints.OwnerUid != binding.Uid ||
                    canonicalTarget.PhysicalStatusOverlay is not { MaxMana: { } mana } overlay || overlay.OwnerUid != binding.Uid)
                    throw InvalidDeathQuery();
                afterMaximumHitpoints = hitpoints.MaximumHitpoints; afterMaximumMana = mana;
                afterCombat = values.Combat ?? throw InvalidDeathQuery();
                var source = new QueryState(binding.Store, binding.Connection, binding.Uid,
                    binding.NativeIdentity, entry, values.QuerySource);
                if (!source.InputsMatch(canonicalTarget) || values.Fields.RightWeaponItemId != source.Right ||
                    values.Fields.LeftWeaponItemId != source.Left || source.AttackSpeedMetadata is not { } metadata ||
                    !metadata.IsCurrentPlain(binding.ItemCatalog)) throw InvalidDeathQuery();
                afterFields = values.Fields; afterJiangHu = values.JiangHu;
                afterMetadata = metadata;
                afterEpoch = new QueryEpoch();
                Queries.Add(afterEpoch, source); // Inert until this Entry points to it.
                afterMonk = MonkCache.PrepareUnpublished(binding, IsMonk(source.Right) || IsMonk(source.Left));
                afterAttackSpeed = AttackSpeedCache.PrepareUnpublished(binding, metadata);
            }
            if (!admitted && values.Combat is not null) throw InvalidDeathQuery();
            perfection = LegacyNativePerfectionClient.OwnedPhysicalDeathPublication.Prepare(
                binding, preparation, canonicalTarget);
        }

        internal static OwnedPhysicalDeathPublication Prepare(LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonicalTarget)
        {
            ArgumentNullException.ThrowIfNull(binding);
            ArgumentNullException.ThrowIfNull(preparation);
            ArgumentNullException.ThrowIfNull(canonicalTarget);
            if (!DeathQueryOwnerCurrentPlain(binding) || !preparation.IsCurrentPlain(binding) ||
                !preparation.TryReadNativeQueryProposal(binding, canonicalTarget, out var values) ||
                !Entries.TryGetValue(binding.NativeIdentity, out var entry)) throw InvalidDeathQuery();
            var result = new OwnedPhysicalDeathPublication(binding, preparation, canonicalTarget, values, entry);
            if (!result.IsCurrentPlain()) throw InvalidDeathQuery();
            return result;
        }

        internal bool IsCurrentPlain() => !published && DeathQueryOwnerCurrentPlain(binding) &&
            preparation.IsCurrentPlain(binding) && Entries.TryGetValue(binding.NativeIdentity, out var current) &&
            ReferenceEquals(current, entry) && entry.Initialized == beforeInitialized && entry.Fields == beforeFields &&
            entry.JiangHu == beforeJiangHu && ReferenceEquals(entry.Epoch, beforeEpoch) &&
            ReferenceEquals(entry.Monk, beforeMonk) && ReferenceEquals(entry.AttackSpeed, beforeAttackSpeed) &&
            ReferenceEquals(binding.Client.Status, status) && status.MaxHitpoints == beforeMaximumHitpoints &&
            status.MaxMana == beforeMaximumMana && WorldPacketHandlers.CombatFields.Read(status) == beforeCombat &&
            (afterMetadata is null || afterMetadata.IsCurrentPlain(binding.ItemCatalog)) &&
            perfection.IsCurrentPlain();

        internal bool IsPreparedFor(LegacyOrdinarySpellBinding actualBinding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation actualPreparation, WorldSession actualTarget) =>
            ReferenceEquals(binding, actualBinding) && ReferenceEquals(preparation, actualPreparation) &&
            ReferenceEquals(canonicalTarget, actualTarget) && IsCurrentPlain();

        // Historical publication plus current actual after-state, never the
        // old before-plan proof after its holders have changed. The real
        // Store's final receipt supplies both ordered owner/actor gates.
        internal bool IsPublishedCurrentPlain() => published && DeathQueryOwnerCurrentPlain(binding) &&
            Entries.TryGetValue(binding.NativeIdentity, out var current) && ReferenceEquals(current, entry) &&
            entry.Initialized == (admitted || beforeInitialized) &&
            entry.Fields == (admitted ? afterFields : beforeFields) &&
            entry.JiangHu == (admitted ? afterJiangHu : beforeJiangHu) &&
            ReferenceEquals(entry.Epoch, admitted ? afterEpoch : beforeEpoch) &&
            ReferenceEquals(entry.Monk, admitted ? afterMonk : beforeMonk) &&
            ReferenceEquals(entry.AttackSpeed, admitted ? afterAttackSpeed : beforeAttackSpeed) &&
            ReferenceEquals(binding.Client.Status, status) &&
            status.MaxHitpoints == (admitted ? afterMaximumHitpoints : beforeMaximumHitpoints) &&
            status.MaxMana == (admitted ? afterMaximumMana : beforeMaximumMana) &&
            WorldPacketHandlers.CombatFields.Read(status) == (admitted ? afterCombat : beforeCombat) &&
            (afterMetadata is null || afterMetadata.IsCurrentPlain(binding.ItemCatalog)) &&
            perfection.IsPublishedCurrentPlain();

        internal void PublishPrepared()
        {
            if (admitted)
            {
                entry.Fields = afterFields;
                entry.JiangHu = afterJiangHu;
                entry.Epoch = afterEpoch;
                entry.Monk = afterMonk;
                entry.AttackSpeed = afterAttackSpeed;
                // Plain assignments on the original captured Status object,
                // under the existing actual Store/native publication gates.
                status.MaxHitpoints = afterMaximumHitpoints;
                status.MaxMana = afterMaximumMana;
                afterCombat.Write(status);
                entry.Initialized = true;
            }
            perfection.PublishPrepared();
            published = true;
        }
    }

    private static bool DeathQueryOwnerCurrentPlain(LegacyOrdinarySpellBinding binding) =>
        WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
        Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
        !binding.IsRetired && ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) &&
        ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, binding.Player) &&
        ReferenceEquals(binding.Player.Owner, binding.Client) && binding.Player.UID == binding.Uid &&
        binding.Store.IsBoundSpellOwnerCurrent(binding);

    private static InvalidOperationException InvalidDeathQuery() => new("ordinaryPhysicalDeathQueryOwnerChanged");
}
