using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyEquipmentDurabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualAdmittedFrameRefusesMissingOrDuplicateRequiredCatalogLookup(bool duplicate) => Run(async f =>
    {
        // Modify only the actual owned catalog's concrete production index after
        // genuine admission. No replacement Store, PIN, owner, saver or callback.
        var index = Assert.IsType<Dictionary<int, IReadOnlyList<ItemType>>>(f.Catalog.ItemsById);
        var key = checked((int)EquipmentFixture.Necklace);
        var original = index[key];
        var row = Assert.Single(original);
        if (duplicate) index[key] = new[] { row, row with { Name = "DuplicateOwnedNecklace" } };
        else Assert.True(index.Remove(key));
        try { await AssertCatalogRefusal(f); }
        finally { index[key] = original; }
    });

    [Theory]
    [InlineData(ItemType.RequiredClassFieldIndex)]
    [InlineData(ItemType.RequiredLevelFieldIndex)]
    [InlineData(ItemType.RequiredGenderFieldIndex)]
    [InlineData(ItemType.RequiredStrengthFieldIndex)]
    [InlineData(ItemType.RequiredAgilityFieldIndex)]
    public Task ActualAdmittedFrameRefusesMalformedRequiredFieldEvenWhenOldGetterWouldDefaultToZero(int field) => Run(async f =>
    {
        var row = Assert.Single(f.Catalog.ItemsById[checked((int)EquipmentFixture.Necklace)]);
        var fields = Assert.IsType<string[]>(row.Fields);
        var original = fields[field];
        fields[field] = "not-an-unsigned-number";
        try { await AssertCatalogRefusal(f); }
        finally { fields[field] = original; }
    });

    [Fact]
    public Task ActualAdmittedFrameRefusesTruncatedRequiredMetadataWithoutMovingTheItem() => Run(async f =>
    {
        var index = Assert.IsType<Dictionary<int, IReadOnlyList<ItemType>>>(f.Catalog.ItemsById);
        var key = checked((int)EquipmentFixture.Necklace);
        var original = index[key];
        var row = Assert.Single(original);
        index[key] = new[] { row with { Fields = row.Fields.Take(ItemType.RequiredAgilityFieldIndex).ToArray() } };
        try { await AssertCatalogRefusal(f); }
        finally { index[key] = original; }
    });

    private static async Task AssertCatalogRefusal(EquipmentFixture f)
    {
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection, f.Current));
        Assert.True(f.Recovery.IsReadyFor(f.Data.Users, f.Data.Items));
        Assert.NotNull(f.Native.OrdinarySpellBinding);
        var current = f.Current;
        var files = f.Data.Files();
        var native = CaptureNative(f);
        await f.Dispatch();
        Assert.Same(current, f.Current);
        Assert.Empty(f.Sent);
        AssertNativeUnchanged(f, native);
        f.Data.AssertFiles(files);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.False(f.Recovery.IsBlocked);
        Assert.Contains("equipmentMoveCatalogUnavailableOrInvalid", f.Log.ToString(), StringComparison.Ordinal);
    }
}
