using System.Net.Sockets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Fact]
    public Task FailedCommittedHitFencesBothOwnersAndCancelsTargetsRealArmedRepeat() => Run(async f =>
    {
        // Target earns its own repeat through an ordinary actual attack; no
        // native flag, registration, callback or descriptor is injected.
        var reverse = new MsgAttackPacket(new(Target, Actor, 830, 584, MsgAttackType.Physical,
            0, 0, 17, MsgAttackEffect.None, 19));
        await f.Owner.SendAsync(Target, reverse.ToFrameBytes());
        var actor = f.Binding(Actor); var target = f.Binding(Target);
        Assert.True(target.Client.OnAutoAttack);
        var files = f.Files(); var after = HpImage(f, files, 2000, 1700);
        f.Clear(); bool observed = false;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Actor || !IsPhysical(bytes)) return;
            f.AssertFiles(after);
            Assert.Equal(1700L, f.Current(Target).Character.MinHitPoints);
            Assert.Equal(1700, target.Player.HitPoints);
            Assert.True(target.Client.OnAutoAttack);
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(actor));
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(target));
            observed = true;
            throw new SocketException((int)SocketError.ConnectionReset);
        };
        try
        {
            await f.Owner.Handler.HandleFrameAsync(f.Owner.Connection(Actor),
                OwnedEquipmentBehaviorFixture.ReadFrame(Request().ToFrameBytes()));
        }
        finally { f.BeforeSend = null; }
        Assert.True(observed); f.AssertFiles(after); Assert.True(f.Owner.Recovery.IsBlocked);
        Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Actor), out _));
        Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Target), out _));
        Assert.True(actor.IsRetired); Assert.True(target.IsRetired);
        Assert.False(actor.Client.OnAutoAttack); Assert.False(target.Client.OnAutoAttack);
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
        int outputs = f.Order.Count; f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
        f.AssertFiles(after); Assert.Equal(outputs, f.Order.Count);
        Assert.False(target.Client.OnAutoAttack);
    });
}
