using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerVipTransactionTests
{
    [Fact]
    public async Task RemovedActualOwnerCannotBeRecreatedByACapturedNpcContext()
    {
        using var fixture = new Fixture();
        var context = fixture.Context;
        var files = fixture.Files();
        Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));

        await fixture.Purchase(35, context);

        Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
        fixture.AssertFiles(files);
        Assert.DoesNotContain(fixture.Sent, IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenuineClockCallbackCannotOverwriteAReplacedOrRemovedPreparedOwner(bool replace)
    {
        using var fixture = new Fixture();
        var context = fixture.Context;
        var original = fixture.Current;
        var replacement = original with { Username = "replacement-owner" };
        var files = fixture.Files();
        var called = 0;
        fixture.Clock.BeforeRead = () =>
        {
            fixture.Clock.BeforeRead = null;
            called++;
            if (replace) fixture.Sessions.Set(fixture.Connection, replacement);
            else Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));
        };

        await fixture.Purchase(35, context);

        Assert.Equal(1, called);
        if (replace) Assert.Same(replacement, fixture.Current);
        else Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
        fixture.AssertFiles(files);
        Assert.DoesNotContain(fixture.Sent, IsSuccess);
    }

    [Fact]
    public async Task GenuineClockCallbackRetirementCannotCommitToAnEndedConnection()
    {
        using var fixture = new Fixture();
        var before = fixture.Current;
        var files = fixture.Files();
        var called = 0;
        fixture.Clock.BeforeRead = () =>
        {
            fixture.Clock.BeforeRead = null;
            called++;
            fixture.Trades.MarkConnectionEnded(fixture.Connection);
        };

        await fixture.Purchase(35);

        Assert.Equal(1, called);
        Assert.True(fixture.Trades.HasConnectionEnded(fixture.Connection));
        fixture.AssertUnchanged(before, files, 0);
    }

    [Theory]
    [InlineData((byte)35, 30UL, 6UL)]
    [InlineData((byte)197, 100UL, 7UL)]
    public async Task TwoOverlappingRequestsCannotSpendTheSameSinglePurchaseBudget(byte option, ulong cost, ulong level)
    {
        using var fixture = new Fixture(points: cost);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var before = fixture.Current;
        var bytes = File.ReadAllBytes(fixture.UserPath);
        var context = fixture.Context;
        var claimedRead = 0;
        fixture.Clock.BeforeRead = () =>
        {
            if (Interlocked.Exchange(ref claimedRead, 1) != 0) return;
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)), "The real clock read was not released.");
        };
        Task? first = null;
        Task? second = null;
        Exception? primaryFailure = null;
        try
        {
            first = Task.Run(() => fixture.Purchase(option, context, cancellation.Token));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "The first request did not reach its actual clock read.");
            second = Task.Run(async () =>
            {
                secondStarted.Set();
                await fixture.Purchase(option, context, cancellation.Token);
            });
            Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(10)));
            release.Set();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

            fixture.AssertCommitted(before, 0, level, FutureExpiry.AddDays(30), bytes);
            AssertBurst(fixture.Sent.Where(IsSuccess).ToArray(), level);
        }
        catch (Exception error)
        {
            primaryFailure = error;
            throw;
        }
        finally
        {
            release.Set();
            var tasks = new[] { first, second }.OfType<Task>().ToArray();
            if (tasks.Any(task => !task.IsCompleted)) cancellation.Cancel();
            try { await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception cleanup)
            {
                if (primaryFailure is null) throw;
                if (!ReferenceEquals(primaryFailure, cleanup))
                    throw new AggregateException("VIP request and task cleanup both failed.", primaryFailure, cleanup);
            }
        }
    }
}
