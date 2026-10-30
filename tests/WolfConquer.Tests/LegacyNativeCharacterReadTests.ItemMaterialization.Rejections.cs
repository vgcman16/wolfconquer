using System.Reflection;
using WolfConquer.Core.World;
using Materialization = WolfConquer.Core.World.LegacyNativeItemLoadMaterialization;
using Destination = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Destination;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    [Theory]
    [InlineData(64, 0u)]
    [InlineData(99, 0u)]
    [InlineData(136, 0u)]
    [InlineData(216, 0u)]
    [InlineData(218, 0u)]
    [InlineData(4, 100u)]
    public Task UnrepresentableRootLocationRefusesBeforeOwnerOrMetadataPublication(int position, uint warehouse) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativePoolItems.ItemsBase = null;
        var image = MaterializationImage(1, [Row(71, position: (ushort)position, warehouse: warehouse)]);
        Assert.Throws<InvalidDataException>(() => Materialization.RequiredDestinations(image));
        AssertMaterializationDidNotPublish(scope);
    });

    [Theory]
    [InlineData(181305u, 0, 100u)]
    [InlineData(4020001u, 211, 0u)]
    [InlineData(410009u, 213, 0u)]
    [InlineData(410009u, 217, 0u)]
    public Task UnsupportedMainPerfectionContainerRefusesRatherThanDroppingOrInventingHydration(
        uint id, int position, uint warehouse) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativePoolItems.ItemsBase = null;
        var perfection = new LegacyNativeClientItemPerfectionRecord { ItemUID = 71, Level = 1 };
        var image = MaterializationImage(1, [Row(71, id, (ushort)position, warehouse)], perfection);
        Assert.Throws<InvalidDataException>(() => Materialization.RequiredDestinations(image));
        AssertMaterializationDidNotPublish(scope);
    });

    [Fact]
    public Task OnlyFilteredRootWithChildrenStillRefusesWhenNoDestinationOwnerWouldBeCaptured() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var root = Row(71, 205901); root.DepositeCount = 1;
        var image = MaterializationImage(1, [root, Row(72)]);
        Assert.Throws<InvalidDataException>(() => Materialization.RequiredDestinations(image));
        AssertMaterializationDidNotPublish(scope);
    });

    [Theory]
    [InlineData("owner")]
    [InlineData("missing-row")]
    [InlineData("null-row")]
    [InlineData("wrong-id")]
    public Task ActualRuneMetadataMustExistAndMatchItsRealKeyBeforeConstructorUse(string fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Metadata(4020002);
        var image = MaterializationImage(1, [Row(71, 4020002, 102)]);
        if (fault == "owner") LegacyNativePoolItems.ItemsBase = null;
        else if (fault == "missing-row") LegacyNativePoolItems.ItemsBase!.Remove(4020002);
        else if (fault == "null-row") LegacyNativePoolItems.ItemsBase![4020002] = null!;
        else row.ID = 1;
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Rune);
        Assert.Throws<InvalidDataException>(() => Materialization.Prepare(image, scope.Client, scope.Player));
        AssertMaterializationDidNotPublish(scope);
    });

    [Theory]
    [InlineData("owner")]
    [InlineData("row")]
    [InlineData("id")]
    [InlineData("spell")]
    [InlineData("job")]
    [InlineData("guild")]
    [InlineData("player-class")]
    [InlineData("bag-slot")]
    public Task FinalWitnessRejectsChangedActualMetadataAndPlayerEvidenceBeforePublication(string change) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Metadata(4020002); row.RuneSpellID = 4020002;
        var image = MaterializationImage(1, [Row(71, 4020002, 102)]);
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Rune);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player);
        ushort oldSlot = LegacyNativeGameItemRuntime.MythsoulBag;
        try
        {
            if (change == "owner") LegacyNativePoolItems.ItemsBase = new();
            else if (change == "row") LegacyNativePoolItems.ItemsBase![4020002] = new() { ID = 4020002 };
            else if (change == "id") row.ID++;
            else if (change == "spell") row.RuneSpellID++;
            else if (change == "job") row.Class = 2001;
            else if (change == "guild") scope.Player.GuildID++;
            else if (change == "player-class") SetMaterializationClass(scope.Player, 2001);
            else LegacyNativeGameItemRuntime.MythsoulBag++;
            Assert.Throws<InvalidDataException>(() => plan.RequireCurrent()); AssertMaterializationDidNotPublish(scope);
        }
        finally { LegacyNativeGameItemRuntime.MythsoulBag = oldSlot; }
    });

    [Fact]
    public Task UnqueriedMetadataDisplayFieldsDoNotBecomeNewLoadPrerequisites() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Metadata(4020002);
        var image = MaterializationImage(1, [Row(71, 4020002, 102)]);
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Rune);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player);
        row.Name = null; row.Level++; row.Description = "Changed display metadata";
        plan.RequireCurrent(); AssertMaterializationDidNotPublish(scope);
    });

    private static void SetMaterializationClass(LegacyNativePlayerRuntime player, uint value) =>
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, value);

    private sealed class MaterializationComparer : IEqualityComparer<uint>
    {
        internal uint Trigger;
        internal Action? Mutation;
        internal int Calls;
        public bool Equals(uint left, uint right) => left == right;
        public int GetHashCode(uint value)
        {
            if (value == Trigger && Mutation is { } mutation)
            { Mutation = null; Calls++; mutation(); }
            return value.GetHashCode();
        }
    }

    [Theory]
    [InlineData("player-class")]
    [InlineData("guild")]
    [InlineData("earlier-job")]
    [InlineData("earlier-row")]
    public Task FinalActualComparerCallbackCannotInvalidateEarlierChecksAndStillPublish(string mutation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = scope.Metadata(4020001); scope.Metadata(4020002);
        var catalog = LegacyNativePoolItems.ItemsBase!;
        var comparer = new MaterializationComparer { Trigger = 4020002 };
        // Install a real Dictionary comparer on this owned concrete catalog;
        // no metadata adapter or fake accepting lookup replaces the real API.
        typeof(Dictionary<uint, LegacyNativeItemRuneMetadata>).GetField("_comparer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(catalog, comparer);
        var image = MaterializationImage(2, [Row(71, 4020001, 101), Row(72, 4020002, 102)]);
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Rune);
        var plan = Materialization.Prepare(image, scope.Client, scope.Player);
        comparer.Mutation = () =>
        {
            if (mutation == "player-class") SetMaterializationClass(scope.Player, 2001);
            else if (mutation == "guild") scope.Player.GuildID++;
            else if (mutation == "earlier-job") first.Class = 2001;
            else catalog[4020001] = new LegacyNativeItemRuneMetadata { ID = 4020001 };
        };
        Assert.Throws<InvalidDataException>(() => plan.RequireCurrent());
        Assert.Equal(1, comparer.Calls); AssertMaterializationDidNotPublish(scope);
    });

    [Theory]
    [InlineData("effect-date")]
    [InlineData("expiration")]
    [InlineData("purification-overflow")]
    public Task LateActualChildConversionFailurePreservesExistingOwnersAndCorrectedInputCanPrepare(string fault) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var existing = scope.Item(99); scope.Client.Inventory!.ClientItems[99] = existing;
        scope.Client.DemonExterminator!.ItemUID = 999;
        byte[] prior = [0xD3, 0xD1]; scope.Queue.Enqueue(prior);
        var root = Row(71, position: 4); root.DepositeCount = 1;
        var bad = Row(72);
        if (fault == "effect-date") bad.EffectAddedOn = long.MaxValue;
        else if (fault == "expiration") bad.Expiration = long.MaxValue;
        else
        {
            bad.PurificationItemID = 800001; bad.PurificationAddedOn = DateTime.MaxValue.Ticks - 1;
            bad.PurificationDuration = 1;
        }
        byte[] bytes = ItemsFile(1, [root, bad]); byte[] original = bytes.ToArray();
        var image = LegacyNativeItemLoadImage.Parse(bytes);
        using var owners = LegacyNativeItemLoadOwners.Capture(scope.Client, scope.Player, Destination.Equipment);
        Assert.ThrowsAny<ArgumentException>(() => Materialization.Prepare(image, scope.Client, scope.Player));
        Assert.Same(existing, Assert.Single(scope.Client.Inventory.ClientItems).Value);
        Assert.Empty(scope.Client.Equipment!.ClientItems); Assert.Empty(scope.Client.Warehouse!.ClientItems);
        Assert.Equal(999u, scope.Client.DemonExterminator.ItemUID); Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.Equal(original, bytes);
        var corrected = MaterializationImage(1, [root, Row(72)]);
        var plan = Materialization.Prepare(corrected, scope.Client, scope.Player); plan.RequireCurrent();
        Assert.Equal(72u, Assert.Single(plan.Placements).Item.Deposite[72].Uid);
        Assert.Same(existing, Assert.Single(scope.Client.Inventory.ClientItems).Value);
        Assert.Equal(999u, scope.Client.DemonExterminator.ItemUID); Assert.Same(prior, Assert.Single(scope.Queue));
    });
}
