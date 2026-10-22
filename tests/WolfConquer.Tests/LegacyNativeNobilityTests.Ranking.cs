using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.World.LegacyNativeNobility.NobilityRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityTests
{
    [Theory]
    [InlineData(0UL, false)]
    [InlineData(200_000_000UL, false)]
    [InlineData(200_000_001UL, true)]
    [InlineData(ulong.MaxValue, true)]
    public Task CreateRankAdmissionIsStrictlyAboveHardCoded200MillionNotMutableThresholdFields(ulong donation, bool admitted) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var ranking = new Ranking { KnightDonation = ulong.MaxValue, BaronDonation = ulong.MaxValue, EarlDonation = ulong.MaxValue };
            var row = Row(donation: donation, position: 0);
            ranking.CreateRank(row);
            Assert.Equal(admitted ? 1 : 0, ranking.ClientPoll.Count);
            if (admitted) Assert.Same(row, ranking.ClientPoll[17]);
            Assert.Equal(0, row.Position); // Filtered records are not reset to -1.
            Assert.Equal(ulong.MaxValue, ranking.EarlDonation);
            Assert.Empty(scope.Native.Queue); Assert.Empty(scope.Recycled);
            Assert.Equal(0, scope.Packet.Position);
        });

    [Fact]
    public Task CreateRankKeepsStoredSameKeyRowInsteadOfReplacingItWithTheArgument() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            var stored = Row(donation: 250_000_000, position: 0, name: "Stored");
            var supplied = Row(donation: ulong.MaxValue, position: 77, name: "Supplied");
            ranking.ClientPoll.Add(17, stored);
            ranking.CreateRank(supplied);
            Assert.Same(stored, Assert.Single(ranking.ClientPoll).Value);
            Assert.Equal(250_000_000UL, stored.Donation); Assert.Equal(0, stored.Position);
            Assert.Equal(77, supplied.Position); Assert.Equal(ulong.MaxValue, supplied.Donation);
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task CreateRankRekeysPhysicalAliasesByRowUidAndKeepsStableTieOrder() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            var first = Row(90, 300_000_000, 0, "Z");
            var second = Row(10, 300_000_000, 1, "A");
            var added = Row(17, 250_000_000, 2);
            ranking.ClientPoll.Add(900, first); ranking.ClientPoll.Add(100, second);
            ranking.CreateRank(added);
            Assert.Equal(new uint[] { 90, 10, 17 }, ranking.ClientPoll.Keys);
            Assert.Same(first, ranking.ClientPoll[90]); Assert.Same(second, ranking.ClientPoll[10]);
            Assert.Same(added, ranking.ClientPoll[17]);
            Assert.False(ranking.ClientPoll.ContainsKey(900)); Assert.False(ranking.ClientPoll.ContainsKey(100));
            Assert.Equal(0, first.Position); Assert.Equal(1, second.Position); Assert.Equal(2, added.Position);
            Assert.Equal(250_000_000UL, ranking.KingDonation); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task CreateRankCapsAtFiftyAndSamplesThresholdsAtTwoFourteenAndFortyNineNotRankBoundaries() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            var rows = Enumerable.Range(1, 51).Select(index => Row((uint)index,
                1_000_000_000UL - (ulong)index * 1_000_000UL, index - 1)).ToArray();
            foreach (var row in rows) ranking.ClientPoll.Add(row.UID, row);
            ranking.CreateRank(rows[50]);
            Assert.Equal(50, ranking.ClientPoll.Count);
            Assert.Equal(Enumerable.Range(1, 50).Select(value => (uint)value), ranking.ClientPoll.Keys);
            for (int index = 0; index < 50; index++)
            {
                Assert.Same(rows[index], ranking.ClientPoll[(uint)index + 1]);
                Assert.Equal(index, rows[index].Position);
            }
            Assert.Equal(-1, rows[50].Position); Assert.False(ranking.ClientPoll.ContainsKey(51));
            Assert.Equal(997_000_000UL, ranking.KingDonation);
            Assert.Equal(985_000_000UL, ranking.PrinceDonation);
            Assert.Equal(950_000_000UL, ranking.DukeDonation);
            Assert.Equal(LegacyNativeNobilityRank.Prince, rows[16].Rank);
            Assert.Equal(LegacyNativeNobilityRank.Duke, rows[17].Rank);
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task UnreachedThresholdSlotsRetainStaleValuesAndExcludedRowsRetainTheirPosition() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.PrepareRecycled(2);
            var ranking = new Ranking { KingDonation = 11, PrinceDonation = 22, DukeDonation = 33 };
            var kept = Row(17, 250_000_000, 0); var excluded = Row(18, 200_000_000, 44);
            ranking.ClientPoll.Add(17, kept); ranking.ClientPoll.Add(18, excluded);
            ranking.CreateRank(kept);
            Assert.Same(kept, Assert.Single(ranking.ClientPoll).Value); Assert.Equal(44, excluded.Position);
            Assert.Equal(11UL, ranking.KingDonation); Assert.Equal(22UL, ranking.PrinceDonation); Assert.Equal(33UL, ranking.DukeDonation);
            kept.Donation = 0; ranking.CreateRank(kept);
            Assert.Empty(ranking.ClientPoll); Assert.Equal(0, kept.Position);
            Assert.Equal(11UL, ranking.KingDonation); Assert.Equal(22UL, ranking.PrinceDonation); Assert.Equal(33UL, ranking.DukeDonation);
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task UnderFiftyUpdateAlwaysRebuildsEvenForAZeroDonationArgument() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = new Ranking();
            for (uint uid = 1; uid <= 49; uid++) ranking.ClientPoll.Add(uid, Row(uid, 0, 99));
            var supplied = Row(99, 0, 66);
            ranking.UpdateRank(supplied);
            Assert.Empty(ranking.ClientPoll); Assert.Equal(66, supplied.Position);
            Assert.Empty(scope.Recycled); Assert.Equal(0, scope.Packet.Position);
        });

    [Theory]
    [InlineData(299_999_999UL, false)]
    [InlineData(300_000_000UL, false)]
    [InlineData(300_000_001UL, true)]
    public Task FullRankingUsesStrictLastPhysicalEntryDonationForNewUidAdmission(ulong donation, bool admitted) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = Fifty();
            var previousLast = ranking.ClientPoll[50]; var supplied = Row(99, donation, -1);
            ranking.UpdateRank(supplied);
            Assert.Equal(50, ranking.ClientPoll.Count); Assert.Equal(admitted, ranking.ClientPoll.ContainsKey(99));
            if (admitted)
            {
                Assert.Same(supplied, ranking.ClientPoll[99]); Assert.Equal(0, supplied.Position);
                Assert.False(ranking.ClientPoll.ContainsKey(50)); Assert.Equal(-1, previousLast.Position);
                Assert.Empty(scope.Recycled); Assert.Equal(0, scope.Packet.Position);
            }
            else
            {
                Assert.Equal(-1, supplied.Position); Assert.Same(previousLast, ranking.ClientPoll[50]);
                Assert.Equal(49, previousLast.Position); Assert.Single(scope.Recycled); Assert.Equal(237, scope.Packet.Position);
            }
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task FullRankingDoesNotSearchForActualMinimumBeforeRejectingAnOutsideUid() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = Fifty();
            ranking.ClientPoll[50].Donation = 500_000_000;
            var supplied = Row(99, 400_000_000);
            ranking.UpdateRank(supplied);
            Assert.False(ranking.ClientPoll.ContainsKey(99)); Assert.Equal(50, ranking.ClientPoll.Count);
            Assert.Equal(-1, supplied.Position); Assert.Equal(49, ranking.ClientPoll[50].Position);
            Assert.Single(scope.Recycled); Assert.Equal(237, scope.Packet.Position);
        });

    [Fact]
    public Task ExistingFullRankingKeyStillRebuildsWhenItsDonationNoLongerPassesAdmission() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var ranking = Fifty();
            var stored = ranking.ClientPoll[1]; stored.Donation = 0;
            var supplied = Row(1, 0, 77);
            ranking.UpdateRank(supplied);
            Assert.Equal(49, ranking.ClientPoll.Count); Assert.False(ranking.ClientPoll.ContainsKey(1));
            Assert.Equal(0, stored.Position); Assert.Equal(77, supplied.Position);
            Assert.Equal(0, ranking.ClientPoll[2].Position); Assert.Empty(scope.Recycled);
        });
}
