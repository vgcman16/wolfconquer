using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Issued from the actual Store binding and native active Rune query. This is
// a closed prepared dependency, never an accepting callback or caller flag.
internal sealed partial class LegacyRuneCastEligibility
{
    private readonly LegacyOrdinarySpellBinding? binding;
    private readonly LegacyOwnedNativeItemImage? image;
    private readonly LegacyRuneSpellClassification classification;
    private readonly bool alternate;
    private readonly RuneRequirementWitness[] requirements = [];

    private LegacyRuneCastEligibility(LegacyOrdinarySpellBinding binding, LegacyRuneSpellClassification classification, bool alternate,
        RuneRequirementWitness[] requirements)
    { this.binding = binding; image = binding.Items.Image; this.classification = classification; this.alternate = alternate;
      this.requirements = requirements; }
    private LegacyRuneCastEligibility(LegacyRuneSpellClassification ordinary) => classification = ordinary;

    internal static bool TryCapture(WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell,
        out LegacyRuneCastEligibility? proof)
    {
        try { return TryCaptureCore(context, session, spell, out proof); }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or OverflowException)
        { proof = null; return false; }
    }

    private static bool TryCaptureCore(WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell,
        out LegacyRuneCastEligibility? proof)
    {
        proof = null;
        var data = context.WorldData;
        if (data is null) return spell.IsRuneSkill is not true;
        if (!LegacyRuneSpellClassification.TryCapture(data, spell.Id, out var classification))
            return spell.IsRuneSkill is not true && !LegacyRuneSpellClassification.HasExplicitEntry(data, spell.Id);
        if (!classification!.IsRuneSkill)
        {
            if (spell.IsRuneSkill is true || !classification.IsCurrent()) return false;
            proof = new(classification); return true;
        }
        if (context.Sessions is not { } store || session.NativeGameClient is not { } client ||
            client.Player is not { } player || client.OrdinarySpellBinding is not { } binding ||
            !ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.Connection, context.Connection) ||
            !ReferenceEquals(binding.ItemCatalog, data) || !ReferenceEquals(binding.ItemSnapshot, session.PlayerItems) ||
            !store.IsBoundSpellOwnerCurrent(binding) || session.Character.Reborn < 2 || session.Character.Level < 15) return false;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            binding.Items.RequireCurrentBeforePreparation();
            if (client.Equipment is not { } equipment || client.Rune is not { } runes ||
                equipment.Alternante != session.AlternateEquipmentActive) return false;
            // Read every captured Rune's actual adopted metadata before deciding
            // that one active provider exists; a malformed tail cannot be absence.
            var mapped = new Dictionary<uint, ushort>();
            var requirements = new Dictionary<uint, RuneRequirementWitness>();
            foreach (var item in session.PlayerItems.RuneItems)
            {
                if (item.ItemId > int.MaxValue || !data.ItemsById.TryGetValue((int)item.ItemId, out var rows) ||
                    rows.Count != 1 || rows[0] is not { } metadata || metadata.Id != item.ItemId ||
                    metadata.Fields.Count <= ItemType.RuneSpellIdFieldIndex) return false;
                if (!requirements.TryGetValue(item.ItemId, out var required))
                    requirements.Add(item.ItemId, required = new RuneRequirementWitness(metadata));
                var encoded = required.Encoded;
                var encodedSpell = LegacyGameItemPacket.CreateRuneSpellOverride(item.ItemId, encoded);
                if (encodedSpell is null || LegacyRuneSpellIdentity.IsNoSkill(item.ItemId, encoded, LegacyRuneMappingSemantics.RawSelf)) continue;
                var id = encoded / 100;
                if (id is 0 or > ushort.MaxValue) return false;
                if (WorldPacketHandlers.PassesOwnedRuneCastRequirements(session.Character, required.Class, required.Level))
                    mapped.Add(item.Uid, checked((ushort)id));
            }
            // This literal native query implements normal/alternate fallback;
            // neither all stored Runes nor the inactive set grants a cast.
            var worn = runes.WornObjects;
            bool found = false;
            foreach (var item in worn)
                if (LegacyNativeItemTypeRuneMetadata.isRuneCheck(item.ITEM_ID, item.Position) &&
                    mapped.TryGetValue(item.Uid, out var mappedId) && mappedId == spell.Id) found = true;
            if (!found) return false;
            var candidate = new LegacyRuneCastEligibility(binding, classification, session.AlternateEquipmentActive, requirements.Values.ToArray());
            if (!candidate.IsCurrentPlain()) return false;
            proof = candidate; return true;
        }
    }

    // Called under the real Store/native publication boundary after complete
    // item proofs. No filesystem, metadata lookup, comparer or callback here.
    internal bool IsCurrentPlain() => classification.IsCurrent() && RequirementsCurrentPlain() && (binding is null || !binding.IsRetired && !binding.Recovery.IsBlocked &&
        ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) &&
        ReferenceEquals(binding.Items.Image, image) && binding.Items.PhysicalOwnerIsCurrentPlain() &&
        binding.Client.Equipment is { } equipment && equipment.Alternante == alternate);
}

public static partial class WorldPacketHandlers
{
    internal static bool PassesOwnedRuneCastRequirements(CharacterSnapshot character, ItemType metadata) =>
        PassesLegacyItemClassRequirement(character, metadata) && PassesLegacyItemLevelRequirement(character, metadata);
    internal static bool PassesOwnedRuneCastRequirements(CharacterSnapshot character, uint requiredClass, uint requiredLevel) =>
        PassesLegacyItemClassRequirement(character, requiredClass) && PassesLegacyItemLevelRequirement(character, requiredLevel);
}
