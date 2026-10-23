using System.Globalization;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeRuneEquippedQueriesTests
{
    [Fact]
    public void WornObjectsReturnsFreshArraysOfTheSameItemsAndKeepsTheNativeAlternateShadowRules()
    {
        using var f = new Fixture();
        ushort[] positions = [0, 211, 212, 101, 121, 102, 122, 120, 119, 139, 140];
        foreach (ushort position in positions)
            Assert.True(f.Storage.Add(new() { Uid = (uint)position + 1, Position = position }));
        var normal = f.Storage.WornObjects;
        Assert.Equal(new ushort[] { 101, 102, 120, 119 }, normal.Select(item => item.Position));
        Assert.NotSame(normal, f.Storage.WornObjects);
        Assert.Same(f.Storage.Objects[3], normal[0]);
        f.Client.Equipment!.Alternante = true;
        Assert.Equal(new ushort[] { 121, 122, 120, 139, 140 }, f.Storage.WornObjects.Select(item => item.Position));
        f.Storage.Objects[4].Position = 211; // Remove only the121 shadow, without changing cached array identity.
        Assert.Same(normal[0], f.Storage.WornObjects[0]);
        Assert.Equal((ushort)101, f.Storage.WornObjects[0].Position);
    }

    [Fact]
    public void MissingEquipmentOrNullRowsRefuseWornQueriesWithoutRepairingStorage()
    {
        using var f = new Fixture();
        f.Client.Equipment = null;
        foreach (ushort position in new ushort[] { 0, 211, 212 })
            f.Storage.Add(new() { Uid = (uint)position + 1, Position = position });
        Assert.Empty(f.Storage.WornObjects);
        f.Storage.Add(new() { Uid = 999, Position = 101 });
        Assert.Empty(f.Storage.WornObjects);
        f.Client.Equipment = new(f.Client);
        var cache = (LegacyNativeGameItemRuntime[])ReviewCache.GetValue(f.Storage)!;
        cache[0] = null!;
        Assert.Empty(f.Storage.WornObjects);
        Assert.Same(cache, ReviewCache.GetValue(f.Storage));
        Assert.Null(cache[0]);
    }

    [Theory]
    [InlineData((byte)0, (ushort)140)]
    [InlineData((byte)1, (ushort)140)]
    [InlineData((byte)2, (ushort)14)]
    public void RebirthAndLevelGatesClearOutAndRefResultsBeforeTouchingMissingEquipmentOrMetadata(byte reborn, ushort level)
    {
        using var f = new Fixture();
        f.Ready(reborn, level);
        f.Storage.Add(new() { Uid = 1, Position = 101 });
        f.Client.Equipment = null;
        LegacyNativePoolItems.ItemsBase = null;
        Assert.False(f.Storage.IsEquipped(null!, out LegacyNativeGameItemRuntime? item));
        Assert.Null(item);
        byte value = 255;
        Assert.False(f.Storage.IsEquipped(null!, ref value));
        Assert.Equal((byte)0, value);
        Assert.False(f.Storage.IsEquipped(null!));
    }

    [Fact]
    public void SuccessfulLookupsReturnTheFirstActualItemAndRefLevelComesFromItsMutableId()
    {
        using var f = new Fixture();
        f.Ready();
        var first = f.Rune(1, 4_031_107, 101, "XPBooster");
        f.Rune(2, 4_031_109, 102, "XPBooster");
        Assert.True(f.Storage.IsEquipped("xpBOOST", out LegacyNativeGameItemRuntime? item));
        Assert.Same(first, item);
        byte level = 99;
        Assert.True(f.Storage.IsEquipped("XPBooster", ref level));
        Assert.Equal((byte)7, level);
        first.ITEM_ID = 4_031_109;
        Assert.True(f.Storage.IsEquipped("XPBooster", ref level));
        Assert.Equal((byte)9, level);
        Assert.True(f.Storage.IsEquipped(""));
        Assert.False(f.Storage.IsEquipped("absent", ref level));
        Assert.Equal((byte)0, level);
    }

    [Fact]
    public void ForeignRuneOwnerCannotTransferEffectsAndRestoredOwnerUsesCurrentEquipmentAndMetadata()
    {
        using var f = new Fixture();
        f.Ready();
        var baseRune = f.Rune(1, 4_031_107, 101, "Base");
        var alternate = f.Rune(2, 4_031_109, 121, "Alternate");
        Assert.True(f.Storage.IsEquipped("base"));
        var foreign = new LegacyNativeGameClientRuntime();
        foreign.Equipment = new(foreign) { Alternante = true };
        foreign.Player.Reborn = 2;
        SetLevel(foreign.Player, 15);
        f.Storage.Owner = foreign;
        Assert.False(f.Storage.IsEquipped("alternate", out LegacyNativeGameItemRuntime? item));
        Assert.Null(item);
        Assert.False(f.Storage.IsEquipped("base"));
        Assert.Same(baseRune, f.Storage.Objects[0]);
        Assert.Same(foreign, f.Storage.Owner);
        f.Storage.Owner = f.Client;
        f.Client.Equipment!.Alternante = true;
        Assert.True(f.Storage.IsEquipped("alternate", out item));
        Assert.Same(alternate, item);
        LegacyNativePoolItems.ItemsBase = new() { [alternate.ITEM_ID] = new() { ID = alternate.ITEM_ID, Name = "Replacement" } };
        Assert.True(f.Storage.IsEquipped("replacement"));
        Assert.False(f.Storage.IsEquipped("alternate"));
        f.Client.Player.Reborn = 0;
        Assert.False(f.Storage.IsEquipped("replacement"));
    }

    [Fact]
    public void OrdinaryItemsNeverRequireRuneMetadataAndValidRunesKeepRealJobRequirements()
    {
        using var f = new Fixture();
        f.Ready();
        var ordinary = new LegacyNativeGameItemRuntime { Uid = 1, ITEM_ID = 100000, Position = 101 };
        f.Storage.Add(ordinary);
        Assert.False(f.Storage.IsEquipped("x"));
        LegacyNativePoolItems.ItemsBase![100000] = new() { Name = null, Class = 0 };
        Assert.False(f.Storage.IsEquipped(null!)); // Non-rune gate precedes ToLower on either name.
        LegacyNativePoolItems.ItemsBase[100000].Class = 1000;
        Assert.False(f.Storage.IsEquipped(null!)); // Job rejection precedes classification/name.
        LegacyNativePoolItems.ItemsBase[100000] = null!;
        Assert.False(f.Storage.IsEquipped("x"));
        var rune = f.Rune(2, 4_031_107, 102, "XPBooster");
        LegacyNativePoolItems.ItemsBase[rune.ITEM_ID].Class = 1000;
        Assert.False(f.Storage.IsEquipped("XP"));
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(f.Client.Player, 1000u);
        Assert.True(f.Storage.IsEquipped("XP", out var result));
        Assert.Same(rune, result);
    }

    [Fact]
    public void MissingRuneNameQueryOrCatalogRefusesWithoutThrowingOrRepairingState()
    {
        using var f = new Fixture();
        f.Ready();
        var rune = f.Rune(1, 4_031_107, 101, "XPBooster");
        Assert.False(f.Storage.IsEquipped(null!));
        LegacyNativePoolItems.ItemsBase![rune.ITEM_ID].Name = null;
        Assert.False(f.Storage.IsEquipped("XP"));
        rune.Position = 211;
        LegacyNativePoolItems.ItemsBase = null;
        f.Client.Equipment = null;
        Assert.False(f.Storage.IsEquipped(null!));
        rune.Position = 101;
        f.Client.Equipment = new(f.Client);
        Assert.False(f.Storage.IsEquipped("XP", out var item));
        Assert.Null(item);
        Assert.Null(LegacyNativePoolItems.ItemsBase);
        Assert.Same(rune, f.Storage.Objects[0]);
    }

    [Fact]
    public void RuneKeywordMatchingIsOrdinalIgnoreCaseInEveryHostCulture()
    {
        using var f = new Fixture();
        f.Ready();
        f.Rune(1, 4_031_107, 101, "INERT");
        CultureInfo prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.False(f.Storage.IsEquipped("ınert"));
            Assert.True(f.Storage.IsEquipped("inert"));
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.True(f.Storage.IsEquipped("inert"));
            Assert.False(f.Storage.IsEquipped("ınert"));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void MissingEquipmentRefusesLookupAndClearsResultsWithoutRepairingOwners()
    {
        using var f = new Fixture();
        f.Ready();
        var rune = f.Rune(1, 4_031_107, 101, "XPBooster");
        LegacyNativeGameItemRuntime? item = rune;
        byte level = 77;
        f.Client.Equipment = null;
        Assert.False(f.Storage.IsEquipped("XP", out item));
        Assert.Null(item);
        Assert.False(f.Storage.IsEquipped("XP", ref level));
        Assert.Equal((byte)0, level);
        Assert.Null(f.Client.Equipment);
        f.Storage.Owner = null;
        Assert.Empty(f.Storage.WornObjects);
        Assert.Null(f.Storage.Owner);
    }

    [Fact]
    public void CorruptCachedAliasCannotAuthorizeWornRunesOrDuplicateEffects()
    {
        using var f = new Fixture();
        f.Ready();
        var first = f.Rune(1, 4_031_107, 101, "First");
        var second = f.Rune(2, 4_031_109, 102, "Second");
        var cache = (LegacyNativeGameItemRuntime[])ReviewCache.GetValue(f.Storage)!;
        cache[0] = second;
        Assert.Throws<InvalidDataException>(() => f.Storage.ContainsUID(first.Uid));
        Assert.False(f.Storage.IsEquipped("First"));
        Assert.False(f.Storage.IsEquipped("Second", out LegacyNativeGameItemRuntime? item));
        Assert.Null(item);
        Assert.Empty(f.Storage.WornObjects);
        Assert.Same(cache, ReviewCache.GetValue(f.Storage));
        Assert.Same(second, cache[0]);
        Assert.Same(second, cache[1]);
    }

    private static void SetLevel(LegacyNativePlayerRuntime player, ushort level) =>
        typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, level);

    private sealed class Fixture : IDisposable
    {
        private readonly LegacyNativeItemTypeRuneMetadata? prior = LegacyNativePoolItems.ItemsBase;
        private readonly LegacyNativeSafeRandom priorRandom = LegacyNativePoolRandom.GetRandom;
        internal LegacyNativeGameClientRuntime Client { get; }
        internal LegacyNativeRuneItemStorage Storage { get; }

        internal Fixture()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            Client = new();
            LegacyNativePoolItems.ItemsBase = new();
            Client.Equipment = new(Client);
            Storage = new(Client);
            Client.Rune = Storage;
        }

        internal void Ready(byte reborn = 2, ushort level = 15)
        {
            Client.Player.Reborn = reborn;
            SetLevel(Client.Player, level);
        }

        internal LegacyNativeGameItemRuntime Rune(uint uid, uint id, ushort position, string name)
        {
            LegacyNativePoolItems.ItemsBase![id] = new() { ID = id, RuneSpellID = id, Name = name };
            var item = new LegacyNativeGameItemRuntime { Uid = uid, ITEM_ID = id, Position = position };
            Assert.True(Storage.Add(item));
            return item;
        }

        public void Dispose()
        {
            LegacyNativePoolItems.ItemsBase = prior;
            LegacyNativePoolRandom.GetRandom = priorRandom;
        }
    }
}
