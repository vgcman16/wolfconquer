using WolfConquer.Core.Accounts;

namespace WolfConquer.Core.World;

// Historical result DATA only. No result object can install a connection binding
// or expose the genuine SQL owner held privately by the Frame.
internal sealed class SqlConnectionHandoffResult
{
    internal SqlConnectionHandoffResult(SqlAccountWorldHandoffFailure failure,
        long accountId = 0, uint characterUid = 0, string? username = null)
    {
        if (!Enum.IsDefined(failure) || (failure == SqlAccountWorldHandoffFailure.None
            ? accountId <= 0 || characterUid == 0 || string.IsNullOrWhiteSpace(username)
            : accountId != 0 || characterUid != 0 || username is not null))
            throw new ArgumentException("Invalid retained SQL connection result.");
        Failure = failure; AccountId = accountId; CharacterUid = characterUid; Username = username;
    }

    internal bool IsRetained => Failure == SqlAccountWorldHandoffFailure.None;
    internal SqlAccountWorldHandoffFailure Failure { get; }
    internal long AccountId { get; }
    internal uint CharacterUid { get; }
    internal string? Username { get; }
}
