using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffColdRedemptionKeepsTrueAccountSeparateFromCharacterAndIsOneUse() => RunSqlHandoffAsync(async f =>
    {
        var before = await f.SnapshotAsync();
        var offer = await f.RequireOfferAsync();
        Assert.Equal((long)SqlWorldStateTestData.FirstAccount, offer.AccountId);
        Assert.Equal(SqlWorldStateTestData.FirstCharacter, offer.CharacterUid);
        Assert.NotEqual(offer.AccountId, (long)offer.CharacterUid);
        Assert.Equal(OwnedModernSqlAuthDatabase.FirstUsername, offer.Username);
        Assert.Equal(SqlHandoffFixture.Endpoint, offer.WorldEndpoint);
        Assert.Equal(TimeSpan.Zero, offer.IssuedAtUtc.Offset); Assert.Equal(TimeSpan.Zero, offer.ExpiresAtUtc.Offset);
        Assert.Equal(TimeSpan.FromMinutes(2), offer.ExpiresAtUtc - offer.IssuedAtUtc);
        Assert.True(offer.Token.Length == 64 && offer.Token.All(value => value is >= '0' and <= '9' or >= 'A' and <= 'F'));
        Assert.False((offer.ToString() ?? "").Contains(offer.Token, StringComparison.Ordinal));
        Assert.Equal((1L, 1L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());
        await AssertSqlHandoffRowsUnchangedAsync(f, before, includeHandoffs: false);

        var cold = f.Database.CreateWorldStateRepository();
        Assert.NotSame(f.World, cold);
        var result = await f.RedeemAsync(offer, SqlHandoffFixture.Endpoint with { Role = "DifferentDisplayRole" }, cold);
        var owner = await AssertSqlHandoffRedeemedAsync(f, result);
        Assert.Equal((1L, 0L, 1L), await f.Database.ObserveSqlHandoffCountsAsync());
        var committed = await f.SnapshotAsync();
        AssertSqlHandoffRedeemRefused(await f.RedeemAsync(offer), SqlAccountWorldHandoffFailure.AlreadyRedeemed);
        await AssertSqlHandoffRowsUnchangedAsync(f, committed);
        await offer.DisposeAsync();
        await f.Database.WaitForSqlHandoffIssuerReleaseAsync(offer.AccountId);
        Assert.Equal(SqlWorldStateReadStatus.Found, (await owner.ReadBundleAsync()).Status);
        await owner.DisposeAsync();
        AssertSqlHandoffRedeemRefused(await f.RedeemAsync(offer), SqlAccountWorldHandoffFailure.AlreadyRedeemed);
        await AssertSqlHandoffRowsUnchangedAsync(f, committed);
        await AssertSqlHandoffRowsUnchangedAsync(f, before, includeHandoffs: false);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffConcurrentSameTokenReturnsExactlyOneCurrentWorldOwner() => RunSqlHandoffAsync(async f =>
    {
        var offer = await f.RequireOfferAsync();
        var before = await f.SnapshotAsync();
        var left = f.RedeemAsync(offer, repository: f.Database.CreateWorldStateRepository());
        var right = f.RedeemAsync(offer, repository: f.Database.CreateWorldStateRepository());
        // Both actual started operations settle even when either faults. Every
        // returned custody is registered by the fixture before task completion.
        var results = await Task.WhenAll(left, right);
        await AssertSqlHandoffRedeemedAsync(f, Assert.Single(results, result => result.IsRedeemed));
        AssertSqlHandoffRedeemRefused(Assert.Single(results, result => !result.IsRedeemed), SqlAccountWorldHandoffFailure.AlreadyRedeemed);
        Assert.Equal((1L, 0L, 1L), await f.Database.ObserveSqlHandoffCountsAsync());
        await AssertSqlHandoffRowsUnchangedAsync(f, before, includeHandoffs: false);
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlHandoffRetainsAuthenticationExclusionButReleasesWorldAndAllowsAnotherAccount() => RunSqlHandoffAsync(async f =>
    {
        var first = await f.RequireOfferAsync();
        var before = await f.SnapshotAsync();
        AssertSqlHandoffIssueRefused(await f.IssueAsync(), SqlAccountWorldHandoffFailure.SessionConflict);
        await AssertSqlHandoffRowsUnchangedAsync(f, before);
        var world = await f.RequireWorldOwnerAsync(first.AccountId);
        Assert.Equal(first.CharacterUid, world.CharacterUid);
        var second = await f.RequireOfferAsync(username: OwnedModernSqlAuthDatabase.SecondUsername);
        Assert.Equal((long)SqlWorldStateTestData.SecondAccount, second.AccountId);
        Assert.Equal(SqlWorldStateTestData.SecondCharacter, second.CharacterUid);
        Assert.False(string.Equals(first.Token, second.Token, StringComparison.Ordinal));
        Assert.Equal((2L, 2L, 0L), await f.Database.ObserveSqlHandoffCountsAsync());
        await AssertSqlHandoffRowsUnchangedAsync(f, before, includeHandoffs: false);
    });
}
