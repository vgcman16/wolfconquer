namespace WolfConquer.Core.Accounts;

public interface IAccountHandoffTicketRedeemer
{
    // Validate expiry/replay and consume under the same actual storage owner.
    // Publish the consumed record before returning Success; separate TryGet/Save
    // calls do not satisfy this contract. Durability belongs to the store type.
    AccountHandoffRedeemResult Redeem(string token, DateTimeOffset nowUtc);
}
