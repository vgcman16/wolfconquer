using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClanTests
{
    [Fact]
    public Task RemovingRealMemberClearsPlayerReferencesSendsTwoFramesAndSubtractsOnlyClanBp() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        var clan = Clan(8, "Wolf");
        var member = new LegacyNativeClanMember { UID = 17, Name = "Member", Online = true };
        clan.Members[17] = member;
        p.Client.Player.MyClan = clan;
        p.Client.Player.MyClanMember = member;
        p.Client.Player.ClanName = "Wolf";
        p.Client.Player.ClanUID = 8;
        p.Client.Player.ClanRank = 100;
        p.Client.Player.ExtraBattlePower = 50;
        p.Client.Player.ClanBp = 30;
        Assert.True(clan.RemoveMember(p.Client));
        Assert.Empty(clan.Members);
        Assert.Null(p.Client.Player.MyClan);
        Assert.Null(p.Client.Player.MyClanMember);
        Assert.Equal("", p.Client.Player.ClanName);
        Assert.Equal(0u, p.Client.Player.ClanUID);
        Assert.Equal((ushort)0, p.Client.Player.ClanRank);
        Assert.Equal(0u, p.Client.Player.ClanBp);
        Assert.Equal(50u, p.Client.Player.ExtraBattlePower);
        Assert.True(member.Online); // Removed row is not rewritten or marked offline by this method.
        Assert.Equal(2, p.Queue.Count);
        Assert.Equal("10000108170000000000000000000000", Convert.ToHexString(p.Queue.Dequeue()));
        Assert.Equal("0E00800808111202082A1202082A", Convert.ToHexString(p.Queue.Dequeue()));
    });

    [Fact]
    public Task AbsentMemberDoesNotClearReferencesOrAllocateSendPackets() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        var clan = Clan(8, "Wolf");
        var member = new LegacyNativeClanMember { UID = 17 };
        p.Client.Player.MyClan = clan;
        p.Client.Player.MyClanMember = member;
        p.Client.Player.ClanUID = 8;
        p.Client.Player.ClanBp = 30;
        p.FailNextTake(); // Would fault if the absent branch touched the recycler.
        Assert.False(clan.RemoveMember(p.Client));
        Assert.Same(clan, p.Client.Player.MyClan);
        Assert.Same(member, p.Client.Player.MyClanMember);
        Assert.Equal(8u, p.Client.Player.ClanUID);
        Assert.Equal(30u, p.Client.Player.ClanBp);
        Assert.Empty(p.Queue);
    });

    [Fact]
    public Task RecyclerFaultOccursAfterMembershipRemovalAndReferenceResetButBeforeBpClear() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        var clan = Clan(8, "Wolf");
        var member = new LegacyNativeClanMember { UID = 17 };
        clan.Members[17] = member;
        p.Client.Player.MyClan = clan;
        p.Client.Player.MyClanMember = member;
        p.Client.Player.ClanName = "Wolf";
        p.Client.Player.ClanUID = 8;
        p.Client.Player.ClanRank = 100;
        p.Client.Player.ClanBp = 30;
        p.FailNextTake();
        Assert.Throws<NullReferenceException>(() => clan.RemoveMember(p.Client));
        Assert.Empty(clan.Members);
        Assert.Null(p.Client.Player.MyClan);
        Assert.Null(p.Client.Player.MyClanMember);
        Assert.Equal("", p.Client.Player.ClanName);
        Assert.Equal(0u, p.Client.Player.ClanUID);
        Assert.Equal((ushort)0, p.Client.Player.ClanRank);
        Assert.Equal(30u, p.Client.Player.ClanBp);
        Assert.Equal(30u, p.Client.Player.ExtraBattlePower);
        Assert.Empty(p.Queue);
    });

    [Fact]
    public Task WholeBroadcastUsesClanUidRatherThanThePlayerClanReference() => NativePacketTestThread.Run(() =>
    {
        using var state = new State();
        using var p = new Packets();
        var clan = Clan(8, "Wolf");
        var sameUidQueue = new Queue<byte[]>();
        var wrongUidQueue = new Queue<byte[]>();
        var sameUid = NativeSpellTestFixture.CreateOwner(sameUidQueue);
        var wrongUid = NativeSpellTestFixture.CreateOwner(wrongUidQueue);
        sameUid.Player.ClanUID = 8;
        sameUid.Player.MyClan = Clan(99, "UnrelatedReference");
        wrongUid.Player.ClanUID = 9;
        wrongUid.Player.MyClan = clan;
        LegacyNativePoolClients.GamePoll![1] = sameUid;
        LegacyNativePoolClients.GamePoll[2] = wrongUid;
        p.Stream.ClanAppendSingleClientCreate(8, "Wolf", LegacyNativeMsgClan.Info.Info);
        clan.Send(p.Stream);
        Assert.Single(sameUidQueue);
        Assert.Empty(wrongUidQueue);
        Assert.Equal(NativeSpellTestFixture.Bytes(p.Stream), sameUidQueue.Dequeue());
    });

    [Fact]
    public Task ShareWriterPreservesLeaderAndShareAsTwoEntriesWithTheSameNativeType() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets();
        var clan = Clan(8, "Wolf");
        clan.ShareBattlePower(300, 70, p.Client);
        // Real update protobuf: UID17; two type42 entries, values300 then70.
        Assert.Equal("1300800808111205082A10AC021204082A1046", Convert.ToHexString(Assert.Single(p.Queue)));
        Assert.Equal(0u, p.Client.Player.ClanBp); // Writer alone does not mutate the stat owner.
    });
}
