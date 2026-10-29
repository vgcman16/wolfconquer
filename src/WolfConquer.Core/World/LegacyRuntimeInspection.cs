namespace WolfConquer.Core.World;

public sealed record LegacyRuntimeInspectionRequest(int SchemaVersion, Guid QueryId, Guid ExpectedDomain,
    uint AccountId, uint CharacterUid);

public enum LegacyRuntimeInspectionStatus { Observed, Rejected }

public sealed record LegacyRuntimeNativeVitals(uint Uid, int Hitpoints, uint Mana);

public sealed record LegacyRuntimeInspectionSnapshot(Guid Domain, uint AccountId, uint CharacterUid,
    DateTimeOffset ObservedUtc, string ProtocolProfile, LegacyOwnedPlayerCharacter ManagedCharacter,
    LegacyRuntimeNativeVitals NativeVitals, LegacyOwnedPlayerItem[] ManagedItems,
    LegacyOwnedPlayerItem[]? NativeItems, bool? SelectedValuesEqual, string? NativeItemsUnavailableReason = null)
{
    public const int MaximumRows = 200;
    public string Coverage => "Managed inventory/equipment roots and current HP/Mana; native roots only when actual owners exist. Null native rows mean unavailable, never empty; deposits, other containers and native derived maxima excluded.";
    public string ConsistencyBound => "Current issuance/actor owners and repeated selected witnesses; raw-item/other native writers and restore ABA are not excluded. Equal values do not prove their lock discipline.";
}

public sealed record LegacyRuntimeInspectionResult(int SchemaVersion, Guid QueryId,
    LegacyRuntimeInspectionStatus Status, string Reason, LegacyRuntimeInspectionSnapshot? Snapshot)
{
    public bool ReadOnly => true;
    public bool RuntimeObserved => Status == LegacyRuntimeInspectionStatus.Observed && Snapshot is not null;
    public bool AtomicNativeTupleProven => false;
    public bool LiveStoreSynchronized => false;
    public string EvidenceBound => "Diagnostic observed values only; no saved-player-file comparison, acceptance PASS, signed host/code attestation or continuous liveness.";
}
