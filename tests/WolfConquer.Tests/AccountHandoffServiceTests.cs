using WolfConquer.Core.Accounts;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class AccountHandoffServiceTests : IDisposable
{
    private const string SigningSecret = "test-handoff-signing-secret";
    private readonly string _handoffRoot = Path.Combine(Path.GetTempPath(), "wolf-handoff-store-" + Guid.NewGuid());
    private DateTimeOffset _now = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IssueCreatesPendingTicketForAccountAndWorldEndpoint()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var service = CreateService(store);

        var ticket = service.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);

        Assert.False(string.IsNullOrWhiteSpace(ticket.Token));
        Assert.Equal(1293600, ticket.AccountId);
        Assert.Equal("user02", ticket.Username);
        Assert.Equal(ConquerRuntimeDefaults.WorldServer, ticket.WorldEndpoint);
        Assert.Equal(_now, ticket.IssuedAtUtc);
        Assert.Equal(_now.AddMinutes(2), ticket.ExpiresAtUtc);
        Assert.False(ticket.IsRedeemed);
        Assert.True(store.TryGet(ticket.Token, out var stored));
        Assert.Equal(ticket, stored);
    }

    [Fact]
    public void RedeemReturnsSuccessForUnexpiredTicket()
    {
        var service = CreateService();
        var ticket = service.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);

        var result = service.Redeem(ticket.Token);

        Assert.Equal(AccountHandoffRedeemStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(ticket.Token, result.Ticket?.Token);
        Assert.True(result.Ticket?.IsRedeemed);
    }

    [Fact]
    public void RedeemRejectsUnknownTicket()
    {
        var service = CreateService();

        var result = service.Redeem("missing-ticket");

        Assert.Equal(AccountHandoffRedeemStatus.UnknownTicket, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Ticket);
    }

    [Fact]
    public void RedeemRejectsExpiredTicket()
    {
        var service = CreateService();
        var ticket = service.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        _now = _now.AddMinutes(3);

        var result = service.Redeem(ticket.Token);

        Assert.Equal(AccountHandoffRedeemStatus.ExpiredTicket, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(ticket.Token, result.Ticket?.Token);
        Assert.False(result.Ticket?.IsRedeemed);
    }

    [Fact]
    public void RedeemRejectsSecondUseOfTicket()
    {
        var service = CreateService();
        var ticket = service.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);

        Assert.Equal(AccountHandoffRedeemStatus.Success, service.Redeem(ticket.Token).Status);
        var result = service.Redeem(ticket.Token);

        Assert.Equal(AccountHandoffRedeemStatus.AlreadyRedeemed, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(ticket.Token, result.Ticket?.Token);
        Assert.True(result.Ticket?.IsRedeemed);
    }

    [Fact]
    public void RedeemReturnsSuccessForTicketIssuedBySeparateFileBackedService()
    {
        var issuer = CreateFileService();
        var ticket = issuer.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);

        var result = CreateFileService().Redeem(ticket.Token);

        Assert.Equal(AccountHandoffRedeemStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(ticket.Token, result.Ticket?.Token);
        Assert.Equal(1293600, result.Ticket?.AccountId);
        Assert.Equal("user02", result.Ticket?.Username);
        Assert.Equal(ConquerRuntimeDefaults.WorldServer, result.Ticket?.WorldEndpoint);
        Assert.True(result.Ticket?.IsRedeemed);
        Assert.True(new FileAccountHandoffTicketStore(_handoffRoot, SigningSecret).TryGet(ticket.Token, out var stored));
        Assert.True(stored.IsRedeemed);
        var ticketFileNames = Directory.EnumerateFiles(_handoffRoot).Select(Path.GetFileName).ToArray();
        Assert.DoesNotContain(ticket.Token + ".json", ticketFileNames);
        var ticketJson = File.ReadAllText(Directory.EnumerateFiles(_handoffRoot, "*.json").Single());
        Assert.DoesNotContain(ticket.Token, ticketJson, StringComparison.Ordinal);
        Assert.Contains("signature", ticketJson, StringComparison.Ordinal);
    }

    [Fact]
    public void RedeemRejectsSecondUseAcrossFileBackedServiceInstances()
    {
        var ticket = CreateFileService().Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);

        Assert.Equal(AccountHandoffRedeemStatus.Success, CreateFileService().Redeem(ticket.Token).Status);
        var result = CreateFileService().Redeem(ticket.Token);

        Assert.Equal(AccountHandoffRedeemStatus.AlreadyRedeemed, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(ticket.Token, result.Ticket?.Token);
        Assert.Equal(1293600, result.Ticket?.AccountId);
        Assert.True(result.Ticket?.IsRedeemed);
    }

    [Fact]
    public void RedeemRejectsExpiredTicketIssuedBySeparateFileBackedService()
    {
        var ticket = CreateFileService().Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        _now = _now.AddMinutes(3);

        var result = CreateFileService().Redeem(ticket.Token);

        Assert.Equal(AccountHandoffRedeemStatus.ExpiredTicket, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(ticket.Token, result.Ticket?.Token);
        Assert.Equal(1293600, result.Ticket?.AccountId);
        Assert.False(result.Ticket?.IsRedeemed);
    }

    [Fact]
    public async Task ParallelFileBackedRedeemsAllowOnlyOneSuccess()
    {
        var ticket = CreateFileService().Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => CreateFileService().Redeem(ticket.Token))));

        Assert.Single(results, result => result.Status == AccountHandoffRedeemStatus.Success);
        Assert.Equal(7, results.Count(result => result.Status == AccountHandoffRedeemStatus.AlreadyRedeemed));
    }

    [Fact]
    public void FileBackedStoreRejectsTamperedTicketPayload()
    {
        var ticket = CreateFileService().Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        var ticketPath = Directory.EnumerateFiles(_handoffRoot, "*.json").Single();
        var originalJson = File.ReadAllText(ticketPath);

        File.WriteAllText(ticketPath, originalJson.Replace("\"accountId\": 1293600", "\"accountId\": 1293601", StringComparison.Ordinal));

        var store = new FileAccountHandoffTicketStore(_handoffRoot, SigningSecret);
        Assert.False(store.TryGet(ticket.Token, out _));
        Assert.Equal(AccountHandoffRedeemStatus.UnknownTicket, CreateFileService().Redeem(ticket.Token).Status);
    }

    [Fact]
    public void FileBackedStoreRejectsNullSignedFieldsWithoutThrowing()
    {
        var ticket = CreateFileService().Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        var ticketPath = Directory.EnumerateFiles(_handoffRoot, "*.json").Single();
        var originalJson = File.ReadAllText(ticketPath);

        File.WriteAllText(ticketPath, originalJson.Replace("\"username\": \"user02\"", "\"username\": null", StringComparison.Ordinal));

        var store = new FileAccountHandoffTicketStore(_handoffRoot, SigningSecret);
        Assert.False(store.TryGet(ticket.Token, out _));
        Assert.Equal(AccountHandoffRedeemStatus.UnknownTicket, CreateFileService().Redeem(ticket.Token).Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_handoffRoot))
        {
            Directory.Delete(_handoffRoot, recursive: true);
        }
    }

    private AccountHandoffService CreateService(
        IAccountHandoffTicketStore? store = null)
    {
        return new AccountHandoffService(
            store ?? new InMemoryAccountHandoffTicketStore(),
            () => _now,
            TimeSpan.FromMinutes(2));
    }

    private AccountHandoffService CreateFileService()
    {
        return CreateService(new FileAccountHandoffTicketStore(_handoffRoot, SigningSecret));
    }
}
