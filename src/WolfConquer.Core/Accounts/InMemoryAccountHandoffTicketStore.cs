namespace WolfConquer.Core.Accounts;

public sealed class InMemoryAccountHandoffTicketStore : IAccountHandoffTicketStore, IAccountHandoffTicketRedeemer
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AccountHandoffTicket> _tickets = new(StringComparer.Ordinal);

    public IReadOnlyCollection<AccountHandoffTicket> Tickets
    {
        get
        {
            lock (_gate)
            {
                return _tickets.Values.ToArray();
            }
        }
    }

    public void Save(AccountHandoffTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentException.ThrowIfNullOrWhiteSpace(ticket.Token);

        lock (_gate)
        {
            _tickets[ticket.Token] = ticket;
        }
    }

    public bool TryGet(string token, out AccountHandoffTicket ticket)
    {
        lock (_gate)
        {
            return _tickets.TryGetValue(token, out ticket!);
        }
    }

    public AccountHandoffRedeemResult Redeem(string token, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.UnknownTicket);
        }

        // One actual store gate covers lookup, validation and consumption for
        // every service sharing this ephemeral, process-local store instance.
        lock (_gate)
        {
            if (!_tickets.TryGetValue(token, out var ticket))
            {
                return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.UnknownTicket);
            }
            if (ticket.IsRedeemed)
            {
                return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.AlreadyRedeemed, ticket);
            }
            if (ticket.ExpiresAtUtc <= nowUtc)
            {
                return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.ExpiredTicket, ticket);
            }

            var redeemed = ticket with { RedeemedAtUtc = nowUtc };
            _tickets[token] = redeemed;
            return new AccountHandoffRedeemResult(AccountHandoffRedeemStatus.Success, redeemed);
        }
    }
}
