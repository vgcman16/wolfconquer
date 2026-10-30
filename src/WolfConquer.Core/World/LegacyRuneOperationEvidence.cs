using System.Numerics;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneMetadata(uint ItemId, uint RuneSpellId, uint RequiredClass, uint RequiredLevel, bool? IsRuneSkill);

// Detached recorded input, never a live permission or an accepted afterimage.
internal sealed record LegacyRuneOperation(bool Equip, uint SelectedUid, ushort RequestedPosition,
    LegacyRuneMetadata[] Metadata, bool AlternateEquipmentActive, byte BeastLevel, uint BeastFlags, byte? BeastYellowSlots,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    LegacyRuneTransformDependency? Transform = null)
{
    internal byte AvailableYellowSlots => checked((byte)(5 + (BeastYellowSlots ?? 0) + BitOperations.PopCount(BeastFlags & 31)));

    internal void Validate(LegacyRuneMappingSemantics semantics = LegacyRuneMappingSemantics.RawSelf)
    {
        if (!Enum.IsDefined(semantics)) throw new InvalidDataException("Unknown Rune mapping semantics.");
        if (SelectedUid == 0 || !Equip && RequestedPosition != 212 || Metadata is null || Metadata.Length is < 1 or > ushort.MaxValue ||
            Equip && (BeastLevel == 0) != (BeastYellowSlots is null) || !Equip && (BeastLevel != 0 || BeastFlags != 0 || BeastYellowSlots is not null))
            throw new InvalidDataException("Invalid recorded Rune operation inputs.");
        _ = AvailableYellowSlots;
        if (semantics == LegacyRuneMappingSemantics.RawSelf)
            (Transform ?? throw new InvalidDataException("Current Rune intent requires captured transform presence.")).Validate();
        else if (Transform is not null) throw new InvalidDataException("Legacy Rune intent has unexpected current transform input.");
        ValidateMetadata(Metadata, semantics);
    }

    internal static void ValidateMetadata(LegacyRuneMetadata[] metadata,
        LegacyRuneMappingSemantics semantics = LegacyRuneMappingSemantics.RawSelf)
    {
        if (!Enum.IsDefined(semantics)) throw new InvalidDataException("Unknown Rune metadata semantics.");
        if (metadata is null || metadata.Length is < 1 or > ushort.MaxValue)
            throw new InvalidDataException("Invalid recorded Rune metadata count.");
        uint previous = 0;
        foreach (var row in metadata)
        {
            var noSkill = row is not null && LegacyRuneSpellIdentity.IsNoSkill(row.ItemId, row.RuneSpellId, semantics);
            if (row is null || row.ItemId <= previous || row.ItemId / 10_000 is < 401 or > 403 ||
                !noSkill && row.RuneSpellId / 100 is 0 or > ushort.MaxValue || noSkill != (row.IsRuneSkill is null))
                throw new InvalidDataException("Invalid, duplicate or unordered recorded Rune metadata.");
            previous = row.ItemId;
        }
    }

    internal static uint[] RequiredMetadata(PlayerItemSnapshot items, uint selectedUid)
    {
        var selected = items.RuneItems.SingleOrDefault(row => row.Uid == selectedUid)
            ?? throw new InvalidDataException("Selected Rune is not in the captured owned Rune container.");
        foreach (var row in items.RuneItems)
            if (row.Position is not (0 or 211 or 212) && !IsEquipmentSlot(row.ItemId, row.Position))
                throw new InvalidDataException("Captured Rune has an invalid equipped family/position.");
        return items.RuneItems.Where(row => IsEquipmentSlot(row.ItemId, row.Position)).Select(row => row.ItemId)
            .Append(selected.ItemId).Distinct().Order().ToArray();
    }

    private static bool IsEquipmentSlot(uint id, ushort position) => (id / 10_000) switch
    {
        401 => position is 101 or 121,
        402 => position is 102 or 122,
        403 => position is (>= 103 and <= 114) or (>= 123 and <= 134),
        _ => false,
    };
}

// The actual Store binding and admission-published Beast issue this capability.
// No caller can construct one from a detached catalog or a claimed UID.
internal sealed partial class LegacyRuneOperationEvidence
{
    private readonly WorldPacketHandlerContext context;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldSession expected;
    private readonly CatalogWitness catalog;
    private readonly BeastWitness? beast;
    private readonly LegacyRuneMetadata[] metadata;
    private readonly LegacyRuneTransformDependency transform;
    internal LegacyRuneOperation Operation { get; }

    private LegacyRuneOperationEvidence(WorldPacketHandlerContext context, WorldSession stable, bool equip, uint uid, ushort position)
    {
        this.context = context;
        binding = context.Connection.NativeGameClient?.OrdinarySpellBinding ?? throw Invalid();
        if (context.Sessions is null || !ReferenceEquals(context.Sessions, binding.Store) ||
            !binding.Store.TryGet(context.Connection, out var current) || current is null ||
            !ReferenceEquals(current.NativeGameClient, binding.Client) || current.Character.Uid != stable.Character.Uid ||
            !ReferenceEquals(binding.ItemCatalog, context.WorldData) || !binding.Store.IsBoundSpellOwnerCurrent(binding)) throw Invalid();
        expected = current;
        catalog = new(binding.ItemCatalog ?? throw Invalid(), LegacyRuneOperation.RequiredMetadata(stable.PlayerItems, uid));
        beast = equip ? new(binding) : null;
        metadata = catalog.Rows.ToArray();
        transform = LegacyRuneTransformDependency.Capture(stable.Character);
        Operation = new(equip, uid, equip ? position : (ushort)212, metadata.ToArray(), stable.AlternateEquipmentActive,
            beast?.Level ?? 0, beast?.Flags ?? 0, beast?.Slots, new LegacyRuneTransformDependency(transform.Fields));
        Operation.Validate();
        if (expected.AlternateEquipmentActive != stable.AlternateEquipmentActive ||
            binding.Client.Equipment?.Alternante != stable.AlternateEquipmentActive ||
            !LegacyRuneOperation.RequiredMetadata(expected.PlayerItems, uid).SequenceEqual(Operation.Metadata.Select(row => row.ItemId)) || !IsCurrent()) throw Invalid();
    }

    internal static LegacyRuneOperationEvidence Capture(WorldPacketHandlerContext context, WorldSession stable,
        bool equip, uint uid, ushort position) => new(context, stable, equip, uid, position);

    internal bool IsCurrent()
    {
        if (!ReferenceEquals(context.WorldData, binding.ItemCatalog) || !binding.Store.IsBoundSpellOwnerCurrent(binding) ||
            !binding.Store.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
            !binding.Items.Image.IsCurrentPlain() || !catalog.IsCurrent() || beast is not null && !beast.IsCurrent() ||
            binding.Client.Equipment?.Alternante != Operation.AlternateEquipmentActive ||
            Operation.Metadata.Length != metadata.Length || !transform.SameAs(Operation.Transform)) return false;
        for (var index = 0; index < metadata.Length; index++)
            if (!ReferenceEquals(metadata[index], Operation.Metadata[index])) return false;
        return true;
    }

    private static InvalidOperationException Invalid() => new("runeOperationRequiresCurrentAdmittedDependencies");
}
