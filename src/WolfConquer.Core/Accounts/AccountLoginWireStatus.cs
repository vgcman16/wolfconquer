namespace WolfConquer.Core.Accounts;

public enum AccountLoginWireStatus : byte
{
    Success = 0,
    MissingUsername = 1,
    WrongPassword = 2,
    Banned = 3,
    AlreadyOnline = 4,
    AmbiguousAccount = 5,
    InvalidPacket = 6,
    ServiceUnavailable = 7,
}
