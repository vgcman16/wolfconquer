using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    public static IEnumerable<object[]> UnresolvedRebornClasses() =>
        new uint[] { 0, uint.MaxValue, 1058 }.Concat(PrestigeTargetClassFixture.ObservedUnmodeled)
            .Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(UnresolvedRebornClasses))]
    public void RebornClassResolutionUnsupportedPriorCannotConsumeTheWaterFallbackBucket(uint previousClass) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(1, firstClass: previousClass);
            var water = new Ranking.Rank(Ranking.Type.WaterTao) { MaxItems = 30 };
            water.AddItem(user.PrestrigeEntry!);
            Ranking.Ranks.Add(Ranking.Type.WaterTao, water);
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            var error = Record.Exception(() => Ranking.CheckReborn(user));

            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [MemberData(nameof(UnresolvedRebornClasses))]
    public void RebornClassResolutionUnsupportedCurrentCannotAuthorizeKnownPriorCleanup(uint currentClass) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(1, firstClass: 13002);
            SetRebornCurrentClass(user, currentClass);
            RebornSelectedOwner(user);
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            var error = Record.Exception(() => Ranking.CheckReborn(user));

            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData(10000u)]
    [InlineData(10001u)]
    [InlineData(10002u)]
    public void RebornClassResolutionPriorWorldOnlySkipsBeforeCurrentClassGateAndRankConsultation(uint previousClass) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(1, firstClass: previousClass);
            SetRebornCurrentClass(user, 0); // Must not be consulted for a World-only prior class.
            var water = new Ranking.Rank(Ranking.Type.WaterTao) { MaxItems = 30 };
            water.AddItem(user.PrestrigeEntry!);
            Ranking.Ranks.Add(Ranking.Type.WaterTao, water);
            var escaped = Ranking.Ranks;
            Ranking.Ranks = null!;
            Ranking.SynRoot = null!;
            var before = new PrestigeRebornGraphImage(escaped);
            var clientBefore = new PrestigeRebornUserImage(user);

            var error = Record.Exception(() => Ranking.CheckReborn(user));

            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
            Assert.Null(error);
        });

    [Theory]
    [InlineData(10000u)]
    [InlineData(10001u)]
    [InlineData(10002u)]
    public void RebornClassResolutionPriorWorldOnlyStillRequiresAnActivePositiveUid(uint previousClass) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(1, uid: 0, firstClass: previousClass);
            Ranking.Ranks = null!;
            Ranking.SynRoot = null!;
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            var error = Record.Exception(() => Ranking.CheckReborn(user));

            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });
}
