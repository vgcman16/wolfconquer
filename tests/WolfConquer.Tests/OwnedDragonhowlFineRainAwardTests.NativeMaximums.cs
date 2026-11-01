using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlFineRainAwardTests
{
    [Theory]
    [InlineData((ushort)17_490, false)]
    [InlineData((ushort)17_650, true)]
    public Task RealDeathPublishesItsNativeMaximumsBeforeTheFirstClearVector(
        ushort spell, bool threshold) => Run(spell, threshold, async c =>
    {
        var f = c.Owner; var peer = Binding(f, Peer);
        var status = peer.Client.Status;
        var unjoined = NativeUnjoinedStatus(status);
        var expected = ExpectedFiles(c, dead: true);
        int observed = 0;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (!IsFirstFineRainDeathClear(recipient, bytes)) return;
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            Assert.Equal(0, observed++);
            // All actual journal/native/holder participants publish before a
            // socket observes even this early historical death-prefix vector.
            AssertDead(c, peer);
            Assert.Same(status, peer.Client.Status);
            Assert.Equal(unjoined, NativeUnjoinedStatus(status));
            f.Data.AssertFiles(expected);
        };
        try { await f.SendAsync(Actor, c.Request()); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, observed);
        AssertDead(c, peer); Assert.Same(status, peer.Client.Status);
        Assert.Equal(unjoined, NativeUnjoinedStatus(status));
        Assert.False(f.Recovery.IsBlocked);
        f.Data.AssertFiles(expected);
    });

    private static bool IsFirstFineRainDeathClear(uint recipient, byte[] bytes) => recipient == Peer &&
        StatusVector(bytes, out var words) && !Flag(words, 212) && !Flag(words, 213) &&
        !Flag(words, LegacyPlayerDeathState.DeadStatusFlag);

    // These other native MsgStatus fields have no joined completed-query
    // publication in this path. The maxima repair must not silently guess or
    // overwrite them from managed aliases, or replace the Status object.
    private static object NativeUnjoinedStatus(LegacyNativeMsgStatus status) => new
    {
        status.MaxAttack, status.MinAttack, status.Defence, status.MagicAttack,
        status.MagicDefence, status.Stamp, status.UID,
    };
}
