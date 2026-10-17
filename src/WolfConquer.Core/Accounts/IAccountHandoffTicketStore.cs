namespace WolfConquer.Core.Accounts;

public interface IAccountHandoffTicketStore
{
    void Save(AccountHandoffTicket ticket);

    bool TryGet(string token, out AccountHandoffTicket ticket);
}
