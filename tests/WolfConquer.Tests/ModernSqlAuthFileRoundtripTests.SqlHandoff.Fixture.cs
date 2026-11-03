using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Runtime;
using AccountColumn = WolfConquer.Tests.OwnedModernSqlAuthDatabase.AuthenticationAccountColumn;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private static async Task RunSqlHandoffAsync(Func<SqlHandoffFixture, Task> body,
        bool seedWorld = true, bool initializeHandoff = true)
    {
        var fixture = new SqlHandoffFixture();
        var errors = new List<Exception>();
        try { await fixture.InitializeAsync(seedWorld, initializeHandoff); await body(fixture); }
        catch (Exception error) { errors.Add(error); }
        await fixture.CleanupAsync(errors);
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private sealed class SqlHandoffFixture
    {
        internal static readonly ConquerEndpoint Endpoint = new("OwnedSqlWorld", "127.0.0.1", 19165);
        private readonly List<OwnedModernSqlAuthDatabase> databases = [];
        private readonly ConcurrentDictionary<SqlAccountWorldHandoffOffer, byte> offers = new();
        private readonly ConcurrentBag<SqlAccountAuthenticationCustody> authenticationOwners = [];
        private readonly ConcurrentBag<SqlWorldBundleCustody> worldOwners = [];
        internal OwnedModernSqlAuthDatabase Database => databases[0];
        internal SqlWorldStateRepository World { get; private set; } = null!;
        internal SqlWorldStateBundle? FirstImage { get; private set; }

        internal async Task InitializeAsync(bool seedWorld, bool initializeHandoff)
        {
            await CreateOtherDatabaseAsync(seedWorld, initializeHandoff);
            World = Database.CreateWorldStateRepository();
        }

        internal async Task<OwnedModernSqlAuthDatabase> CreateOtherDatabaseAsync(bool seedWorld = true, bool initializeHandoff = true)
        {
            var database = await OwnedModernSqlAuthDatabase.CreateAsync(SqlWorldStateTestData.FirstAccount, SqlWorldStateTestData.SecondAccount);
            databases.Add(database);
            var repository = database.CreateWorldStateRepository();
            await repository.InitializeSchemaAsync();
            await database.SetAuthenticationAccountColumnAsync(SqlWorldStateTestData.FirstAccount, AccountColumn.EntityId, (long)SqlWorldStateTestData.FirstCharacter);
            await database.SetAuthenticationAccountColumnAsync(SqlWorldStateTestData.SecondAccount, AccountColumn.EntityId, (long)SqlWorldStateTestData.SecondCharacter);
            if (seedWorld)
            {
                var initial = await SeedBundlePairAsync(database, repository);
                if (databases.Count == 1) FirstImage = initial.First;
            }
            if (initializeHandoff) await repository.EnsureAccountHandoffSchemaAsync();
            return database;
        }

        internal SqlAccountWorldHandoffIssuer CreateIssuer(TimeSpan? lifetime = null,
            OwnedModernSqlAuthDatabase? accountDatabase = null, OwnedModernSqlAuthDatabase? worldDatabase = null) =>
            new((accountDatabase ?? Database).CreateRepository(),
                worldDatabase is null ? World : worldDatabase.CreateWorldStateRepository(), Endpoint, lifetime);

        internal async Task<SqlAccountWorldHandoffIssueResult> IssueAsync(SqlAccountWorldHandoffIssuer? issuer = null,
            string username = OwnedModernSqlAuthDatabase.FirstUsername,
            string password = OwnedModernSqlAuthDatabase.SyntheticPassword, CancellationToken token = default)
        {
            var result = await (issuer ?? CreateIssuer()).IssueAsync(new AccountLoginRequest(username, password), token);
            if (result.Offer is { } offer) offers.TryAdd(offer, 0);
            return result;
        }

        internal async Task<SqlAccountWorldHandoffOffer> RequireOfferAsync(SqlAccountWorldHandoffIssuer? issuer = null,
            string username = OwnedModernSqlAuthDatabase.FirstUsername)
        {
            var issued = await IssueAsync(issuer, username);
            Assert.True(issued.IsIssued);
            Assert.Equal(SqlAccountWorldHandoffFailure.None, issued.Failure);
            return Assert.IsType<SqlAccountWorldHandoffOffer>(issued.Offer);
        }

        internal Task<SqlAccountWorldHandoffRedeemResult> RedeemAsync(SqlAccountWorldHandoffOffer offer,
            ConquerEndpoint? endpoint = null, SqlWorldStateRepository? repository = null, CancellationToken token = default) =>
            RedeemTokenAsync(offer.Token, endpoint, repository, token);

        internal async Task<SqlAccountWorldHandoffRedeemResult> RedeemTokenAsync(string token,
            ConquerEndpoint? endpoint = null, SqlWorldStateRepository? repository = null, CancellationToken cancellationToken = default)
        {
            var result = await (repository ?? Database.CreateWorldStateRepository())
                .RedeemAccountHandoffAsync(token, endpoint ?? Endpoint, cancellationToken);
            if (result.Custody is { } custody) worldOwners.Add(custody);
            return result;
        }

        internal async Task<SqlWorldBundleCustody> RequireWorldOwnerAsync(long accountId)
        {
            var acquired = await Database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(accountId);
            if (acquired.Custody is { } custody) worldOwners.Add(custody);
            Assert.True(acquired.IsAcquired);
            Assert.Equal(SqlWorldStateFailure.None, acquired.Failure);
            return Assert.IsType<SqlWorldBundleCustody>(acquired.Custody);
        }

        internal async Task<SqlAccountAuthenticationCustody> RequireFreshAuthenticationOwnerAsync()
        {
            var acquired = await Database.CreateRepository().TryAcquireAuthenticationAsync(
                new AccountLoginRequest(OwnedModernSqlAuthDatabase.FirstUsername, OwnedModernSqlAuthDatabase.SyntheticPassword));
            if (acquired.Custody is { } custody) authenticationOwners.Add(custody);
            Assert.True(acquired.IsAcquired); Assert.Equal(SqlAccountAuthenticationFailure.None, acquired.Failure);
            return Assert.IsType<SqlAccountAuthenticationCustody>(acquired.Custody);
        }

        // Used only after the KILL case has joined disposal, observed every
        // allowed provider error, proved the original session gone and acquired
        // a fresh real auth owner. Other offers and cleanup failures stay tracked.
        internal bool UnregisterJoinedKilledOffer(SqlAccountWorldHandoffOffer offer) => offers.TryRemove(offer, out _);

        internal async Task<(string[] Accounts, string[] World, string[] Handoffs)> SnapshotAsync() =>
            (await Database.SnapshotAuthenticationRowsAsync(), await Database.SnapshotSqlHandoffWorldRowsAsync(),
                await Database.SnapshotSqlHandoffTablesAsync());

        internal async Task CleanupAsync(List<Exception> errors)
        {
            foreach (var offer in offers.Keys)
                try { await offer.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var owner in authenticationOwners)
                try { await owner.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var owner in worldOwners)
                try { await owner.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var database in databases)
            {
                try { await database.AssertAuthenticationClaimsReleasedAsync(SqlWorldStateTestData.FirstAccount, SqlWorldStateTestData.SecondAccount); }
                catch (Exception error) { errors.Add(error); }
                try { await database.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            }
        }
    }

    private static void AssertSqlHandoffIssueRefused(SqlAccountWorldHandoffIssueResult result, SqlAccountWorldHandoffFailure failure)
    {
        Assert.False(result.IsIssued); Assert.Null(result.Offer); Assert.Equal(failure, result.Failure);
    }

    private static void AssertSqlHandoffRedeemRefused(SqlAccountWorldHandoffRedeemResult result, SqlAccountWorldHandoffFailure failure)
    {
        Assert.False(result.IsRedeemed); Assert.Null(result.Custody); Assert.Equal(failure, result.Failure);
    }

    private static async Task AssertSqlHandoffRowsUnchangedAsync(SqlHandoffFixture f,
        (string[] Accounts, string[] World, string[] Handoffs) before, bool includeHandoffs = true)
    {
        var after = await f.SnapshotAsync();
        Assert.Equal(before.Accounts, after.Accounts); Assert.Equal(before.World, after.World);
        if (includeHandoffs) Assert.Equal(before.Handoffs, after.Handoffs);
    }

    private static async Task<SqlWorldBundleCustody> AssertSqlHandoffRedeemedAsync(SqlHandoffFixture f,
        SqlAccountWorldHandoffRedeemResult result)
    {
        Assert.True(result.IsRedeemed); Assert.Equal(SqlAccountWorldHandoffFailure.None, result.Failure);
        Assert.Equal((long)SqlWorldStateTestData.FirstAccount, result.AccountId);
        Assert.Equal(SqlWorldStateTestData.FirstCharacter, result.CharacterUid);
        Assert.Equal(OwnedModernSqlAuthDatabase.FirstUsername, result.Username);
        var owner = Assert.IsType<SqlWorldBundleCustody>(result.Custody);
        Assert.Equal(result.AccountId, owner.AccountId); Assert.Equal(result.CharacterUid, owner.CharacterUid);
        var read = await owner.ReadBundleAsync();
        Assert.Equal(SqlWorldStateReadStatus.Found, read.Status); Assert.Equal(SqlWorldStateFailure.None, read.Failure);
        var record = Assert.IsType<SqlWorldStateBundleRecord>(read.Record);
        Assert.Equal(1L, record.Revision); AssertBundleImage(Assert.IsType<SqlWorldStateBundle>(f.FirstImage), record.Image);
        return owner;
    }
}
