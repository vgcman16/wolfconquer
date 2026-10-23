using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePlayerAddFlagTests
{
    [Theory]
    [InlineData((int)Flag.Superman)]
    [InlineData((int)Flag.Cyclone)]
    [InlineData((int)Flag.ShurikenVortex)]
    [InlineData((int)Flag.FatalStrike)]
    [InlineData((int)Flag.ChaintBolt)]
    [InlineData((int)Flag.Oblivion)]
    [InlineData((int)Flag.CannonBarrage)]
    [InlineData((int)Flag.BlackbeardsRage)]
    [InlineData((int)Flag.BladeFlurry)]
    [InlineData((int)Flag.SuperCyclone)]
    [InlineData((int)Flag.DragonCyclone)]
    [InlineData((int)Flag.Omnipotence)]
    [InlineData((int)Flag.ThunderRampage)]
    public Task AllThirteenXpFlagsCommitBonusThenRejectDuplicateWithoutChangingAnyExistingState(int index)
        => NativePacketTestThread.Run(() =>
    {
        var flag = (Flag)index;
        using var f = new NativeAddFlagTestFixture();
        NativeAddFlagTestFixture.Ready(f.Owner);
        f.Rune();
        f.Player.XPBooster = 99; f.Player.XPKiller = 88;
        var before = DateTime.Now;
        Assert.True(f.Player.AddFlag(flag, 123, true, 7));
        var after = DateTime.Now;
        var row = f.Row(flag);
        Assert.InRange(row.Timer, before, after); Assert.InRange(row.TimerInvoker, before, after);
        Assert.Equal((byte)7, f.Player.XPBooster); Assert.Equal((byte)0, f.Player.XPKiller);
        Assert.Equal(NativeAddFlagTestFixture.Status(flag), Assert.Single(f.Queue));
        f.Player.XPBooster = 91; f.Player.XPKiller = 81;
        var snapshot = CaptureAdmission(f);
        Assert.False(f.Player.AddFlag(flag, 999, false, 44, 55, 66));
        AssertAdmissionUnchanged(f, snapshot);
        Assert.Same(row, f.Row(flag)); Assert.Equal(123, row.Secounds);
        Assert.Equal(7, row.InvokerSecouds); Assert.True(row.RemoveOnDead);
    });

    [Theory]
    [InlineData((int)Flag.ReflectMelee)]
    [InlineData((int)Flag.Ball)]
    [InlineData((int)Flag.Invisibility)]
    public Task AdjacentOrdinaryFlagsSkipMissingRuneAndPreserveXpBytes(int index) => NativePacketTestThread.Run(() =>
    {
        var flag = (Flag)index;
        using var f = new NativeAddFlagTestFixture();
        f.Owner.Rune = null;
        f.Player.XPBooster = 77;
        f.Player.XPKiller = 66;
        var before = DateTime.Now;
        Assert.True(f.Player.AddFlag(flag, 10, false));
        var after = DateTime.Now;
        Assert.InRange(f.Row(flag).Timer, before, after);
        Assert.Equal((byte)77, f.Player.XPBooster);
        Assert.Equal((byte)66, f.Player.XPKiller);
        Assert.Equal(NativeAddFlagTestFixture.Status(flag), Assert.Single(f.Queue));
    });

    [Theory]
    [InlineData((byte)0, (ushort)140, (byte)0)]
    [InlineData((byte)1, (ushort)140, (byte)0)]
    [InlineData((byte)2, (ushort)14, (byte)0)]
    [InlineData((byte)2, (ushort)15, (byte)0)]
    public Task XpLookupRejectsForeignRuneOwnersRegardlessOfTheirEligibility(
        byte reborn, ushort level, byte expected) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var item = f.Rune();
        var foreign = new LegacyNativeGameClientRuntime();
        foreign.Equipment = new(foreign);
        NativeAddFlagTestFixture.Ready(foreign, reborn, level);
        f.Runes.Owner = foreign;
        Assert.Equal((byte)0, f.Player.Reborn);
        Assert.Equal((ushort)0, f.Player.Level);
        f.Player.XPBooster = 99;
        f.Player.XPKiller = 88;
        var vector = f.Player.BitVector;
        var before = DateTime.Now;

        Assert.True(f.Player.AddFlag(Flag.Superman, 20, true));

        var after = DateTime.Now;
        Assert.Equal(expected, f.Player.XPBooster);
        Assert.Equal((byte)0, f.Player.XPKiller);
        Assert.InRange(f.Row(Flag.Superman).Timer, before, after);
        Assert.Same(vector, f.Player.BitVector);
        Assert.Same(foreign, f.Runes.Owner);
        Assert.Same(item, f.Runes.Objects[0]);
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.Superman), Assert.Single(f.Queue));
    });

    [Fact]
    public Task DuplicateXpAdmissionDoesNotRequeryMutableRuneOrResetCurrentCounters() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var item = f.Rune();
        NativeAddFlagTestFixture.Ready(f.Owner);
        Assert.True(f.Player.AddFlag(Flag.Superman, 20, false));
        var row = f.Row(Flag.Superman);
        var timer = row.Timer;
        Assert.Equal((byte)7, f.Player.XPBooster);
        LegacyNativePoolItems.ItemsBase![4_031_109] = new() { ID = 4_031_109, Name = "XPBooster" };
        item.ITEM_ID = 4_031_109; // Actual setter requires the new metadata row to exist first.
        f.Player.XPKiller = 33;
        Assert.False(f.Player.AddFlag(Flag.Superman, 999, true));
        Assert.Equal((byte)7, f.Player.XPBooster);
        Assert.Equal((byte)33, f.Player.XPKiller);
        f.Player.Reborn = 0; // Duplicate refusal does not re-evaluate the changed eligibility.
        f.Player.XPKiller = 44;
        Assert.False(f.Player.AddFlag(Flag.Superman, 999, true));
        Assert.Equal((byte)7, f.Player.XPBooster);
        Assert.Equal((byte)44, f.Player.XPKiller);
        Assert.Same(row, f.Row(Flag.Superman));
        Assert.Equal(timer, row.Timer);
        Assert.Equal(20, row.Secounds);
        Assert.False(row.RemoveOnDead);
        Assert.Single(f.Queue);
    });

    [Theory]
    [InlineData(51)]
    [InlineData(52)]
    [InlineData(53)]
    [InlineData(54)]
    [InlineData(55)]
    [InlineData(56)] // No named MsgGameUpdate.DataType; source numeric window still sends it.
    [InlineData(57)]
    [InlineData(58)]
    [InlineData(59)]
    [InlineData(60)]
    [InlineData(61)]
    public Task AddPublishesStatusThenExactTimedWindowIncludingUnnamedIdAndNegativeSeconds(int index) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var flag = (Flag)index;
        f.Owner.Rune = null;
        var vector = f.Player.BitVector;
        var before = DateTime.Now;

        Assert.True(f.Player.AddFlag(flag, -3, true, 5, uint.MaxValue, 3));

        var after = DateTime.Now;
        var row = f.Row(flag);
        Assert.Same(vector, f.Player.BitVector);
        Assert.InRange(row.Timer, before, after);
        Assert.InRange(row.TimerInvoker, before, after);
        Assert.Equal(index, row.Key);
        Assert.Equal(-3, row.Secounds);
        Assert.Equal(5, row.InvokerSecouds);
        Assert.True(row.RemoveOnDead);
        Assert.True(vector.Contain(index));
        var frames = f.Queue.ToArray();
        Assert.Equal(index is >= 52 and <= 60 ? 2 : 1, frames.Length);
        Assert.Equal(NativeAddFlagTestFixture.Status(flag), frames[0]);
        if (frames.Length == 2)
            Assert.Equal(NativeAddFlagTestFixture.Timed(flag, -3, uint.MaxValue, 3), frames[1]);
        Assert.False(f.Owner.FullLoading); // AddFlag and its native sends have no FullLoading gate.
    });
}
