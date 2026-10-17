namespace WolfConquer.Core.World;

internal sealed record LegacyEquipmentQueryRequest(
    LegacyEquipmentRebuildReason Reason,
    LegacyEquipmentQueryPublicationScope Scope,
    bool CallItems = true);

[Flags]
internal enum LegacyEquipmentQueryPublishedPhases
{
    None = 0, AppendItems = 1, ServerInfo = 2, PeerSpawn = 4,
    Status = 8, Perfection = 16, Hitpoints = 32,
}

internal readonly record struct LegacyEquipmentQueryPublicationReceipt(
    LegacyEquipmentQueryPublishedPhases Phases,
    int HitpointAssignments,
    int HitpointPairs);

internal readonly record struct LegacyEquipmentQueryOutcome(
    WorldSession? Session,
    WorldSession? RequiredParticipantSession,
    bool Admitted,
    LegacyEquipmentQueryPublicationLoss Loss,
    LegacyEquipmentQueryPublicationReceipt Receipt)
{
    public bool Completed => Session is not null && Loss == LegacyEquipmentQueryPublicationLoss.None;
}
