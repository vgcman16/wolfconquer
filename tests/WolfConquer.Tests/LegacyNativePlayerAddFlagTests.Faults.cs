using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerAddFlagTests
{
    [Theory]
    [InlineData("rune")]
    [InlineData("rune-owner")]
    [InlineData("equipment")]
    [InlineData("metadata")]
    public Task InvalidXpQueryClearsStaleBonusWithoutFaultingOrRenewingExistingRegistration(string missing) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        NativeAddFlagTestFixture.Ready(f.Owner);
        f.Rune();
        var vector = f.Player.BitVector;
        Assert.True(vector.TryAdd((int)Flag.Superman, 123, false, 0));
        var row = f.Row(Flag.Superman);
        var timer = row.Timer;
        f.Player.XPBooster = 99;
        f.Player.XPKiller = 88;
        if (missing == "rune") f.Owner.Rune = null;
        else if (missing == "rune-owner") f.Runes.Owner = null;
        else if (missing == "equipment") f.Owner.Equipment = null;
        else LegacyNativePoolItems.ItemsBase!.Clear();

        Assert.False(f.Player.AddFlag(Flag.Superman, 999, true));

        Assert.Equal((byte)0, f.Player.XPBooster);
        Assert.Equal((byte)0, f.Player.XPKiller);
        Assert.Same(vector, f.Player.BitVector);
        Assert.Same(row, f.Row(Flag.Superman));
        Assert.Equal(timer, row.Timer);
        Assert.Equal(123, row.Secounds);
        Assert.True(vector.Contain((int)Flag.Superman));
        Assert.Empty(f.Queue);
    });

    [Fact]
    public Task NullVectorFaultOccursAfterSuccessfulXpQueryAndResetButBeforeRegistrationMutation() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        NativeAddFlagTestFixture.Ready(f.Owner);
        f.Rune();
        var former = f.Player.BitVector;
        f.Player.BitVector = null!;
        f.Player.XPBooster = 99;
        f.Player.XPKiller = 88;

        Assert.Throws<NullReferenceException>(() => f.Player.AddFlag(Flag.Superman, 10, false));

        Assert.Equal((byte)7, f.Player.XPBooster);
        Assert.Equal((byte)0, f.Player.XPKiller);
        Assert.Null(f.Player.BitVector);
        Assert.Empty(former.ArrayFlags);
        Assert.All(former.bits, word => Assert.Equal(0u, word));
        Assert.Empty(f.Queue);
    });

    [Fact]
    public Task NativeTryAddPublishesDictionaryBeforeNullBitmapFaultAndDoesNotRepairIt() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var vector = f.Player.BitVector;
        var cachedEmpty = vector.GetFlags();
        vector.bits = null!;
        var before = DateTime.Now;

        Assert.Throws<NullReferenceException>(() => f.Player.AddFlag(Flag.TeamLeader, 30, true, 5));

        var after = DateTime.Now;
        var row = f.Row(Flag.TeamLeader);
        Assert.Same(vector, f.Player.BitVector);
        Assert.Null(vector.bits);
        Assert.Equal(6, row.Key);
        Assert.Equal(30, row.Secounds);
        Assert.Equal(5, row.InvokerSecouds);
        Assert.True(row.RemoveOnDead);
        Assert.InRange(row.Timer, before, after);
        Assert.InRange(row.TimerInvoker, before, after);
        Assert.True(vector.ContainFlag(6));
        Assert.Same(cachedEmpty, vector.GetFlags()); // Native Update=true occurs AFTER the failed bitmap Add.
        Assert.Empty(cachedEmpty);
        Assert.Empty(f.Queue);
    });

    [Fact]
    public Task DictionaryContainmentNotRawVisibilityControlsDuplicateAdditionWithoutClockRenewal() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var vector = f.Player.BitVector;
        vector.Add(6); // Bit alone is not a registration.
        Assert.True(f.Player.AddFlag(Flag.TeamLeader, 20, false));
        var row = f.Row(Flag.TeamLeader);
        var timer = row.Timer;
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.TeamLeader), Assert.Single(f.Queue));
        f.Queue.Clear();
        vector.Remove(6); // Registration alone still blocks another Add.

        Assert.False(f.Player.AddFlag(Flag.TeamLeader, 999, true, 4));

        Assert.Same(row, f.Row(Flag.TeamLeader));
        Assert.Equal(timer, row.Timer);
        Assert.Equal(20, row.Secounds);
        Assert.False(row.RemoveOnDead);
        Assert.Equal(0, row.InvokerSecouds);
        Assert.Equal(default, row.TimerInvoker);
        Assert.False(vector.Contain(6));
        Assert.Empty(f.Queue);
    });

    [Theory]
    [InlineData(6)]
    [InlineData(52)]
    public Task NullViewFaultFollowsActualRegistrationBitmapAndOwnerStatusPublication(int index) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var flag = (Flag)index;
        var vector = f.Player.BitVector;
        f.Player.View = null!;

        Assert.Throws<NullReferenceException>(() => f.Player.AddFlag(flag, 30, true));

        Assert.Same(vector, f.Player.BitVector);
        Assert.True(vector.Contain(index));
        Assert.Equal(30, f.Row(flag).Secounds);
        Assert.Equal(NativeAddFlagTestFixture.Status(flag), Assert.Single(f.Queue));
        Assert.Null(f.Player.View); // Timed2215 is not reached when StatusFlag's screen publication faults.
    });

    [Fact]
    public Task SuccessfulTimedFlagSendsStatusToReceiverOwnerAndTimedFrameToForeignActualViewOwner() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var otherQueue = new Queue<byte[]>();
        var other = NativeSpellTestFixture.CreateOwner(otherQueue);
        other.Player.UID = 999;
        f.Player.View = other.Player.View;

        Assert.True(f.Player.AddFlag(Flag.Accelerated, 77, false, 0, 11, 22));

        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.Accelerated), Assert.Single(f.Queue));
        Assert.Equal(NativeAddFlagTestFixture.Timed(Flag.Accelerated, 77, 11, 22), Assert.Single(otherQueue));
        Assert.Same(f.Owner, f.Player.Owner);
        Assert.Same(other, f.Player.View.Owner);
        Assert.Equal(17u, f.Player.UID);
        Assert.Equal(999u, other.Player.UID);
        Assert.False(f.Owner.FullLoading);
        Assert.False(other.FullLoading);
    });
}
