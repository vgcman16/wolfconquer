using WolfConquer.Core.World;
using Action = WolfConquer.Core.World.LegacyNativeClientAchievement.TypeAchievement;
using Msg = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAchievementTests
{
    [Theory]
    [InlineData(10101, 0, 10101u, 1)]
    [InlineData(10132, 31, 10132u, 0)]
    [InlineData(10201, 32, 10201u, 1)]
    [InlineData(11432, 447, 11432u, 0)]
    [InlineData(100, -1, 10356u, 0)]
    public Task CreateFlaIdUsesNativeDecimalMappingAndCommitsBeforeThreeOrderedMessages(int id, int flag, uint displayId, int score) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var achievement = Attach(scope.Client);
        achievement.CreateFlaID(id, scope.Client, scope.Packet);
        var expected = Words(); expected[flag / 32] = 1u << (flag % 32);
        Assert.Equal(expected, achievement.MyFlags()); Assert.Equal(score, achievement.Score());
        byte[][] frames = scope.Queue.ToArray(); Assert.Equal(3, frames.Length);
        Assert.Equal(ScalarFrame(Action.SendQueue, 17, displayId), frames[0]);
        AssertChat(frames[1], "Wolf", $"Wolf received [Achievement {displayId}]! ", Msg.ChatMode.Talk, Msg.MsgColor.red);
        Assert.Equal(ArrayFrame(Action.BigHash, 17, expected), frames[2]);
        achievement.CreateFlaID(id, null!, null!); // An existing bit never touches client, writer or globals.
        Assert.Equal(3, scope.Queue.Count);
    });

    [Theory]
    [InlineData(11501)]
    [InlineData(0)]
    public Task NativeMappedIndexFaultPrecedesAllMutationAndMessages(int id) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var achievement = Attach(scope.Client);
        Assert.Throws<IndexOutOfRangeException>(() => achievement.CreateFlaID(id, scope.Client, scope.Packet));
        Assert.Equal(new uint[14], achievement.MyFlags()); Assert.Empty(scope.Queue);
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
    });

    [Theory]
    [InlineData(449, 11502u)]
    [InlineData(480, 11601u)]
    public Task AboveSizeDoesNotSetBitButStillNotifiesOnEveryInvocation(int flag, uint displayId) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var achievement = Attach(scope.Client);
        for (int i = 0; i < 2; i++) achievement.AddFlag(flag, scope.Client, scope.Packet);
        Assert.Equal(new uint[14], achievement.MyFlags()); Assert.Equal(0, achievement.Score());
        byte[][] frames = scope.Queue.ToArray(); Assert.Equal(6, frames.Length);
        for (int i = 0; i < 6; i += 3)
        {
            Assert.Equal(ScalarFrame(Action.SendQueue, 17, displayId), frames[i]);
            AssertChat(frames[i + 1], "Wolf", $"Wolf received [Achievement {displayId}]! ", Msg.ChatMode.Talk, Msg.MsgColor.red);
            Assert.Equal(ArrayFrame(Action.BigHash, 17, new uint[14]), frames[i + 2]);
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FirstNotificationFailureRetainsCommittedBitAndRepeatedCallIsSilent(bool nullClient) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var achievement = Attach(scope.Client);
        Assert.Throws<NullReferenceException>(() => achievement.AddFlag(0, nullClient ? null! : scope.Client, null!));
        Assert.Equal(Words(1), achievement.MyFlags()); Assert.Empty(scope.Queue);
        achievement.AddFlag(0, null!, null!); Assert.Empty(scope.Queue);
        Assert.Equal(321, scope.Packet.Size); Assert.Equal(237, scope.Packet.Position);
    });

    [Fact]
    public Task DetachedMutationStillUsesCurrentClientPlayerForNotificationAndFinalBitmap() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var detached = new LegacyNativeClientAchievement(new uint[14], 17);
        var current = Attach(scope.Client, 16); scope.Client.Player.UID = 73; scope.Client.Player.Name = "NewWolf";
        detached.AddFlag(0, scope.Client, scope.Packet);
        Assert.Equal(Words(1), detached.MyFlags()); Assert.Equal(Words(16), current.MyFlags());
        byte[][] frames = scope.Queue.ToArray(); Assert.Equal(3, frames.Length);
        Assert.Equal(ScalarFrame(Action.SendQueue, 73, 10101), frames[0]);
        AssertChat(frames[1], "NewWolf", "NewWolf received [Achievement 10101]! ", Msg.ChatMode.Talk, Msg.MsgColor.red);
        Assert.Equal(ArrayFrame(Action.BigHash, 73, Words(16)), frames[2]);
        scope.Queue.Clear(); detached.Send(scope.Client, scope.Packet);
        Assert.Equal(ArrayFrame(Action.BigHash, 73, Words(16)), Assert.Single(scope.Queue));
    });

    [Fact]
    public Task MissingCurrentAchievementFaultsOnlyAfterNotificationAndChatWithBitRetained() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var detached = new LegacyNativeClientAchievement([], 17);
        Assert.Null(scope.Client.Player.Achievement);
        Assert.Throws<NullReferenceException>(() => detached.AddFlag(0, scope.Client, scope.Packet));
        Assert.Equal(Words(1), detached.MyFlags());
        byte[][] frames = scope.Queue.ToArray(); Assert.Equal(2, frames.Length);
        Assert.Equal(ScalarFrame(Action.SendQueue, 17, 10101), frames[0]);
        AssertChat(frames[1], "Wolf", "Wolf received [Achievement 10101]! ", Msg.ChatMode.Talk, Msg.MsgColor.red);
    });

    [Fact]
    public Task GuildPathUsesPoolGuildIdNotMembersAndPreservesAliasDuplicatesBeforeFinalHash() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var achievement = Attach(scope.Client);
        var guild = LegacyNativeGuildRuntime.CreateLoadedGuild("Pack"); guild.Info.GuildID = 77;
        scope.Client.Player.MyGuild = guild; scope.Client.Player.GuildID = 77;
        var otherQueue = new Queue<byte[]>(); var other = MakeClient(otherQueue, 18, "Other"); other.Player.GuildID = 77;
        var excludedQueue = new Queue<byte[]>(); var excluded = MakeClient(excludedQueue, 19, "Excluded"); excluded.Player.GuildID = 78;
        scope.Poll.TryAdd(1, scope.Client); scope.Poll.TryAdd(2, scope.Client); scope.Poll.TryAdd(3, other); scope.Poll.TryAdd(4, excluded);
        Assert.Empty(guild.Members); Assert.Null(other.Player.MyGuild); Assert.False(other.FullLoading);
        achievement.AddFlag(0, scope.Client, scope.Packet);
        byte[][] frames = scope.Queue.ToArray(); Assert.Equal(4, frames.Length);
        Assert.Equal(ScalarFrame(Action.SendQueue, 17, 10101), frames[0]);
        AssertChat(frames[1], "ALL", "Wolf received [Achievement 10101]! ", Msg.ChatMode.Guild, Msg.MsgColor.yellow);
        Assert.Equal(frames[1], frames[2]); Assert.Equal(frames[1], Assert.Single(otherQueue));
        Assert.Equal(ArrayFrame(Action.BigHash, 17, Words(1)), frames[3]);
        Assert.Empty(excludedQueue); Assert.Equal(0, scope.FirstRecycled.Position); Assert.Equal(3, scope.Recycle.Count);
    });

    [Fact]
    public Task GuildFailureKeepsNotificationAndBitButPreventsChatAndFinalHash() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var achievement = Attach(scope.Client);
        scope.Client.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Pack");
        LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => achievement.AddFlag(0, scope.Client, scope.Packet));
        Assert.Equal(Words(1), achievement.MyFlags());
        Assert.Equal(ScalarFrame(Action.SendQueue, 17, 10101), Assert.Single(scope.Queue));
        Assert.Equal(0, scope.FirstRecycled.Position); Assert.Equal(3, scope.Recycle.Count);
        achievement.AddFlag(0, null!, null!); Assert.Single(scope.Queue);
    });
}
