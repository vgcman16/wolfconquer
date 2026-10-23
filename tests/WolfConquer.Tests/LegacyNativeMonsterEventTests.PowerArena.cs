using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterEventTests
{
    [Fact]
    public void PowerArenaHasGenuineBirthStateWithoutSchedulerPublication()
    {
        using var state = new State(); var p = new LegacyNativeMsgPowerArena();
        Assert.Equal(LegacyNativeProcesType.Dead, p.Process); Assert.Equal(5, p.StartMinutes);
        Assert.Equal(default, p.StartTimer); Assert.Equal(default, p.PrepareStart); Assert.Null(LegacyNativeMsgSchedules.PowerArena);
    }
    [Theory]
    [InlineData(1770u, 29, true)] [InlineData(1770u, 0, false)]
    [InlineData(1771u, 44, true)] [InlineData(1771u, 0, false)]
    [InlineData(1772u, 59, true)] [InlineData(1772u, 0, false)]
    [InlineData(1773u, 34, true)] [InlineData(1773u, 0, false)]
    [InlineData(1774u, 44, true)] [InlineData(1774u, 0, false)]
    [InlineData(1775u, 59, true)] [InlineData(1775u, 0, false)]
    [InlineData(1777u, 89, true)] [InlineData(1777u, 0, false)]
    public Task GuardianMethodIncrementsBeforeApplyingEveryNativeMapThreshold(uint map, int old, bool sends) => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Alive };
        packets.Client.Player.Map = map; packets.Client.Player.DefeatedArenaGuardians = old; p.CheckMonstersMap(packets.Client.Player);
        Assert.Equal(old + 1, packets.Client.Player.DefeatedArenaGuardians); Assert.Equal(sends ? 1 : 0, packets.Queue.Count);
        if (sends)
        {
            var chat = Chat(packets.Queue.Dequeue());
            Assert.Equal("You have flattened enough guardians to claim experience from the Agate now.", chat.Content[3]);
            Assert.Equal((uint)LegacyNativeMsgMessage.ChatMode.Talk, chat.ChatType); Assert.Equal((uint)LegacyNativeMsgMessage.MsgColor.red, chat.Color);
        }
    });
    [Theory]
    [InlineData(1770u, true)] [InlineData(1771u, true)] [InlineData(1772u, true)] [InlineData(1773u, true)]
    [InlineData(1774u, true)] [InlineData(1775u, true)] [InlineData(1777u, true)] [InlineData(1776u, false)]
    [InlineData(1002u, false)] [InlineData(0u, false)] [InlineData(uint.MaxValue, false)]
    public Task InPowerArenaUsesOnlyTheSevenOriginalMapIds(uint map, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); var p = new LegacyNativeMsgPowerArena();
        packets.Client.Player.Map = map; Assert.Equal(expected, p.InPowerArena(packets.Client));
    });
    [Fact]
    public Task OutsideMapAndRepeatedThresholdCallsRetainSourceIncrementAndRepeatedNotifications() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Alive };
        packets.Client.Player.Map = 1776; p.CheckMonstersMap(packets.Client.Player); Assert.Equal(1, packets.Client.Player.DefeatedArenaGuardians); Assert.Empty(packets.Queue);
        packets.Client.Player.Map = 1770; packets.Client.Player.DefeatedArenaGuardians = 30;
        p.CheckMonstersMap(packets.Client.Player); p.CheckMonstersMap(packets.Client.Player);
        Assert.Equal(32, packets.Client.Player.DefeatedArenaGuardians); Assert.Equal(2, packets.Queue.Count);
        packets.Queue.Clear(); packets.Client.Player.DefeatedArenaGuardians = int.MaxValue;
        p.CheckMonstersMap(packets.Client.Player); Assert.Equal(int.MinValue, packets.Client.Player.DefeatedArenaGuardians); Assert.Empty(packets.Queue);
    });
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(3)] [InlineData(255)]
    public Task InactiveModesReturnBeforePlayerDereference(int mode) => NativePacketTestThread.Run(() =>
    {
        var p = new LegacyNativeMsgPowerArena { Process = (LegacyNativeProcesType)mode }; p.CheckMonstersMap(null!);
    });
    [Fact]
    public Task AliveCounterMutationPrecedesRealMessagePoolFailure() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); var p = new LegacyNativeMsgPowerArena { Process = LegacyNativeProcesType.Alive };
        packets.Client.Player.Map = 1770; packets.Client.Player.DefeatedArenaGuardians = 29; packets.FailNextTake();
        Assert.Throws<NullReferenceException>(() => p.CheckMonstersMap(packets.Client.Player));
        Assert.Equal(30, packets.Client.Player.DefeatedArenaGuardians); Assert.Empty(packets.Queue);
    });
}
