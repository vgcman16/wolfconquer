using System.Buffers.Binary;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.World.LegacyNativeNobility.NobilityRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityTests
{
    [Fact]
    public Task NullArgumentStillTakesAndDisposesTheActualRecycledPacketBeforeAnyDictionaryMutation() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking(); var row = Row(position: 0);
            ranking.ClientPoll.Add(17, row);
            Assert.Throws<NullReferenceException>(() => ranking.CreateRank(null!));
            Assert.Same(row, Assert.Single(ranking.ClientPoll).Value); Assert.Equal(0, row.Position);
            Assert.Empty(scope.Recycled); Assert.Equal(0, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task NullStoredRowFaultKeepsNewArgumentInsertionAndOriginalDictionaryBeforeClear() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            ranking.ClientPoll.Add(99, null!); var added = Row();
            var physical = ranking.ClientPoll;
            Assert.Throws<NullReferenceException>(() => ranking.CreateRank(added));
            Assert.Same(physical, ranking.ClientPoll); Assert.Equal(2, ranking.ClientPoll.Count);
            Assert.Null(ranking.ClientPoll[99]); Assert.Same(added, ranking.ClientPoll[17]);
            Assert.Equal(-1, added.Position); Assert.Equal(0, scope.Packet.Position);
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task DuplicatePhysicalUidAliasesFaultAfterClearAndSecondPositionMutationKeepingOnlyFirstReinsertedKey() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking(); var aliased = Row(position: 0);
            ranking.ClientPoll.Add(900, aliased); ranking.ClientPoll.Add(901, aliased);
            var physical = ranking.ClientPoll;
            Assert.Throws<ArgumentException>(() => ranking.CreateRank(aliased));
            Assert.Same(physical, ranking.ClientPoll);
            Assert.Equal(17u, Assert.Single(ranking.ClientPoll).Key);
            Assert.Same(aliased, ranking.ClientPoll[17]); Assert.Equal(1, aliased.Position);
            Assert.False(ranking.ClientPoll.ContainsKey(900)); Assert.False(ranking.ClientPoll.ContainsKey(901));
            Assert.Equal(0, scope.Packet.Position); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task UnpublishedPoolFaultRetainsRebuiltPrefixAndThresholdCommitButDoesNotProcessLaterRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking { KingDonation = 1, PrinceDonation = 2, DukeDonation = 3 };
            var first = Row(1, 500_000_000, 0); var second = Row(2, 400_000_000, 1);
            var reached = Row(3, 300_000_000, -1); var later = Row(4, 250_000_000, 99);
            foreach (var row in new[] { first, second, reached, later }) ranking.ClientPoll.Add(row.UID, row);
            LegacyNativePoolClients.GamePoll = null;
            Assert.Throws<NullReferenceException>(() => ranking.CreateRank(first));
            Assert.Equal(new uint[] { 1, 2, 3 }, ranking.ClientPoll.Keys);
            Assert.Equal(2, reached.Position); Assert.Equal(99, later.Position);
            Assert.False(ranking.ClientPoll.ContainsKey(4));
            Assert.Equal(300_000_000UL, ranking.KingDonation);
            Assert.Equal(2UL, ranking.PrinceDonation); Assert.Equal(3UL, ranking.DukeDonation);
            Assert.Equal(0, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
            Assert.Empty(scope.Native.Queue); Assert.Null(LegacyNativePoolClients.GamePoll);
        });

    [Fact]
    public Task PromotionBroadcastFaultOccursAfterIconCopyAndStopsLaterRankingRowsWithoutRollback() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            var reached = Row(name: "Reached"); var later = Row(18, 250_000_000, 77);
            ranking.ClientPoll.Add(17, reached); ranking.ClientPoll.Add(18, later);
            scope.Pool[17] = scope.Native.Owner; scope.Pool[99] = null!;
            Assert.Throws<NullReferenceException>(() => ranking.CreateRank(reached));
            Assert.Same(reached, Assert.Single(ranking.ClientPoll).Value); Assert.Equal(0, reached.Position);
            Assert.Equal(77, later.Position); Assert.Equal(LegacyNativeNobilityRank.King, scope.Native.Owner.Player.NobilityRank);
            Assert.InRange(scope.Native.Queue.Count, 1, 2); // ConcurrentDictionary recipient order is not promised.
            Assert.Equal(Icon(17, 300_000_000, 12, 0), scope.Native.Queue.ElementAt(0));
            foreach (byte[] chat in scope.Native.Queue.Skip(1)) AssertPromotion(chat, "Reached", "King");
            Assert.Equal((ushort)2179, BinaryPrimitives.ReadUInt16LittleEndian(NativeSpellTestFixture.Bytes(scope.Packet).AsSpan(2)));
            Assert.Equal(0, scope.Packet.Position); Assert.Null(scope.Pool[99]);
        });

    [Fact]
    public Task NullDictionaryFaultsBeforeRecycledTakeAndIsNotReplacedByAnyRankingMethod() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking { ClientPoll = null! };
            Assert.Throws<ArgumentNullException>(() => ranking.CreateRank(Row()));
            Assert.Throws<NullReferenceException>(() => ranking.UpdateRank(Row()));
            Assert.Throws<NullReferenceException>(() => ranking.RemoveAndUpdateTheRest(17));
            Assert.Null(ranking.ClientPoll); Assert.Single(scope.Recycled);
            Assert.Equal(237, scope.Packet.Position); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task RemoveUsesPhysicalKeyThenUpdatesRemainingActualRowsFromSnapshotWithoutResettingThresholds() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.PrepareRecycled(2);
            var ranking = new Ranking { KingDonation = 11, PrinceDonation = 22, DukeDonation = 33 };
            var removed = Row(1, 500_000_000, 0); var second = Row(2, 400_000_000, 1); var third = Row(3, 300_000_000, 2);
            ranking.ClientPoll.Add(1, removed); ranking.ClientPoll.Add(2, second); ranking.ClientPoll.Add(3, third);
            ranking.RemoveAndUpdateTheRest(1);
            Assert.Equal(new uint[] { 2, 3 }, ranking.ClientPoll.Keys);
            Assert.Same(second, ranking.ClientPoll[2]); Assert.Same(third, ranking.ClientPoll[3]);
            Assert.Equal(0, second.Position); Assert.Equal(1, third.Position); Assert.Equal(0, removed.Position);
            Assert.Equal(11UL, ranking.KingDonation); Assert.Equal(22UL, ranking.PrinceDonation); Assert.Equal(33UL, ranking.DukeDonation);
            Assert.Empty(scope.Recycled); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task MissingRemovalKeyStillUpdatesAllSnapshotRowsDespiteFirstRebuildChangingTheDictionary() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.PrepareRecycled(2);
            var ranking = new Ranking(); var low = Row(1, 0, 77); var kept = Row(2, 300_000_000, 0);
            ranking.ClientPoll.Add(1, low); ranking.ClientPoll.Add(2, kept);
            ranking.RemoveAndUpdateTheRest(999);
            Assert.Same(kept, Assert.Single(ranking.ClientPoll).Value);
            Assert.Equal(77, low.Position); Assert.Equal(0, kept.Position);
            Assert.Empty(scope.Recycled); Assert.Empty(scope.Native.Queue);
        });
}
