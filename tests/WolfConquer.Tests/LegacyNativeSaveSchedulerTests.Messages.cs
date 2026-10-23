using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSaveSchedulerTests
{
    [Fact]
    public Task SchedulerSystemMessageSendsToEveryActualOnlineOwnerWithoutMapOrScreenFiltering() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var secondQueue = new Queue<byte[]>();
        var second = NativeSpellTestFixture.CreateOwner(secondQueue);
        s.Client.Player.Map = 1002; second.Player.Map = 9999;
        LegacyNativePoolClients.GamePoll![17] = s.Client;
        LegacyNativePoolClients.GamePoll[18] = second;
        LegacyNativeMsgSchedules.SendSysMesage("Owned tournament notice", LegacyNativeMsgMessage.ChatMode.Center,
            LegacyNativeMsgMessage.MsgColor.yellow, SendScren: true);
        var a = Assert.Single(s.Queue); var b = Assert.Single(secondQueue);
        Assert.Equal(a, b);
        var talk = Talk(a);
        Assert.Equal("Owned tournament notice", talk.Content[3]);
        Assert.Equal((uint)LegacyNativeMsgMessage.ChatMode.Center, talk.ChatType);
        Assert.Equal((uint)LegacyNativeMsgMessage.MsgColor.yellow, talk.Color);
    });

    [Fact]
    public Task RaceStatusUsesRealRankNameTimeAwardRecordBroadcast() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var map = s.Map(1950);
        s.Client.Player.Name = "Runner";
        var race = new LegacyNativeMsgSteedRace(); Set(race, "Map", map);
        Invoke(race, "Status", s.Client, 2, 40000, 500000);
        var bytes = Assert.Single(s.Queue);
        Assert.Equal(52, bytes.Length); Assert.Equal((ushort)1071, Opcode(bytes));
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 4)); Assert.Equal(2, BitConverter.ToInt32(bytes, 8));
        Assert.Equal("Runner", System.Text.Encoding.UTF8.GetString(bytes, 12, 6));
        Assert.Equal(40000, BitConverter.ToInt32(bytes, 44)); Assert.Equal(500000, BitConverter.ToInt32(bytes, 48));
    });
}
