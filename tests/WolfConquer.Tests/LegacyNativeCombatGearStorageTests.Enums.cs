using WolfConquer.Core.Data;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearStorageTests
{
    [Fact]
    public void EnumWidthsFlagsAndRepresentativeLiteralNumbersStayNative()
    {
        foreach (var type in new[] { typeof(Gear.TypeID), typeof(Gear.Promotion), typeof(Gear.TypeExp),
                     typeof(Gear.RankType), typeof(Gear.Flag), typeof(Gear.DataType),
                     typeof(JadeTable.Type), typeof(JadeTable.AttrType) })
        {
            Assert.Equal(typeof(uint), Enum.GetUnderlyingType(type));
            Assert.True(type.IsDefined(typeof(FlagsAttribute), inherit: false));
        }
        foreach (var type in new[] { typeof(Gear.SpellID), typeof(Gear.PacketsID), typeof(Gear.MapItemEffect) })
        {
            Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(type));
            Assert.True(type.IsDefined(typeof(FlagsAttribute), inherit: false));
        }
        Assert.Equal(typeof(ulong), Enum.GetUnderlyingType(typeof(Gear.LotteryEffect)));
        Assert.True(typeof(Gear.LotteryEffect).IsDefined(typeof(FlagsAttribute), inherit: false));
        Assert.Equal(201U, (uint)Gear.TypeID.Dragonhowl);
        Assert.Equal(403U, (uint)Gear.TypeID.TharnCutter);
        Assert.Equal(1305U, (uint)Gear.TypeID.Taoist5);
        Assert.Equal(703U, (uint)Gear.TypeID.Pirate3);
        Assert.Equal((ushort)17490, (ushort)Gear.SpellID.TripleAttackDragonhowl);
        Assert.Equal((ushort)10950, (ushort)Gear.SpellID.StarFlow);
        Assert.Equal((ushort)22070, (ushort)Gear.SpellID.StarVolcanoPassive);
        Assert.Equal((ushort)22060, (ushort)Gear.SpellID.FrozenNut);
        Assert.Equal(5U, (uint)Gear.Promotion.Fire);
        Assert.Equal(2U, (uint)Gear.TypeExp.Super);
        Assert.Equal(150000000U, (uint)Gear.RankType.Overall_Warrior);
        Assert.Equal(220000000U, (uint)Gear.RankType.Overall_Pirate);
        Assert.Equal(10000500U, (uint)Gear.RankType.WEEK_Pirate);
        Assert.Equal(337U, (uint)Gear.Flag.StarFlow);
        Assert.Equal(419U, (uint)Gear.Flag.Sailing);
        Assert.Equal(120U, (uint)Gear.DataType.ArchiveSkill);
        Assert.Equal(137U, (uint)Gear.DataType.ArrowBlades);
        Assert.Equal((ushort)2420, (ushort)Gear.PacketsID.MsgCombatGear);
        Assert.Equal((ushort)2054, (ushort)Gear.PacketsID.MsgRankMemberShow);
        Assert.Equal((ushort)4220, (ushort)Gear.MapItemEffect.IceAgePassive);
        Assert.Equal(4UL, (ulong)Gear.LotteryEffect.Free10Sail);
        Assert.Equal(8UL, (ulong)Gear.LotteryEffect.Free1Sail);
        Assert.Equal(3U, (uint)JadeTable.Type.Sacred);
        Assert.Equal(24U, (uint)JadeTable.AttrType.Spell);
    }
}
