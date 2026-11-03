using WolfConquer.Core.Accounts;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class AccountHandoffRedemptionSafetyTests
{
    private static readonly DateTimeOffset Issued = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TwoServicesSharingRealMemoryStoreConsumeTicketExactlyOnce()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var issuer = new AccountHandoffService(store, () => Issued);
        var ticket = issuer.Issue(12345, "owned-user", ConquerRuntimeDefaults.WorldServer);
        var redeemedAt = Issued.AddSeconds(30);
        var first = new AccountHandoffService(store, () => redeemedAt);
        var second = new AccountHandoffService(store, () => redeemedAt);
        var results = new AccountHandoffRedeemResult[16];
        Parallel.For(0, results.Length, index =>
            results[index] = (index % 2 == 0 ? first : second).Redeem(ticket.Token));

        Assert.Single(results, result => result.Status == AccountHandoffRedeemStatus.Success);
        Assert.Equal(15, results.Count(result => result.Status == AccountHandoffRedeemStatus.AlreadyRedeemed));
        var expected = ticket with { RedeemedAtUtc = redeemedAt };
        Assert.All(results, result => Assert.Equal(expected, result.Ticket));
        Assert.Equal(12345L, expected.AccountId); Assert.Equal("owned-user", expected.Username);
        Assert.Equal(ConquerRuntimeDefaults.WorldServer, expected.WorldEndpoint);
        Assert.Equal(Issued, expected.IssuedAtUtc); Assert.Equal(Issued.AddMinutes(2), expected.ExpiresAtUtc);
        Assert.Equal<DateTimeOffset?>(redeemedAt, expected.RedeemedAtUtc);
        Assert.True(store.TryGet(ticket.Token, out var stored)); Assert.Equal(expected, stored);
        Assert.Equal(expected, Assert.Single(store.Tickets));
        var later = new AccountHandoffService(store, () => Issued.AddDays(1)).Redeem(ticket.Token);
        Assert.Equal(AccountHandoffRedeemStatus.AlreadyRedeemed, later.Status); Assert.Equal(expected, later.Ticket);
        Assert.True(store.TryGet(ticket.Token, out stored)); Assert.Equal(expected, stored);
    }

    [Fact]
    public void UnknownBlankAndNullInputsDoNotConsumeAnExistingTicket()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var service = new AccountHandoffService(store, () => Issued);
        var ticket = service.Issue(12345, "owned-user", ConquerRuntimeDefaults.WorldServer);
        foreach (string? token in new string?[] { null, "", " ", "unknown" })
        {
            var result = service.Redeem(token!);
            Assert.Equal(AccountHandoffRedeemStatus.UnknownTicket, result.Status); Assert.Null(result.Ticket);
            Assert.True(store.TryGet(ticket.Token, out var saved)); Assert.Equal(ticket, saved);
            Assert.Equal(ticket, Assert.Single(store.Tickets));
        }
        Assert.Equal(AccountHandoffRedeemStatus.Success, service.Redeem(ticket.Token).Status);
    }

    [Fact]
    public void ExactExpiryRefusesWithoutConsumingOrAlteringRecord()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var issuer = new AccountHandoffService(store, () => Issued);
        var ticket = issuer.Issue(12345, "owned-user", ConquerRuntimeDefaults.WorldServer);
        var expiry = new AccountHandoffService(store, () => ticket.ExpiresAtUtc);
        var result = expiry.Redeem(ticket.Token);
        Assert.Equal(AccountHandoffRedeemStatus.ExpiredTicket, result.Status); Assert.Equal(ticket, result.Ticket);
        Assert.True(store.TryGet(ticket.Token, out var saved)); Assert.Equal(ticket, saved); Assert.False(saved.IsRedeemed);
        Assert.Equal(ticket, Assert.Single(store.Tickets));
        var after = new AccountHandoffService(store, () => ticket.ExpiresAtUtc.AddTicks(1)).Redeem(ticket.Token);
        Assert.Equal(AccountHandoffRedeemStatus.ExpiredTicket, after.Status); Assert.Equal(ticket, after.Ticket);
    }

    [Fact]
    public void SeparateTicketsRemainIndependentlyConsumable()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var service = new AccountHandoffService(store, () => Issued);
        var first = service.Issue(12345, "first", ConquerRuntimeDefaults.WorldServer);
        var second = service.Issue(67890, "second", ConquerRuntimeDefaults.WorldServer);
        Assert.NotEqual(first.Token, second.Token);
        var firstResult = service.Redeem(first.Token);
        Assert.Equal(AccountHandoffRedeemStatus.Success, firstResult.Status);
        Assert.True(store.TryGet(second.Token, out var pending)); Assert.Equal(second, pending); Assert.False(pending.IsRedeemed);
        var secondResult = service.Redeem(second.Token);
        Assert.Equal(AccountHandoffRedeemStatus.Success, secondResult.Status);
        Assert.Equal(first with { RedeemedAtUtc = Issued }, firstResult.Ticket);
        Assert.Equal(second with { RedeemedAtUtc = Issued }, secondResult.Ticket);
        Assert.Equal(2, store.Tickets.Count);
    }

    [Fact]
    public void UnsupportedStoreAndNullFailAtConstructionWithoutCallingStorage()
    {
        var store = new UnsupportedStore();
        Assert.Throws<ArgumentException>(() => new AccountHandoffService(store));
        Assert.Equal(0, store.Calls);
        Assert.Throws<ArgumentNullException>(() => new AccountHandoffService(null!));
    }

    private sealed class UnsupportedStore : IAccountHandoffTicketStore
    {
        internal int Calls { get; private set; }
        public void Save(AccountHandoffTicket ticket) { Calls++; throw new InvalidOperationException(); }
        public bool TryGet(string token, out AccountHandoffTicket ticket)
        { Calls++; ticket = null!; throw new InvalidOperationException(); }
    }
}
