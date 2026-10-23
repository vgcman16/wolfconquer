using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativePerfectionPositionTests
{
    [Theory]
    [InlineData(0u, 0)] [InlineData(111003u, 1)] [InlineData(120001u, 2)]
    [InlineData(130003u, 3)] [InlineData(410003u, 4)] [InlineData(900000u, 5)]
    [InlineData(204003u, 5)] [InlineData(150000u, 6)] [InlineData(203003u, 7)]
    [InlineData(160013u, 8)] [InlineData(300000u, 9)] [InlineData(201003u, 10)]
    [InlineData(202003u, 11)] [InlineData(2100005u, -1)] [InlineData(134155u, -1)]
    [InlineData(4100000u, -1)] [InlineData(200000u, -1)] [InlineData(350001u, -1)]
    [InlineData(370001u, -1)]
    public void WholeGetterUsesNativeClassifierAndPreservesZeroAndShieldBranchOrder(uint id, int expected)
    {
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = id, Position = ushort.MaxValue };
        Assert.Equal(expected, item.GetPerfectionPosition);
        Assert.Equal(ushort.MaxValue, item.Position);
        Assert.Equal(id, item.ITEM_ID);
    }
}
