using WolfConquer.Core.Data;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearStorageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlackJadeDbItemUsesLiveTypeCacheAndAllocatesFreshMissRows(bool loaded)
    {
        using var tables = new TableScope();
        GearTable.m_Load = loaded; // DBItem does not consult the load flag.
        var jade = new Gear.BlackJade(73, 99);
        var firstMiss = jade.DBItem;
        var secondMiss = jade.DBItem;
        Assert.NotSame(firstMiss, secondMiss);
        AssertEmpty(firstMiss);
        firstMiss.TypeRune = 123;
        Assert.Equal(0U, secondMiss.TypeRune);
        Assert.Empty(JadeTable.Info);
        var row = new JadeTable.CQ { ID = 999, TypeRune = 7, Exp = 31 };
        JadeTable.Info[73] = row; // Lookup authority is the cache key, not row.ID or jade.Data.
        Assert.Same(row, jade.DBItem);
        row.Exp = 37;
        Assert.Equal(37U, jade.DBItem.Exp);
        jade.Data = uint.MaxValue;
        Assert.Same(row, jade.DBItem);
        var replacement = new JadeTable.CQ { TypeRune = 11 };
        JadeTable.Info[73] = replacement;
        Assert.Same(replacement, jade.DBItem);
        jade.Type = 74;
        AssertEmpty(jade.DBItem);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WeaponDbItemUsesExactCurrentItemAndLevelKeysWithoutRowNormalization(bool loaded)
    {
        using var tables = new TableScope();
        GearTable.m_Load = loaded;
        var weapon = new Gear.Weapon { ItemID = Gear.TypeID.Dragonhowl, Level = 3 };
        var firstMiss = weapon.DBItem;
        AssertEmpty(firstMiss);
        Assert.NotSame(firstMiss, weapon.DBItem);
        Assert.Empty(GearTable.Info);
        var row = new GearTable.CQ { ID = 999, Type = 777, Level = 88, Skills = new uint[] { 7, 9 } };
        var levels = new Dictionary<uint, GearTable.CQ> { [3] = row };
        GearTable.Info[201] = levels;
        Assert.Same(row, weapon.DBItem);
        row.Skills[0] = 11;
        Assert.Equal(11U, weapon.DBItem.Skills[0]);
        var replacement = new GearTable.CQ { ID = 123 };
        levels[3] = replacement;
        Assert.Same(replacement, weapon.DBItem);
        weapon.Level = 4;
        AssertEmpty(weapon.DBItem);
        var fourth = new GearTable.CQ { Exp = 17 };
        levels[4] = fourth;
        Assert.Same(fourth, weapon.DBItem);
        weapon.ItemID = Gear.TypeID.None;
        AssertEmpty(weapon.DBItem);
        Assert.Single(GearTable.Info);
        Assert.Equal(2, levels.Count);
    }

    [Fact]
    public void TableLookupKeepsNativeOutNullAndCachedNullSuccessSemantics()
    {
        using var tables = new TableScope();
        Assert.False(GearTable.TryGetValue(201, 3, out var row));
        Assert.Null(row);
        GearTable.Info[201] = new Dictionary<uint, GearTable.CQ>();
        Assert.False(GearTable.TryGetValue(201, 3, out row));
        Assert.Null(row);
        GearTable.Info[201][3] = null!;
        Assert.True(GearTable.TryGetValue(201, 3, out row));
        Assert.Null(row);
        Assert.Null(new Gear.Weapon { ItemID = Gear.TypeID.Dragonhowl, Level = 3 }.DBItem);
        JadeTable.Info[73] = null!;
        Assert.Null(new Gear.BlackJade(73, 0).DBItem);
    }

    [Fact]
    public void UninitializedCachesFaultWithoutLazyInitializationOrFallbackRows()
    {
        using var tables = new TableScope();
        GearTable.Info = null!;
        JadeTable.Info = null!;
        GearTable.m_Load = false;
        Assert.Throws<NullReferenceException>(() => GearTable.TryGetValue(201, 3, out _));
        Assert.Throws<NullReferenceException>(() => new Gear.Weapon().DBItem);
        Assert.Throws<NullReferenceException>(() => new Gear.BlackJade().DBItem);
        Assert.Null(GearTable.Info);
        Assert.Null(JadeTable.Info);
        Assert.False(GearTable.m_Load);
    }

    [Fact]
    public void NullInnerLevelDictionaryFaultsInsteadOfBeingTreatedAsAMissingType()
    {
        using var tables = new TableScope();
        GearTable.Info[201] = null!;
        var weapon = new Gear.Weapon { ItemID = Gear.TypeID.Dragonhowl, Level = 3 };
        Assert.Throws<NullReferenceException>(() => GearTable.TryGetValue(201, 3, out _));
        Assert.Throws<NullReferenceException>(() => weapon.DBItem);
        Assert.Null(GearTable.Info[201]);
    }

    private static void AssertEmpty(GearTable.CQ row)
    {
        Assert.Equal(0U, row.ID);
        Assert.Equal(0U, row.Type);
        Assert.Equal(0U, row.Level);
        Assert.Equal(0U, row.Exp);
        Assert.Equal(0U, row.MaxHP);
        Assert.Equal(0U, row.PAttack);
        Assert.Equal(0U, row.PDefense);
        Assert.Equal(0U, row.MDefense);
        Assert.Null(row.Skills);
        Assert.Equal(0U, row.Score);
    }

    private static void AssertEmpty(JadeTable.CQ row)
    {
        Assert.Equal(0U, row.ID);
        Assert.Equal(JadeTable.Type.Refined, row.Type);
        Assert.Equal(0U, row.attr_type1);
        Assert.Equal(0U, row.attr_value1);
        Assert.Equal(0U, row.attr_type2);
        Assert.Equal(0U, row.attr_value2);
        Assert.Equal(0U, row.score);
        Assert.Equal(0U, row.points);
        Assert.Equal(0U, row.rate);
        Assert.Equal(0U, row.TypeRune);
        Assert.Equal(0U, row.Level);
        Assert.Equal(0U, row.Exp);
        Assert.Equal(0U, row.u12);
        Assert.Equal(0U, row.u13);
        Assert.Equal(0U, row.TypeCombatGear);
    }

    private sealed class TableScope : IDisposable
    {
        private readonly Dictionary<uint, Dictionary<uint, GearTable.CQ>>? gear = GearTable.Info;
        private readonly Dictionary<uint, JadeTable.CQ>? jade = JadeTable.Info;
        private readonly bool loaded = GearTable.m_Load;
        private readonly string location = GearTable.Location;

        internal TableScope()
        {
            GearTable.Info = new Dictionary<uint, Dictionary<uint, GearTable.CQ>>();
            JadeTable.Info = new Dictionary<uint, JadeTable.CQ>();
        }

        public void Dispose()
        {
            GearTable.Info = gear!;
            JadeTable.Info = jade!;
            GearTable.m_Load = loaded;
            GearTable.Location = location;
        }
    }
}
