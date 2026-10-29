using System.Collections;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicResourceCostCalculatorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ZeroManaDoesNotConsultAnUnrelatedWornMpMasterCatalog(bool alternate, bool throwOnRead)
    {
        var id = CreateMpMasterRuneItemId(3);
        var items = CreatePlayerItems(new PlayerItemSnapshotItem { Uid = 9001, ItemId = id,
            Position = alternate ? (ushort)123 : (ushort)103 });
        var originalRune = items.RuneItems[0];
        var attributes = new Dictionary<string, string>(CreateLegacyRuneEligibleAttributes())
        {
            ["StatusFlags"] = BuildStatusFlags(LegacyMagicResourceCostCalculator.ScurvyBombStatusFlag)
        };
        var catalog = new OptionalRuneReadProbe(CreateItemCatalog((int)id, "MPMaster(+3)"), throwOnRead);
        var cost = LegacyMagicResourceCostCalculator.Calculate(CreateSpell(useMana: 0, useStamina: 5),
            items, catalog, attributes, alternateEquipmentActive: alternate, characterClass: 7051);
        Assert.Equal(0, catalog.Reads); Assert.Equal((ushort)0, cost.UseMana);
        Assert.False(cost.HasMpMaster); Assert.Null(cost.MpMasterLevel);
        Assert.Equal((ushort)0, cost.MpMasterReductionPercent);
        Assert.Equal((ushort)11, cost.UseStamina); Assert.Equal((ushort)6, cost.ScurvyBombStaminaSurcharge);
        Assert.Equal((ushort)0, cost.DragonPierceStaminaSurcharge);
        Assert.Same(originalRune, Assert.Single(items.RuneItems));
    }

    [Fact]
    public void PositiveManaControlActuallyReadsTheSameRealCatalogAndPreservesItsReduction()
    {
        var id = CreateMpMasterRuneItemId(3);
        var catalog = new OptionalRuneReadProbe(CreateItemCatalog((int)id, "MPMaster(+3)"), false);
        var cost = LegacyMagicResourceCostCalculator.Calculate(CreateSpell(useMana: 100, useStamina: 5),
            CreatePlayerItems(new PlayerItemSnapshotItem { Uid = 9001, ItemId = id, Position = 103 }),
            catalog, CreateLegacyRuneEligibleAttributes(), characterClass: 7051);
        Assert.True(catalog.Reads > 0); Assert.Equal((ushort)75, cost.UseMana);
        Assert.Equal((byte)3, cost.MpMasterLevel); Assert.Equal((ushort)25, cost.MpMasterReductionPercent);
        Assert.Equal((ushort)5, cost.UseStamina);
    }

    private sealed class OptionalRuneReadProbe(IReadOnlyDictionary<int, IReadOnlyList<ItemType>> actual,
        bool throwOnRead) : IReadOnlyDictionary<int, IReadOnlyList<ItemType>>
    {
        internal int Reads { get; private set; }
        private void Observe()
        {
            Reads++;
            if (throwOnRead) throw new InvalidOperationException("Optional MPMaster catalog read was not needed.");
        }
        public IReadOnlyList<ItemType> this[int key] { get { Observe(); return actual[key]; } }
        public IEnumerable<int> Keys { get { Observe(); return actual.Keys; } }
        public IEnumerable<IReadOnlyList<ItemType>> Values { get { Observe(); return actual.Values; } }
        public int Count { get { Observe(); return actual.Count; } }
        public bool ContainsKey(int key) { Observe(); return actual.ContainsKey(key); }
        public bool TryGetValue(int key, out IReadOnlyList<ItemType> value)
        { Observe(); return actual.TryGetValue(key, out value!); }
        public IEnumerator<KeyValuePair<int, IReadOnlyList<ItemType>>> GetEnumerator()
        { Observe(); return actual.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
