namespace WolfConquer.Core.Accounts;

public interface IAccountHandoffTicketRedeemer
{
    AccountHandoffRedeemResult Redeem(string token, DateTimeOffset nowUtc);
}
