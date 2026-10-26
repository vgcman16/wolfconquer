using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(1_100_003U, (ushort)3)]
    [InlineData(1_100_006U, (ushort)6)]
    [InlineData(1_100_009U, (ushort)12)]
    public Task RegisteredPhysicalSashBoundLockedRoundTripCommitsSevenStoresBeforeLegacyFrames(
        uint sashItemId, ushort capacity) => RunAsync(async fixture =>
    {
        fixture.SeedTransfer(sashItemId: sashItemId);
        var original = fixture.Capture();
        await AssertTransferShowAsync(fixture, capacity, empty: true);
        foreach (var withdraw in new[] { false, true })
        {
            var before = fixture.Capture();
            var observed = 0;
            fixture.SendObserver = _ => { AssertTransferCommitted(fixture, before, withdraw); observed++; };

            await fixture.InvokeTransferAsync(withdraw);

            AssertTransferCommitted(fixture, before, withdraw);
            AssertTransferFrames(fixture, before, withdraw);
            Assert.Equal(withdraw ? 2 : 4, observed);
            fixture.SendObserver = null;
            var committed = fixture.Capture();
            await fixture.InvokeTransferAsync(withdraw);
            fixture.AssertUnchanged(committed); // Repeated UID request cannot move or publish twice.
            await AssertTransferShowAsync(fixture, capacity, empty: withdraw);
        }
        Assert.Equal(JsonSerializer.Serialize(original.Owner.PlayerItems), JsonSerializer.Serialize(fixture.Current.PlayerItems));
        fixture.AssertFiles(original.Files); // Entire roundtrip restores the exact original file set/bytes.
    });

    [WindowsTradeJournalFailureFact]
    public Task RegisteredPhysicalSashHeldItemLeafRefusesDepositThenSameOwnerRetries() =>
        RunTransferSharingFailureAsync(withdraw: false);

    [WindowsTradeJournalFailureFact]
    public Task RegisteredPhysicalSashHeldItemLeafRefusesWithdrawThenSameOwnerRetries() =>
        RunTransferSharingFailureAsync(withdraw: true);

    private static Task RunTransferSharingFailureAsync(bool withdraw) => RunAsync(async fixture =>
    {
        fixture.SeedTransfer(deposited: withdraw);
        var before = fixture.Capture();
        fixture.HoldTransferItemFile();

        await fixture.InvokeTransferAsync(withdraw);

        fixture.ReleaseFiles(); // Actual invocation terminal first; owned lease remains in existing cleanup otherwise.
        fixture.AssertUnchanged(before);
        await fixture.InvokeTransferAsync(withdraw);
        AssertTransferCommitted(fixture, before, withdraw);
        AssertTransferFrames(fixture, before, withdraw);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredPhysicalSashSecondSendFailureCannotUndoOrRepeatDurableMove(bool withdraw) => RunAsync(async fixture =>
    {
        fixture.SeedTransfer(deposited: withdraw);
        var before = fixture.Capture();
        var injected = new IOException("owned physical sash postcommit notification failure");
        var attempted = 0;
        fixture.SendObserver = _ =>
        {
            AssertTransferCommitted(fixture, before, withdraw);
            if (++attempted == 2) throw injected;
        };

        var failure = await Record.ExceptionAsync(() => fixture.InvokeTransferAsync(withdraw));

        Assert.Same(injected, Assert.IsType<IOException>(failure));
        Assert.Equal(2, attempted);
        AssertTransferCommitted(fixture, before, withdraw);
        var frames = fixture.Sent.Skip(before.FrameCount).ToArray();
        Assert.Equal(2, frames.Length); // Attempted-send capture, not delivery proof.
        var expected = ExpectedTransferFrames(before, withdraw);
        Assert.Equal(expected[0], frames[0]);
        Assert.Equal(expected[1], frames[1]);
        fixture.SendObserver = null;
        var committed = fixture.Capture();
        await fixture.InvokeTransferAsync(withdraw);
        fixture.AssertUnchanged(committed);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public Task RegisteredPhysicalSashFirstFrameOwnerReplacementOrEndStopsRemainingFramesAndStaleSet(
        bool withdraw, bool endOwner) => RunAsync(async fixture =>
    {
        fixture.SeedTransfer(deposited: withdraw);
        var before = fixture.Capture();
        WorldSession? committed = null;
        WorldSession? replacement = null;
        var observed = 0;
        fixture.SendObserver = _ =>
        {
            Assert.Equal(1, ++observed);
            AssertTransferCommitted(fixture, before, withdraw);
            committed = fixture.Current;
            if (endOwner)
            {
                lock (fixture.Trades.SyncRoot) fixture.Trades.MarkConnectionEnded(fixture.Connection);
            }
            else
            {
                replacement = committed with { Character = committed.Character with { Name = "SameUidReplacement" } };
                Assert.Equal(committed.Character.Uid, replacement.Character.Uid);
                Assert.Equal(committed.AccountId, replacement.AccountId);
                fixture.Sessions.Set(fixture.Connection, replacement); // Actual store ownership change.
            }
        };

        await fixture.InvokeTransferAsync(withdraw);

        Assert.Equal(1, observed);
        Assert.NotNull(committed);
        Assert.Same(endOwner ? committed : replacement, fixture.Current);
        Assert.Equal(ExpectedTransferFrames(before, withdraw)[0], Assert.Single(fixture.Sent.Skip(before.FrameCount)));
        fixture.AssertFiles(ExpectedTransferFiles(fixture, before, withdraw));
        Assert.Equal(before.Json, JsonSerializer.Serialize(before.Owner));
        fixture.SendObserver = null;
        var after = fixture.Capture();
        await fixture.InvokeTransferAsync(withdraw);
        fixture.AssertUnchanged(after);
    });
}
