namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerVipTransactionTests
{
    [Theory]
    [InlineData((byte)35, 30UL, 6UL)]
    [InlineData((byte)197, 100UL, 7UL)]
    public async Task CommittedFirstSendRetirementKeepsTheDebitAndStopsTheRemainingSuccessTail(byte option, ulong cost, ulong level)
    {
        using var fixture = new Fixture();
        var before = fixture.Current;
        var bytes = File.ReadAllBytes(fixture.UserPath);
        var context = fixture.Context;
        var retired = 0;
        Exception? observationFailure = null;
        fixture.BeforeSend = frame =>
        {
            if (!IsVipUpdate(frame)) return;
            retired++;
            observationFailure = Record.Exception(() =>
            {
                fixture.AssertCommitted(before, 150 - cost, level, FutureExpiry.AddDays(30), bytes);
                fixture.Trades.MarkConnectionEnded(fixture.Connection);
                Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));
            });
        };

        await fixture.Purchase(option, context);

        Assert.Null(observationFailure);
        Assert.Equal(1, retired);
        Assert.Single(fixture.Sent, IsVipUpdate);
        Assert.Single(fixture.Sent);
        Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
        fixture.AssertDurable(before, 150 - cost, level, FutureExpiry.AddDays(30), bytes);
        var committedFiles = fixture.Files();
        await fixture.Purchase(option, context);
        fixture.AssertFiles(committedFiles);
        Assert.Single(fixture.Sent);
    }

    [Theory]
    [InlineData((byte)35, 30UL, 6UL)]
    [InlineData((byte)197, 100UL, 7UL)]
    public async Task ActualTransportFailureAfterCommitCannotRefundOrReplayTheCapturedPurchase(byte option, ulong cost, ulong level)
    {
        using var fixture = new Fixture();
        var before = fixture.Current;
        var bytes = File.ReadAllBytes(fixture.UserPath);
        var context = fixture.Context;
        var attempts = 0;
        Exception? observationFailure = null;
        fixture.BeforeSend = frame =>
        {
            if (!IsVipUpdate(frame)) return;
            attempts++;
            observationFailure = Record.Exception(() =>
                fixture.AssertCommitted(before, 150 - cost, level, FutureExpiry.AddDays(30), bytes));
            throw new IOException("Owned transport fixture failed before accepting this frame.");
        };

        try { await fixture.Purchase(option, context); }
        catch (IOException) { } // A caller may propagate the real failed send; it must retain the commit.

        Assert.Null(observationFailure);
        Assert.Equal(1, attempts);
        Assert.Empty(fixture.Sent);
        fixture.AssertCommitted(before, 150 - cost, level, FutureExpiry.AddDays(30), bytes);
        var committed = fixture.Current;
        var files = fixture.Files();
        fixture.BeforeSend = null;
        await fixture.Purchase(option, context);
        fixture.AssertUnchanged(committed, files, 0);
        Assert.Empty(fixture.Sent);
    }
}
