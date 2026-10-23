using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativePlayerAddSpellFlagTests
{
    [Theory]
    [InlineData((int)Flag.TeamLeader)]
    [InlineData((int)Flag.Superman)]
    [InlineData((int)Flag.Accelerated)]
    public Task ExistingRegistrationIsReplacedOnSameVectorWithoutARemoveStatusFrame(int index) => NativePacketTestThread.Run(() =>
    {
        var flag = (Flag)index;
        using var f = new NativeAddFlagTestFixture();
        NativeAddFlagTestFixture.Ready(f.Owner);
        f.Rune();
        var vector = f.Player.BitVector;
        Assert.True(vector.TryAdd((int)flag, 19, false, 0));
        var former = f.Row(flag);
        var formerTimer = former.Timer;
        f.Player.XPBooster = 77;
        f.Player.XPKiller = 66;
        var before = DateTime.Now;

        f.Player.AddSpellFlag(flag, -3, true, 7);

        var after = DateTime.Now;
        var row = f.Row(flag);
        Assert.Same(vector, f.Player.BitVector);
        Assert.NotSame(former, row);
        Assert.Equal(formerTimer, former.Timer);
        Assert.Equal(19, former.Secounds);
        Assert.False(former.RemoveOnDead);
        Assert.InRange(row.Timer, before, after);
        Assert.InRange(row.TimerInvoker, before, after);
        Assert.Equal(-3, row.Secounds);
        Assert.Equal(7, row.InvokerSecouds);
        Assert.True(row.RemoveOnDead);
        Assert.True(vector.Contain((int)flag));
        Assert.Equal(flag == Flag.Superman ? (byte)7 : (byte)77, f.Player.XPBooster);
        Assert.Equal(flag == Flag.Superman ? (byte)0 : (byte)66, f.Player.XPKiller);
        var frames = f.Queue.ToArray();
        Assert.Equal(flag == Flag.Accelerated ? 2 : 1, frames.Length);
        Assert.Equal(NativeAddFlagTestFixture.Status(flag), frames[0]);
        if (frames.Length == 2) Assert.Equal(NativeAddFlagTestFixture.Timed(flag, -3), frames[1]);
        Assert.DoesNotContain(former, vector.GetFlags());
        Assert.Same(row, Assert.Single(vector.GetFlags()));
    });

    [Fact]
    public Task RawBitWithoutRegistrationDoesNotInventARemovalSendBeforeAdding() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var vector = f.Player.BitVector;
        vector.Add(6);

        f.Player.AddSpellFlag(Flag.TeamLeader, 20, false);

        Assert.Same(vector, f.Player.BitVector);
        Assert.Equal(20, f.Row(Flag.TeamLeader).Secounds);
        Assert.True(vector.Contain(6));
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.TeamLeader), Assert.Single(f.Queue));
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task MissingOptionalXpRuneAllowsCoherentReplacementOrAdditionWithoutBonus(
        bool metadataFault, bool hadRegistration) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        NativeAddFlagTestFixture.Ready(f.Owner);
        f.Rune();
        var vector = f.Player.BitVector;
        Assert.True(vector.TryAdd((int)Flag.Superman, 19, false, 0));
        var former = f.Row(Flag.Superman);
        var oldSnapshot = vector.GetFlags();
        Assert.Same(former, Assert.Single(oldSnapshot));
        // Both branches have a coherent prestate. Raw bitmap/dictionary
        // corruption admission is separate FSB023 work, not a Rune requirement.
        if (!hadRegistration) Assert.True(vector.TryRemove((int)Flag.Superman));
        var formerTimer = former.Timer;
        f.Player.XPBooster = 77;
        f.Player.XPKiller = 66;
        if (metadataFault) LegacyNativePoolItems.ItemsBase!.Clear();
        else f.Owner.Rune = null;

        var before = DateTime.Now;
        f.Player.AddSpellFlag(Flag.Superman, 999, true);
        var after = DateTime.Now;

        Assert.Same(vector, f.Player.BitVector);
        Assert.True(vector.ContainFlag((int)Flag.Superman));
        Assert.True(vector.Contain((int)Flag.Superman));
        var row = f.Row(Flag.Superman);
        Assert.NotSame(former, row);
        Assert.Same(row, Assert.Single(vector.GetFlags()));
        Assert.Equal((int)Flag.Superman, row.Key);
        Assert.Equal(999, row.Secounds);
        Assert.True(row.RemoveOnDead);
        Assert.Equal(0, row.InvokerSecouds);
        Assert.Equal(default, row.TimerInvoker);
        Assert.InRange(row.Timer, before, after);
        Assert.Same(former, Assert.Single(oldSnapshot)); // Captured array still owns the old object.
        Assert.Equal(19, former.Secounds);
        Assert.Equal(formerTimer, former.Timer);
        Assert.False(former.RemoveOnDead);
        Assert.Equal((byte)0, f.Player.XPBooster);
        Assert.Equal((byte)0, f.Player.XPKiller);
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.Superman), Assert.Single(f.Queue));
        if (metadataFault) Assert.Empty(LegacyNativePoolItems.ItemsBase!);
        else Assert.Null(f.Owner.Rune);
    });

    [Fact]
    public Task NullVectorFaultsBeforeXpQueryUnlikeDirectAddFlag() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        NativeAddFlagTestFixture.Ready(f.Owner);
        f.Rune();
        var former = f.Player.BitVector;
        f.Player.BitVector = null!;
        f.Player.XPBooster = 77;
        f.Player.XPKiller = 66;

        Assert.Throws<NullReferenceException>(() => f.Player.AddSpellFlag(Flag.Superman, 20, false));

        Assert.Null(f.Player.BitVector);
        Assert.Empty(former.ArrayFlags);
        Assert.Equal((byte)77, f.Player.XPBooster);
        Assert.Equal((byte)66, f.Player.XPKiller);
        Assert.Empty(f.Queue);
    });

    [Fact]
    public Task NullViewFaultOccursAfterOldRowRemovalNewRowPublicationAndOneStatusSend() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var vector = f.Player.BitVector;
        Assert.True(vector.TryAdd(6, 19, false, 0));
        var former = f.Row(Flag.TeamLeader);
        f.Player.View = null!;
        var before = DateTime.Now;

        Assert.Throws<NullReferenceException>(() => f.Player.AddSpellFlag(Flag.TeamLeader, 22, true));

        var after = DateTime.Now;
        Assert.Same(vector, f.Player.BitVector);
        var row = f.Row(Flag.TeamLeader);
        Assert.NotSame(former, row);
        Assert.Equal(22, row.Secounds);
        Assert.True(row.RemoveOnDead);
        Assert.InRange(row.Timer, before, after);
        Assert.True(vector.Contain(6));
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.TeamLeader), Assert.Single(f.Queue));
        Assert.Null(f.Player.View);
    });

    [Theory]
    [InlineData((int)Flag.TeamLeader)]
    [InlineData((int)Flag.Accelerated)]
    public Task OmittedArgumentsForwardZeroStampShowAmountAndAmount(int index) => NativePacketTestThread.Run(() =>
    {
        var flag = (Flag)index;
        using var f = new NativeAddFlagTestFixture();

        f.Player.AddSpellFlag(flag, 22, false);

        var row = f.Row(flag);
        Assert.Equal(0, row.InvokerSecouds);
        Assert.Equal(default, row.TimerInvoker);
        Assert.Equal(22, row.Secounds);
        Assert.False(row.RemoveOnDead);
        var frames = f.Queue.ToArray();
        Assert.Equal(flag == Flag.Accelerated ? 2 : 1, frames.Length);
        Assert.Equal(NativeAddFlagTestFixture.Status(flag), frames[0]);
        if (frames.Length == 2) Assert.Equal(NativeAddFlagTestFixture.Timed(flag, 22), frames[1]);
    });

    [Fact]
    public Task ForeignClientHoldingThePlayerDoesNotRedirectRuneQueryVectorOrOwnerSend() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        NativeAddFlagTestFixture.Ready(f.Owner);
        f.Rune();
        var receiver = f.Player;
        var vector = receiver.BitVector;
        Assert.True(vector.TryAdd((int)Flag.Superman, 19, false, 0));
        var former = f.Row(Flag.Superman);
        var otherQueue = new Queue<byte[]>();
        var holder = NativeSpellTestFixture.CreateOwner(otherQueue);
        holder.Player = receiver;
        Assert.Null(holder.Rune);

        holder.Player.AddSpellFlag(Flag.Superman, 22, false);

        Assert.Same(receiver, holder.Player);
        Assert.Same(f.Owner, holder.Player.Owner);
        Assert.Same(vector, holder.Player.BitVector);
        Assert.NotSame(former, f.Row(Flag.Superman));
        Assert.Equal((byte)7, receiver.XPBooster);
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.Superman), Assert.Single(f.Queue));
        Assert.Empty(otherQueue);
        Assert.False(holder.FullLoading);
        Assert.False(f.Owner.FullLoading);
    });
}
