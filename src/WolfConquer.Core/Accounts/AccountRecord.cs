namespace WolfConquer.Core.Accounts;

public sealed record AccountRecord(
    long Id,
    string Username,
    string Password,
    long EntityId,
    long BannedId,
    bool IsOnline);
