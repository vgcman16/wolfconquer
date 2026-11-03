namespace WolfConquer.Core.Accounts;

public enum SqlAccountAuthenticationFailure
{
    None,
    MissingUsername,
    WrongPassword,
    AmbiguousAccount,
    Banned,
    AlreadyOnline,
    InvalidAccountState,
    IdentityConflict,
    SessionConflict,
    SessionRetired,
    DomainConflict,
}

/// <summary>Acquisition result; only its actual custody, when present, retains the SQL owner.</summary>
public sealed class SqlAccountAuthenticationAcquireResult
{
    internal SqlAccountAuthenticationAcquireResult(SqlAccountAuthenticationCustody? custody, SqlAccountAuthenticationFailure failure)
    {
        if (!Enum.IsDefined(failure) || (custody is not null) != (failure == SqlAccountAuthenticationFailure.None))
            throw new ArgumentException("Invalid authentication custody result shape.");
        Custody = custody;
        Failure = failure;
    }

    public SqlAccountAuthenticationCustody? Custody { get; }
    public SqlAccountAuthenticationFailure Failure { get; }
    public bool IsAcquired => Custody is not null;
}
