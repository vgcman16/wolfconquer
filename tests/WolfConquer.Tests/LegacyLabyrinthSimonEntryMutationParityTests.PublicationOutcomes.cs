namespace WolfConquer.Tests;

public sealed partial class LegacyLabyrinthSimonEntryMutationParityTests
{
    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    public async Task ActualSimonEntryAuthorizationChangeBeforeActionPreservesBothFilesAndSendsNoSuccess(byte option)
    {
        using var harness = CreateHarness(option, 8888,
            Items(InventoryItem(100, MeteorItemId, stackSize: 3, seed: 21)));
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
