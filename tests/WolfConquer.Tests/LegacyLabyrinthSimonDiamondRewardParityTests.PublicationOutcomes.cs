namespace WolfConquer.Tests;

public sealed partial class LegacyLabyrinthSimonDiamondRewardParityTests
{
    [Fact]
    public async Task ActualSimonRewardAuthorizationChangeBeforeActionPreservesItemsAndSendsNoSuccess()
    {
        using var harness = CreateHarness(13,
            Items(InventoryItem(10, MoonDiamondItemId, stackSize: 17, seed: 15)));
        var replacement = harness.OriginalSession with { ActiveNpcId = 999 };
        harness.SetBeforeAction(replacement);

        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Same(replacement, harness.CurrentSession);
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        Assert.Empty(harness.ClientSent);
    }
}
