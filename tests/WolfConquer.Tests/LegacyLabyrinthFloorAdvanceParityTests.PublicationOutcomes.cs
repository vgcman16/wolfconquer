using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyLabyrinthFloorAdvanceParityTests
{
    [Theory]
    [MemberData(nameof(FloorAdvanceCases))]
    public async Task FloorObserverFailureRetainsThePublishedTokenAndDestinationFilesWithoutOutput(
        uint npcId, uint tokenItemId, long destinationMapId, long destinationX, long destinationY)
    {
        using var harness = CreateHarness(RouteFor(npcId),
            Items(InventoryItem(100, tokenItemId), InventoryItem(300, 720001, stackSize: 7)), includeObserver: true);
        harness.FailObserver = true;

        Assert.True(await harness.InvokeAsync());

        var published = Assert.IsType<WorldSession>(harness.PublishedAtFailure);
        Assert.False(harness.HasCurrentSession); Assert.True(harness.Context.TradeRecovery!.IsBlocked);
        Assert.Null(published.ActiveNpcId);
        Assert.DoesNotContain(published.PlayerItems.InventoryItems, row => row.Uid == 100);
        AssertItemsEqual(published.PlayerItems, harness.ReloadItems());
        var disk = harness.ReloadCharacter();
        Assert.Equal(destinationMapId, disk.MapId); Assert.Equal(destinationX, disk.X); Assert.Equal(destinationY, disk.Y);
        Assert.Empty(harness.ClientSent); Assert.Empty(harness.ObserverSent);
        Assert.Contains("publicationStage=Observer", harness.LogText, StringComparison.Ordinal);
        Assert.Contains("compensationSuppressed=true", harness.LogText, StringComparison.Ordinal);
        Assert.DoesNotContain("rollback=complete", harness.LogText, StringComparison.OrdinalIgnoreCase);
        var items = File.ReadAllBytes(harness.ItemPath); var character = File.ReadAllBytes(harness.CharacterPath);
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));
        Assert.Equal(items, File.ReadAllBytes(harness.ItemPath)); Assert.Equal(character, File.ReadAllBytes(harness.CharacterPath));
        Assert.Empty(harness.ClientSent);
    }

    [Fact]
    public async Task ActualFloorAuthorizationChangeBeforeActionPreservesBothFilesAndSendsNoSuccess()
    {
        var route = RouteFor(1153);
        using var harness = CreateHarness(route, Items(InventoryItem(100, route.TokenItemId)), includeObserver: true);
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
