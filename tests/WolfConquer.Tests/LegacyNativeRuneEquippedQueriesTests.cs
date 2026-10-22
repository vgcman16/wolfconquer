using System.Globalization;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeRuneEquippedQueriesTests
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
    public void ExcludedPositionsSkipNullEquipmentButEligibleItemsAndNullRowsFaultWithoutFiltering()
    {
        using var f = new Fixture();
        f.Client.Equipment = null;
        foreach (ushort position in new ushort[] { 0, 211, 212 })
            f.Storage.Add(new() { Uid = (uint)position + 1, Position = position });
        Assert.Empty(f.Storage.WornObjects);
        f.Storage.Add(new() { Uid = 999, Position = 101 });
        Assert.Throws<NullReferenceException>(() => f.Storage.WornObjects);
        f.Storage.Objects[0] = null!;
        Assert.Throws<NullReferenceException>(() => f.Storage.WornObjects);
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
    public void LookupUsesCurrentRuneOwnerEquipmentPlayerAndPoolReferencesNotTheHoldingClient()
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
        Assert.True(f.Storage.IsEquipped("alternate", out LegacyNativeGameItemRuntime? item));
        Assert.Same(alternate, item);
        Assert.False(f.Storage.IsEquipped("base"));
        Assert.Same(baseRune, f.Storage.Objects[0]);
        LegacyNativePoolItems.ItemsBase = new() { [alternate.ITEM_ID] = new() { Name = "Replacement" } };
        Assert.True(f.Storage.IsEquipped("replacement"));
        Assert.False(f.Storage.IsEquipped("alternate"));
        foreign.Player.Reborn = 0;
        Assert.False(f.Storage.IsEquipped("replacement"));
    }

    [Fact]
    public void MetadataAndJobRequirementPrecedeRuneClassificationAndNameEvaluation()
    {
        using var f = new Fixture();
        f.Ready();
        var ordinary = new LegacyNativeGameItemRuntime { Uid = 1, ITEM_ID = 100000, Position = 101 };
        f.Storage.Add(ordinary);
        Assert.Throws<KeyNotFoundException>(() => f.Storage.IsEquipped("x"));
        LegacyNativePoolItems.ItemsBase![100000] = new() { Name = null, Class = 0 };
        Assert.False(f.Storage.IsEquipped(null!)); // Non-rune gate precedes ToLower on either name.
        LegacyNativePoolItems.ItemsBase[100000].Class = 1000;
        Assert.False(f.Storage.IsEquipped(null!)); // Job rejection precedes classification/name.
        LegacyNativePoolItems.ItemsBase[100000] = null!;
        Assert.Throws<NullReferenceException>(() => f.Storage.IsEquipped("x"));
    }

    [Fact]
    public void MatchingRuneWithNullNameOrQueryFaultsButUnwornRunesDoNotTouchMetadata()
    {
        using var f = new Fixture();
        f.Ready();
        var rune = f.Rune(1, 4_031_107, 101, "XPBooster");
        Assert.Throws<NullReferenceException>(() => f.Storage.IsEquipped(null!));
        LegacyNativePoolItems.ItemsBase![rune.ITEM_ID].Name = null;
        Assert.Throws<NullReferenceException>(() => f.Storage.IsEquipped("XP"));
        rune.Position = 211;
        LegacyNativePoolItems.ItemsBase = null;
        f.Client.Equipment = null;
        Assert.False(f.Storage.IsEquipped(null!));
        rune.Position = 101;
        f.Client.Equipment = new(f.Client);
        Assert.Throws<NullReferenceException>(() => f.Storage.IsEquipped("XP"));
    }

    [Fact]
    public void NativeLookupUsesCurrentCultureLowercasingRatherThanOrdinalIgnoreCase()
    {
        using var f = new Fixture();
        f.Ready();
        f.Rune(1, 4_031_107, 101, "INERT");
        CultureInfo prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(f.Storage.IsEquipped("ınert"));
            Assert.False(f.Storage.IsEquipped("inert"));
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.True(f.Storage.IsEquipped("inert"));
            Assert.False(f.Storage.IsEquipped("ınert"));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void FaultClearsTheOutItemButLeavesTheRefLevelUntouchedAndDoesNotRepairOwners()
    {
        using var f = new Fixture();
        f.Ready();
        var rune = f.Rune(1, 4_031_107, 101, "XPBooster");
        LegacyNativeGameItemRuntime? item = rune;
        byte level = 77;
        f.Client.Equipment = null;
        Assert.Throws<NullReferenceException>(() => f.Storage.IsEquipped("XP", out item));
        Assert.Null(item);
        Assert.Throws<NullReferenceException>(() => f.Storage.IsEquipped("XP", ref level));
        Assert.Equal((byte)77, level);
        Assert.Null(f.Client.Equipment);
        f.Storage.Owner = null;
        Assert.Throws<NullReferenceException>(() => f.Storage.IsEquipped("XP"));
        Assert.Null(f.Storage.Owner);
    }

    [Fact]
    public void QueriesUseTheActualCachedArrayEvenWhenItDiffersFromThePrivateDictionary()
    {
        using var f = new Fixture();
        f.Ready();
        var first = f.Rune(1, 4_031_107, 101, "First");
        var second = f.Rune(2, 4_031_109, 102, "Second");
        f.Storage.Objects[0] = second;
        Assert.True(f.Storage.ContainsUID(first.Uid));
        Assert.False(f.Storage.IsEquipped("First"));
        Assert.True(f.Storage.IsEquipped("Second", out LegacyNativeGameItemRuntime? item));
        Assert.Same(second, item);
        Assert.Equal(2, f.Storage.WornObjects.Length);
        Assert.All(f.Storage.WornObjects, row => Assert.Same(second, row));
    }

    private static void SetLevel(LegacyNativePlayerRuntime player, ushort level) =>
        typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, level);

    private sealed class Fixture : IDisposable
    {
        private readonly LegacyNativeItemTypeRuneMetadata? prior = LegacyNativePoolItems.ItemsBase;
        internal LegacyNativeGameClientRuntime Client { get; } = new();
        internal LegacyNativeRuneItemStorage Storage { get; }

        internal Fixture()
        {
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

        public void Dispose() => LegacyNativePoolItems.ItemsBase = prior;
    }
}
