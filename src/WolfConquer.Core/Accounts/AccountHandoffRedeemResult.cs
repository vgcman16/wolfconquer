namespace WolfConquer.Core.Accounts;

public sealed record AccountHandoffRedeemResult(
    AccountHandoffRedeemStatus Status,
    AccountHandoffTicket? Ticket = null)
{
    public bool IsSuccess => Status == AccountHandoffRedeemStatus.Success;
}
