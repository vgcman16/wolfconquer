using WolfConquer.Core.World;
using Msg = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAchievementTests
{
    [Fact]
    public Task GuildMessageExplicitModeAndColorUseNativeDefaultsForEveryMatchingPoolAlias() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var guild = LegacyNativeGuildRuntime.CreateLoadedGuild("Pack"); guild.Info.GuildID = 0;
        scope.Poll.TryAdd(1, scope.Client); scope.Poll.TryAdd(2, scope.Client);
        guild.SendMessajGuild("custom", Msg.ChatMode.World, Msg.MsgColor.white);
        var frames = scope.Queue.ToArray(); Assert.Equal(2, frames.Length); Assert.Equal(frames[0], frames[1]);
        AssertChat(frames[0], "ALL", "custom", Msg.ChatMode.World, Msg.MsgColor.white);
        Assert.Equal(0, scope.FirstRecycled.Position); Assert.Equal(3, scope.Recycle.Count);
    });

    [Fact]
    public Task EmptyGuildPoolSkipsNullInfoButStillTakesAndDisposesActualPacket() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var guild = LegacyNativeGuildRuntime.CreateLoadedGuild("Pack"); guild.Info = null!;
        guild.SendMessajGuild("nothing");
        Assert.Empty(scope.Queue); Assert.Equal(0, scope.FirstRecycled.Position); Assert.Equal(3, scope.Recycle.Count);
        Assert.Equal(321, scope.FirstRecycled.Size);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task GuildNullRowPlayerOrInfoFaultsWithoutCatchAndStillDisposesPacket(int fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var guild = LegacyNativeGuildRuntime.CreateLoadedGuild("Pack");
        if (fault == 0) scope.Poll.TryAdd(1, null!);
        else
        {
            scope.Poll.TryAdd(1, scope.Client);
            if (fault == 1) scope.Client.Player = null!;
            else guild.Info = null!;
        }
        Assert.Throws<NullReferenceException>(() => guild.SendMessajGuild("fault"));
        Assert.Empty(scope.Queue); Assert.Equal(0, scope.FirstRecycled.Position); Assert.Equal(3, scope.Recycle.Count);
        Assert.Equal(321, scope.FirstRecycled.Size);
    });
}
