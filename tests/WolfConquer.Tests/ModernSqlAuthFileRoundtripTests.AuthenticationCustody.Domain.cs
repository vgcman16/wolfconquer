using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationVerifiesSameOwnedDomainWithActualTargetConnectionAndTransaction() => RunAuthenticationCustodyAsync(async f =>
    {
        var before = await f.Database.SnapshotAuthenticationRowsAsync();
        var owner = await f.RequireOwnerAsync();
        var target = await f.OpenDomainAsync();
        Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyDomainAsync(target, null, default));
        var transaction = await f.BeginDomainTransactionAsync(target);
        Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyDomainAsync(target, transaction, default));
        Assert.Equal(SqlAccountAuthenticationFailure.None, await owner.VerifyAsync());
        Assert.Equal(before, await f.Database.SnapshotAuthenticationRowsAsync());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlAuthenticationForeignOwnedSchemaRefusalPreservesHealthyLocalOwner() => RunAuthenticationCustodyAsync(async f =>
    {
        var foreign = await f.CreateOtherDatabaseAsync();
        Assert.NotEqual(f.Database.SchemaName, foreign.SchemaName);
        var localBefore = await f.Database.SnapshotAuthenticationRowsAsync();
        var foreignBefore = await foreign.SnapshotAuthenticationRowsAsync();
        var local = await f.RequireOwnerAsync();
        var other = await f.RequireOwnerAsync(database: foreign);
        Assert.Equal(local.AccountId, other.AccountId);
        Assert.Equal(local.CharacterUid, other.CharacterUid);
        var target = await f.OpenDomainAsync(foreign);
        // These are two actual nonce-owned schemas on the configured fixture
        // endpoint. This control does not claim a second physical SQL server.
        Assert.Equal(SqlAccountAuthenticationFailure.DomainConflict, await local.VerifyDomainAsync(target, null, default));
        Assert.Equal(SqlAccountAuthenticationFailure.None, await other.VerifyDomainAsync(target, null, default));
        Assert.Equal(SqlAccountAuthenticationFailure.None, await local.VerifyAsync());
        Assert.Equal(SqlAccountAuthenticationFailure.None, await other.VerifyAsync());
        var same = await f.OpenDomainAsync();
        Assert.Equal(SqlAccountAuthenticationFailure.None, await local.VerifyDomainAsync(same, null, default));
        Assert.Equal(localBefore, await f.Database.SnapshotAuthenticationRowsAsync());
        Assert.Equal(foreignBefore, await foreign.SnapshotAuthenticationRowsAsync());
    });
}
