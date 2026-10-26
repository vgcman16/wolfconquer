using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerFullPromotionTransactionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ARemovedOrReplacedRealOwnerCannotBeRecreatedByTheCapturedNpcContext(bool replace) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var context = fixture.Context;
            var files = Files(fixture);
            var replacement = before with { Username = "another-current-holder-publication" };
            if (replace) Assert.True(fixture.Sessions.TryCompareAndSet(fixture.Connection, before, replacement));
            else Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));

            await fixture.Purchase(context);

            if (replace) Assert.Same(replacement, fixture.Current);
            else Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            AssertFiles(fixture, files);
            Assert.Empty(fixture.Sent);
        });

    [Fact]
    public Task ActualTransportRetirementCannotSpendThroughAnEarlierNpcContext() =>
        RunAsync(async fixture =>
        {
            var context = fixture.Context;
            var files = Files(fixture);
            await fixture.Connection.CompleteTransportCloseAsync();
            Assert.True(fixture.Trades.HasConnectionEnded(fixture.Connection));
            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));

            await fixture.Purchase(context);

            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            AssertFiles(fixture, files);
            Assert.Empty(fixture.Sent);
        });

    [Fact]
    public Task TheActualBlockedRecoveryOwnerPreventsThePromotionBeforeAnySave() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            fixture.Recovery.Block("owned FullPromotion recovery refusal");
            await fixture.Purchase();
            AssertUnchanged(fixture, before, files);
            Assert.Empty(fixture.Sent);
        });

    [Theory]
    [InlineData("sessions")]
    [InlineData("trades")]
    [InlineData("recovery")]
    public Task MissingRealTransactionParticipantsDoNotGrantDefaultAcceptance(string missing) =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            var context = fixture.Context;
            context = missing switch
            {
                "sessions" => context with { Sessions = null },
                "trades" => context with { Trades = null },
                _ => context with { TradeRecovery = null },
            };
            await fixture.Purchase(context);
            AssertUnchanged(fixture, before, files);
            Assert.Empty(fixture.Sent);
        });

    [Fact]
    public Task TwoParallelRequestsForTheSameActualSessionCannotSpendOneBudgetTwice() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            var captured = fixture.Context;
            using var firstStarted = new ManualResetEventSlim();
            using var secondStarted = new ManualResetEventSlim();
            Task? first = null;
            Task? second = null;
            Exception? primary = null;
            try
            {
                lock (fixture.Trades.SyncRoot)
                {
                    first = fixture.Track(Task.Run(async () => { firstStarted.Set(); await fixture.Purchase(captured); }));
                    second = fixture.Track(Task.Run(async () => { secondStarted.Set(); await fixture.Purchase(captured); }));
                    Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(10)));
                    Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(10)));
                    Assert.False(first.IsCompleted);
                    Assert.False(second.IsCompleted);
                }
                await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

                AssertCommitted(fixture, before, files, 0, 1051, 3);
                Assert.Empty(fixture.Sent);
                Assert.Equal(1, fixture.Log.ToString().Split('\n').Count(line =>
                    line.StartsWith("WorldServer applied LegacyNpc AccountManager full promotion", StringComparison.Ordinal)));
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                try { await Task.WhenAll(new[] { first, second }.OfType<Task>()).WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception cleanup)
                {
                    if (primary is null) throw;
                    if (!ReferenceEquals(primary, cleanup)) throw new AggregateException(primary, cleanup);
                }
            }
        }, donation: "30");
}
