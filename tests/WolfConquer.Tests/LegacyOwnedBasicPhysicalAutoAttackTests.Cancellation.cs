using System.Net.Sockets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Theory]
    [InlineData("same-stance")]
    [InlineData("walk")]
    [InlineData("jump")]
    public Task RealActionCancelsAnAlreadyAcceptedPhysicalRepeat(string action) => Run(async f =>
    {
        await f.HitAsync(2000, 1700, repeat: false);
        var expected = action == "same-stance" ? f.Files() : MovedImage(f, f.Files(), action == "walk" ? 831 : 832);
        if (action == "same-stance") await f.Owner.SendAsync(Actor, new MsgActionPacket
            { Type = MsgActionType.ChangeStance, DwParam = 0 }.ToFrameBytes());
        else if (action == "walk") await f.Owner.SendAsync(Actor, new MsgMovementPacket
            { Uid = Actor, Direction = 6, Running = MsgMovementMode.Walk }.ToFrameBytes());
        else await f.Owner.SendAsync(Actor, new MsgActionPacket
            { Type = MsgActionType.Jump, TargetPositionX = 832, TargetPositionY = 584 }.ToFrameBytes());
        Assert.Equal(action == "jump" ? 100U : 0U, LegacyActionState.RequireCurrent(f.Current().Character));
        Assert.Equal(action == "same-stance" ? 830 : action == "walk" ? 831 : 832, f.Current().Character.X);
        Assert.Equal(584, f.Current().Character.Y); f.AssertFiles(expected);
        var beforeDue = f.Files(); int outputs = f.Order.Count;
        f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
        f.AssertFiles(beforeDue); f.AssertCoherent(1700); Assert.Equal(outputs, f.Order.Count);
    });

    [Fact]
    public Task ActualTransportEofCancelsTheAcceptedRepeatWithoutAnotherReceive() => Run(async f =>
    {
        await f.HitAsync(2000, 1700, repeat: false);
        var native = f.Binding(Target).Player;
        await f.Owner.Connection(Actor).CompleteTransportCloseAsync();
        Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Actor), out _));
        var target = f.Current(Target);
        var beforeDue = f.Files(); int outputs = f.Order.Count;
        f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
        f.AssertFiles(beforeDue); Assert.Same(target, f.Current(Target));
        Assert.Equal(1700, native.HitPoints); Assert.Equal(outputs, f.Order.Count);
    });

    [Fact]
    public Task EscapedTargetNativeReplacementCannotRedirectAnAcceptedRepeat() => Run(async f =>
    {
        await f.HitAsync(2000, 1700, repeat: false);
        var target = f.Current(Target); var binding = f.Binding(Target); var client = target.NativeGameClient!; var physical = client.Player;
        var before = f.Files(); int outputs = f.Order.Count;
        // DECLARED negative physical escape. This new object grants no owner,
        // HP receiver or successful attack; preserve it until own cleanup.
        var foreign = new LegacyNativePlayerRuntime(client);
        try
        {
            client.Player = foreign;
            Assert.False(f.Owner.Store.IsBoundSpellOwnerCurrent(binding));
            f.Clock.Advance(OrdinaryDelay); await f.TickAsync();
            Assert.Same(foreign, client.Player); Assert.Same(target, f.Current(Target));
            Assert.Equal(1700, physical.HitPoints); Assert.Equal(0, foreign.HitPoints);
            f.AssertFiles(before); Assert.Equal(outputs, f.Order.Count);
        }
        finally { client.Player = physical; }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RejectedInitialPhysicalRequestCannotArmARepeat(bool peace) => Run(async f =>
    {
        await f.SitAsync();
        var before = f.Files(); var stamp = f.Binding().Player.AttackStamp;
        await f.AttackAsync(); f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
        f.AssertFiles(before); f.AssertCoherent(2000); Assert.Empty(f.Order);
        Assert.Equal(250U, LegacyActionState.RequireCurrent(f.Current().Character));
        Assert.Equal(250U, (uint)f.Binding().Player.Action);
        Assert.Equal(stamp, f.Binding().Player.AttackStamp); Assert.False(f.Binding().Client.OnAutoAttack);
    }, peace: peace, outOfRange: !peace);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FailedInteractionSendPreservesTheCommittedHitButCannotArmAFutureRepeat(bool viewer) => Run(async f =>
    {
        await f.SitAsync();
        var before = f.Files(); var after = HpImage(f, before, 2000, 1700);
        var stamp = f.Clock.GetLocalNow().DateTime; bool failed = false;
        var actorBinding = f.Binding(Actor); var targetBinding = f.Binding(Target);
        f.BeforeSend = (recipient, bytes) =>
        {
            if (IsPhysical(bytes))
            {
                f.AssertFiles(after); f.AssertCoherent(1700); AssertCommittedActor(f, stamp);
                Assert.Equal(InteractionGolden(), bytes);
            }
            if (recipient == (viewer ? Target : Actor) && IsPhysical(bytes))
            { failed = true; throw new SocketException((int)SocketError.ConnectionReset); }
        };
        // The actual Frame handles peer disconnects. Call it directly here so
        // the shared fixture's diagnostic sender-exception latch does not
        // rethrow this intentionally injected SocketException after dispatch.
        await f.Owner.Handler.HandleFrameAsync(f.Owner.Connection(Actor),
            OwnedEquipmentBehaviorFixture.ReadFrame(Request().ToFrameBytes()));
        f.BeforeSend = null;
        Assert.True(failed); f.AssertFiles(after);
        Assert.True(f.Owner.Recovery.IsBlocked);
        Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Actor), out _));
        Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Target), out _));
        Assert.True(actorBinding.IsRetired); Assert.True(targetBinding.IsRetired);
        Assert.False(actorBinding.Client.OnAutoAttack);
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
        int outputs = f.Order.Count; f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
        await f.Owner.Handler.HandleFrameAsync(f.Owner.Connection(Actor),
            OwnedEquipmentBehaviorFixture.ReadFrame(Request().ToFrameBytes()));
        f.AssertFiles(after); Assert.True(f.Owner.Recovery.IsBlocked);
        Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Actor), out _));
        Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Target), out _));
        Assert.True(actorBinding.IsRetired); Assert.True(targetBinding.IsRetired);
        Assert.False(actorBinding.Client.OnAutoAttack); Assert.Equal(outputs, f.Order.Count);
        Assert.DoesNotContain(f.Order, row => IsHp(row.Bytes, Target));
    });

    [Fact]
    public Task SameValueStanceQueuedDuringInteractionCancelsTheAcceptedRepeat() => Run(async f =>
    {
        await f.SitAsync();
        var before = f.Files(); var after = HpImage(f, before, 2000, 1700);
        var stamp = f.Clock.GetLocalNow().DateTime; Task? queuedStance = null;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (queuedStance is not null || recipient != Actor || !IsPhysical(bytes)) return;
            f.AssertFiles(after); f.AssertCoherent(1700); AssertCommittedActor(f, stamp);
            Assert.Equal(InteractionGolden(), bytes);
            // Queue the actual Frame while the physical Frame owns the lease;
            // do not wait for it inside the transport observer.
            queuedStance = f.Owner.SendAsync(Actor, new MsgActionPacket { Type = MsgActionType.ChangeStance, DwParam = 0 }
                .ToFrameBytes());
            Assert.False(queuedStance.IsCompleted);
        };
        await f.AttackAsync(); f.BeforeSend = null;
        Assert.NotNull(queuedStance); await queuedStance;
        f.AssertFiles(after); f.AssertCoherent(1700); AssertCommittedActor(f, stamp);
        Assert.False(f.Binding().Client.OnAutoAttack);
        var interactions = f.Order.Where(row => IsPhysical(row.Bytes)).ToArray();
        Assert.Equal(new[] { Actor, Target }, interactions.Select(row => row.Recipient));
        Assert.All(interactions, row => Assert.Equal(InteractionGolden(), row.Bytes));
        var hpPair = f.Order.Where(row => IsHpPair(row.Bytes, Target)).ToArray();
        Assert.Equal(2, hpPair.Length); Assert.All(hpPair, row => Assert.Equal(Target, row.Recipient));
        Assert.Equal(MaximumGolden(Target), hpPair[0].Bytes);
        Assert.Equal(HpGolden(Target, 1700), hpPair[1].Bytes);
        Assert.True(f.Order.FindIndex(row => IsHpPair(row.Bytes, Target)) >
            f.Order.FindLastIndex(row => IsPhysical(row.Bytes)));
        int outputs = f.Order.Count; f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
        f.AssertFiles(after); f.AssertCoherent(1700); AssertCommittedActor(f, stamp); Assert.Equal(outputs, f.Order.Count);
    });

    [Fact]
    public Task ActualVictimCharacterReplacementRefusalPreservesSitAndDoesNotSpendTheAttack() => Run(async f =>
    {
        await f.SitAsync();
        var before = f.Files(); var actor = f.Current(); var target = f.Current(Target);
        var stamp = f.Binding().Player.AttackStamp;
        // Real reads remain possible; replacement of the actual owned target
        // Character is denied by the OS. No saver, queue or result is supplied.
        using (var lease = new FileStream(f.Owner.CharacterPath(Target), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.True(lease.CanRead);
            await f.AttackAsync();
            f.AssertFiles(before); f.AssertCoherent(2000); Assert.Empty(f.Order);
            Assert.Same(actor, f.Current()); Assert.Same(target, f.Current(Target));
            Assert.Equal(250U, LegacyActionState.RequireCurrent(f.Current().Character));
            Assert.Equal(250U, (uint)f.Binding().Player.Action);
            Assert.Equal(stamp, f.Binding().Player.AttackStamp); Assert.False(f.Binding().Client.OnAutoAttack);
            await f.TickAsync();
            f.AssertFiles(before); Assert.Empty(f.Order);
            Assert.Equal(stamp, f.Binding().Player.AttackStamp); Assert.False(f.Binding().Client.OnAutoAttack);
        }
        // The same actual clock and legitimate request can now commit. A
        // rejected writer did not consume the first attack's cooldown.
        await f.HitAsync(2000, 1700, repeat: false);
    });
}
