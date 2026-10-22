using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatGearStorageTests
{
    [Fact]
    public void ConstructorCreatesDistinctEmptyOwnersWithoutReplacingTables()
    {
        using var tables = new TableScope();
        var gearRows = LegacyNativeCombatGearTable.Info;
        var jadeRows = LegacyNativeDaoqiDictTypeTable.Info;
        LegacyNativeCombatGearTable.m_Load = true;
        var location = new string("owned-table-location".ToCharArray());
        LegacyNativeCombatGearTable.Location = location;
        var first = new Gear();
        var second = new Gear();

        Assert.Empty(first.Items);
        Assert.Empty(first.BlackJadeBag);
        Assert.Empty(first.Info);
        Assert.NotSame(first.Items, second.Items);
        Assert.NotSame(first.BlackJadeBag, second.BlackJadeBag);
        Assert.NotSame(first.Info, second.Info);
        Assert.NotSame(first.AirPowers, second.AirPowers);
        Assert.Equal(0U, first.MasteryPoints);
        Assert.Equal(0U, first.TaoistReiki);
        Assert.Equal(0U, first.PirateExp);
        Assert.Equal(0U, first.Lottery);
        Assert.Equal(0U, first.StarFlow);
        Assert.Equal(0U, first.Archshadow);
        Assert.Equal(0U, first.ArrowBlades);
        Assert.Equal(0U, first.GetClass());
        Assert.Same(gearRows, LegacyNativeCombatGearTable.Info);
        Assert.Same(jadeRows, LegacyNativeDaoqiDictTypeTable.Info);
        Assert.True(LegacyNativeCombatGearTable.m_Load);
        Assert.Same(location, LegacyNativeCombatGearTable.Location);
        Assert.NotNull(Gear.GetRandom);
        Assert.Equal(new ushort[] { 21670, 21680, 21690, 21700, 22070, 22080, 22090, 22100, 22110 },
            Gear.GET_Passive);
    }

    [Fact]
    public void PublicStorageRetainsAssignedObjectsAndCrossOwnerAliases()
    {
        var first = new Gear();
        var second = new Gear();
        var weapon = new Gear.Weapon();
        var jade = new Gear.BlackJade(9, 11);
        var values = new[] { 1.25, -3.5 };
        first.Items[Gear.TypeID.Dragonhowl] = weapon;
        first.BlackJadeBag[73] = jade;
        first.Info["native-key"] = values;
        second.Items = first.Items;
        second.BlackJadeBag = first.BlackJadeBag;
        second.Info = first.Info;
        second.AirPowers = first.AirPowers;

        Assert.Same(weapon, second.Items[Gear.TypeID.Dragonhowl]);
        Assert.Same(jade, second.BlackJadeBag[73]);
        Assert.Same(values, second.Info["native-key"]);
        second.Items[Gear.TypeID.Dragonhowl].Exp = 17;
        second.BlackJadeBag[73].Data = 19;
        second.Info["native-key"][0] = 2.5;
        second.AirPowers.Points = 23;
        Assert.Equal(17U, weapon.Exp);
        Assert.Equal(19U, jade.Data);
        Assert.Equal(2.5, values[0]);
        Assert.Equal(23U, first.AirPowers.Points);
        Assert.Same(first.Items, second.Items);
        Assert.Same(first.AirPowers, second.AirPowers);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(45U)]
    [InlineData(uint.MaxValue)]
    public void SetClassOnlyOverwritesItsPhysicalUInt32(uint value)
    {
        var gear = new Gear { MasteryPoints = 31, StarFlow = 37 };
        var items = gear.Items;
        var powers = gear.AirPowers;
        gear.SetClass(123);
        gear.SetClass(value);
        gear.SetClass(value);
        Assert.Equal(value, gear.GetClass());
        Assert.Equal(31U, gear.MasteryPoints);
        Assert.Equal(37U, gear.StarFlow);
        Assert.Same(items, gear.Items);
        Assert.Same(powers, gear.AirPowers);
        Assert.Empty(items);
    }

    [Fact]
    public void RankAndPowersAreIndependentZeroInitializedStorage()
    {
        var rank = new Gear.VRANK();
        Assert.Equal(0.0, rank.LukyStrike);
        Assert.Equal(0.0, rank.Parry);
        Assert.Equal(0.0, rank.CoreStrike);
        Assert.Equal((ushort)0, rank.Magic);
        Assert.Equal((ushort)0, rank.MagicLevel);
        Assert.Equal(0U, rank.PAttack);
        Assert.Equal(0U, rank.PDefense);
        Assert.Equal(0U, rank.MDefense);
        Assert.Equal(0U, rank.PrestigeScore);
        var first = new Gear.Powers();
        var second = new Gear.Powers();
        Assert.Equal(0U, first.Id);
        Assert.Equal(0U, first.SenseLevel);
        Assert.Equal(0U, first.SenseAipPower);
        Assert.Equal(0U, first.ArmedLevel);
        Assert.Equal(0U, first.ArmedAipPower);
        Assert.Equal(0U, first.OverLoadLevel);
        Assert.Equal(0U, first.OverLoadAipPower);
        Assert.Equal(0U, first.Points);
        first.Points = uint.MaxValue;
        Assert.Equal(0U, second.Points);
    }

    [Fact]
    public void WeaponOwnsSixDistinctJadesAndTwoDistinctSevenWordArrays()
    {
        var first = new Gear.Weapon();
        var second = new Gear.Weapon();
        Assert.Equal(Gear.TypeID.None, first.ItemID);
        Assert.Equal(0U, first.Level);
        Assert.Equal(0U, first.Exp);
        Assert.Equal(0U, first.Equip);
        Assert.Equal(0U, first.Transform);
        Assert.Equal(0U, first.MasteryScore);
        Assert.Equal(6, first.BlackJades.Length);
        Assert.Equal(2, first.TypeAnima.Length);
        Assert.NotSame(first.BlackJades, second.BlackJades);
        Assert.NotSame(first.TypeAnima, second.TypeAnima);
        for (var i = 0; i < 6; i++)
        {
            Assert.NotNull(first.BlackJades[i]);
            Assert.Equal(0U, first.BlackJades[i].Type);
            Assert.Equal(0U, first.BlackJades[i].Data);
            Assert.NotSame(first.BlackJades[i], second.BlackJades[i]);
            for (var j = 0; j < i; j++) Assert.NotSame(first.BlackJades[j], first.BlackJades[i]);
        }
        Assert.NotSame(first.TypeAnima[0], first.TypeAnima[1]);
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(new uint[7], first.TypeAnima[i]);
            Assert.NotSame(first.TypeAnima[i], second.TypeAnima[i]);
        }
        first.BlackJades[0].Data = 41;
        first.TypeAnima[0][0] = 43;
        Assert.Equal(0U, first.BlackJades[1].Data);
        Assert.Equal(0U, first.TypeAnima[1][0]);
        Assert.Equal(0U, second.BlackJades[0].Data);
        Assert.Equal(0U, second.TypeAnima[0][0]);
    }

    [Theory]
    [InlineData(0U, uint.MaxValue)]
    [InlineData(uint.MaxValue, 0U)]
    public void BlackJadeConstructorRetainsVerbatimTypeAndData(uint type, uint data)
    {
        var empty = new Gear.BlackJade();
        var jade = new Gear.BlackJade(type, data);
        Assert.Equal(0U, empty.Type);
        Assert.Equal(0U, empty.Data);
        Assert.Equal(type, jade.Type);
        Assert.Equal(data, jade.Data);
    }
}
