using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task RegisteredSashPhysicalSendFaultKeepsCommittedWalletAndRetryOnlyReportsInsufficientFunds() => RunAsync(async fixture =>
    {
        fixture.Seed(cp: 150, count: 0);
        var before = fixture.Capture();
        var committedFiles = ExpectedPostcommitSashFiles(fixture, before);
        var firstFrame = PostcommitSashCpFrame();
        var sendFailure = new IOException("Owned sash-upgrade postcommit physical send failure.");
        var observed = 0;
        fixture.SendObserver = frame =>
        {
            // This is the real connection send delegate, not a persistence callback.
            AssertPostcommitSashState(fixture, before, committedFiles);
            Assert.Equal(firstFrame, frame);
            Assert.Equal(1, ++observed);
            throw sendFailure;
        };

        var actualFailure = await Assert.ThrowsAsync<IOException>(() => fixture.InvokeAsync());

        Assert.Same(sendFailure, actualFailure);
        Assert.Equal(1, observed);
        // Sent records the attempted physical frame before its observer throws.
        Assert.Equal(firstFrame, Assert.Single(fixture.Sent.Skip(before.FrameCount)));
        AssertPostcommitSashState(fixture, before, committedFiles);
        var committed = fixture.Capture();
        fixture.SendObserver = null;

        // This action has no nonce: this retry is unaffordable, not replay-deduplicated.
        await fixture.InvokeAsync();

        Assert.Same(committed.Owner, fixture.Current);
        Assert.Equal(committed.Json, JsonSerializer.Serialize(fixture.Current));
        fixture.AssertFiles(committed.Files);
        Assert.Equal(LegacyMsgMessagePacket.SystemTalk(
                "You need 90 CPs in your inventory to open 1 slot in your sash.").ToFrameBytes(),
            Assert.Single(fixture.Sent.Skip(committed.FrameCount)));
        AssertPostcommitSashState(fixture, before, committedFiles);
    });

    [Fact]
    public Task RegisteredSashRealEndedMarkerAfterFirstPhysicalSendStopsRemainingSuccessWithoutUndoingCommit() => RunAsync(async fixture =>
    {
        fixture.Seed(cp: 150, count: 0);
        var before = fixture.Capture();
        var committedFiles = ExpectedPostcommitSashFiles(fixture, before);
        var observed = 0;
        WorldSession? committedOwner = null;
        fixture.SendObserver = frame =>
        {
            AssertPostcommitSashState(fixture, before, committedFiles);
            Assert.Equal(PostcommitSashCpFrame(), frame);
            Assert.Equal(1, ++observed);
            committedOwner = fixture.Current;
            lock (fixture.Trades.SyncRoot)
            {
                Assert.False(fixture.Trades.HasConnectionEnded(fixture.Connection));
                fixture.Trades.MarkConnectionEnded(fixture.Connection);
                Assert.True(fixture.Trades.HasConnectionEnded(fixture.Connection));
            }
        };

        await fixture.InvokeAsync();

        Assert.Equal(1, observed);
        Assert.Same(committedOwner, fixture.Current);
        Assert.Equal(PostcommitSashCpFrame(), Assert.Single(fixture.Sent.Skip(before.FrameCount)));
        Assert.True(fixture.Trades.HasConnectionEnded(fixture.Connection));
        AssertPostcommitSashState(fixture, before, committedFiles);
    });

    private static byte[] PostcommitSashCpFrame() => new LegacyMsgUpdatePacket(Fixture.Uid,
        [new(LegacyMsgUpdateDataType.ConquerPoints, 60)]).ToFrameBytes();

    private static Dictionary<string, byte[]> ExpectedPostcommitSashFiles(Fixture fixture, Snapshot before)
    {
        var files = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
        var path = Path.GetRelativePath(fixture.Root, fixture.CharacterPath);
        files[path] = ProjectSashFile(before.Files[path], 60, 1);
        return files;
    }

    private static void AssertPostcommitSashState(Fixture fixture, Snapshot before,
        Dictionary<string, byte[]> expectedFiles)
    {
        var expected = WithSashWallet(before.Owner, 60, 1);
        var current = fixture.Current;
        Assert.Equal(expected.Character with { Attributes = current.Character.Attributes }, current.Character);
        Assert.Equal(expected.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            current.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(before.Owner with { Character = current.Character }, current);
        Assert.Same(before.Owner.PlayerItems, current.PlayerItems);
        Assert.Equal(60, current.Character.ConquerPoints);
        Assert.Equal(1UL.ToString(CultureInfo.InvariantCulture), current.Character.Attributes["InventorySashCount"]);
        fixture.AssertFiles(expectedFiles);
    }
}
