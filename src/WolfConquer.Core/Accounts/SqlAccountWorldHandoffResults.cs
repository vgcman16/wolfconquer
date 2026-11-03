using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.Accounts;

public enum SqlAccountWorldHandoffFailure
{
    None, MissingUsername, WrongPassword, AmbiguousAccount, Banned, AlreadyOnline,
    InvalidAccountState, SessionConflict, SessionRetired, IdentityConflict, DomainConflict,
    WorldUnavailable, InvalidToken, UnknownTicket, Expired, AlreadyRedeemed, EndpointMismatch
}

public sealed class SqlAccountWorldHandoffIssueResult
{
    internal SqlAccountWorldHandoffIssueResult(SqlAccountWorldHandoffOffer? offer, SqlAccountWorldHandoffFailure failure)
    {
        if (!Enum.IsDefined(failure) || (offer is not null) != (failure == SqlAccountWorldHandoffFailure.None))
            throw new ArgumentException("Invalid handoff issue result.");
        Offer = offer;
        Failure = failure;
    }

    public bool IsIssued => Offer is not null;
    public SqlAccountWorldHandoffFailure Failure { get; }
    public SqlAccountWorldHandoffOffer? Offer { get; }
}

public sealed class SqlAccountWorldHandoffRedeemResult
{
    internal SqlAccountWorldHandoffRedeemResult(SqlWorldBundleCustody? custody, long accountId,
        uint characterUid, string? username, SqlAccountWorldHandoffFailure failure)
    {
        if (!Enum.IsDefined(failure) || (custody is not null) != (failure == SqlAccountWorldHandoffFailure.None) ||
            (custody is null && (accountId != 0 || characterUid != 0 || username is not null)) ||
            (custody is not null && (accountId <= 0 || characterUid == 0 || string.IsNullOrWhiteSpace(username) ||
                custody.AccountId != accountId || custody.CharacterUid != characterUid)))
            throw new ArgumentException("Invalid handoff redemption result.");
        Custody = custody;
        AccountId = accountId;
        CharacterUid = characterUid;
        Username = username;
        Failure = failure;
    }

    public bool IsRedeemed => Custody is not null;
    public SqlAccountWorldHandoffFailure Failure { get; }
    public long AccountId { get; }
    public uint CharacterUid { get; }
    public string? Username { get; }
    public SqlWorldBundleCustody? Custody { get; }
}