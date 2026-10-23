using WolfConquer.Core.World;
using Jiang = WolfConquer.Core.World.LegacyNativeJiangHu;
using Table = WolfConquer.Core.Data.LegacyNativeJianHuTable;
using Attr = WolfConquer.Core.World.LegacyNativeJiangHu.Stage.AtributesType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeJiangHuTests
{
    public static IEnumerable<object[]> Attributes()=>Enumerable.Range(0,16).Append(255).Select(type=>new object[]{type});

    [Theory]
    [MemberData(nameof(Attributes))]
    public void ComputeStatusPreservesAllFifteenWholeBranchesAndUnknownNoOpWithoutReadingClient(int raw)
    {
        using var scope=new Scope();
        var value=New();
        value.ComputeStatus(null!,(Attr)raw,91);
        Assert.Equal(SingleStatus((Attr)raw,91),Status(value.statusclient));
        value.ComputeStatus(null!,(Attr)raw,91);
        Assert.Equal(SingleStatus((Attr)raw,182),Status(value.statusclient));
    }

    [Theory]
    [InlineData(1,100u)]
    [InlineData(2,220u)]
    [InlineData(3,360u)]
    [InlineData(7,1120u)]
    [InlineData(8,1440u)]
    [InlineData(9,1800u)]
    public void AlignmentStrengthUsesSeparateNativePercentTableIncludingEightAndNineSpecialCases(int count,uint expected)
    {
        using var scope=new Scope();
        var value=New();
        var stars=Enumerable.Range(0,count).Select(i=>new Jiang.Stage.Star(i){Level=1}).ToList();
        value.CalculateInner_StrengthAlignments(stars);
        Assert.Equal(expected,value.Inner_Strength);
        Assert.Equal(unchecked((byte)(count<8?count-1:count==8?count:count+1)),value.GetPercentALignment(count));
        Assert.Equal((byte)255,value.GetPercentALignment(0));
    }

    [Fact]
    public void WholeRebuildGroupsAdjacentNonzeroUidsIgnoresStarActivateAndUsesMetadataPowerButStarType()
    {
        using var scope=new Scope();
        var value=New();
        var user=new LegacyNativeGameClientRuntime();
        Star(value,0,0,Attr.PAttack,1,100,active:false);
        Star(value,0,2,Attr.PAttack,1,100,active:false); // UID0 gap is filtered before adjacency.
        Star(value,0,3,Attr.Mdefense,2,50,active:true);
        Star(value,1,0,Attr.MaxMana,3,40,active:false);
        value.CreateStatusAtributes(user);
        Assert.Equal(490u,value.Inner_Strength); // aligned2x100=>220 + single120 + single150.
        Assert.Equal(220u,user.Status.MaxAttack);
        Assert.Equal(220u,user.Status.MinAttack);
        Assert.Equal(100u,user.Status.MagicDefence); // Source adds50 twice to client.
        Assert.Equal(50u,value.statusclient.MagicDefence);
        Assert.Equal(40u,user.Status.MaxMana);
        Assert.Equal((byte)1,value.Rank);
        Assert.Same(value,Assert.Single(Jiang.JiangHuRanking.TopRank100!));
        var cached=value.statusclient;
        value.CreateStatusAtributes(user);
        Assert.NotSame(cached,value.statusclient);
        Assert.Equal(440u,user.Status.MaxAttack); // Additive destination, not replace.
        Assert.Equal(490u,value.Inner_Strength);
    }

    [Fact]
    public void CachedInterserverPathAppliesAllNativeCapsAndDoubleMagicDefenceWithoutRebuildingOrRanking()
    {
        using var scope=new Scope();
        var value=New();
        var user=new LegacyNativeGameClientRuntime(){OnInterServer=true};
        var cached=value.statusclient;
        foreach(var field in typeof(LegacyNativeMsgStatus).GetFields().Where(field=>field.FieldType==typeof(uint)))
            field.SetValue(cached,20000u);
        value.Inner_Strength=77;
        value.CreateStatusAtributes(user);
        Assert.Same(cached,value.statusclient);
        Assert.Equal(new uint[]{12150,20000,6480,6480,4860,20000,40000,20000,20000,20000,20000,5400,5400,5400,810,2430},Status(user.Status));
        Assert.Equal(77u,value.Inner_Strength);
        Assert.Null(Jiang.JiangHuRanking.TopRank100);
        LegacyNativeServerConfig.IsInterServer=true;
        user.OnInterServer=false;
        value.CreateStatusAtributes(user);
        Assert.Equal(80000u,user.Status.MagicDefence);
        Assert.Same(cached,value.statusclient);
        value.CreateStatusAtributes(null);
        Assert.Equal(77u,value.Inner_Strength);
    }

    [Fact]
    public void MissingLaterMetadataLeavesFreshCacheAndEarlierStatusStrengthButNoDestinationOrRankPublication()
    {
        using var scope=new Scope();
        var value=New();
        var user=new LegacyNativeGameClientRuntime();
        Star(value,0,0,Attr.PAttack,1,100);
        var later=Star(value,0,1,Attr.MAttack,2,50);
        Table.Atributes.Remove(later.UID);
        var old=value.statusclient;
        value.Inner_Strength=999;
        Assert.Throws<KeyNotFoundException>(()=>value.CreateStatusAtributes(user));
        Assert.NotSame(old,value.statusclient);
        Assert.Equal(100u,value.statusclient.MaxAttack);
        Assert.Equal(100u,value.Inner_Strength);
        Assert.Equal(new uint[16],Status(user.Status));
        Assert.Null(Jiang.JiangHuRanking.TopRank100);
    }

    [Fact]
    public void AlignedPowerUsesNativeUInt16WrapBeforeClientCapsAndStrengthUsesStarLevels()
    {
        using var scope=new Scope();
        var value=New();
        var user=new LegacyNativeGameClientRuntime();
        for(int i=0;i<9;i++) Star(value,0,i,Attr.MaxMana,6,65535);
        value.CreateStatusAtributes(user);
        // Each65535+floor(65535*50/100)=98302=>UInt16=32766; sum9=>32750.
        Assert.Equal(32750u,value.statusclient.MaxMana);
        Assert.Equal(32750u,user.Status.MaxMana);
        Assert.Equal(9000u,value.Inner_Strength); // 9*500 with100% nine-star strength bonus.
    }
}
