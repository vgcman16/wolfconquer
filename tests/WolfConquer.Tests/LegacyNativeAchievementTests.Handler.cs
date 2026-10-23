using WolfConquer.Core.World;
using Action = WolfConquer.Core.World.LegacyNativeClientAchievement.TypeAchievement;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAchievementTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    [InlineData(4294967295u)]
    public Task HandlerReadsWholeRequestThenIgnoresUnknownActionWithoutClientAccess(uint action) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Request((Action)action, 17, 10101);
        LegacyNativeClientAchievement.AchievementHandler(null!, scope.Packet);
        Assert.Equal(20, scope.Packet.Position); Assert.Empty(scope.Queue); Assert.Null(scope.Client.Player.Achievement);
    });

    [Fact]
    public Task HandlerSendQueueUsesFourthWordIgnoresRequestedUidAndPreservesNativeMapping() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var achievement = Attach(scope.Client);
        scope.Request(Action.SendQueue, uint.MaxValue, 10101, reserved: 11432);
        LegacyNativeClientAchievement.AchievementHandler(scope.Client, scope.Packet);
        Assert.Equal(Words(1), achievement.MyFlags()); Assert.Equal(3, scope.Queue.Count);
        Assert.Equal(ScalarFrame(Action.SendQueue, 17, 10101), scope.Queue.Peek());
        scope.Queue.Clear(); scope.Request(Action.SendQueue, 0, uint.MaxValue);
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeClientAchievement.AchievementHandler(scope.Client, scope.Packet));
        Assert.Equal(20, scope.Packet.Position); Assert.Empty(scope.Queue); Assert.Equal(Words(1), achievement.MyFlags());
    });

    [Fact]
    public Task HandlerCheakViewUsesActualRawMapLookupAndTargetBitmapNotCanSeeOrRequesterBitmap() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); Attach(scope.Client, 1);
        scope.Client.Player.X = 18; scope.Client.Player.Y = 18; scope.Client.Player.Map = 1002; scope.Client.Player.DynamicID = 7;
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) }; scope.Client.Map = map;
        var targetQueue = new Queue<byte[]>(); var target = MakeClient(targetQueue, 73, "Target"); Attach(target, 0x80000000, 37);
        target.Player.X = 19; target.Player.Y = 19; target.Player.Map = 9999; target.Player.DynamicID = 8;
        Assert.True(map.View.EnterMap(target.Player)); Assert.False(scope.Client.Player.View.CanSee(target.Player));
        scope.Request(Action.CheakView, 73, uint.MaxValue);
        LegacyNativeClientAchievement.AchievementHandler(scope.Client, scope.Packet);
        var expected = Words(0x80000000); expected[1] = 37;
        Assert.Equal(ArrayFrame(Action.CheakView, 73, expected), Assert.Single(scope.Queue)); Assert.Empty(targetQueue);
        Assert.Equal(4, scope.Packet.Position); Assert.Equal(Words(1), scope.Client.Player.Achievement!.MyFlags());
        Assert.All(scope.Client.Player.View.src, bucket => Assert.Empty(bucket.Values));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HandlerCheakViewNoMapOrAbsentTargetIsSilentWithoutAchievementOwner(bool mapPresent) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (mapPresent) scope.Client.Map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        scope.Request(Action.CheakView, 999, 0);
        LegacyNativeClientAchievement.AchievementHandler(scope.Client, scope.Packet);
        Assert.Empty(scope.Queue); Assert.Equal(20, scope.Packet.Position); Assert.Null(scope.Client.Player.Achievement);
    });

    [Fact]
    public Task HandlerCheakViewFoundPlayerWithMissingAchievementFaultsBeforeWriterReset() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) }; scope.Client.Map = map;
        var target = MakeClient(new Queue<byte[]>(), 73, "Target"); Assert.True(map.View.EnterMap(target.Player));
        scope.Request(Action.CheakView, 73, 0);
        var before = Bytes(scope.Packet);
        Assert.Throws<NullReferenceException>(() => LegacyNativeClientAchievement.AchievementHandler(scope.Client, scope.Packet));
        Assert.Empty(scope.Queue); Assert.Equal(20, scope.Packet.Position); Assert.Equal(before, Bytes(scope.Packet));
    });
}
