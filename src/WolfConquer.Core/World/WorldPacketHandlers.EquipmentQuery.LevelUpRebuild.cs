using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // This preparation is issued by the real XP writer and its registered
    // award purpose. It is not the 2203 owner or a completed-query stamp.
    internal sealed partial class PreparedLevelUpRebuild
    {
        private readonly WorldPacketHandlerContext context;
        private readonly OwnedCombatExperience purpose;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession expected, proposed;
        private readonly LegacyNativeAttributeMapProof attributes, paid;
        private readonly LegacyItemSaveGraph items;
        private readonly LegacyOwnedPhysicalMetadataWitness metadata;
        private readonly LegacyAttackSpeedMetadata speed;
        private readonly LegacyPreparedSpellFileWrite? spellFile;
        private WorldSession? committed;
        private int started;

        internal string? LockedSpellPath => spellFile?.SpellsPath;

        private PreparedLevelUpRebuild(WorldPacketHandlerContext context, OwnedCombatExperience purpose,
            LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed)
        {
            this.context = context; this.purpose = purpose; this.binding = binding;
            this.expected = expected; this.proposed = proposed;
            attributes = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.Attributes);
            paid = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.PaidNobility);
            if (!LegacyItemSaveGraph.TryCapture(expected.PlayerItems, out var captured) || captured is null)
                throw new InvalidDataException("levelUpRebuildItemsInvalid");
            items = captured;
            binding.Items.RequireCurrentBeforePreparation();
            var querySource = proposed with { PlayerItems = proposed.PlayerItems with
                { EquipmentItems = LegacyEquipmentQuerySelector.Select(proposed) } };
            metadata = LegacyOwnedPhysicalMetadataWitness.CaptureAttributeQuery(binding, context.WorldData, querySource);
            var selected = LegacyEquipmentQuerySelector.Select(proposed).Where(row => row.Durability != 0).ToArray();
            speed = new(context.WorldData, selected.SingleOrDefault(row => row.Position % 50 == 4)?.ItemId ?? 0,
                selected.SingleOrDefault(row => row.Position % 50 == 5)?.ItemId ?? 0,
                selected.SingleOrDefault(row => row.Position % 50 == 6)?.ItemId ?? 0);
            PreflightOwnedEquipmentQuery(context, proposed, metadata, LegacyEquipmentRebuildReason.LevelUp, restoreHitpoints: true);
            spellFile = PrepareAttributeDerivedFiles(context, binding, expected, proposed, metadata);
        }

        internal static bool TryPrepare(WorldPacketHandlerContext context, OwnedCombatExperience purpose,
            LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed,
            out PreparedLevelUpRebuild? plan)
        {
            plan = null;
            if (!purpose.CoversRebuild(context, binding, expected, proposed) ||
                !binding.Store.IsLevelUpRebuildOwnerHeld(binding, expected, context) ||
                !ReferenceEquals(context.WorldData, binding.ItemCatalog) ||
                !ReferenceEquals(proposed.NativeGameClient, binding.Client) || proposed.AccountId != binding.AccountId ||
                proposed.Character.Uid != binding.Uid || proposed.Character.Level <= expected.Character.Level ||
                proposed.Character.MinHitPoints != expected.Character.MinHitPoints || proposed.Character.MinMana != expected.Character.MinMana ||
                proposed.Character.MaxMana != expected.Character.MaxMana ||
                !ReferenceEquals(proposed.PlayerItems, expected.PlayerItems) || proposed.AlternateEquipmentActive != expected.AlternateEquipmentActive ||
                !context.Connection.HasSendRoute || binding.Client.Status is null || binding.Player.BitVector is null ||
                !binding.Recovery.IsReadyForUsers(binding.UsersRoot)) return false;
            try
            {
                var prepared = new PreparedLevelUpRebuild(context, purpose, binding, expected, proposed);
                if (!prepared.IsCurrentPlain()) return false;
                plan = prepared; return true;
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
                OverflowException or FormatException or IOException or UnauthorizedAccessException or NotSupportedException) { return false; }
        }

        internal bool IsCurrentPlain() => Volatile.Read(ref started) == 0 &&
            purpose.CoversRebuild(context, binding, expected, proposed) &&
            binding.Store.IsLevelUpRebuildOwnerHeld(binding, expected, context) && attributes.IsCurrentPlain() && paid.IsCurrentPlain() &&
            items.IsCurrent(expected.PlayerItems) && metadata.IsCurrentPlain() && speed.IsCurrentPlain(context.WorldData) &&
            (spellFile is null || spellFile.DiskImageIsCurrent());

        // The actual prepared final holder is bound before its writer commits.
        // A later real committed receipt is separately required for execution.
        internal void BindCommitted(WorldSession final)
        {
            if (committed is not null || !IsCurrentPlain() || !SameProgression(final) ||
                final.Character.MinHitPoints != proposed.Character.MinHitPoints || final.Character.MinMana != proposed.Character.MinMana ||
                final.Character.MaxMana != proposed.Character.MaxMana ||
                !items.IsCurrent(final.PlayerItems)) throw new InvalidOperationException("levelUpRebuildFinalChanged");
            committed = final;
        }

        private bool SameProgression(WorldSession current) => current.AccountId == binding.AccountId &&
            ReferenceEquals(current.NativeGameClient, binding.Client) && current.Character.Uid == binding.Uid &&
            current.Character.Level == proposed.Character.Level && current.Character.Experience == proposed.Character.Experience &&
            current.Character.Class == proposed.Character.Class && current.Character.Reborn == proposed.Character.Reborn &&
            current.Character.Strength == proposed.Character.Strength && current.Character.Agility == proposed.Character.Agility &&
            current.Character.Vitality == proposed.Character.Vitality && current.Character.Spirit == proposed.Character.Spirit &&
            current.Character.AttributePoints == proposed.Character.AttributePoints;
    }
}
