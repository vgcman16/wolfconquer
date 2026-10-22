using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.World.LegacyNativeNobility.NobilityRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityTests
{
    [Fact]
    public Task IconUsesLookupKeyAndActualCurrentPlayerRatherThanPlayerUidOrNobilityBacklink() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            var player = scope.Native.Owner.Player;
            player.UID = 999;
            player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("G");
            player.MyGuildMember = new LegacyNativeGuildRuntime.Member { UID = 998 };
            player.UnionMemeber = new LegacyNativeUnionMember { UID = 997 };
            var row = Row(position: 0);
            scope.Pool[17] = scope.Native.Owner;

            ranking.UpdateClientIcon(scope.Packet, row);

            Assert.Equal(LegacyNativeNobilityRank.King, player.NobilityRank);
            Assert.Equal(12u, player.MyGuildMember.NobilityRank);
            Assert.Equal(LegacyNativeNobilityRank.King, player.UnionMemeber.NobilityRank);
            Assert.Equal(999u, player.UID); Assert.Equal(998u, player.MyGuildMember.UID);
            Assert.Equal(997u, player.UnionMemeber.UID); Assert.Null(player.Nobility);
            Assert.Equal(Icon(17, 300_000_000, 12, 0), Assert.Single(scope.Native.Queue));
            Assert.Equal(4, scope.Packet.Position); Assert.Equal(0, row.Position);
            Assert.Empty(ranking.ClientPoll);
        });

    [Fact]
    public Task MissingPoolKeySkipsNullPacketWithoutChangingTheUnrelatedPlayer() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            scope.Pool[99] = scope.Native.Owner;
            ranking.UpdateClientIcon(null!, Row(position: 0));
            Assert.Equal(LegacyNativeNobilityRank.Serf, scope.Native.Owner.Player.NobilityRank);
            Assert.Empty(scope.Native.Queue); Assert.Equal(321, scope.Packet.Size); Assert.Equal(237, scope.Packet.Position);
        });

    [Fact]
    public Task FakeRecipientStillGetsActualRankMutationAndPacketConstructionButNoQueuedSend() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            scope.Native.Owner.Fake = true; scope.Native.Owner.Socket = null;
            scope.Pool[17] = scope.Native.Owner;
            ranking.UpdateClientIcon(scope.Packet, Row(position: 3));
            Assert.Equal(LegacyNativeNobilityRank.Prince, scope.Native.Owner.Player.NobilityRank);
            Assert.Equal(Icon(17, 300_000_000, 9, 3), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task NullPacketFaultOccursAfterPlayerAndActualMemberRankMutationBeforeAnySend() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            var player = scope.Native.Owner.Player;
            player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("G");
            player.MyGuildMember = new LegacyNativeGuildRuntime.Member();
            player.UnionMemeber = new LegacyNativeUnionMember();
            scope.Pool[17] = scope.Native.Owner;
            Assert.Throws<NullReferenceException>(() => ranking.UpdateClientIcon(null!, Row(position: 17)));
            Assert.Equal(LegacyNativeNobilityRank.Duke, player.NobilityRank);
            Assert.Equal(7u, player.MyGuildMember.NobilityRank);
            Assert.Equal(LegacyNativeNobilityRank.Duke, player.UnionMemeber.NobilityRank);
            Assert.Empty(scope.Native.Queue); Assert.Equal(321, scope.Packet.Size); Assert.Equal(237, scope.Packet.Position);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualNullClientOrPlayerFaultsBeforePacketConstructionWithoutRepair(bool nullPlayer) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            if (nullPlayer)
            {
                scope.Native.Owner.Player = null!;
                scope.Pool[17] = scope.Native.Owner;
            }
            else scope.Pool[17] = null!;
            Assert.Throws<NullReferenceException>(() => ranking.UpdateClientIcon(scope.Packet, Row(position: 0)));
            Assert.Equal(321, scope.Packet.Size); Assert.Equal(237, scope.Packet.Position);
            Assert.Empty(scope.Native.Queue); Assert.Single(scope.Pool);
            if (nullPlayer) Assert.Null(scope.Native.Owner.Player); else Assert.Null(scope.Pool[17]);
        });

    [Fact]
    public Task PromotionQueuesIconBeforeGenuineGlobalChatAndUpdatesCurrentPlayerNotItsSeparateRecord() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            var observerQueue = new Queue<byte[]>();
            var observer = NativeSpellTestFixture.CreateOwner(observerQueue);
            var separate = Row(17, 0, -1, "Separate");
            scope.Native.Owner.Player.Nobility = separate;
            scope.Pool[17] = scope.Native.Owner; scope.Pool[99] = observer;
            var row = Row(name: "Promoted Wolf");

            ranking.CreateRank(row);

            Assert.Equal(0, row.Position); Assert.Equal(LegacyNativeNobilityRank.King, row.Rank);
            Assert.Equal(LegacyNativeNobilityRank.King, scope.Native.Owner.Player.NobilityRank);
            Assert.Same(separate, scope.Native.Owner.Player.Nobility);
            Assert.Equal(-1, separate.Position); Assert.Equal(0UL, separate.Donation);
            Assert.Equal(2, scope.Native.Queue.Count);
            Assert.Equal(Icon(17, 300_000_000, 12, 0), scope.Native.Queue.ElementAt(0));
            byte[] chat = scope.Native.Queue.ElementAt(1);
            AssertPromotion(chat, "Promoted Wolf", "King");
            Assert.Equal(chat, Assert.Single(observerQueue)); Assert.NotSame(chat, observerQueue.Peek());
            Assert.Equal(0, scope.Packet.Position); Assert.Empty(scope.Recycled);
        });

    [Fact]
    public Task PositionChangeWithinTheSameRankUpdatesIconWithoutPromotionChat() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            scope.Pool[17] = scope.Native.Owner;
            var row = Row(position: 1, name: null);
            ranking.CreateRank(row);
            Assert.Equal(0, row.Position);
            Assert.Equal(Icon(17, 300_000_000, 12, 0), Assert.Single(scope.Native.Queue));
            Assert.Equal(LegacyNativeNobilityRank.King, scope.Native.Owner.Player.NobilityRank);
        });

    [Fact]
    public Task DemotionUpdatesIconAndRankWithoutBroadcastingPromotionText() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            ranking.ClientPoll.Add(1, Row(1, 600_000_000, 0));
            ranking.ClientPoll.Add(2, Row(2, 500_000_000, 1));
            ranking.ClientPoll.Add(3, Row(3, 400_000_000, 2));
            var row = Row(position: 0); ranking.ClientPoll.Add(17, row);
            scope.Pool[17] = scope.Native.Owner;
            ranking.CreateRank(row);
            Assert.Equal(3, row.Position); Assert.Equal(LegacyNativeNobilityRank.Prince, row.Rank);
            Assert.Equal(LegacyNativeNobilityRank.Prince, scope.Native.Owner.Player.NobilityRank);
            Assert.Equal(Icon(17, 300_000_000, 9, 3), Assert.Single(scope.Native.Queue));
        });

    [Fact]
    public Task UnchangedPositionSkipsIconAndPromotionEvenWithUnpublishedPoolAndStalePlayerRank() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            var row = Row(position: 0); LegacyNativePoolClients.GamePoll = null;
            ranking.CreateRank(row);
            Assert.Same(row, ranking.ClientPoll[17]); Assert.Equal(0, row.Position);
            Assert.Equal(LegacyNativeNobilityRank.Serf, scope.Native.Owner.Player.NobilityRank);
            Assert.Null(LegacyNativePoolClients.GamePoll); Assert.Empty(scope.Native.Queue);
            Assert.Equal(321, scope.Packet.Size); Assert.Equal(0, scope.Packet.Position);
        });
}
