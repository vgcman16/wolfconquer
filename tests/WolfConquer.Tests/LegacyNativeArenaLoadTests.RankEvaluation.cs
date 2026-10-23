using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaLoadTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RankingTranslationEvaluatesEverySnapshotKeyOnceInSourceOrderAndKeepsTies(int count)
    {
        var rows = Enumerable.Range(0, count).Select(_ => new object()).ToArray();
        var reads = new List<object>();
        object[] result = LegacyNativeArenaRankingOrder.Descending(rows, row =>
        {
            reads.Add(row);
            return 17;
        });
        Assert.Equal(count, reads.Count);
        Assert.Equal(rows, reads);
        Assert.Equal(rows, result);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RankingTranslationRetainsFrameworkFirstNullKeyFaultEvenForSingleton(int count)
    {
        var rows = Enumerable.Range(0, count).Select(_ => new LegacyNativeArenaUser()).ToArray();
        rows[0].Info = null!;
        int calls = 0;
        Assert.Throws<NullReferenceException>(() => LegacyNativeArenaRankingOrder.Descending(rows, row =>
        {
            calls++;
            return row.Info.CurrentHonor;
        }));
        Assert.Equal(1, calls);
        Assert.Null(rows[0].Info);
    }
}
