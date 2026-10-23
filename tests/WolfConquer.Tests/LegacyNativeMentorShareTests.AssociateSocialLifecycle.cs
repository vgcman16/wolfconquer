using WolfConquer.Core.World;
using Associate = WolfConquer.Core.World.LegacyNativeAssociateRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMentorShareTests
{
    [Theory]
    [InlineData(1, true)] [InlineData(1, false)] [InlineData(2, true)]
    [InlineData(2, false)] [InlineData(3, true)] [InlineData(3, false)]
    public Task AssociateOnLoadingFriendEnemyAndPartnerKeepNativeLiveVsStoredColumns(int relation, bool online) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var peer = scope.Peer(21, online: online);
        scope.Player.Associate!.Add((byte)relation, new Associate.Member
        { UID = 21, Name = "Saved", Signature = "SavedSig", Mesh = 123, Level = 456, NobilityRank = 789 });
        scope.Player.Associate.OnLoading(scope.Client);
        byte[] own = Assert.Single(scope.Queue);
        Assert.Equal(relation == 3 ? (ushort)2004 : (ushort)2139, BitConverter.ToUInt16(own, 2));
        Assert.Equal(21u, BitConverter.ToUInt32(own, 4)); Assert.Equal(online ? (byte)1 : (byte)0, own[9]);
        Assert.Equal(relation == 1 ? (byte)15 : relation == 2 ? (byte)19 : (byte)5, own[8]);
        if (relation == 3)
        {
            Assert.Equal(123u, BitConverter.ToUInt32(own, 12)); Assert.Equal(456u, BitConverter.ToUInt32(own, 16)); Assert.Equal(789u, BitConverter.ToUInt32(own, 20));
        }
        else
        {
            Assert.Equal(online ? peer.Player.Mesh : 0u, BitConverter.ToUInt32(own, 20));
            Assert.Equal(online ? (uint)peer.Player.Level : 0u, BitConverter.ToUInt32(own, 24));
        }
        if (online && relation != 2)
        {
            byte[] reciprocal = Assert.Single(scope.Sent(peer));
            Assert.Equal(17u, BitConverter.ToUInt32(reciprocal, 4)); Assert.Equal(relation == 1 ? (byte)12 : (byte)2, reciprocal[8]);
        }
        else Assert.Empty(scope.Sent(peer));
    });

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public Task AssociateOnDisconnectSendsExactNativeOfflineKindWithoutRemovingCachesOrInventingBpUpdates(int relation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var peer = scope.Peer(21);
        scope.Player.Associate!.Add((byte)relation, new Associate.Member { UID = 21 });
        scope.Player.Associate.OnlineApprentice[21] = peer;
        scope.Player.Associate.OnDisconnect(scope.Packet, scope.Client);
        byte[] sent = Assert.Single(scope.Sent(peer)); Assert.Empty(scope.Queue);
        if (relation >= 4)
        {
            var info = ReadInfo(sent); Assert.Equal(relation == 4 ? 2u : 1u, info.Mode); Assert.Equal(0u, info.Online); Assert.Equal(0u, info.Share);
            Assert.Equal(relation == 4 ? 21u : 17u, info.Mentor); Assert.Equal(relation == 4 ? 17u : 21u, info.Apprentice);
            Assert.Equal(relation == 4 ? 140u : 100u, info.Level); Assert.Equal("B17", info.Names[3]);
        }
        else
        {
            Assert.Equal(relation == 1 ? (ushort)2139 : (ushort)2004, BitConverter.ToUInt16(sent, 2));
            Assert.Equal(17u, BitConverter.ToUInt32(sent, 4)); Assert.Equal(relation == 1 ? (byte)13 : (byte)3, sent[8]); Assert.Equal((byte)0, sent[9]);
        }
        Assert.Same(peer, scope.Player.Associate.OnlineApprentice[21]); Assert.True(scope.Player.Associate.Contain((byte)relation, 21));
        Assert.Same(peer, LegacyNativePoolClients.GamePoll![21]); Assert.Equal(37u, peer.Player.ExtraBattlePower);
    });

    [Theory]
    [InlineData(2)] [InlineData(6)]
    public Task AssociateOnDisconnectEnemyAndExplorerRowsAreNativeSilentWithoutDroppingRecords(int relation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var peer = scope.Peer(21);
        scope.Player.Associate!.Add((byte)relation, new Associate.Member { UID = 21 });
        scope.Player.Associate.OnDisconnect(scope.Packet, scope.Client);
        Assert.Empty(scope.Queue); Assert.Empty(scope.Sent(peer)); Assert.True(scope.Player.Associate.Contain((byte)relation, 21));
    });
}
