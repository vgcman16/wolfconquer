namespace WolfConquer.Core.Accounts;

// Internal carrier intentionally has no generated value ToString for the bearer secret.
internal sealed class SqlAccountWorldHandoffIssued
{
    internal SqlAccountWorldHandoffIssued(SqlAccountWorldHandoffFailure failure, string? token, DateTime issued, DateTime expires)
    {
        if ((token is not null) != (failure == SqlAccountWorldHandoffFailure.None))
            throw new ArgumentException("Invalid handoff issuance shape.");
        Failure = failure; Token = token; Issued = issued; Expires = expires;
    }
    internal SqlAccountWorldHandoffFailure Failure { get; }
    internal string? Token { get; }
    internal DateTime Issued { get; }
    internal DateTime Expires { get; }
}