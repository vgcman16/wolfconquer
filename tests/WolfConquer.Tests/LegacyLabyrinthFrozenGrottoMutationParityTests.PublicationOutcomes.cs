namespace WolfConquer.Tests;

public sealed partial class LegacyLabyrinthFrozenGrottoMutationParityTests
{
    [Theory]
    [InlineData(FrozenGrottoGuardian5NpcId)]
    [InlineData(FrozenFroto4NpcId)]
    public async Task ActualFrozenAuthorizationChangeBeforeActionPreservesFilesAndSendsNoSuccess(uint npcId)
    {
        using var harness = CreateHarness(npcId, npcId == FrozenGrottoGuardian5NpcId ? (byte)2 : (byte)1,
            Items(InventoryItem(100, GrottoKeyItemId)), includeObserver: true);
        var replacement = harness.OriginalSession with { ActiveNpcId = 999 };
        harness.SetBeforeAction(replacement);

        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Same(replacement, harness.CurrentSession);
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.False(harness.Context.TradeRecovery!.IsBlocked);
        Assert.Empty(harness.ClientSent); Assert.Empty(harness.ObserverSent);
    }
}
