namespace WolfConquer.Core.World;

internal enum LegacyRuneContainerKind { Store, Bag, Drop }

internal sealed record LegacyRuneContainerOperation(LegacyRuneContainerKind Kind, uint SelectedUid,
    LegacyRuneMetadata[] Metadata, bool AlternateEquipmentActive,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    LegacyRuneTransformDependency? Transform = null)
{
    internal void Validate(LegacyRuneMappingSemantics semantics = LegacyRuneMappingSemantics.RawSelf)
    {
        if (!Enum.IsDefined(Kind) || SelectedUid == 0) throw new InvalidDataException("Invalid recorded Rune container action.");
        if (semantics == LegacyRuneMappingSemantics.RawSelf)
            (Transform ?? throw new InvalidDataException("Current Rune container intent requires captured transform presence.")).Validate();
        else if (Transform is not null) throw new InvalidDataException("Legacy Rune container intent has unexpected transform input.");
        LegacyRuneOperation.ValidateMetadata(Metadata, semantics);
    }
}

// Uses the actual admitted metadata/item dependency capability, not Unequip's
// gameplay policy. Container actions retain their own source prerequisites.
internal sealed class LegacyRuneContainerEvidence
{
    private readonly LegacyRuneOperationEvidence dependencies;
    private readonly LegacyRuneContainerKind kind;
    private readonly LegacyRuneMetadata[] metadata;
    internal LegacyRuneContainerOperation Operation { get; }

    private LegacyRuneContainerEvidence(WorldPacketHandlerContext context, WorldSession stable,
        LegacyRuneContainerKind kind, uint uid)
    {
        if (!Enum.IsDefined(kind)) throw new InvalidDataException("Unknown Rune container action.");
        dependencies = LegacyRuneOperationEvidence.Capture(context, stable, false, uid, 212);
        this.kind = kind; metadata = dependencies.Operation.Metadata.ToArray();
        var transform = dependencies.Operation.Transform!;
        Operation = new(kind, uid, metadata.ToArray(), dependencies.Operation.AlternateEquipmentActive,
            new LegacyRuneTransformDependency(transform.Fields));
        Operation.Validate();
        if (!IsCurrent()) throw new InvalidOperationException("Rune container dependencies changed.");
    }

    internal static LegacyRuneContainerEvidence Capture(WorldPacketHandlerContext context, WorldSession stable,
        LegacyRuneContainerKind kind, uint uid) => new(context, stable, kind, uid);

    internal bool IsCurrent()
    {
        if (!dependencies.IsCurrent() || Operation.Kind != kind || Operation.SelectedUid != dependencies.Operation.SelectedUid ||
            Operation.AlternateEquipmentActive != dependencies.Operation.AlternateEquipmentActive || Operation.Metadata.Length != metadata.Length ||
            !dependencies.Operation.Transform!.SameAs(Operation.Transform)) return false;
        for (var index = 0; index < metadata.Length; index++)
            if (!ReferenceEquals(metadata[index], Operation.Metadata[index])) return false;
        return true;
    }
}
