namespace WolfConquer.Core.Accounts;

public sealed class InMemoryAccountHandoffTicketStore : IAccountHandoffTicketStore
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
}
