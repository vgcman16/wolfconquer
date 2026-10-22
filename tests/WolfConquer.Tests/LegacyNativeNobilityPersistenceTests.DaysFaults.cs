using WolfConquer.Core.World;
using Nobility = WolfConquer.Core.World.LegacyNativeNobility;
using Rank = WolfConquer.Core.World.LegacyNativeNobilityRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityPersistenceTests
{
    [Fact]
    public Task OutOfRangeDayCountPublishesPaidRankBeforeDateFailureAndNothingLater() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var nobility = scope.OwnNobility();
        scope.Ranking.ClientPoll.Add(scope.Player.UID, nobility);
        var days = new LegacyNativeDaysNobility(scope.Player);
        var retainedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        days.SetDataAnyWay(scope.Player.UID, 88, (byte)Rank.Earl, retainedTime.ToBinary(), true);
        Assert.Throws<ArgumentOutOfRangeException>(() => days.Paid(int.MaxValue, Rank.King));
        Assert.Equal(Rank.King, days.PaidRank); Assert.Equal(retainedTime.ToBinary(), days.PeriodTime.ToBinary());
        Assert.Equal(88ul, days.LastNobilityDonation); Assert.True(days.IsActive);
        Assert.Same(nobility, Assert.Single(scope.Ranking.ClientPoll).Value);
        Assert.Equal(400000001ul, nobility.Donation); Assert.Equal(20, nobility.Position);
        Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task MissingNobilityFailsAfterRealDatePublicationBeforeDonationAndRankRemoval() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var retained = new Nobility(17, "retained", 400000001, 1002, 1);
        scope.Ranking.ClientPoll.Add(17, retained);
        var days = new LegacyNativeDaysNobility(scope.Player);
        days.SetDataAnyWay(17, 88, (byte)Rank.Earl, 1, true);
        var before = DateTime.Now;
        Assert.Throws<NullReferenceException>(() => days.Paid(0, Rank.Prince));
        var after = DateTime.Now;
        PeriodBetween(days, before, after, 0);
        Assert.Equal(Rank.Prince, days.PaidRank); Assert.Equal(88ul, days.LastNobilityDonation); Assert.True(days.IsActive);
        Assert.Same(retained, Assert.Single(scope.Ranking.ClientPoll).Value);
        Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task MissingRankingFailsAfterDonationCaptureBeforeActiveAndNobilityMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var nobility = scope.OwnNobility();
        var days = new LegacyNativeDaysNobility(scope.Player);
        LegacyNativePoolNobility.NobilityRanking = null!;
        var before = DateTime.Now;
        Assert.Throws<NullReferenceException>(() => days.Paid(0, Rank.Duke));
        var after = DateTime.Now;
        PeriodBetween(days, before, after, 0);
        Assert.Equal(Rank.Duke, days.PaidRank); Assert.Equal(400000001ul, days.LastNobilityDonation); Assert.False(days.IsActive);
        Assert.Equal(400000001ul, nobility.Donation); Assert.Equal(20, nobility.Position); Assert.Equal(Rank.Serf, scope.Player.NobilityRank);
        Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task RealRankingIconFaultKeepsRemovalAndRemainingRankPrefixButDoesNotActivatePaidState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var nobility = scope.OwnNobility();
        var remaining = new Nobility(88, "remaining", 500000001, 1002, 1);
        scope.Ranking.ClientPoll.Add(17, nobility);
        scope.Ranking.ClientPoll.Add(88, remaining);
        var days = new LegacyNativeDaysNobility(scope.Player);
        LegacyNativePoolClients.GamePoll = null;
        var before = DateTime.Now;
        Assert.Throws<NullReferenceException>(() => days.Paid(1, Rank.King));
        var after = DateTime.Now;
        PeriodBetween(days, before, after, 1);
        Assert.Equal(Rank.King, days.PaidRank); Assert.Equal(400000001ul, days.LastNobilityDonation); Assert.False(days.IsActive);
        Assert.False(scope.Ranking.ClientPoll.ContainsKey(17));
        Assert.Same(remaining, Assert.Single(scope.Ranking.ClientPoll).Value); Assert.Equal(0, remaining.Position);
        Assert.Equal(400000001ul, nobility.Donation); Assert.Equal(20, nobility.Position);
        Assert.Equal(11, scope.Available); scope.UnserializedDisposed(0);
        Assert.Empty(scope.Native.Queue);
    });

    [Fact]
    public Task MissingActualPacketBinFaultOccursAfterRemovalButBeforeRemainingRankMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var nobility = scope.OwnNobility();
        var remaining = new Nobility(88, "remaining", 500000001, 1002, 1) { Position = 20 };
        scope.Ranking.ClientPoll.Add(17, nobility);
        scope.Ranking.ClientPoll.Add(88, remaining);
        var days = new LegacyNativeDaysNobility(scope.Player);
        scope.UseNullBin();
        Assert.Throws<NullReferenceException>(() => days.Paid(0, Rank.King));
        Assert.Equal(Rank.King, days.PaidRank); Assert.Equal(400000001ul, days.LastNobilityDonation); Assert.False(days.IsActive);
        Assert.False(scope.Ranking.ClientPoll.ContainsKey(17));
        Assert.Same(remaining, Assert.Single(scope.Ranking.ClientPoll).Value); Assert.Equal(20, remaining.Position);
        Assert.Equal(400000001ul, nobility.Donation); Assert.Equal(20, nobility.Position);
        Assert.Empty(scope.Native.Queue);
    });
}
