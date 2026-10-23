using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusPrerequisiteTests
{
    [Theory]
    [InlineData(13002u, 172u, (int)Flag.FullPowerWater)]
    [InlineData(13051u, 172u, (int)Flag.FullPowerWater)]
    [InlineData(14002u, 173u, (int)Flag.FullPowerFire)]
    [InlineData(14051u, 173u, (int)Flag.FullPowerFire)]
    public Task TenPowersAcquiresGenuineFullPowerFlagThenLiteralIcon(uint job, uint icon, int rawFlag) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Class(job); s.Player.TaoistPower = 10;
        s.Player.UpdateTaoPower(s.Packet); var flag = (Flag)rawFlag;
        Assert.True(s.Player.ContainFlag(flag)); var state = s.Player.BitVector.ArrayFlags[rawFlag];
        Assert.Equal(2592000, state.Secounds); Assert.False(state.RemoveOnDead);
        Assert.Equal(new ushort[] { 2176, 2176 }, s.Queue.Select(p => BitConverter.ToUInt16(p, 2)));
        Assert.Equal(Icon(17, icon, 10), s.Queue.Last());
        var oldState = state; s.Queue.Clear(); s.Packet.InitWriter(); s.Player.UpdateTaoPower(s.Packet);
        Assert.Same(oldState, s.Player.BitVector.ArrayFlags[rawFlag]); Assert.Equal(Icon(17, icon, 10), Assert.Single(s.Queue));
    });

    [Theory]
    [InlineData(13002u, 9, 172u)] [InlineData(14002u, 11, 173u)] [InlineData(1001u, 10, 173u)]
    public Task NotExactlyTenOrNotTaoistStillPublishesSourceIconWithoutFlag(uint job, byte power, uint icon) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Class(job); s.Player.TaoistPower = power; s.Player.UpdateTaoPower(s.Packet);
        Assert.False(s.Player.ContainFlag(Flag.FullPowerWater)); Assert.False(s.Player.ContainFlag(Flag.FullPowerFire));
        Assert.Equal(Icon(17, icon, power), Assert.Single(s.Queue));
    });

    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(10, true)] [InlineData(255, true)]
    public Task SendPowerIsReadOnlyEvenAtTenAndZeroDoesNotTouchNullRecipient(byte power, bool sends) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Class(13002); s.Player.TaoistPower = power;
        s.Player.SendPowerTaoist(sends ? s.Client : null!, s.Packet);
        Assert.False(s.Player.ContainFlag(Flag.FullPowerWater)); Assert.False(s.Player.ContainFlag(Flag.FullPowerFire));
        if (sends) Assert.Equal(Icon(17, 172, power), Assert.Single(s.Queue)); else Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(10, 13002u, 100)] [InlineData(0, 10000u, 100)] [InlineData(0, 13002u, 9)]
    public Task TimerGatesReturnBeforeEquipmentAndPreserveStoredStamp(byte power, uint job, int elapsed) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Class(job); s.Player.TaoistPower = power; s.Client.Equipment = null;
        var stamp = new DateTime(2026, 1, 1); s.Player.TaoistStampPower = stamp;
        s.Player.UpdateTaoistPower(stamp.AddSeconds(elapsed));
        Assert.Equal(stamp, s.Player.TaoistStampPower); Assert.Equal(power, s.Player.TaoistPower); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(0u, false)] [InlineData(620002u, false)] [InlineData(620003u, true)] [InlineData(620439u, true)] [InlineData(620440u, false)]
    public Task DueTimerUsesRealRightWeaponClassifierAndStoresSourceNowNotArgument(uint item, bool gains) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Class(13002); s.Player.TaoistPower = 9;
        if (item != 0) s.Client.Equipment!.ClientItems[44] = new() { Uid = 44, ITEM_ID = item, Position = 4 };
        s.Player.TaoistStampPower = new DateTime(2026, 1, 1); var before = DateTime.Now;
        s.Player.UpdateTaoistPower(DateTime.MaxValue);
        Assert.InRange(s.Player.TaoistStampPower, before, DateTime.Now); Assert.NotEqual(DateTime.MaxValue, s.Player.TaoistStampPower);
        Assert.Equal(gains ? 10 : 9, s.Player.TaoistPower); Assert.Equal(gains, s.Player.ContainFlag(Flag.FullPowerWater));
        if (gains) Assert.Equal(Icon(17, 172, 10), s.Queue.Last()); else Assert.Empty(s.Queue);
    });

    [Fact]
    public Task DueTimerFaultsBeforeStampPublicationIfRealEquipmentOwnerIsAbsent() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Class(13002); s.Client.Equipment = null;
        var old = new DateTime(2026, 1, 1); s.Player.TaoistStampPower = old;
        Assert.Throws<NullReferenceException>(() => s.Player.UpdateTaoistPower(old.AddSeconds(10)));
        Assert.Equal(old, s.Player.TaoistStampPower); Assert.Equal(0, s.Player.TaoistPower); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task TimerDateOverflowIsNotClampedOrHidden() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Class(13002); s.Player.TaoistStampPower = DateTime.MaxValue;
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Player.UpdateTaoistPower(DateTime.MaxValue));
        Assert.Equal(DateTime.MaxValue, s.Player.TaoistStampPower); Assert.Empty(s.Queue);
    });
}
