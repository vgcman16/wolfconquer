using Associate = WolfConquer.Core.World.LegacyNativeAssociateRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMentorShareTests
{
    [Theory]
    [InlineData(4)] [InlineData(5)]
    public Task AssociateOnLoadingOfflineMentorAndApprenticeUseStoredRowsWithFullLevelAndLiteralNames(int relation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var offline = scope.Peer(21, online: false);
        scope.Player.Associate!.Add((byte)relation, new Associate.Member
        { UID = 21, Name = "Saved", Signature = "SavedSig", Class = 123, Mesh = 456, Level = 999, Timer = 0x1122334455667788 });
        scope.Player.Associate.OnLoading(scope.Client);
        var info = ReadInfo(Assert.Single(scope.Queue));
        Assert.Equal(relation == 4 ? 1u : 2u, info.Mode); Assert.Equal(relation == 4 ? 21u : 17u, info.Mentor);
        Assert.Equal(relation == 4 ? 17u : 21u, info.Apprentice); Assert.Equal(0u, info.Online); Assert.Equal(0u, info.Share);
        Assert.Equal(999u, info.Level); Assert.Equal(123u, info.Class); Assert.Equal(456u, info.Mesh); Assert.Equal((ushort)0, info.Pk);
        Assert.Equal(0x55667788u, info.Date);
        Assert.Equal(relation == 4 ? new[] { "Saved", "", "", "" } : new[] { "NULL", "Saved", "NULL", "SavedSig" }, info.Names);
        Assert.Empty(scope.Sent(offline)); Assert.Null(scope.Player.MyMentor);
    });

    [Fact]
    public Task AssociateOnLoadingOnlineApprenticePublishesBothNativeModesAndRetainsUInt16ExperienceNarrowing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var student = scope.Peer(21);
        scope.Player.Associate!.Add(5, new Associate.Member
        { UID = 21, Timer = 77, ExpBalls = 65537, Blessing = uint.MaxValue, Stone = 131074 });
        scope.Player.Associate.OnLoading(scope.Client);
        var ownerInfo = ReadInfo(Assert.Single(scope.Queue)); var studentInfo = ReadInfo(Assert.Single(scope.Sent(student)));
        Assert.Equal(2u, ownerInfo.Mode); Assert.Equal(1u, studentInfo.Mode); Assert.Equal(17u, ownerInfo.Mentor); Assert.Equal(21u, ownerInfo.Apprentice);
        Assert.Equal(17u, studentInfo.Mentor); Assert.Equal(21u, studentInfo.Apprentice); Assert.Equal(0u, ownerInfo.Share); Assert.Equal(50u, studentInfo.Share);
        Assert.Equal(100u, ownerInfo.Level); Assert.Equal(140u, studentInfo.Level); Assert.Equal((ushort)22, ownerInfo.Pk); Assert.Equal((ushort)18, studentInfo.Pk);
        Assert.Equal(1ul, ownerInfo.Experience); Assert.Equal(1ul, studentInfo.Experience);
        Assert.Equal(ushort.MaxValue, ownerInfo.Blessing); Assert.Equal((ushort)2, ownerInfo.Composing);
        Assert.Same(student, scope.Player.Associate.OnlineApprentice[21]); Assert.Equal(37u, student.Player.ExtraBattlePower);
        Assert.Null(student.Player.MyMentor);
    });

    [Fact]
    public Task AssociateOnLoadingOnlineMentorUsesReciprocalStoredApprenticeAndPublishesToActualOwners() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var mentor = scope.Peer(30, 140, 260);
        scope.Player.Associate!.Add(4, new Associate.Member { UID = 30, Timer = 99 });
        mentor.Player.Associate!.Add(5, new Associate.Member { UID = 17, Timer = 77, ExpBalls = 3, Stone = 4, Blessing = 5 });
        scope.Player.Associate.OnLoading(scope.Client);
        var mentorInfo = ReadInfo(Assert.Single(scope.Sent(mentor))); var ownInfo = ReadInfo(Assert.Single(scope.Queue));
        Assert.Equal(2u, mentorInfo.Mode); Assert.Equal(1u, ownInfo.Mode); Assert.Equal(30u, ownInfo.Mentor); Assert.Equal(17u, ownInfo.Apprentice);
        Assert.Equal(77u, ownInfo.Date); Assert.Equal(50u, ownInfo.Share); Assert.Equal((ushort)31, ownInfo.Pk); Assert.Equal((ushort)18, mentorInfo.Pk);
        Assert.Equal(3ul, ownInfo.Experience); Assert.Equal((ushort)4, ownInfo.Composing); Assert.Equal((ushort)5, ownInfo.Blessing);
        Assert.Same(scope.Client, mentor.Player.Associate.OnlineApprentice[17]); Assert.Null(scope.Player.MyMentor);
        Assert.Equal(new[] { "P30", "P17", "S17", "B17" }, ownInfo.Names);
    });

    [Theory]
    [InlineData(4)] [InlineData(5)]
    public Task AssociateOnLoadingDoesNotRepublishMentorPacketsWhenActualOnlineCacheAlreadyContainsPeer(int relation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var peer = scope.Peer(30, 140, 260);
        scope.Player.Associate!.Add((byte)relation, new Associate.Member { UID = 30 });
        if (relation == 4)
        {
            peer.Player.Associate!.Add(5, new Associate.Member { UID = 17 });
            peer.Player.Associate.OnlineApprentice[17] = scope.Client;
        }
        else scope.Player.Associate.OnlineApprentice[30] = peer;
        scope.Player.Associate.OnLoading(scope.Client);
        Assert.Empty(scope.Queue); Assert.Empty(scope.Sent(peer));
    });

    [Fact]
    public Task AssociateOnLoadingRetainsMissingReciprocalModeFaultRatherThanFabricatingMentorMembership() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var mentor = scope.Peer(30, 140, 260);
        scope.Player.Associate!.Add(4, new Associate.Member { UID = 30 });
        Assert.Throws<KeyNotFoundException>(() => scope.Player.Associate.OnLoading(scope.Client));
        Assert.Empty(scope.Queue); Assert.Empty(scope.Sent(mentor)); Assert.Empty(mentor.Player.Associate!.OnlineApprentice);
    });
}
