using WolfConquer.Core.World;
using Nobility = WolfConquer.Core.World.LegacyNativeNobility;
using Rank = WolfConquer.Core.World.LegacyNativeNobilityRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityPersistenceTests
{
    [Theory]
    [InlineData((byte)Rank.King, 0, (byte)Rank.King)]
    [InlineData((byte)Rank.Prince, 6, (byte)Rank.Prince)]
    [InlineData((byte)Rank.Duke, 15, (byte)Rank.Prince)]
    public Task PaidUsesNativePositionQuirksAndSecondPaymentCapturesAlreadyZeroDonation(byte requested, int position, byte actual) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var nobility = scope.OwnNobility();
        scope.Ranking.ClientPoll.Add(scope.Player.UID, nobility);
        var days = new LegacyNativeDaysNobility(scope.Player);
        var before = DateTime.Now;
        days.Paid(2, (Rank)requested);
        var after = DateTime.Now;
        PeriodBetween(days, before, after, 2);
        Assert.Equal((Rank)requested, days.PaidRank); Assert.True(days.IsActive);
        Assert.Equal(400000001ul, days.LastNobilityDonation);
        Assert.Equal(0ul, nobility.Donation); Assert.Equal(position, nobility.Position);
        Assert.Equal((Rank)actual, scope.Player.NobilityRank);
        Assert.Empty(scope.Ranking.ClientPoll);
        before = DateTime.Now;
        days.Paid(0, (Rank)requested);
        after = DateTime.Now;
        PeriodBetween(days, before, after, 0);
        Assert.Equal(0ul, days.LastNobilityDonation);
        Assert.True(days.IsActive); Assert.Equal(position, nobility.Position);
        Assert.Equal(12, scope.Available); Assert.Empty(scope.Native.Queue); // Paid itself sends no icon.
    });

    [Theory]
    [InlineData((byte)Rank.Serf)]
    [InlineData((byte)Rank.Knight)]
    [InlineData((byte)255)]
    public Task UnsupportedPaidRankStillRemovesRankAndPublishesDateDonationBeforeDeactivating(byte requested) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var nobility = scope.OwnNobility();
        scope.Player.NobilityRank = Rank.Earl;
        scope.Ranking.ClientPoll.Add(scope.Player.UID, nobility);
        var days = new LegacyNativeDaysNobility(scope.Player);
        days.SetDataAnyWay(scope.Player.UID, 99, (byte)Rank.King, 1, true);
        var before = DateTime.Now;
        days.Paid(-1, (Rank)requested);
        var after = DateTime.Now;
        PeriodBetween(days, before, after, -1);
        Assert.Equal((Rank)requested, days.PaidRank); Assert.False(days.IsActive);
        Assert.Equal(400000001ul, days.LastNobilityDonation);
        Assert.Equal(400000001ul, nobility.Donation); Assert.Equal(20, nobility.Position);
        Assert.Equal(Rank.Earl, scope.Player.NobilityRank); Assert.Empty(scope.Ranking.ClientPoll);
        Assert.Equal(12, scope.Available); Assert.Empty(scope.Native.Queue);
    });

    [Fact]
    public Task PaidRemovesCapturedIdentifierNotCurrentPlayerUidAndRetainsActualAliasInRank() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var days = new LegacyNativeDaysNobility(scope.Player); // Captures17.
        var removed = new Nobility(17, "old-identifier", 500000001, 1002, 1) { Position = 0 };
        scope.Player.UID = 999;
        var owned = scope.OwnNobility(position: 1);
        scope.Ranking.ClientPoll.Add(17, removed);
        scope.Ranking.ClientPoll.Add(999, owned);
        days.Paid(0, Rank.King);
        Assert.Equal(17u, days.Identifier); Assert.Equal(999u, scope.Player.UID);
        Assert.False(scope.Ranking.ClientPoll.ContainsKey(17));
        Assert.Same(owned, Assert.Single(scope.Ranking.ClientPoll).Value);
        Assert.Equal(0ul, scope.Ranking.ClientPoll[999].Donation); // Same mutable owner survives removal/re-ranking.
        Assert.Equal(400000001ul, days.LastNobilityDonation);
        Assert.Equal(0, owned.Position); Assert.Equal(Rank.King, scope.Player.NobilityRank);
        Assert.Equal(11, scope.Available); Assert.Empty(scope.Native.Queue);
    });
}
