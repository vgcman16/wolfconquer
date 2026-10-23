using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Associate = WolfConquer.Core.World.LegacyNativeAssociateRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMentorShareTests
{
    [Fact]
    public Task AddApprenticeAndMentorCopyRealPlayerFieldsButDoNotInventReciprocalMentorBindings() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var student = scope.Peer(21); var row = scope.Player.Associate!;
        row.AddAprrentice(scope.Client, student.Player);
        Assert.Same(student, row.OnlineApprentice[21]); var member = row.Associat[5][21];
        Assert.Equal(student.Player.UID, member.UID); Assert.Equal(student.Player.Name, member.Name); Assert.Equal(student.Player.Signature, member.Signature);
        Assert.Equal(student.Player.Class, member.Class); Assert.Equal((uint)student.Player.Level, member.Level); Assert.Equal(student.Player.Mesh, member.Mesh);
        Assert.Equal(0ul, member.Timer); Assert.Null(student.Player.MyMentor); Assert.False(student.Player.Associate!.Associat.ContainsKey(4));
        student.Player.Associate.AddMentor(student, scope.Player);
        Assert.Equal(17u, student.Player.Associate.Associat[4][17].UID); Assert.Null(student.Player.MyMentor);
        Assert.Same(row, Associate.Associates[17]); Assert.Empty(scope.Queue); Assert.Empty(scope.Sent(student));
    });

    [Fact]
    public Task AddApprenticeRetainsOnlinePrefixForZeroUidOwnerAndDoesNotOverwriteExistingOnlineReference() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var student = scope.Peer(21);
        var zero = new Associate.MyAsociats(0); zero.AddAprrentice(scope.Client, student.Player);
        Assert.Same(student, zero.OnlineApprentice[21]); Assert.Empty(zero.Associat); Assert.False(Associate.Associates.ContainsKey(0));
        var row = scope.Player.Associate!; row.OnlineApprentice[21] = scope.Client;
        row.AddAprrentice(scope.Client, student.Player);
        Assert.Same(scope.Client, row.OnlineApprentice[21]); Assert.False(row.Associat.ContainsKey(5));
    });

    [Fact]
    public Task ApprenticeCapacityUsesNativeByteCastAndExistingModeLimitBeforeSendingExactFailureMessage() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var student = scope.Peer(21); var row = scope.Player.Associate!;
        row.Associat[5] = new(); LegacyNativeTutorInfo.TutorTypes[0].StudentNum = 0;
        row.AddAprrentice(scope.Client, student.Player);
        Assert.Empty(row.Associat[5]); Assert.Empty(row.OnlineApprentice);
        Assert.Contains("Sorry, you used the limit of aprrentice`s", Encoding.UTF8.GetString(Assert.Single(scope.Queue)));
        scope.Queue.Clear(); LegacyNativeTutorInfo.TutorTypes[0].StudentNum = -1;
        row.AddAprrentice(scope.Client, student.Player);
        Assert.Same(student, row.OnlineApprentice[21]); Assert.True(row.Contain(5, 21)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PartnerAndFriendAddsRetainNativeMetadataTimerAndDoNotUpdateAStaleExistingRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var peer = scope.Peer(21); var row = scope.Player.Associate!;
        long earliest = DateTime.Now.AddDays(3).Ticks;
        row.AddPartener(scope.Client, peer.Player); var partner = row.Associat[3][21]; long latest = DateTime.Now.AddDays(3).Ticks;
        Assert.InRange(partner.Timer, (ulong)earliest, (ulong)latest);
        row.AddFriends(scope.Client, peer.Player); var friend = row.Associat[1][21];
        Assert.Equal(peer.Player.Name, friend.Name); Assert.Equal(peer.Player.Signature, friend.Signature); Assert.Equal(0ul, friend.Timer);
        peer.Player.Name = "Changed"; row.AddFriends(scope.Client, peer.Player);
        Assert.Same(friend, row.Associat[1][21]); Assert.Equal("P21", friend.Name);
        Assert.Contains("Sorry, you used the limit of frinds", Encoding.UTF8.GetString(Assert.Single(scope.Queue)));
    });

    [Fact]
    public Task EnemyAddAtCapacityEvictsActualFirstValueThenPublishesWholeNativeEnemyFrame() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var killer = scope.Peer(21); var row = scope.Player.Associate!;
        for (uint uid = 100; uid < 120; uid++) row.Add(2, new Associate.Member { UID = uid });
        uint nativeFirst = row.Associat[2].Values.ToArray()[0].UID;
        row.AddEnemy(scope.Client, killer.Player);
        Assert.Equal(20, row.Associat[2].Count); Assert.False(row.Contain(2, nativeFirst)); Assert.True(row.Contain(2, 21));
        var member = row.Associat[2][21]; Assert.Equal(killer.Player.Name, member.Name); Assert.Equal(killer.Player.Signature, member.Signature);
        byte[] bytes = Assert.Single(scope.Queue); Assert.Equal((ushort)2139, BitConverter.ToUInt16(bytes, 2)); Assert.Equal(21u, BitConverter.ToUInt32(bytes, 4));
        Assert.Equal((byte)19, bytes[8]); Assert.Equal((byte)1, bytes[9]); Assert.Equal(128, bytes.Length);
    });

    [Fact]
    public Task PkExplorerUpdatesExistingNativeReferenceWrapsKillsAndReturnsAscendingRanking() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var peer = scope.Peer(21); var row = scope.Player.Associate!;
        peer.Player.Map = 1002; row.AddPKExplorer(scope.Client, peer.Player);
        var member = row.Associat[6][21]; Assert.Equal((ushort)1, member.KillsCount); Assert.Equal(1002u, member.KillMap);
        member.KillsCount = ushort.MaxValue; peer.Player.Map = 2000; row.AddPKExplorer(scope.Client, peer.Player);
        Assert.Same(member, row.Associat[6][21]); Assert.Equal((ushort)0, member.KillsCount); Assert.Equal(2000u, member.KillMap);
        row.Add(6, new Associate.Member { UID = 30, KillsCount = 5 }); row.Add(6, new Associate.Member { UID = 31, KillsCount = 2 });
        Assert.Equal(new uint[] { 21, 31, 30 }, row.GetPkExplorerRank().Select(value => value.UID));
        uint now = row.GetTimePkExplorer(); Assert.InRange(now / 1000000, 1u, 12u); Assert.InRange(now / 10000 % 100, 1u, 31u);
        Assert.InRange(now / 100 % 100, 0u, 23u); Assert.InRange(now % 100, 0u, 59u); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task PkExplorerAtCapacityEvictsActualFirstRatherThanSortingKillsOrTimerAndZeroOwnerDoesNothing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var peer = scope.Peer(21); var row = scope.Player.Associate!;
        for (uint uid = 100; uid < 150; uid++) row.Add(6, new Associate.Member { UID = uid, KillsCount = (ushort)(uid - 100) });
        uint first = row.Associat[6].Values.ToArray()[0].UID;
        row.AddPKExplorer(scope.Client, peer.Player);
        Assert.Equal(50, row.Associat[6].Count); Assert.False(row.Contain(6, first)); Assert.Equal((ushort)1, row.Associat[6][21].KillsCount);
        var zero = new Associate.MyAsociats(0); zero.AddPKExplorer(scope.Client, null!); Assert.Empty(zero.Associat);
        Assert.Empty(zero.GetPkExplorerRank());
    });

    [Fact]
    public Task MemberLiveQueriesUseActualGlobalClientBpNotSavedColumnsAndRetainNullPoolFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var peer = scope.Peer(21); var row = new Associate.Member { UID = 21, Level = 999, NobilityRank = 999, Class = 999 };
        Assert.Equal(1u, row.IsOnline); Assert.Equal((ushort)237, row.BattlePower);
        peer.Equipment!.BattlePower = 200; Assert.Equal((ushort)337, row.BattlePower);
        LegacyNativePoolClients.GamePoll!.TryRemove(21, out _); Assert.Equal(0u, row.IsOnline); Assert.Equal((ushort)0, row.BattlePower);
        LegacyNativePoolClients.GamePoll = null!;
        Assert.Throws<NullReferenceException>(() => row.IsOnline); Assert.Throws<NullReferenceException>(() => row.BattlePower);
    });
}
