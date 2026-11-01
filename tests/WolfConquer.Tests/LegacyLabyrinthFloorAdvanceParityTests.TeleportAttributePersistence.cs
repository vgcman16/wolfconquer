using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyLabyrinthFloorAdvanceParityTests
{
    [Fact]
    public async Task ActualFloorAdvancePersistsHistoryFromTheRetainedLowercasePreviousScopeKey()
    {
        var route = RouteFor(1153);
        using var harness = CreateHarness(route, Items(InventoryItem(100, route.TokenItemId),
            InventoryItem(300, 720001, stackSize: 7)), includeObserver: true);
        // Same actual authorization, typed character and item/file custody.
        // Only the transient map spelling and opaque DATA are projected through
        // the real Store; no native owner or accepting persistence is supplied.
        var initial = harness.CurrentSession;
        var fields = new Dictionary<string, string>(initial.Character.Attributes, StringComparer.Ordinal)
            { ["pdinamycid"] = "555", ["Other"] = "upper", ["other"] = "lower" };
        Assert.True(fields.Remove("DynamicID")); fields.Add("dynamicid", "77");
        harness.SetBeforeAction(initial with { Character = initial.Character with { Attributes = fields } });
        int writes = harness.SessionWriteCount;

        Assert.True(await harness.InvokeAsync());

        var final = harness.CurrentSession;
        Assert.Equal(writes + 2, harness.SessionWriteCount);
        Assert.Equal((route.DestinationMapId, route.DestinationX, route.DestinationY),
            (final.Character.MapId, final.Character.X, final.Character.Y));
        Assert.Equal("0", final.Character.Attributes["dynamicid"]);
        Assert.Equal("77", final.Character.Attributes["pdinamycid"]);
        Assert.DoesNotContain("DynamicID", final.Character.Attributes.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("PDinamycID", final.Character.Attributes.Keys, StringComparer.Ordinal);
        Assert.Equal("upper", final.Character.Attributes["Other"]); Assert.Equal("lower", final.Character.Attributes["other"]);
        Assert.Same(StringComparer.Ordinal, LegacyNativeAttributeMapProof.CaptureRaw(final.Character.Attributes).CapturedComparer);
        Assert.DoesNotContain(final.PlayerItems.InventoryItems, row => row.Uid == 100);
        AssertItemsEqual(final.PlayerItems, harness.ReloadItems());
        var disk = harness.ReloadCharacter();
        Assert.Equal("0", disk.Attributes["DynamicID"]);
        Assert.Equal("77", disk.Attributes["PDinamycID"]); // Canonical persisted key, actual retained-alias value.
        Assert.Equal("keep-me", disk.Attributes["Sentinel"]);
        Assert.Equal("77", Assert.IsType<WolfConquer.Core.Data.CharacterSnapshot>(harness.CharacterAtCommit).Attributes["PDinamycID"]);
        AssertSourceOrderedSuccessPackets(harness, route, bytes => AssertRemoveInventory(bytes, uid: 100));
        Assert.False(harness.Context.TradeRecovery!.IsBlocked);
    }

    [Fact]
    public async Task ActualFloorAdvanceRefusesAmbiguousHistoryBeforeTokenFilesOrTeleportEffects()
    {
        var route = RouteFor(1153);
        using var harness = CreateHarness(route, Items(InventoryItem(100, route.TokenItemId)), includeObserver: true);
        var original = harness.CurrentSession;
        var fields = new Dictionary<string, string>(original.Character.Attributes, StringComparer.Ordinal)
            { ["PDinamycID"] = "555", ["pdinamycid"] = "555" };
        harness.SetBeforeAction(original with { Character = original.Character with { Attributes = fields } });
        var before = harness.CurrentSession; int writes = harness.SessionWriteCount;
        var items = File.ReadAllBytes(harness.ItemPath); var character = File.ReadAllBytes(harness.CharacterPath);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => harness.InvokeAsync().AsTask());

        Assert.Equal("characterAttributeOwnedFieldAmbiguous", error.Message);
        Assert.Same(before, harness.CurrentSession); Assert.Equal(writes, harness.SessionWriteCount);
        Assert.Equal(items, File.ReadAllBytes(harness.ItemPath)); Assert.Equal(character, File.ReadAllBytes(harness.CharacterPath));
        Assert.Contains(harness.CurrentSession.PlayerItems.InventoryItems, row => row.Uid == 100);
        Assert.Empty(harness.ClientSent); Assert.Empty(harness.ObserverSent); Assert.Empty(harness.Wire);
        Assert.False(harness.Context.TradeRecovery!.IsBlocked);
    }
}
