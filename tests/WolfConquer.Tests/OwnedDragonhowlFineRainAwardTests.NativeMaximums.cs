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
        (uint, uint, uint, uint, uint)? publishedCombat = null;
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
            publishedCombat = NativeCombatStatus(status);
            f.Data.AssertFiles(expected);
        };
        try { await f.SendAsync(Actor, c.Request()); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, observed);
        AssertDead(c, peer); Assert.Same(status, peer.Client.Status);
        Assert.Equal(unjoined, NativeUnjoinedStatus(status));
        var packets = f.Frames(Peer).Select(bytes =>
            LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) ? packet : null)
            .Where(packet => packet is not null).ToArray();
        var deathPacket = Assert.IsType<LegacyPlayerStatusPacket>(packets.Last());
        Assert.NotNull(publishedCombat);
        Assert.Equal(PacketCombatStatus(deathPacket), publishedCombat.Value);
        Assert.Equal(publishedCombat.Value, NativeCombatStatus(status));
        Assert.False(f.Recovery.IsBlocked);
        f.Data.AssertFiles(expected);
    });

    private static bool IsFirstFineRainDeathClear(uint recipient, byte[] bytes) => recipient == Peer &&
        StatusVector(bytes, out var words) && !Flag(words, 212) && !Flag(words, 213) &&
        !Flag(words, LegacyPlayerDeathState.DeadStatusFlag);

    private static (uint, uint, uint, uint, uint) NativeCombatStatus(LegacyNativeMsgStatus status) =>
        (status.MaxAttack, status.MinAttack, status.Defence, status.MagicAttack, status.MagicDefence);
    private static (uint, uint, uint, uint, uint) PacketCombatStatus(LegacyPlayerStatusPacket status) =>
        (status.MaxAttack, status.MinAttack, status.Defence, status.MagicAttack, status.MagicDefence);

    // Other fields remain outside the joined publication.
    private static object NativeUnjoinedStatus(LegacyNativeMsgStatus status) => new
    {
        status.Stamp, status.UID,
    };
}
