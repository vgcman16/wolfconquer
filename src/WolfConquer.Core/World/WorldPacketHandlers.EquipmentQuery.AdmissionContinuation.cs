using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // A real published admission origin and its retained login operation are
    // required. This is neither a resumed level step nor an allocation owner.
    internal sealed partial class PreparedAdmissionContinuationQuery
    {
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyOrdinaryAdmissionPlan.ExperienceAdmissionOrigin origin;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession admitted;
        private readonly LegacyExperienceContinuation pending;
        private readonly LegacyNativeAttributeMapProof attributes, paid;
        private readonly LegacyItemSaveGraph items;
        private readonly LegacyOwnedPhysicalMetadataWitness metadata;
        private readonly LegacyAttackSpeedMetadata speed;
        private readonly LegacyPreparedSpellFileWrite? spellFile;
        private readonly CompletedAdmissionContinuationQuery? predecessor;
        private readonly byte[] admittedImage;
        private readonly int startupOrdinal;
        private int started;

        internal string? LockedSpellPath => spellFile?.SpellsPath;

        private PreparedAdmissionContinuationQuery(WorldPacketHandlerContext context,
            LegacyOrdinaryAdmissionPlan.ExperienceAdmissionOrigin origin)
            : this(context, origin, origin.Admitted, null) { }

        private PreparedAdmissionContinuationQuery(WorldPacketHandlerContext context,
            LegacyOrdinaryAdmissionPlan.ExperienceAdmissionOrigin origin, WorldSession current,
            CompletedAdmissionContinuationQuery? predecessor)
        {
            this.context = context; this.origin = origin; binding = origin.Binding; admitted = current;
            this.predecessor = predecessor; startupOrdinal = predecessor is null ? 0 : 1;
            admittedImage = CaptureAdmissionQueryImage();
            pending = LegacyExperienceContinuationCodec.ReadCharacter(admitted.Character, admitted.AccountId)
                ?? throw InvalidContinuationQueryMutation();
            if (!Equals(pending, LegacyExperienceContinuationCodec.ReadCharacterImage(admittedImage,
                    origin.AccountId, checked((uint)origin.CharacterUid)))) throw InvalidContinuationQueryMutation();
            attributes = LegacyNativeAttributeMapProof.CaptureRaw(admitted.Character.Attributes);
            paid = LegacyNativeAttributeMapProof.CaptureRaw(admitted.Character.PaidNobility);
            if (!LegacyItemSaveGraph.TryCapture(admitted.PlayerItems, out var captured) || captured is null)
                throw InvalidContinuationQueryMutation();
            items = captured;
            binding.Items.RequireCurrentBeforePreparation();
            var selected = LegacyEquipmentQuerySelector.Select(admitted);
            var querySource = admitted with { PlayerItems = admitted.PlayerItems with { EquipmentItems = selected } };
            metadata = LegacyOwnedPhysicalMetadataWitness.CaptureAttributeQuery(binding, context.WorldData, querySource);
            var effective = selected.Where(row => row.Durability != 0).ToArray();
            speed = new(context.WorldData, effective.SingleOrDefault(row => row.Position % 50 == 4)?.ItemId ?? 0,
                effective.SingleOrDefault(row => row.Position % 50 == 5)?.ItemId ?? 0,
                effective.SingleOrDefault(row => row.Position % 50 == 6)?.ItemId ?? 0);
            PreflightOwnedEquipmentQuery(context, admitted, metadata, LegacyEquipmentRebuildReason.InitialWorldEntry);
            _ = LegacyShowEquipmentPacket.CreateEquipmentRefresh(admitted.PlayerItems, admitted.AlternateEquipmentActive).ToFrameBytes();
            spellFile = PrepareAttributeDerivedFiles(context, binding, admitted, admitted, metadata);
        }

        internal static bool TryPrepare(WorldPacketHandlerContext context,
            LegacyOrdinaryAdmissionPlan.ExperienceAdmissionOrigin origin, out PreparedAdmissionContinuationQuery? plan)
        {
            plan = null;
            var binding = origin.Binding; var expected = origin.Admitted;
            if (!binding.Store.IsAdmissionContinuationQueryOwnerHeld(binding, expected, context) ||
                !origin.IsCurrentPlain() || !ReferenceEquals(context.WorldData, binding.ItemCatalog) ||
                !context.Connection.HasSendRoute || binding.Client.Status is null || binding.Player.BitVector is null) return false;
            try
            {
                var prepared = new PreparedAdmissionContinuationQuery(context, origin);
                if (!prepared.IsCurrentPlain()) return false;
                plan = prepared; return true;
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
                OverflowException or FormatException or IOException or UnauthorizedAccessException or NotSupportedException) { return false; }
        }

        internal bool IsCurrentPlain() => Volatile.Read(ref started) == 0 && AdmissionSourceIsCurrent() && AdmissionImageIsCurrent() &&
            binding.Store.IsAdmissionContinuationQueryOwnerHeld(binding, admitted, context) &&
            attributes.IsCurrentPlain() && paid.IsCurrentPlain() && items.IsCurrent(admitted.PlayerItems) &&
            metadata.IsCurrentPlain() && speed.IsCurrentPlain(context.WorldData) &&
            (spellFile is null || spellFile.DiskImageIsCurrent());

        private bool SameProgression(WorldSession current) => current.AccountId == binding.AccountId &&
            ReferenceEquals(current.NativeGameClient, binding.Client) && current.Character.Uid == binding.Uid &&
            current.Character.Level == admitted.Character.Level && current.Character.Experience == admitted.Character.Experience &&
            current.Character.Class == admitted.Character.Class && current.Character.Reborn == admitted.Character.Reborn &&
            current.Character.Strength == admitted.Character.Strength && current.Character.Agility == admitted.Character.Agility &&
            current.Character.Vitality == admitted.Character.Vitality && current.Character.Spirit == admitted.Character.Spirit &&
            current.Character.AttributePoints == admitted.Character.AttributePoints &&
            Equals(pending, LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId));
    }
}
