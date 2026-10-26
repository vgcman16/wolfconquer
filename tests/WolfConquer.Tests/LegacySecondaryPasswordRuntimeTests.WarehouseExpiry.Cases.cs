using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Theory]
    [InlineData(LegacyWarehouseAction.Show)]
    [InlineData(LegacyWarehouseAction.Show_WH_House)]
    [InlineData(LegacyWarehouseAction.ShowInventorySash)]
    public async Task ActualD13WarehouseExpiryCommitsSelectedRootLockBytesBeforeOriginalChunkAndOrderedFollowups(
        LegacyWarehouseAction action)
    {
        const uint uid = 1_299_761;
        SeedWarehouseExpiry(uid, action);
        var identity = new LegacyLocalServerIdentity(42);
        var owner = NewOwner(identity: identity);
        Action<byte[]>? witness = null;
        var player = await Login(owner, uid, beforeSend: frame => witness?.Invoke(frame));
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, Current(owner, player), identity));
        var before = CaptureExpiry(owner, player);
        var files = ExpectedExpiryFiles(uid, before);
        var expected = ExpectedExpiryFrames(before.Session, action, ownServer: true);
        Assert.Equal(7, expected.Length);
        var observed = 0;
        Exception? callbackFailure = null;
        witness = frame =>
        {
            var index = observed++;
            callbackFailure ??= Record.Exception(() =>
            {
                AssertExpiryCommitted(owner, player, before, files);
                Assert.Equal(expected[index], frame);
            });
        };
        var calls = new List<Task>();

        await SashPinFrameAsync(owner, player, ExpiryRequest(uid, action), calls);

        Assert.Null(callbackFailure);
        Assert.Equal(expected.Length, observed);
        AssertExpiryCommitted(owner, player, before, files);
        AssertExpiryFrames(player, before.Frames, expected);
        var committed = CaptureExpiry(owner, player);
        var replay = ExpectedExpiryFrames(committed.Session, action, ownServer: true);
        observed = 0;
        callbackFailure = null;
        witness = frame =>
        {
            var index = observed++;
            callbackFailure ??= Record.Exception(() =>
            {
                AssertExpiryReadOnly(owner, player, committed);
                Assert.Equal(replay[index], frame);
            });
        };

        await SashPinFrameAsync(owner, player, ExpiryRequest(uid, action), calls);

        Assert.Null(callbackFailure);
        Assert.Equal(5, observed);
        AssertExpiryReadOnly(owner, player, committed);
        Assert.Equal(5, replay.Length); // Still displays; the two cleared-root updates do not repeat.
        AssertExpiryFrames(player, committed.Frames, replay);
        Assert.All(calls, call => Assert.True(call.IsCompletedSuccessfully));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualD13WarehouseExpiryMissingOrWrappedIdentityOnlyDisplaysLockedItemsWithoutWrites(bool wrapped)
    {
        const uint uid = 1_299_762;
        SeedWarehouseExpiry(uid, LegacyWarehouseAction.Show);
        LegacyLocalServerIdentity? identity = wrapped ? new(65_536) : null;
        var owner = NewOwner(identity: identity);
        Action<byte[]>? witness = null;
        var player = await Login(owner, uid, beforeSend: frame => witness?.Invoke(frame));
        var before = CaptureExpiry(owner, player);
        Assert.Equal((ushort)0, player.NativeClient.Player.ServerID);
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, before.Session, identity));
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, before.Session, new(0)));
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, before.Session, out _, out var admitted));
        Assert.True(admitted);
        var expected = ExpectedExpiryFrames(before.Session, LegacyWarehouseAction.Show, ownServer: false);
        Assert.Equal(5, expected.Length);
        var observed = 0;
        Exception? callbackFailure = null;
        witness = frame =>
        {
            var index = observed++;
            callbackFailure ??= Record.Exception(() =>
            {
                AssertExpiryReadOnly(owner, player, before);
                Assert.Equal(expected[index], frame);
            });
        };
        var calls = new List<Task>();

        await SashPinFrameAsync(owner, player, ExpiryRequest(uid, LegacyWarehouseAction.Show), calls);

        Assert.Null(callbackFailure);
        Assert.Equal(5, observed);
        AssertExpiryReadOnly(owner, player, before);
        AssertExpiryFrames(player, before.Frames, expected);
        Assert.All(calls, call => Assert.True(call.IsCompletedSuccessfully));
    }

    [WindowsTradeJournalFailureFact]
    public async Task ActualD13WarehouseExpiryRefusedItemReplacePublishesNothingAndSameOwnerRetriesAfterRelease()
    {
        const uint uid = 1_299_763;
        SeedWarehouseExpiry(uid, LegacyWarehouseAction.Show);
        var owner = NewOwner(identity: new(42));
        Action<byte[]>? witness = null;
        var player = await Login(owner, uid, beforeSend: frame => witness?.Invoke(frame));
        var before = CaptureExpiry(owner, player);
        var files = ExpectedExpiryFiles(uid, before);
        var calls = new List<Task>();
        using (File.Open(ItemPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await SashPinFrameAsync(owner, player, ExpiryRequest(uid, LegacyWarehouseAction.Show), calls);
            AssertExpiryReadOnly(owner, player, before);
            Assert.Equal(before.Frames, player.Sent.Count);
        } // Actual operation ended before the owned sharing refusal is released.
        Exception? callbackFailure = null;
        var observed = 0;
        witness = _ =>
        {
            observed++;
            callbackFailure ??= Record.Exception(() => AssertExpiryCommitted(owner, player, before, files));
        };

        await SashPinFrameAsync(owner, player, ExpiryRequest(uid, LegacyWarehouseAction.Show), calls);

        Assert.Null(callbackFailure);
        Assert.Equal(7, observed);
        AssertExpiryCommitted(owner, player, before, files);
        AssertExpiryFrames(player, before.Frames, ExpectedExpiryFrames(before.Session, LegacyWarehouseAction.Show, ownServer: true));
        Assert.All(calls, call => Assert.True(call.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task ActualD13WarehouseExpiryMalformedFifthTimerRejectsAllEarlierRefreshesAndFrames()
    {
        const uint uid = 1_299_764;
        SeedWarehouseExpiry(uid, LegacyWarehouseAction.Show, malformedLast: true);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        var before = CaptureExpiry(owner, player);
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, before.Session, new(42)));
        var selected = before.Session.PlayerItems.WarehouseItems.Where(row => row.WarehouseId == 8).OrderBy(row => row.Uid).ToArray();
        Assert.Equal(24U, selected[4].Uid);
        Assert.Equal(20_260_230, selected[4].UnlockTimer);
        var calls = new List<Task>();

        await SashPinFrameAsync(owner, player, ExpiryRequest(uid, LegacyWarehouseAction.Show), calls);

        AssertExpiryReadOnly(owner, player, before);
        Assert.Equal(before.Frames, player.Sent.Count);
        Assert.All(calls, call => Assert.True(call.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task ActualD13WarehouseExpiryReplacementAfterFirstChunkStopsFollowupsButKeepsDurableClearOnReplay()
    {
        const uint uid = 1_299_765;
        SeedWarehouseExpiry(uid, LegacyWarehouseAction.Show);
        var owner = NewOwner(identity: new(42));
        Action<byte[]>? witness = null;
        var player = await Login(owner, uid, beforeSend: frame => witness?.Invoke(frame));
        var before = CaptureExpiry(owner, player);
        var files = ExpectedExpiryFiles(uid, before);
        var expected = ExpectedExpiryFrames(before.Session, LegacyWarehouseAction.Show, ownServer: true);
        WorldSession? replacement = null;
        Exception? callbackFailure = null;
        var observed = 0;
        witness = frame =>
        {
            observed++;
            callbackFailure ??= Record.Exception(() =>
            {
                Assert.Equal(1, observed);
                AssertExpiryCommitted(owner, player, before, files);
                Assert.Equal(expected[0], frame);
                replacement = Current(owner, player) with { };
                owner.Sessions.Set(player.Connection, replacement);
            });
        };
        var calls = new List<Task>();

        await SashPinFrameAsync(owner, player, ExpiryRequest(uid, LegacyWarehouseAction.Show), calls);

        Assert.Null(callbackFailure);
        Assert.Equal(1, observed);
        Assert.Same(replacement, Current(owner, player));
        Assert.Equal(expected[0], Assert.Single(player.Sent.Skip(before.Frames)));
        AssertExpiryCommitted(owner, player, before, files);
        var committed = CaptureExpiry(owner, player);
        var replay = ExpectedExpiryFrames(committed.Session, LegacyWarehouseAction.Show, ownServer: true);
        observed = 0;
        callbackFailure = null;
        witness = frame =>
        {
            var index = observed++;
            callbackFailure ??= Record.Exception(() =>
            {
                AssertExpiryReadOnly(owner, player, committed);
                Assert.Equal(replay[index], frame);
            });
        };

        await SashPinFrameAsync(owner, player, ExpiryRequest(uid, LegacyWarehouseAction.Show), calls);

        Assert.Null(callbackFailure);
        Assert.Equal(5, observed);
        AssertExpiryReadOnly(owner, player, committed);
        Assert.Equal(5, replay.Length);
        AssertExpiryFrames(player, committed.Frames, replay);
        Assert.All(calls, call => Assert.True(call.IsCompletedSuccessfully));
    }

    [Fact]
    public async Task ActualD13PhysicalSashShowDoesNotRefreshExpiredChildrenEvenWithGenuineOwnServerIdentity()
    {
        const uint uid = 1_299_766;
        SeedWarehouseExpiry(uid, LegacyWarehouseAction.ShashShow);
        var owner = NewOwner(identity: new(42));
        Action<byte[]>? witness = null;
        var player = await Login(owner, uid, beforeSend: frame => witness?.Invoke(frame));
        var before = CaptureExpiry(owner, player);
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, before.Session, new(42)));
        Exception? callbackFailure = null;
        var observed = 0;
        witness = _ =>
        {
            observed++;
            callbackFailure ??= Record.Exception(() => AssertExpiryReadOnly(owner, player, before));
        };
        var calls = new List<Task>();

        await SashPinFrameAsync(owner, player, ExpiryRequest(uid, LegacyWarehouseAction.ShashShow), calls);

        Assert.Null(callbackFailure);
        Assert.Equal(2, observed);
        AssertExpiryReadOnly(owner, player, before);
        AssertExpiryFrames(player, before.Frames, ExpectedExpiryFrames(before.Session, LegacyWarehouseAction.ShashShow, ownServer: true));
        Assert.All(calls, call => Assert.True(call.IsCompletedSuccessfully));
    }
}
