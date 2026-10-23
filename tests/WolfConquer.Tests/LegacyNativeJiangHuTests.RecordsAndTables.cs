using Jiang = WolfConquer.Core.World.LegacyNativeJiangHu;
using Table = WolfConquer.Core.Data.LegacyNativeJianHuTable;
using Attr = WolfConquer.Core.World.LegacyNativeJiangHu.Stage.AtributesType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeJiangHuTests
{
    [Fact]
    public void WholeRecordContainsAllNineStagesAllEightyOneStarPairsAndMutatesOnlyNativeSlashNames()
    {
        using var scope=new Scope();
        var value=New();
        value.Name="H/e/ro";
        value.CustomizedName="C/ult";
        value.Level=141;
        value.Talent=5;
        value.FreeTimeToday=17;
        value.OnJiangMode=true;
        value.FreeCourse=87654;
        value.RoundBuyPoints=33;
        value.StartCountDwon=new DateTime(2025,1,1);
        value.CountDownEnd=value.StartCountDwon.AddSeconds(300);
        value.ArrayStages[0].Activate=true;
        value.ArrayStages[8].ArrayStars[8].Activate=true;
        value.ArrayStages[8].ArrayStars[8].UID=1538;
        var fields=Enumerable.Repeat("0",181).ToArray();
        new[]{"41","Hero","Cult","141","5","17","1","87654","300","33"}.CopyTo(fields,0);
        fields[10]="1";
        fields[179]="1";
        fields[180]="1538";
        Assert.Equal(string.Join('/',fields)+"/",value.ToString());
        Assert.Equal("Hero",value.Name);
        Assert.Equal("Cult",value.CustomizedName);
        Assert.False(value.ArrayStages[8].Activate);
        Assert.Equal((byte)0,value.ArrayStages[8].ArrayStars[8].Level);
        value.Name="Before/Null";
        value.CustomizedName=null!;
        Assert.Throws<NullReferenceException>(()=>value.ToString());
        Assert.Equal("BeforeNull",value.Name);
    }

    [Fact]
    public void SaveLoadUsesRealFilesFiltersForeignUidAndRewritesOnlyActivatedNinthStagePhysicalAttackStars()
    {
        using var scope=new Scope();
        using var files=new Files();
        var value=New();
        value.StartCountDwon=new DateTime(2025,1,1);
        value.CountDownEnd=value.StartCountDwon.AddSeconds(300);
        value.ArrayStages[8].ArrayStars[0].UID=1538;
        value.ArrayStages[8].ArrayStars[0].Activate=true;
        value.ArrayStages[8].ArrayStars[1].UID=1538;
        var fields=Enumerable.Repeat("0",181).ToArray();
        new[]{"41","Hero","Cultivation","0","3","0","0","30000","300","0"}.CopyTo(fields,0);
        fields[163]="1";fields[164]="1538";fields[166]="1538";
        string row=string.Join('/',fields)+"/";
        var foreign=New(3999900001);
        Table.SaveJiangHu();
        files.Expect("JiangHu.txt",FileText(row));
        Jiang.Poll.Clear();
        Table.LoadJiangHu();
        var loaded=Assert.Single(Jiang.Poll).Value;
        Assert.NotSame(value,loaded);
        Assert.Equal(41u,loaded.UID);
        var changed=loaded.ArrayStages[8].ArrayStars[0];
        Assert.Equal(Attr.MaxLife,changed.Typ);
        Assert.Equal((ushort)1537,changed.UID);
        Assert.Equal((byte)6,changed.Level);
        var inactive=loaded.ArrayStages[8].ArrayStars[1];
        Assert.Equal(Attr.None,inactive.Typ);
        Assert.Equal((byte)0,inactive.Level);
        Assert.Equal((ushort)1538,inactive.UID);
        Assert.Equal(0u,loaded.Inner_Strength); // Native Load invokes full CreateStatusAtributes(null).
        Assert.Equal(new uint[16],Status(loaded.statusclient));
        Assert.Null(Jiang.JiangHuRanking.TopRank100);
        Assert.Equal(3999900001u,foreign.UID);
    }

    [Fact]
    public void WholeTableLoaderKeepsExistingOwnersUsesLiteralSpaceSlotsAndNativeCriticalPowerMultiplier()
    {
        using var scope=new Scope();
        using var files=new Files();
        var old=new Table.Atribut(){Type=99,Power=77};
        Table.Atributes[99]=old;
        Table.CultivateStatus[99]=[99];
        files.Seed("JianghuAttributes.txt",FileText("ignored 10 1 6000","3 11 2 65535","4 1 3 41"));
        files.Seed("JingHuCultivateStatus.txt",FileText("1 3 1 2 3","2 2 15 bad"));
        Table.LoadStatus();
        Assert.Same(old,Table.Atributes[99]);
        Assert.Equal((ushort)60000,Table.Atributes[266].Power);
        Assert.Equal((ushort)65526,Table.Atributes[523].Power);
        Assert.Equal((ushort)41,Table.Atributes[769].Power);
        Assert.Equal(new byte[]{1,2,3},Table.CultivateStatus[1]);
        Assert.Equal(new byte[]{15,0},Table.CultivateStatus[2]);
        Assert.Equal(new byte[]{99},Table.CultivateStatus[99]);
        Assert.Equal(4,Table.Atributes.Count);
    }

    [Fact]
    public void SaveQueuesWholeRecordsBeforeWriteSoLaterNullNamePreservesExistingBytes()
    {
        using var scope=new Scope();
        using var files=new Files();
        files.Seed("JiangHu.txt","previous owned bytes");
        New(41);
        var faulting=New(42);faulting.Name=null!;
        Assert.Throws<NullReferenceException>(()=>Table.SaveJiangHu());
        files.Expect("JiangHu.txt","previous owned bytes");
        Assert.Equal(2,Jiang.Poll.Count);
    }
}
