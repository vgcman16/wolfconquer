using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranking=WolfConquer.Core.Data.LegacyNativePrestigeRanking;
using Inner=WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Fact]
    public void WholeRebuildReplacesPointArrayCreatesCopiedEntryAndKeepsForeignServerOutOfActualRanks()
    {
        using var fixture=new Fixture();var client=fixture.Client;
        var old=client.PrestigePoints;Array.Fill(old,999u);
        client.MyPrestigePoints=777;client.PerfectionStatus.ToxinEraser=19;
        client.CreatePrestigePoints();
        Assert.NotSame(old,client.PrestigePoints);
        Assert.All(old,value=>Assert.Equal(999u,value));
        Assert.Equal(new uint[43],client.PrestigePoints);
        Assert.Equal(0u,client.MyPrestigePoints);
        Assert.Equal(0d,client.PerfectionStatus.ToxinEraser);
        var entry=Assert.IsType<Ranking.Entry>(client.PrestrigeEntry);
        Assert.Equal(41u,entry.UID);Assert.Equal("Hero",entry.Name);
        Assert.Equal(Ranking.Type.WaterTao,entry.type);
        Assert.Equal(client.PrestigePoints,entry.Points);
        Assert.NotSame(client.PrestigePoints,entry.Points);
        client.PrestigePoints[0]=7;
        Assert.Equal(0u,entry.Points![0]);
        Assert.All(Ranking.Ranks.Values,rank=>Assert.Empty(rank.Items));
    }

    [Theory]
    [InlineData(0u,0u)]
    [InlineData(99u,0u)]
    [InlineData(100u,30u)]
    [InlineData(16200u,4860u)]
    [InlineData(16201u,5346u)]
    [InlineData(40500u,13365u)]
    [InlineData(40501u,14580u)]
    [InlineData(60750u,21852u)]
    [InlineData(60751u,24280u)]
    [InlineData(72000u,28800u)]
    [InlineData(72001u,32400u)]
    [InlineData(79200u,35640u)]
    [InlineData(79201u,39600u)]
    [InlineData(80800u,40400u)]
    [InlineData(80801u,48480u)]
    [InlineData(81000u,48600u)]
    [InlineData(81001u,0u)]
    [InlineData(uint.MaxValue,0u)]
    public void JiangStrengthUsesAllSevenNativeBandsAndIntegerDivisionBeforeMultiplication(uint strength,uint expected)
    {
        using var fixture=new Fixture();var client=fixture.Client;
        client.Player.MyJiangHu=new(41){Inner_Strength=strength};
        client.CreatePrestigePoints();
        Assert.Equal(expected,client.PrestigePoints[0]);
        Assert.Equal(expected,client.MyPrestigePoints);
        Assert.Equal(expected,client.PrestrigeEntry!.TotalPoints);
    }

    [Theory]
    [InlineData((ushort)119,1190u)]
    [InlineData((ushort)120,1800u)]
    [InlineData((ushort)129,1935u)]
    [InlineData((ushort)130,2600u)]
    [InlineData((ushort)139,2780u)]
    [InlineData((ushort)140,3500u)]
    public void LevelUsesAllFourNativeRanges(ushort level,uint expected)
    {
        using var fixture=new Fixture();var client=fixture.Client;
        SavedField(client.Player,"_level",level);
        client.CreatePrestigePoints();
        Assert.Equal(expected,client.PrestigePoints[4]);
        Assert.Equal(unchecked((byte)level),client.PrestrigeEntry!.Level);
    }

    [Theory]
    [InlineData(0,0u)]
    [InlineData(1,50u)]
    [InlineData(15,50u)]
    [InlineData(16,100u)]
    [InlineData(30,100u)]
    [InlineData(31,150u)]
    [InlineData(45,150u)]
    [InlineData(46,200u)]
    [InlineData(60,200u)]
    [InlineData(61,250u)]
    public void BothWardrobeCategoriesIntentionallyCountItemsOneAndIgnoreItemsZero(int count,uint expected)
    {
        using var fixture=new Fixture();var client=fixture.Client;
        for(uint i=0;i<100;i++)client.MyWardrobe!.Items[0][i]=new(){Uid=i};
        for(uint i=0;i<count;i++)client.MyWardrobe!.Items[1][i]=new(){Uid=i};
        client.CreatePrestigePoints();
        Assert.Equal(expected,client.PrestigePoints[16]);
        Assert.Equal(expected,client.PrestigePoints[17]);
        Assert.Equal(expected*2,client.MyPrestigePoints);
    }

    [Fact]
    public void BothRealEquipmentLoopsCountTheSameAliasedItemTwiceWithoutDeduplication()
    {
        using var fixture=new Fixture();var client=fixture.Client;
        var same=new LegacyNativeGameItemRuntime(){Uid=71,ItemPoints=17};
        client.Equipment!.CurentEquip=[same,same];
        client.CreatePrestigePoints();
        Assert.Equal(68u,client.PrestigePoints[3]);
        Assert.Equal(68u,client.MyPrestigePoints);
        Assert.Equal(68u,client.PrestrigeEntry!.Points![3]);
        Assert.Same(same,client.Equipment.CurentEquip[0]);
        Assert.Same(same,client.Equipment.CurentEquip[1]);
    }

    [Fact]
    public void GenuineStatsRebirthSubclassInnerPowerAndNobilityOwnersContributeToTheirLiteralSlots()
    {
        using var fixture=new Fixture();var client=fixture.Client;
        client.Player.Strength=10;client.Player.Spirit=20;client.Player.Vitality=30;client.Player.Agility=40;
        client.Player.Reborn=2;client.Player.NobilityRank=LegacyNativeNobilityRank.King;
        client.Player.SubClass=new();
        client.Player.SubClass.src[(LegacyNativeDbLevExp.Sort)1]=new(){Level=9,Phrase=9};
        client.Player.SubClass.src[(LegacyNativeDbLevExp.Sort)2]=new(){Level=1,Phrase=3};
        client.Player.InnerPower=new("Inner",41)
        {Stages=[new Inner.Stage(){UnLocked=true},new(){UnLocked=false},new(){UnLocked=true}]};
        client.CreatePrestigePoints();
        Assert.Equal(200u,client.PrestigePoints[2]);Assert.Equal(500u,client.PrestigePoints[6]);
        Assert.Equal(2000u,client.PrestigePoints[7]);Assert.Equal(1040u,client.PrestigePoints[15]);
        Assert.Equal(12000u,client.PrestigePoints[18]);Assert.Equal(15740u,client.MyPrestigePoints);
    }

    [Theory]
    [InlineData((byte)0,500u)]
    [InlineData((byte)49,10300u)]
    [InlineData((byte)50,10400u)]
    [InlineData((byte)51,10500u)]
    public void ActivatedBeastsScoreIncludesZeroAndChangesStepAfterFifty(byte level,uint expected)
    {
        using var fixture=new Fixture();var client=fixture.Client;
        client.Beasts!.Activated=true;SavedField(client.Beasts,"_level",level);
        client.CreatePrestigePoints();Assert.Equal(expected,client.PrestigePoints[37]);
        client.Beasts.Activated=false;client.CreatePrestigePoints();Assert.Equal(0u,client.PrestigePoints[37]);
    }

    [Fact]
    public void ActualChiScoreAndRankFieldsUseStrictBelowFiftyIncludingNegativeAndZeroRanks()
    {
        using var fixture=new Fixture();var client=fixture.Client;
        client.Player.MyChi=new(41);
        int[] ranks=[49,50,-1,0];int index=0;
        foreach(var power in client.Player.MyChi)
        {
            power.UnLocked=true;power.Rank=ranks[index++];
            power.Attributes=[new(LegacyNativeChiAttribute.CriticalStrike,200,false)];
            Assert.Equal(100,power.Score);
        }
        client.CreatePrestigePoints();
        Assert.Equal(15000u,client.PrestigePoints[1]);Assert.Equal(15000u,client.MyPrestigePoints);
    }

    [Fact]
    public void RealWornRunePropertiesFeedAllThreePointSlotsWithoutFakePrestigeProviders()
    {
        using var fixture=new Fixture();var client=fixture.Client;
        foreach(uint id in new uint[]{4010001,4020027,4030009})
            LegacyNativePoolItems.ItemsBase![id]=new(){ID=id,Name="Owned Rune"};
        client.Rune!.Add(new(){Uid=1,ITEM_ID=4010001,Position=101});
        client.Rune.Add(new(){Uid=2,ITEM_ID=4020027,Position=102});
        client.Rune.Add(new(){Uid=3,ITEM_ID=4030009,Position=103});
        client.CreatePrestigePoints();
        Assert.Equal(3000u,client.PrestigePoints[20]);Assert.Equal(31000u,client.PrestigePoints[21]);
        Assert.Equal(11000u,client.PrestigePoints[22]);Assert.Equal(45000u,client.MyPrestigePoints);
        Assert.All(Ranking.Ranks.Values,rank=>Assert.Empty(rank.Items));
    }

    [Fact]
    public void NativeUInt32EquipmentContributionPreservesTheLargestSafeDoubledValue()
    {
        using var fixture=new Fixture();var client=fixture.Client;
        client.Equipment!.CurentEquip=[new(){ItemPoints=uint.MaxValue/2}];
        client.PrestigeLevel=0;
        client.CreatePrestigePoints();
        Assert.Equal(uint.MaxValue-1,client.PrestigePoints[3]);
        Assert.Equal(uint.MaxValue-1,client.MyPrestigePoints);
        Assert.Equal(0d,client.PerfectionStatus.ToxinEraser);
        Assert.Equal(0d,client.PerfectionStatus.AbsoluteLuck);
    }
}
