using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    // Called only after an actual owner close/loss. Busy may briefly persist
    // until the server observes that close; every other result fails immediately.
    private static async Task<SqlWorldBundleCustody> ReacquireClosedCustodyAsync(SqlWorldStateRepository repository, long accountId)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            var attempt = await repository.TryAcquireBundleCustodyAsync(accountId, deadline.Token);
            if (attempt.IsAcquired)
            {
                Assert.Equal(SqlWorldStateFailure.None, attempt.Failure);
                return Assert.IsType<SqlWorldBundleCustody>(attempt.Custody);
            }
            Assert.Null(attempt.Custody);
            Assert.Equal(SqlWorldStateFailure.SessionCustodyConflict, attempt.Failure);
            await Task.Delay(TimeSpan.FromMilliseconds(25), deadline.Token);
        }
    }
}
