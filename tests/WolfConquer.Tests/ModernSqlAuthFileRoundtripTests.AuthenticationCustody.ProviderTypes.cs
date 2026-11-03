using WolfConquer.Core.Accounts;
using AccountColumn = WolfConquer.Tests.OwnedModernSqlAuthDatabase.AuthenticationAccountColumn;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationTinyBooleanColumnsRemainNumericInBothRepositoryPaths() => RunAuthenticationCustodyAsync(async f =>
    {
        await f.Database.UseTinyAuthenticationPermissionColumnsAsync();
        var rows = await f.Database.SnapshotAuthenticationRowsAsync();
        var permissions = await f.Database.SnapshotTinyAuthenticationPermissionsAsync();
        Assert.Contains("501|2|0", permissions);
        var record = Assert.Single(await f.Database.CreateRepository().FindByUsernameAsync(OwnedModernSqlAuthDatabase.FirstUsername));
        Assert.Equal(2L, record.BannedId);
        Assert.False(record.IsOnline);
        var owner = await f.RequireOwnerAsync();
        Assert.Equal(record.Id, owner.AccountId);
        Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyAsync());
        Assert.Equal(rows, await f.Database.SnapshotAuthenticationRowsAsync());
        Assert.Equal(permissions, await f.Database.SnapshotTinyAuthenticationPermissionsAsync());
        await owner.DisposeAsync();
        await f.Database.AssertAuthenticationClaimsReleasedAsync(owner.AccountId);

        foreach (long online in new[] { 1L, -7L })
        {
            await f.Database.SetAuthenticationAccountColumnAsync(owner.AccountId, AccountColumn.Online, online);
            rows = await f.Database.SnapshotAuthenticationRowsAsync();
            permissions = await f.Database.SnapshotTinyAuthenticationPermissionsAsync();
            Assert.Contains("501|2|" + online.ToString(System.Globalization.CultureInfo.InvariantCulture), permissions);
            record = Assert.Single(await f.Database.CreateRepository().FindByUsernameAsync(OwnedModernSqlAuthDatabase.FirstUsername));
            Assert.Equal(2L, record.BannedId);
            Assert.True(record.IsOnline);
            AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.AlreadyOnline);
            Assert.Equal(rows, await f.Database.SnapshotAuthenticationRowsAsync());
            Assert.Equal(permissions, await f.Database.SnapshotTinyAuthenticationPermissionsAsync());
            await f.Database.AssertAuthenticationClaimsReleasedAsync(owner.AccountId);
        }

        await f.Database.SetAuthenticationAccountColumnAsync(owner.AccountId, AccountColumn.Online, 0L);
        await f.Database.SetAuthenticationAccountColumnAsync(owner.AccountId, AccountColumn.BannedId, 1L);
        rows = await f.Database.SnapshotAuthenticationRowsAsync();
        permissions = await f.Database.SnapshotTinyAuthenticationPermissionsAsync();
        Assert.Contains("501|1|0", permissions);
        record = Assert.Single(await f.Database.CreateRepository().FindByUsernameAsync(OwnedModernSqlAuthDatabase.FirstUsername));
        Assert.Equal(1L, record.BannedId);
        Assert.False(record.IsOnline);
        AssertAuthenticationRefused(await f.AcquireAsync(), SqlAccountAuthenticationFailure.Banned);
        Assert.Equal(rows, await f.Database.SnapshotAuthenticationRowsAsync());
        Assert.Equal(permissions, await f.Database.SnapshotTinyAuthenticationPermissionsAsync());
        await f.Database.AssertAuthenticationClaimsReleasedAsync(owner.AccountId);
    });
}
