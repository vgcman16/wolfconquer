using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Accounts;

public sealed record AccountHandoffTicket(
    string Token,
    long AccountId,
    string Username,
    ConquerEndpoint WorldEndpoint,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RedeemedAtUtc = null)
{
    public bool IsRedeemed => RedeemedAtUtc.HasValue;
}
