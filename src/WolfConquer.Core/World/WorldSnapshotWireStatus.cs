namespace WolfConquer.Core.World;

public enum WorldSnapshotWireStatus : byte
{
    Success = 0,
    MissingSnapshot = 1,
    SnapshotsUnavailable = 2,
    InvalidPacket = 3,
    SnapshotError = 4,
    UnknownTicket = 5,
    ExpiredTicket = 6,
    AlreadyRedeemedTicket = 7,
}
