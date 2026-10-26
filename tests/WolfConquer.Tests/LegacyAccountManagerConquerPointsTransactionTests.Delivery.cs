namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerConquerPointsTransactionTests
{
    [Fact]
    public Task ActualWalletSendFailureAfterCommitCannotRefundOrReplayTheCapturedPurchase() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            var captured = fixture.Context;
            var attempts = 0;
            Exception? observationFailure = null;
            fixture.BeforeSend = frame =>
            {
                attempts++;
                observationFailure = Record.Exception(() =>
                {
                    AssertWalletFrame(frame, 1_000_000_400);
                    AssertCommitted(fixture, before, files, 125, 1_000_000_400);
                });
                throw new IOException("Owned transport failed before accepting the committed wallet update.");
            };

            try { await fixture.Purchase(captured); }
            catch (IOException) { } // Delivery can fail; the successful economic commit must survive.

            Assert.Null(observationFailure);
            Assert.Equal(1, attempts);
            Assert.Empty(fixture.Sent);
            AssertCommitted(fixture, before, files, 125, 1_000_000_400);
            Assert.False(fixture.Recovery.IsBlocked);
            AssertNoFalsePostcommitRejection(fixture);
            var committed = fixture.Current;
            var committedFiles = Files(fixture);
            fixture.BeforeSend = null;

            await fixture.Purchase(captured);

            AssertUnchanged(fixture, committed, committedFiles);
            Assert.Empty(fixture.Sent);
        }, donation: "225");

    [Fact]
    public Task RetirementAtTheCommittedWalletSendCannotRefundOrRecreateTheOldOwner() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            var captured = fixture.Context;
            Dictionary<string, byte[]>? committedFiles = null;
            var attempts = 0;
            Exception? observationFailure = null;
            fixture.BeforeSend = frame =>
            {
                attempts++;
                observationFailure = Record.Exception(() =>
                {
                    AssertWalletFrame(frame, 1_000_000_400);
                    AssertCommitted(fixture, before, files, 125, 1_000_000_400);
                    committedFiles = Files(fixture);
                    fixture.Trades.MarkConnectionEnded(fixture.Connection);
                    Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));
                });
            };

            await fixture.Purchase(captured);

            Assert.Null(observationFailure);
            Assert.Equal(1, attempts);
            AssertWalletFrame(Assert.Single(fixture.Sent), 1_000_000_400);
            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            AssertFiles(fixture, Assert.IsType<Dictionary<string, byte[]>>(committedFiles));
            fixture.BeforeSend = null;

            await fixture.Purchase(captured);

            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            AssertFiles(fixture, committedFiles!);
            Assert.Single(fixture.Sent);
        }, donation: "225");

    [Fact]
    public Task AFailedWalletSendAfterTheSpillBoxCannotRefundOrRepeatTheBankTransfer() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            var captured = fixture.Context;
            var attempts = 0;
            Exception? observationFailure = null;
            fixture.BeforeSend = frame =>
            {
                attempts++;
                observationFailure ??= Record.Exception(() =>
                    AssertCommitted(fixture, before, files, 125, 2_000_000_000, 500_000_444));
                if (!WolfConquer.Core.World.LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out _)) return;
                throw new IOException("Owned wallet send failed after the committed spill notification was accepted.");
            };

            try { await fixture.Purchase(captured); }
            catch (IOException) { }

            Assert.Null(observationFailure);
            Assert.Equal(2, attempts);
            Assert.True(WolfConquer.Core.World.LegacyNpcReplyPacket.TryParse(ReadFrame(Assert.Single(fixture.Sent)), out var box));
            Assert.Equal("500000000 Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.", box.Text);
            AssertCommitted(fixture, before, files, 125, 2_000_000_000, 500_000_444);
            AssertNoFalsePostcommitRejection(fixture);
            var committed = fixture.Current;
            var committedFiles = Files(fixture);
            fixture.BeforeSend = null;

            await fixture.Purchase(captured);

            AssertUnchanged(fixture, committed, committedFiles);
            Assert.Single(fixture.Sent);
        }, donation: "225", conquerPoints: "1500000000");

    [Fact]
    public Task RetirementAtTheCommittedSpillBoxStopsTheWalletTailAndPreservesTheBankImage() =>
        RunAsync(async fixture =>
        {
            var before = fixture.Current;
            var files = Files(fixture);
            var captured = fixture.Context;
            Dictionary<string, byte[]>? committedFiles = null;
            var attempts = 0;
            Exception? observationFailure = null;
            fixture.BeforeSend = frame =>
            {
                attempts++;
                observationFailure = Record.Exception(() =>
                {
                    AssertCommitted(fixture, before, files, 125, 2_000_000_000, 500_000_444);
                    Assert.True(WolfConquer.Core.World.LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var box));
                    Assert.Equal("500000000 Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.", box.Text);
                    committedFiles = Files(fixture);
                    fixture.Trades.MarkConnectionEnded(fixture.Connection);
                    Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));
                });
            };

            await fixture.Purchase(captured);

            Assert.Null(observationFailure);
            Assert.Equal(1, attempts);
            Assert.Single(fixture.Sent);
            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            AssertFiles(fixture, Assert.IsType<Dictionary<string, byte[]>>(committedFiles));
            fixture.BeforeSend = null;

            await fixture.Purchase(captured);

            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
            AssertFiles(fixture, committedFiles!);
            Assert.Single(fixture.Sent);
        }, donation: "225", conquerPoints: "1500000000");

    private static void AssertNoFalsePostcommitRejection(Fixture fixture)
    {
        Assert.DoesNotContain("WorldServer skipped LegacyNpc AccountManager conquer point purchase", fixture.Log.ToString());
        Assert.DoesNotContain("WorldServer held for recovery LegacyNpc AccountManager conquer point purchase", fixture.Log.ToString());
    }
}
