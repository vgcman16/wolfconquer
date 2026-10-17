namespace WolfConquer.Core.Accounts;

public enum AccountLoginStatus
{
    Success,
    MissingUsername,
    WrongPassword,
    Banned,
    AlreadyOnline,
    AmbiguousAccount
}
