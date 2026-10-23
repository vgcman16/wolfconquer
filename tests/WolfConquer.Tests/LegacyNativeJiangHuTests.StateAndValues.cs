using System.Collections;
using System.Reflection;
using WolfConquer.Core.World;
using Jiang = WolfConquer.Core.World.LegacyNativeJiangHu;
using Table = WolfConquer.Core.Data.LegacyNativeJianHuTable;
using Attr = WolfConquer.Core.World.LegacyNativeJiangHu.Stage.AtributesType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeJiangHuTests
{
    [Fact]
    public void ConstructorPublishesSameOwnerAndBuildsNineDistinctStagesOfNineDistinctStarsWithNativeDefaults()
    {
        using var scope=new Scope();
        var value=new Jiang(41);
        Assert.Same(value,Assert.Single(Jiang.Poll).Value);
        Assert.Equal(9,value.ArrayStages.Length);
        Assert.Equal(3,value.Talent);
        Assert.Equal(30000u,value.FreeCourse);
        Assert.Null(value.Name);
        Assert.Null(value.CustomizedName);
        Assert.Null(value.Buckupstar);
        Assert.False(value.OnJiangMode);
        Assert.Equal(default,value.ThreadStamp);
        Assert.Equal(new uint[16],Status(value.statusclient));
        var counts=typeof(Jiang.Stage).GetField("Count",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var stars=typeof(Jiang.Stage.Star).GetField("Count",BindingFlags.Instance|BindingFlags.NonPublic)!;
        for(int stage=0;stage<9;stage++)
        {
            Assert.Equal(stage,counts.GetValue(value.ArrayStages[stage]));
            Assert.False(value.ArrayStages[stage].Activate);
            Assert.Equal(9,value.ArrayStages[stage].ArrayStars.Length);
            for(int star=0;star<9;star++)
            {
                var item=value.ArrayStages[stage].ArrayStars[star];
                Assert.Equal(star,stars.GetValue(item));
                Assert.False(item.Activate);
                Assert.Equal(0,item.UID);
                Assert.Equal(Attr.None,item.Typ);
            }
        }
        Assert.Equal(81,value.ArrayStages.SelectMany(stage=>stage.ArrayStars).Distinct().Count());
        var enumerated=((IEnumerable)value).Cast<Jiang.Stage>().ToArray();
        Assert.Equal(value.ArrayStages,enumerated);
        Assert.All(Enumerable.Range(0,9),i=>Assert.Same(value.ArrayStages[i],enumerated[i]));
        var user=new LegacyNativeGameClientRuntime();
        Assert.Null(user.Player.MyJiangHu);
        Assert.Equal(LegacyNativePkMode.Capture,user.Player.PkMode);
        Assert.Equal(Jiang.AttackFlag.None,user.Player.JiangPkFlag);
        Assert.False(user.OnInterServer);
    }

    [Fact]
    public void AllByteTableLookupsClampExactlyAndValuePackingRetainsNativeWidths()
    {
        ushort[] minutes=[1001,501,385,313,193],castle=[294,625,833,1000,1562],points=[100,120,150,200,300,500],prestige=[30,36,45,60,90,150];
        ushort[] alignment=[0,10,13,15,18,21,25,30,50];
        ushort[] free=[10,20,26,31,52];
        double[] advance=[31.25,31.25,32,32.2,30];
        using var scope=new Scope();
        var value=New();
        for(int raw=0;raw<=255;raw++)
        {
            byte input=(byte)raw;
            int talent=Math.Clamp(raw-1,0,4),level=Math.Clamp(raw-1,0,5);
            Assert.Equal(minutes[talent],Table.GetMinutesOnTalent(input));
            Assert.Equal(castle[talent],Table.GetFreeCourseInCastle(input));
            Assert.Equal(free[talent],Table.GetFreeCourse(input));
            Assert.Equal(advance[talent],Table.GetMinutesInCastle(input));
            Assert.Equal(points[level],Table.GetStatusPoints(input));
            Assert.Equal(prestige[level],Table.GetPrestigePoints(input));
            Assert.Equal(alignment[Math.Min(raw,8)],Table.AlignmentExtraPoints(input));
            ushort packed=Table.ValueToRoll(input,input);
            Assert.Equal((ushort)(raw*257),packed);
            Assert.Equal(input,value.GetValueLevel(packed));
            Assert.Equal((Attr)input,value.GetValueType(packed));
        }
        Assert.Equal((ushort)257,value.ValueToRoll((Attr)257,0)); // Enum narrows to UInt16, not byte.
        Assert.Equal(Attr.MaxLife,value.GetValueType(0xFFFFFF01));
        Assert.Throws<KeyNotFoundException>(()=>Table.GetPower(17));
        Table.Atributes[17]=new(){Power=91};
        Assert.Equal((ushort)91,Table.GetPower(17));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(9)]
    public void GradeCountsActivatedStagesAndOnlyThenAllNineWholeStarLevelBands(int stages)
    {
        using var scope=new Scope();
        var value=New();
        for(int i=0;i<stages;i++) value.ArrayStages[i].Activate=true;
        Assert.Equal(stages,value.GetGrade());
        for(byte level=1;level<=6;level++)
        {
            foreach(var star in value.ArrayStages.SelectMany(stage=>stage.ArrayStars)) star.Level=level;
            Assert.Equal(stages==9?9+level:stages,value.GetGrade());
        }
        value.ArrayStages[8].ArrayStars[8].Level=0;
        Assert.Equal(stages,value.GetGrade());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4294967295L)]
    [InlineData(4294967296L)]
    public void TimeUsesLocalEpochAndNativeFiniteLowUInt32Conversion(long seconds)
    {
        using var scope=new Scope();
        var value=New();
        value.CountDownEnd=new DateTime(1970,1,1).ToLocalTime().AddSeconds(seconds);
        Assert.Equal(unchecked((uint)seconds),value.Time);
    }
}
