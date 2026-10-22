using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

[Flags]
internal enum LegacyEquipmentQueryPublicationLoss
{
    None = 0,
    OwnerLost = 1,
    RequiredParticipantLost = 2,
    AcceptedItemsChanged = 4,
}

// An invocation-local fence, not persistent character state or a gameplay gate.
internal sealed record LegacyEquipmentQueryPublicationScope(
    long OwnerUid,
    PlayerItemSnapshot? ExpectedItems = null,
    WorldActiveSession? RequiredParticipant = null)
{
    internal object? OwnerNativeClientIdentity { get; init; }
    internal object? RequiredNativeClientIdentity { get; init; }
}

internal readonly record struct LegacyEquipmentQueryPublicationResult(
    WorldSession? Session,
    WorldSession? RequiredParticipantSession,
    LegacyEquipmentQueryPublicationLoss Loss = LegacyEquipmentQueryPublicationLoss.None)
{
    public bool Completed => Session is not null && Loss == LegacyEquipmentQueryPublicationLoss.None;
}
