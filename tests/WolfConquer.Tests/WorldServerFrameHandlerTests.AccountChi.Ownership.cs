namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AccountManagerChargeChiAtomicRemovedOrReplacedRealHolderCannotBeRecreated(bool replace) =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var context = fixture.Context;
            var files = ChiFiles(fixture);
            var replacement = before with { Username = "new-current-charge-chi-holder" };
            if (replace) Assert.True(fixture.Sessions.TryCompareAndSet(fixture.Connection, before, replacement));
            else Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));

            await fixture.Purchase(context);

            if (replace) Assert.Same(replacement, fixture.Current);
            else Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            AssertChiFiles(fixture, files);
            Assert.Empty(fixture.Sent);
            Assert.Equal(0, ChiAppliedCount(fixture));
        });

    [Fact]
    public Task AccountManagerChargeChiAtomicActualTransportRetirementStopsTheCapturedPurchase() =>
        RunChiPurchaseAsync(async fixture =>
        {
            var context = fixture.Context;
            await fixture.Connection.CompleteTransportCloseAsync();
            Assert.True(fixture.Trades.HasConnectionEnded(fixture.Connection));
            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            var files = ChiFiles(fixture);
            fixture.Sent.Clear();

            await fixture.Purchase(context);

            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            AssertChiFiles(fixture, files);
            Assert.Empty(fixture.Sent);
            Assert.Equal(0, ChiAppliedCount(fixture));
        });

    [Fact]
    public Task AccountManagerChargeChiAtomicActualBlockedRecoveryRejectsBeforeTheSave() =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);
            fixture.Recovery.Block("owned ChargeChi recovery refusal");

            await fixture.Purchase();

            AssertChiUnchanged(fixture, before, files);
            Assert.Equal(0, ChiAppliedCount(fixture));
        });

    [Theory]
    [InlineData("sessions")]
    [InlineData("trades")]
    [InlineData("recovery")]
    public Task AccountManagerChargeChiAtomicMissingRealParticipantsCannotAuthorizeThePurchase(string missing) =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);
            var context = fixture.Context;
            context = missing switch
            {
                "sessions" => context with { Sessions = null },
                "trades" => context with { Trades = null },
                _ => context with { TradeRecovery = null },
            };

            await fixture.Purchase(context);

            AssertChiUnchanged(fixture, before, files);
            Assert.Equal(0, ChiAppliedCount(fixture));
        });

    [Fact]
    public Task AccountManagerChargeChiAtomicParallelCapturedRequestsCannotDebitForTheSamePermanentBenefitTwice() =>
        RunChiPurchaseAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = ChiFiles(fixture);
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

                AssertChiCommitted(fixture, before, files, 100);
                Assert.Equal(1, ChiAppliedCount(fixture));
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
        }, donation: "200");
}
