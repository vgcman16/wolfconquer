using WolfConquer.Core.World;
using Associate = WolfConquer.Core.World.LegacyNativeAssociateRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMentorShareTests
{
    [Fact]
    public Task SendMentorSharePublishesActualOnlineMentorInfoAndPreservesSourceEmptyBpSetter() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var mentor = scope.Peer(30, 140, 260);
        LinkMentor(scope.Client, mentor, 0x1122334455667788);
        scope.Client.Equipment!.SendMentorShare(scope.Packet);
        var info = ReadInfo(Assert.Single(scope.Queue));
        Assert.Equal(1u, info.Mode); Assert.Equal(30u, info.Mentor); Assert.Equal(17u, info.Apprentice); Assert.Equal(50u, info.Share);
        Assert.Equal(0x55667788u, info.Date); Assert.Equal(140u, info.Level); Assert.Equal(1005u, info.Class); Assert.Equal((ushort)31, info.Pk);
        Assert.Equal(new[] { "P30", "P17", "S17", "B17" }, info.Names);
        Assert.Equal(37u, scope.Player.ExtraBattlePower); Assert.Equal(37u, mentor.Player.ExtraBattlePower); Assert.Empty(scope.Sent(mentor));
    });

    [Fact]
    public Task SendMentorShareApprenticeLoopRetainsZeroMentorIdAndMentorModeFromNativeFactory() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var student = scope.Peer(21);
        LinkApprentice(scope.Client, student, 12345);
        scope.Client.Equipment!.SendMentorShare(scope.Packet);
        var info = ReadInfo(Assert.Single(scope.Sent(student)));
        Assert.Equal(1u, info.Mode); Assert.Equal(0u, info.Mentor); Assert.Equal(21u, info.Apprentice); Assert.Equal(50u, info.Share);
        Assert.Equal(12345u, info.Date); Assert.Equal(140u, info.Level); Assert.Equal((ushort)18, info.Pk);
        Assert.Equal(new[] { "P17", "P21", "S17", "B21" }, info.Names);
        Assert.Equal(37u, student.Player.ExtraBattlePower); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task SendMentorShareReusesPriorGrandMentorIdInLaterApprenticePacketWithoutRewritingEarlierQueuedBytes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var grandMentor = scope.Peer(30, 140, 260); var student = scope.Peer(21);
        LinkMentor(scope.Client, grandMentor, 77); LinkApprentice(scope.Client, student, 88);
        scope.Client.Equipment!.SendMentorShare(scope.Packet);
        var own = ReadInfo(Assert.Single(scope.Queue)); var apprentice = ReadInfo(Assert.Single(scope.Sent(student)));
        Assert.Equal(30u, own.Mentor); Assert.Equal(17u, own.Apprentice); Assert.Equal(77u, own.Date);
        Assert.Equal(30u, apprentice.Mentor); Assert.Equal(21u, apprentice.Apprentice); Assert.Equal(88u, apprentice.Date);
        Assert.Equal(1u, apprentice.Mode); Assert.Equal(50u, own.Share); Assert.Equal(50u, apprentice.Share);
    });

    [Fact]
    public Task SendMentorShareUsesOnlyActualOnlineCacheIntersectionAndDoesNotDiscoverPoolOrOfflineRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var cachedButNotRelated = scope.Peer(21); var relatedButNotCached = scope.Peer(22);
        scope.Player.Associate!.Add(5, new Associate.Member { UID = 22 });
        scope.Player.Associate.OnlineApprentice[21] = cachedButNotRelated;
        scope.Client.Equipment!.SendMentorShare(scope.Packet);
        Assert.Empty(scope.Sent(cachedButNotRelated)); Assert.Empty(scope.Sent(relatedButNotCached)); Assert.Empty(scope.Queue);
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
    });

    [Fact]
    public Task SendMentorShareKeepsNativeOwnerAndMentorIdentityFaultsInsteadOfInferringOrRepairingThem() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var mentor = scope.Peer(30, 140, 260);
        scope.Player.Associate = null;
        Assert.Throws<NullReferenceException>(() => scope.Client.Equipment!.SendMentorShare(scope.Packet));
        scope.Player.Associate = new Associate.MyAsociats(17);
        scope.Player.MyMentor = mentor.Player.Associate;
        scope.Player.Associate.Add(4, new Associate.Member { UID = 30, Timer = 77 });
        mentor.Player.UID = 31; //Pool/mentor row keys are not rewritten by physical-player identity mutation.
        Assert.Throws<KeyNotFoundException>(() => scope.Client.Equipment!.SendMentorShare(scope.Packet));
        Assert.Empty(scope.Queue); Assert.Empty(scope.Sent(mentor)); Assert.Equal(37u, scope.Player.ExtraBattlePower);
    });

    [Fact]
    public Task SendMentorShareRetainsFirstPublishedMentorPacketWhenLaterActualOnlineRowFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var mentor = scope.Peer(30, 140, 260);
        LinkMentor(scope.Client, mentor, 77);
        scope.Player.Associate!.Associat[5] = new();
        scope.Player.Associate.OnlineApprentice[21] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Client.Equipment!.SendMentorShare(scope.Packet));
        var published = ReadInfo(Assert.Single(scope.Queue)); Assert.Equal(30u, published.Mentor); Assert.Equal(17u, published.Apprentice);
        Assert.Equal(50u, published.Share); Assert.Empty(scope.Sent(mentor));
    });

    [Fact]
    public Task SendMentorShareGenuineEmptyEnumerationDoesNotReadNullStreamButNullStreamFaultsOnRequiredSend() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Equipment!.SendMentorShare(null!); Assert.Empty(scope.Queue);
        var mentor = scope.Peer(30, 140, 260); LinkMentor(scope.Client, mentor);
        Assert.Throws<NullReferenceException>(() => scope.Client.Equipment.SendMentorShare(null!));
        Assert.Empty(scope.Queue); Assert.Equal(37u, scope.Player.ExtraBattlePower);
    });
}
