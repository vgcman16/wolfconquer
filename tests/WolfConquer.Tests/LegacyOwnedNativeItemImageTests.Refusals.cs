using System.Collections;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Image = WolfConquer.Core.World.LegacyOwnedNativeItemImage;
using Item = WolfConquer.Core.Data.PlayerItemSnapshotItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedNativeItemImageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualNullCatalogStillRefusesRequiredRuneMetadataInRootsAndDeposits(bool deposited)
    {
        var rune = Row(72, 4020002, deposited ? (ushort)0 : (ushort)102);
        var source = deposited ? Snapshot(new[] { Row(71, children: new[] { rune }) }) : Snapshot(runes: new[] { rune });
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(source);
        var global = LegacyNativePoolItems.ItemsBase; var counter = LegacyNativePoolItems.ItemCounter;
        Image.Preparation? returned = null;
        Assert.Throws<InvalidDataException>(() => returned = Image.Prepare(null, source, Profile));
        Assert.Null(returned); Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(source));
        Assert.Same(global, LegacyNativePoolItems.ItemsBase); Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
    }

    [Fact]
    public void MetadataOnlyPreflightRetainsActualNullAndRequiresOnlyRealRuneIds()
    {
        var empty = Image.ValidateItemIds(null, Array.Empty<uint>(), Profile);
        Assert.Null(empty.Catalog); empty.RequireCurrent(); Assert.True(empty.IsCurrentPlain());
        var nonRune = Image.ValidateItemIds(null, new uint[] { 410009, 205901 }, Profile);
        Assert.Null(nonRune.Catalog); nonRune.RequireCurrent(); Assert.True(nonRune.IsCurrentPlain());
        Assert.Throws<InvalidDataException>(() => Image.ValidateItemIds(null, new uint[] { 410009, 4020002 }, Profile));
        Assert.Throws<InvalidDataException>(() => Image.ValidateItemIds(null, new uint[] { 0 }, Profile));
        Assert.Throws<ArgumentOutOfRangeException>(() => Image.ValidateItemIds(null, Array.Empty<uint>(),
            (WolfConquer.Core.Runtime.ConquerProtocolProfile)int.MaxValue));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("short")]
    [InlineData("class")]
    [InlineData("spell")]
    [InlineData("index")]
    public void RealRuneMetadataRefusesBeforeAnyUidGraphOrGlobalInstallation(string fault)
    {
        var row = Rune();
        WorldDataCatalog data = fault == "missing" ? LegacyItemWorldDataFixture.Create() :
            fault == "duplicate" ? LegacyItemWorldDataFixture.Create(row, Rune()) : LegacyItemWorldDataFixture.Create(row);
        if (fault == "short") data = LegacyItemWorldDataFixture.Create(new ItemType(4020002, "Short", new string[] { "4020002", "Short" }));
        if (fault == "class") ((string[])row.Fields)[2] = "unknown";
        if (fault == "spell") ((string[])row.Fields)[25] = "-1";
        if (fault == "index") ((Dictionary<int, IReadOnlyList<ItemType>>)data.ItemsById)[4020002] = new[] { Rune(4010001) };
        var global = LegacyNativePoolItems.ItemsBase; var counter = LegacyNativePoolItems.ItemCounter;
        Assert.Throws<InvalidDataException>(() => Image.ValidateItemIds(data, new uint[] { 4020002 }, Profile));
        Assert.Same(global, LegacyNativePoolItems.ItemsBase); Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("zero")]
    [InlineData("nested")]
    [InlineData("location")]
    [InlineData("lossy-relic")]
    public void InvalidCanonicalOrUnrepresentableInputNeverGetsSilentlyNormalized(string fault)
    {
        Item row = fault == "zero" ? Row(0) : fault == "lossy-relic" ?
            new Item { Uid = 71, ItemId = 410009, Attribute1 = uint.MaxValue, DepositedItems = Array.Empty<Item>() } : Row(71);
        var source = fault switch
        {
            "duplicate" => Snapshot(new[] { row, Row(71) }),
            "nested" => Snapshot(new[] { Row(71, children: new[] { Row(72, children: new[] { Row(73) }) }) }),
            "location" => Snapshot(equipment: new[] { Row(71, position: 64) }),
            _ => Snapshot(new[] { row })
        };
        Assert.Throws<InvalidDataException>(() => Image.Prepare(LegacyItemWorldDataFixture.Create(), source, Profile));
        Assert.Equal(fault == "lossy-relic" ? uint.MaxValue : 0, row.Attribute1);
        Assert.Equal(fault == "zero" ? 0u : 71u, row.Uid);
    }

    [Fact]
    public void ExistingGlobalIdAndSkillStateSurviveRealMissingMetadataExceptions()
    {
        var previous = LegacyNativePoolItems.ItemsBase;
        try
        {
            var item = new LegacyNativeGameItemRuntime { ITEM_ID = 410009, SpellID = 123, Effect = LegacyNativeGameItemFlags.ItemEffect.Poison };
            LegacyNativePoolItems.ItemsBase = null;
            Assert.Throws<NullReferenceException>(() => item.ITEM_ID = 4010001);
            Assert.Equal(410009u, item.ITEM_ID); Assert.Equal(123u, item.SpellID);
            LegacyNativePoolItems.ItemsBase = new();
            Assert.Throws<KeyNotFoundException>(() => item.ITEM_ID = 4010001);
            LegacyNativePoolItems.ItemsBase[4010001] = null!;
            Assert.Throws<NullReferenceException>(() => item.ITEM_ID = 4010001);
            Assert.Equal(410009u, item.ITEM_ID); Assert.Equal(123u, item.SpellID);
            Assert.Equal(LegacyNativeGameItemFlags.ItemEffect.Poison, item.Effect);
        }
        finally { LegacyNativePoolItems.ItemsBase = previous; }
    }

    [Fact]
    public void ActualNativeDriftRejectsReuseInsteadOfRepairingThePriorOwner()
    {
        var data = LegacyItemWorldDataFixture.Create(); var image = Image.Prepare(data,
            Snapshot(new[] { Row(71, children: new[] { Row(72) }) }), Profile).Image;
        var child = image.Roots[0].Children[0].Item; child.Plus = 17;
        Assert.False(image.IsCurrentPlain());
        Assert.Throws<InvalidDataException>(() => Image.Prepare(data, image.FinalSnapshot, Profile, image));
        Assert.Equal((byte)17, child.Plus);
    }

    private sealed class CallbackFields : List<string>, IReadOnlyList<string>
    {
        internal Action? OnLast;
        string IReadOnlyList<string>.this[int index]
        {
            get
            {
                if (index == 25 && OnLast is { } callback) { OnLast = null; callback(); }
                return base[index];
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LastRequiredFieldCallbackCannotInvalidateEarlierMetadataOrCanonicalChecks(bool canonical)
    {
        var fields = new CallbackFields(); fields.AddRange(Rune().Fields);
        var row = new ItemType(4020002, "Callback", fields); var data = LegacyItemWorldDataFixture.Create(row);
        var image = Image.Prepare(data, Snapshot(runes: new[] { Row(71, 4020002, 102) }), Profile).Image;
        bool fired = false;
        fields.OnLast = () => { fired = true; if (canonical) image.FinalSnapshot.RuneItems[0].PerfectionLevel++;
            else fields[2] = "1001"; };
        Assert.True(image.IsCurrentPlain()); Assert.False(fired); // Plain proof invokes no callback.
        Assert.Throws<InvalidDataException>(() => image.RequireCurrent()); Assert.True(fired);
        Assert.False(image.IsCurrentPlain());
    }

    [CompilerGenerated]
    private sealed class ForeignCompilerLikeList<T>(T[] values) : IReadOnlyList<T>
    {
        private readonly T[] backing = values;
        public int Count => backing.Length;
        public T this[int index] => backing[index];
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)backing).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void GenuineBclReadonlyBackingWorksButForeignCompilerLikeWrapperIsNotTrusted()
    {
        Item[] rows = new[] { Row(71) };
        var source = new PlayerItemSnapshot(Array.AsReadOnly(rows), Array.Empty<Item>(), Array.Empty<Item>(),
            Array.Empty<Item>(), Array.Empty<Item>(), Array.Empty<Item>());
        var data = LegacyItemWorldDataFixture.Create(); var plan = Image.Prepare(data, source, Profile);
        Assert.True(plan.IsCurrentPlain());
        var fake = source with { InventoryItems = new ForeignCompilerLikeList<Item>(rows) };
        Assert.Throws<InvalidDataException>(() => Image.Prepare(data, fake, Profile));
        Assert.True(plan.Image.IsCurrentPlain());
    }

    [Fact]
    public void ConcurrentReadOnlyObserversDoNotShareMutableDepositEnumerationState()
    {
        var image = Image.Prepare(LegacyItemWorldDataFixture.Create(),
            Snapshot(new[] { Row(71, children: new[] { Row(72), Row(73) }) }), Profile).Image;
        Parallel.For(0, 1000, _ => Assert.True(image.IsCurrentPlain()));
        Assert.True(image.IsCurrentPlain());
    }

    [Theory]
    [InlineData(3155378976000000000L)]
    [InlineData(9223372036854775807L)]
    public void InvalidPurificationBinaryRefusesBeforeNativeImagePublication(long binary)
    {
        var row = new Item { Uid = 71, ItemId = 410009, PurificationItemId = 1001,
            PurificationAddedOn = binary, DepositedItems = Array.Empty<Item>() };
        Assert.Throws<ArgumentException>(() => Image.Prepare(LegacyItemWorldDataFixture.Create(), Snapshot(new[] { row }), Profile));
        Assert.Equal(binary, row.PurificationAddedOn);
    }
}
