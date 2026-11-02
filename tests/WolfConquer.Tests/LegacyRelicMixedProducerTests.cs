using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicMixedProducerTests
{
    [Theory]
    [InlineData(true, 213)] [InlineData(true, 214)] [InlineData(true, 215)]
    [InlineData(false, 213)] [InlineData(false, 214)] [InlineData(false, 215)]
    public Task ActualRuneBagAndRelicProducersShareOneMonotonicRegistryAcrossChooseAndRepeatedStartup(bool runeFirst, int choice) => Run(async f =>
    {
        var initialHighWater = f.HighWater;
        var actualRuntime = f.Runtime; var actualHandler = f.Handler; var actualCatalog = f.Catalog;
        var actualDomain = f.Data.ExpectedDomain;
        uint runeA, generated;
        if (runeFirst)
        {
            runeA = await ReturnRune(f, LegacyRelicMixedProducerData.StoredRuneUidA);
            Assert.Equal(initialHighWater + 1, runeA);
            generated = await ComposeRelics(f);
            Assert.Equal(initialHighWater + 2, generated);
        }
        else
        {
            generated = await ComposeRelics(f);
            Assert.Equal(initialHighWater + 1, generated);
            var held = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { RelicFuseItems = f.Current.PlayerItems.RelicFuseItems });
            runeA = await ReturnRune(f, LegacyRelicMixedProducerData.StoredRuneUidA);
            Assert.Equal(initialHighWater + 2, runeA);
            Assert.Equal(held, PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { RelicFuseItems = f.Current.PlayerItems.RelicFuseItems }));
        }
        var selectedUid = await ChooseRelic(f, choice);
        var runeB = await ReturnRune(f, LegacyRelicMixedProducerData.StoredRuneUidB);
        Assert.Equal(initialHighWater + 3, runeB);
        Assert.Equal(3, new[] { runeA, generated, runeB }.Distinct().Count());
        Assert.Same(actualRuntime, f.Runtime); Assert.Same(actualHandler, f.Handler); Assert.Same(actualCatalog, f.Catalog);
        Assert.Equal(actualDomain, f.Data.ExpectedDomain);
        Assert.Empty(f.Current.PlayerItems.RuneItems); Assert.Empty(f.Current.PlayerItems.RelicFuseItems);
        var expectedIds = new[] { LegacyRelicMixedProducerData.KeptUid, runeA, selectedUid, runeB };
        Assert.Equal(expectedIds.Order(), f.Current.PlayerItems.InventoryItems.Select(row => row.Uid).Order());
        f.AssertSettled(initialHighWater + 3); f.AssertConverged();

        var committedFiles = f.Data.Files();
        var committedItems = PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems);
        await f.CloseAsync(); f.OpenRuntime();
        Assert.True(f.Runtime.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
        f.Data.AssertFiles(committedFiles); f.AssertSettled(initialHighWater + 3);
        await f.CloseAsync(); f.OpenRuntime();
        f.Data.AssertFiles(committedFiles); f.AssertSettled(initialHighWater + 3);
        await f.CloseAsync(); await f.ReopenFrameAsync();
        Assert.Equal(committedItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.Equal(expectedIds.Order(), f.Current.PlayerItems.InventoryItems.Select(row => row.Uid).Order());
        f.Data.AssertFiles(committedFiles); f.AssertConverged(); f.AssertSettled(initialHighWater + 3);

        // Every request has genuinely consumed its original selected state.
        // Cold native reload must not turn that history into a second grant.
        await f.SendAsync(LegacyRelicMixedProducerData.RuneReturnRequest(LegacyRelicMixedProducerData.StoredRuneUidA));
        await f.SendAsync(LegacyRelicMixedProducerData.RuneReturnRequest(LegacyRelicMixedProducerData.StoredRuneUidB));
        await f.SendAsync(new LegacyRelicFusePacket(0, LegacyRelicMixedProducerData.FirstRelicUid,
            LegacyRelicMixedProducerData.SecondRelicUid, 0).ToFrameBytes());
        await f.SendAsync(new LegacyRelicFusePacket(1, selectedUid, 0, 0).ToFrameBytes());
        Assert.Empty(f.Sent); f.Data.AssertFiles(committedFiles); f.AssertSettled(initialHighWater + 3); f.AssertConverged();
    });
}
