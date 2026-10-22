using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearQueryTests
{
    [Fact]
    public void DisabledMetadataGatesQueriesBeforeUninitializedOwnersAndDoesNotCreateRanks()
    {
        using var scope = new NativeCombatGearQueryScope(false);
        var gear = new Gear { Items = null!, BlackJadeBag = null!, AirPowers = null! };
        LegacyNativeCombatGearTable.Info = null!;
        Assert.Equal(Gear.Promotion.None, Gear.GetType(7051));
        Assert.Null(gear.GetItems(7051));
        Assert.Null(gear.isEquip(7051));
        Assert.False(gear.isEquip(Gear.TypeID.Pirate1, 7051));
        Assert.False(gear.Check(7051));
        Assert.Equal(0u, gear.GetScore(7051, Gear.TypeID.None));
        gear.UpdateRank(null!); // Flag precedes the argument dereference.
        var ranks = Ranks.Ranks;
        Ranks.Create();
        Assert.Same(ranks, Ranks.Ranks);
        Assert.Empty(ranks);
    }

    [Theory]
    [InlineData(1999u, 0u)]
    [InlineData(2000u, 1u)]
    [InlineData(2999u, 1u)]
    [InlineData(3000u, 0u)]
    [InlineData(4000u, 3u)]
    [InlineData(4999u, 3u)]
    [InlineData(7000u, 2u)]
    [InlineData(7999u, 2u)]
    [InlineData(13000u, 4u)]
    [InlineData(13999u, 4u)]
    [InlineData(14000u, 5u)]
    [InlineData(14999u, 5u)]
    [InlineData(uint.MaxValue, 0u)]
    public void ClassPartitionsUseIntegerThousandsOnlyWhenMetadataIsEnabled(uint value, uint expected)
    {
        using var scope = new NativeCombatGearQueryScope();
        Assert.Equal((Gear.Promotion)expected, Gear.GetType(value));
        Assert.Equal(expected != 0, new Gear().Check(value));
    }

    [Fact]
    public void ItemQueriesUseActualValuesNotKeysAndRetainDuplicateAliasesAndStrictEquipOne()
    {
        using var scope = new NativeCombatGearQueryScope();
        var gear = new Gear();
        var aliased = new Gear.Weapon { ItemID = Gear.TypeID.Dragonhowl, Exp = 7, Equip = 2 };
        gear.Items[Gear.TypeID.Pirate1] = aliased;
        gear.Items[Gear.TypeID.Pirate2] = aliased;
        var values = gear.GetItems(2001);
        Assert.Equal(2, values.Length);
        Assert.All(values, value => Assert.Same(aliased, value));
        Assert.Empty(gear.GetItems(7051));
        Assert.Equal(14u, gear.GetScore(2001, Gear.TypeID.None));
        Assert.Null(gear.isEquip(2001));
        Assert.False(gear.isEquip(Gear.TypeID.Dragonhowl, 2001));
        aliased.Equip = 1;
        Assert.Same(aliased, gear.isEquip(2001));
        Assert.True(gear.isEquip(Gear.TypeID.Dragonhowl, 2001));
        Assert.False(gear.isEquip(Gear.TypeID.Pirate1, 2001));
        gear.Items.Clear();
        Assert.Equal(2, values.Length); // Array publication remains a snapshot of actual references.
        Assert.Empty(gear.GetItems(2001));
        Assert.Empty(gear.GetItems(1));
    }

    [Theory]
    [InlineData(2001u, 250030u, 201u, 10u)]
    [InlineData(4001u, 30u, 401u, 10u)]
    [InlineData(13001u, 45u, 1301u, 17u)]
    [InlineData(14001u, 45u, 1301u, 17u)]
    [InlineData(7001u, 395u, 701u, 0u)]
    public void ScoreUsesEachNativeFamilyFormula(uint classId, uint total, uint individual, uint individualScore)
    {
        using var scope = new NativeCombatGearQueryScope();
        var gear = FamilyGear();
        Assert.Equal(total, gear.GetScore(classId, Gear.TypeID.None));
        Assert.Equal(individualScore, gear.GetScore(classId, (Gear.TypeID)individual));
        Assert.Equal(0u, gear.GetScore(classId, (Gear.TypeID)uint.MaxValue));
        Assert.Equal(0u, gear.GetScore(1, Gear.TypeID.None));
    }

    [Fact]
    public void ScoresKeepUncheckedOverflowAndActualLiveMetadataAliases()
    {
        using var scope = new NativeCombatGearQueryScope();
        var gear = FamilyGear();
        var first = gear.Items[Gear.TypeID.Dragonhowl];
        first.Exp = uint.MaxValue;
        first.Transform = uint.MaxValue;
        Assert.Equal(unchecked(uint.MaxValue + uint.MaxValue * 50000u + 20u + 3u * 50000u),
            gear.GetScore(2001, Gear.TypeID.None));
        var row = LegacyNativeCombatGearTable.Info[(uint)Gear.TypeID.Pirate1][1];
        row.Score += 1000;
        Assert.Equal(1395u, gear.GetScore(7001, Gear.TypeID.None));
        LegacyNativeDaoqiDictTypeTable.Info[71].score += 500;
        Assert.Equal(1895u, gear.GetScore(7001, Gear.TypeID.None));
        Assert.Same(row, gear.Items[Gear.TypeID.Pirate1].DBItem);
    }

    internal static Gear FamilyGear()
    {
        var gear = new Gear();
        foreach (var pair in new[]
        {
            (Gear.TypeID.Dragonhowl, Gear.TypeID.Bloodlust),
            (Gear.TypeID.StoneCracker, Gear.TypeID.ColdMoon),
            (Gear.TypeID.Taoist1, Gear.TypeID.Taoist2),
            (Gear.TypeID.Pirate1, Gear.TypeID.Pirate2),
        })
        {
            gear.Items[pair.Item1] = new Gear.Weapon { ItemID = pair.Item1, Level = 1, Exp = 10, Transform = 2, MasteryScore = 7 };
            gear.Items[pair.Item2] = new Gear.Weapon { ItemID = pair.Item2, Level = 1, Exp = 20, Transform = 3, MasteryScore = 8 };
        }
        LegacyNativeCombatGearTable.Info[(uint)Gear.TypeID.Pirate1] = new() { [1] = new() { Score = 100 } };
        LegacyNativeCombatGearTable.Info[(uint)Gear.TypeID.Pirate2] = new() { [1] = new() { Score = 200 } };
        LegacyNativeDaoqiDictTypeTable.Info[71] = new() { score = 70 };
        gear.BlackJadeBag[1] = new Gear.BlackJade(71, 0); // Key is not DBItem Type authority.
        gear.AirPowers.SenseAipPower = 2;
        gear.AirPowers.ArmedAipPower = 3;
        gear.AirPowers.OverLoadAipPower = 5;
        return gear;
    }
}

// Exact static-reference lease, serialized with the existing native cache collection.
internal sealed class NativeCombatGearQueryScope : IDisposable
{
    private readonly bool loaded = LegacyNativeCombatGearTable.m_Load;
    private readonly Dictionary<uint, Dictionary<uint, LegacyNativeCombatGearTable.CQ>>? info = LegacyNativeCombatGearTable.Info;
    private readonly Dictionary<uint, LegacyNativeDaoqiDictTypeTable.CQ>? jade = LegacyNativeDaoqiDictTypeTable.Info;
    private readonly Dictionary<Ranks.Type, Ranks.Rank> ranks = Ranks.Ranks;

    internal NativeCombatGearQueryScope(bool enabled = true)
    {
        LegacyNativeCombatGearTable.m_Load = enabled;
        LegacyNativeCombatGearTable.Info = new();
        LegacyNativeDaoqiDictTypeTable.Info = new();
        Ranks.Ranks = new();
    }

    public void Dispose()
    {
        LegacyNativeCombatGearTable.m_Load = loaded;
        LegacyNativeCombatGearTable.Info = info!;
        LegacyNativeDaoqiDictTypeTable.Info = jade!;
        Ranks.Ranks = ranks;
    }
}
