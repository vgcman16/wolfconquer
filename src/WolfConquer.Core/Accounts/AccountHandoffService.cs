using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Accounts;

public sealed class AccountHandoffService
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(2);

    private readonly IAccountHandoffTicketStore _tickets;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TimeSpan _lifetime;

    public AccountHandoffService(
        IAccountHandoffTicketStore tickets,
        Func<DateTimeOffset>? utcNow = null,
        TimeSpan? lifetime = null)
    {
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _lifetime = lifetime ?? DefaultLifetime;
        if (_lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Ticket lifetime must be positive.");
        }
    }

    public AccountHandoffTicket Issue(long accountId, string username, ConquerEndpoint worldEndpoint)
    {
        if (accountId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(accountId), "Account id must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(worldEndpoint);

        var issuedAt = _utcNow();
        var ticket = new AccountHandoffTicket(
            Guid.NewGuid().ToString("N"),
            accountId,
            username,
            worldEndpoint,
            issuedAt,
            issuedAt.Add(_lifetime));

        _tickets.Save(ticket);
        return ticket;
    }

    public AccountHandoffRedeemResult Redeem(string token)
    {
        var now = _utcNow();
        if (_tickets is IAccountHandoffTicketRedeemer redeemer)
        {
            return redeemer.Redeem(token, now);
        }

        if (string.IsNullOrWhiteSpace(token) ||
            !_tickets.TryGet(token, out var ticket))
        {
            return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.UnknownTicket);
        }

        if (ticket.IsRedeemed)
        {
            return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.AlreadyRedeemed, ticket);
        }

        if (ticket.ExpiresAtUtc <= now)
        {
            return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.ExpiredTicket, ticket);
        }

        var redeemed = ticket with { RedeemedAtUtc = now };
        _tickets.Save(redeemed);
        return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.Success, redeemed);
    }
}
