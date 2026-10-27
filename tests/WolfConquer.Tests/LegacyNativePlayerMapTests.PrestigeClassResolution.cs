using System.Runtime.ExceptionServices;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerMapTests
{
    public static IEnumerable<object[]> PrestigeBadgeUnresolvedClasses()
    {
        foreach (var id in new uint[] { 0, uint.MaxValue, 11002, 12002, 15002, 19000 }
                     .Concat(PrestigeTargetClassFixture.WorldOnly))
        foreach (var effect in new[] { false, true }) yield return [id, effect];
    }

    [Theory]
    [MemberData(nameof(PrestigeBadgeUnresolvedClasses))]
    public Task PrestigeClassResolutionUnknownAndWorldOnlySpawnBadgesDoNotUseARealWaterRank(uint characterClass, bool effect) =>
        NativePacketTestThread.Run(() => RunPrestigeBadgeScope(scope =>
        {
            Set(scope.Player, "_class", characterClass);
            scope.Player.SpawinEffect = effect;
            var water = Ranking.Ranks[Ranking.Type.WaterTao];
            var row = new Ranking.Entry { UID = scope.Player.UID };
            water.Items.Add(row.UID, row);
            var items = water.Items;

            scope.Player.GetArray(scope.Packet, false, false);

            Assert.Equal((byte)0, Read(scope.Packet).PrestigeRank);
            Assert.Same(items, water.Items);
            Assert.Same(row, Assert.Single(water.Items).Value);
            Assert.Empty(scope.Native.Queue);
        }));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PrestigeClassResolutionUnsupportedSpawnNeedsNoRankRegistryOrGate(bool effect) =>
        NativePacketTestThread.Run(() => RunPrestigeBadgeScope(scope =>
        {
            Set(scope.Player, "_class", 11002u);
            scope.Player.SpawinEffect = effect;
            Ranking.Ranks = null!;
            Ranking.SynRoot = null!;

            scope.Player.GetArray(scope.Packet, false, false);

            Assert.Equal((byte)0, Read(scope.Packet).PrestigeRank);
            Assert.Null(Ranking.Ranks);
            Assert.Null(Ranking.SynRoot);
            Assert.Empty(scope.Native.Queue);
        }));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PrestigeClassResolutionSuffix52UsesItsActualTrojanBadgeInBothSpawnBranches(bool effect) =>
        NativePacketTestThread.Run(() => RunPrestigeBadgeScope(scope =>
        {
            Set(scope.Player, "_class", 1052u);
            scope.Player.SpawinEffect = effect;
            Ranking.Ranks[Ranking.Type.Trojan].Items.Add(901, new() { UID = 901 });
            Ranking.Ranks[Ranking.Type.Trojan].Items.Add(scope.Player.UID, new() { UID = scope.Player.UID });

            scope.Player.GetArray(scope.Packet, false, false);

            Assert.Equal((byte)2, Read(scope.Packet).PrestigeRank);
            Assert.Empty(scope.Native.Queue);
        }));

    private static void RunPrestigeBadgeScope(Action<Scope> action)
    {
        Scope? scope = null;
        var errors = new List<Exception>();
        try { scope = new(); action(scope); } catch (Exception error) { errors.Add(error); }
        try { scope?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Prestige badge assertion and real fixture restore failed.", errors);
    }
}
