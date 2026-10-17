namespace WolfConquer.Core.Data;

public enum CharacterSnapshotLoadStatus
{
    Success,
    MissingSnapshot,
    SnapshotReadFailed,
    SnapshotReadDenied,
    SnapshotParseFailed,
    UidMismatch,
}

public sealed record CharacterSnapshotLoadResult(
    CharacterSnapshot? Snapshot,
    CharacterSnapshotLoadStatus Status,
    long AccountId,
    string Path,
    string Reason,
    string Detail)
{
    public bool Succeeded => Status == CharacterSnapshotLoadStatus.Success;
}
