using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyAttackSpeedMetadataTests
{
    [Fact]
    public void NoRequiredSelectedRowPinsNullableCatalogIdentityWithoutInventingAMetadataDefault()
    {
        // DATA ONLY: this has no cache pointer, prefix owner or attack authority.
        var empty = new LegacyAttackSpeedMetadata(null, 0, 0, 0);
        Assert.True(empty.IsCurrentPlain(null)); Assert.Equal((ushort)0, empty.Right);
        Assert.False(empty.IsCurrentPlain(OwnedEquipmentBehaviorFixture.Catalog()));
        Assert.Throws<InvalidDataException>(() => new LegacyAttackSpeedMetadata(null, 626003, 0, 0));
    }

    [Fact]
    public void ZeroSelectedRowsDoNotReadUnrelatedOpaqueOrMalformedMetadata()
    {
        // DATA ONLY: identity is the only catalog input when no slot is selected.
        // The read-only list and malformed fields must not become witnesses.
        var fields = new[] { "not-an-id" };
        var row = new ItemType(626003, "UnrelatedMalformed", Array.AsReadOnly(fields));
        var rows = Array.AsReadOnly(new[] { row });
        var catalog = LegacyItemWorldDataFixture.Create(rows, []);
        Assert.Same(rows, catalog.Items);
        var proof = new LegacyAttackSpeedMetadata(catalog, 0, 0, 0);
        Assert.True(proof.IsCurrentPlain(catalog));
        Assert.Equal((ushort)0, proof.Right); Assert.Equal((ushort)0, proof.Left); Assert.Equal((ushort)0, proof.Ring);
        Assert.Equal((ushort)0, proof.RightRange); Assert.Equal((ushort)0, proof.LeftRange);
        fields[0] = "still-malformed";
        Assert.IsType<Dictionary<int, IReadOnlyList<ItemType>>>(catalog.ItemsById).Clear();
        Assert.True(proof.IsCurrentPlain(catalog));
        Assert.False(proof.IsCurrentPlain(LegacyItemWorldDataFixture.Create(rows, [])));
        Assert.Throws<InvalidDataException>(() => new LegacyAttackSpeedMetadata(catalog, 626003, 0, 0));
    }

    // DATA ONLY: metadata witnesses do not issue a query/cache/attack owner.
    [Theory]
    [InlineData("0", 0)]
    [InlineData("51", 51)]
    [InlineData("65535", 65535)]
    public void RequiredRawFrequencyUsesTheSourceUInt16Domain(string text, int expected)
    {
        var catalog = OwnedEquipmentBehaviorFixture.Catalog(MetadataItem(text));
        var proof = new LegacyAttackSpeedMetadata(catalog, 626003, 0, 0);
        Assert.Equal(checked((ushort)expected), proof.Right); Assert.Equal((ushort)0, proof.Left);
        Assert.Equal((ushort)0, proof.Ring); Assert.True(proof.IsCurrentPlain(catalog));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("not-a-number")]
    public void RequiredMalformedRawFrequencyCannotDefaultToZero(string text)
    {
        var catalog = OwnedEquipmentBehaviorFixture.Catalog(MetadataItem(text));
        Assert.Throws<InvalidDataException>(() => new LegacyAttackSpeedMetadata(catalog, 626003, 0, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AmbiguousOrMisidentifiedRawRowsCannotBecomeRequiredMetadata(bool rawIdentityMismatch)
    {
        var row = MetadataItem("51");
        if (rawIdentityMismatch) ((string[])row.Fields)[0] = "626009";
        var rows = rawIdentityMismatch ? new[] { row } : new[] { row, MetadataItem("51") };
        var catalog = OwnedEquipmentBehaviorFixture.Catalog(rows);
        Assert.Throws<InvalidDataException>(() => new LegacyAttackSpeedMetadata(catalog, 626003, 0, 0));
    }

    [Theory]
    [InlineData("frequency")]
    [InlineData("other-field")]
    [InlineData("index")]
    public void CurrentProofPinsUsedCompleteFieldsAndTheActualIndex(string changed)
    {
        var row = MetadataItem("51"); var catalog = OwnedEquipmentBehaviorFixture.Catalog(row);
        var proof = new LegacyAttackSpeedMetadata(catalog, 626003, 0, 0);
        Assert.True(proof.IsCurrentPlain(catalog));
        if (changed == "index")
            Assert.IsType<Dictionary<int, IReadOnlyList<ItemType>>>(catalog.ItemsById).Clear();
        else ((string[])row.Fields)[changed == "frequency" ? 16 : 18] = "52";
        Assert.False(proof.IsCurrentPlain(catalog)); Assert.Equal((ushort)51, proof.Right);
    }

    [Fact]
    public Task GenuineStartupAndActualQueryMintSpeedAndFailedBeginRetiresTheCache()
    {
        var catalog = ActualCatalog();
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 2001, strength: 1000, minHitPoints: 1);
        var items = new PlayerItemSnapshot([], [], [],
            new PlayerItemSnapshotItem[] { Item(1038001, 410339, 4), Item(1038002, 900309, 5), Item(1038003, 150249, 6) }, []);
        return OwnedEquipmentBehaviorFixture.RunAsync(new(character, items), catalog, async f =>
        {
            var current = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
            // Real login already completes its startup stats query. Its
            // earlier private cold0 publication is not observable here.
            Assert.True(LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out var startup));
            Assert.NotNull(startup); Assert.Equal((ushort)28, startup.Right); Assert.Equal((ushort)0, startup.Left); Assert.Equal((ushort)27, startup.Ring);
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, LegacyRuneStoragePacket.Startup().ToFrameBytes());
            Assert.True(LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out var completed));
            Assert.NotNull(completed); Assert.NotSame(startup, completed);
            Assert.Equal((ushort)28, completed.Right); Assert.Equal((ushort)0, completed.Left); Assert.Equal((ushort)27, completed.Ring);
            Assert.True(completed.IsCurrentPlain(binding)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var row = Assert.Single(catalog.ItemsById[410339]); var fields = Assert.IsType<string[]>(row.Fields);
            var original = fields[16];
            try
            {
                // Declared negative metadata edit, after a real successful
                // Frame query. The actual owner-gated Begin must retire it
                // before its required metadata capture can throw.
                fields[16] = "65536";
                Assert.False(completed.IsCurrentPlain(binding));
                Assert.Throws<InvalidDataException>(() => LegacyNativeMagicQueryClient.TryBeginQuery(f.Store,
                    f.Connection(OwnedEquipmentBehaviorFixture.Actor), current.Character.Uid, binding.NativeIdentity, out _));
                Assert.False(LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out _));
            }
            finally { fields[16] = original; }
            Assert.False(LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out _));
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, LegacyRuneStoragePacket.Startup().ToFrameBytes());
            Assert.True(LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out var rebuilt));
            Assert.NotNull(rebuilt); Assert.NotSame(completed, rebuilt); Assert.Equal((ushort)28, rebuilt.Right);
            Assert.Equal((ushort)27, rebuilt.Ring); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        });
    }

    private static ItemType MetadataItem(string frequency) => OwnedEquipmentBehaviorFixture.Item(626003,
        fields: new Dictionary<int, string> { [16] = frequency });

    private static PlayerItemSnapshotItem Item(uint uid, uint type, ushort position) => new()
    { Uid = uid, ItemId = type, Position = position, Durability = 10, MaximumDurability = 10, StackSize = 0, Color = 5 };

    private static WorldDataCatalog ActualCatalog()
    {
        // Exact retained DBServer/itemtype.txt rows: source frequencies28/0/27.
        string[] rows = [
            "410339@@BuriedBlade@@0@@10@@130@@0@@36@@0@@0@@0@@0@@100000@@0@@3165@@2476@@0@@28@@0@@0@@0@@7099@@7099@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Blade@@None@@5@@0@@0@@0@@1000@@0@@0@@0@@0@@0@@0@@",
            "900309@@CelestialShield@@2001@@0@@140@@0@@0@@0@@0@@0@@0@@113020@@0@@458@@458@@1252@@0@@0@@5709@@0@@3798@@3798@@0@@0@@0@@0@@0@@0@@0@@0@@29@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Shield@@None@@5@@0@@0@@0@@1090@@0@@0@@0@@0@@0@@0@@",
            "150249@@Thunder@@0@@0@@126@@0@@0@@0@@0@@0@@0@@46480@@0@@1646@@1098@@0@@27@@0@@0@@0@@7099@@7099@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Ring@@None@@5@@0@@0@@0@@4000@@0@@0@@0@@0@@0@@0@@",
        ];
        return OwnedEquipmentBehaviorFixture.Catalog(rows.Select(row => ItemTypeParser.ParseLine(row)).ToArray());
    }
}
