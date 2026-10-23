using System.Text;
using Table = WolfConquer.Core.Data.LegacyNativeInnerPowerTable;
using Attr = WolfConquer.Core.Data.LegacyNativeInnerPowerTable.AtributeType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInnerPowerTests
{
    private sealed class WindowsInnerPowerFactAttribute : FactAttribute
    {
        public WindowsInnerPowerFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "Native InnerPower definition loader uses the genuine Win32 INI owner.";
        }
    }

    private static void Ini(Scope scope, string text) =>
        File.WriteAllText(Path.Combine(scope.DirectoryPath, "NeiGongInfo.ini"), text, Encoding.Unicode);

    private const string OneDefinition = """
        [NeiGong]
        Num=1
        [1]
        id=1
        Name=Wolf Inner
        NeiGongNum=2
        AttriType=1
        AttriValue=99
        [1-1]
        Type=8
        MaxLev=3
        AttriType=2-3
        AttriValue=8-9
        NeiGongValue=4-5-6
        ReqLev=2130
        ReqItemType=123456
        [1-2]
        Type=9
        MaxLev=4
        AttriType=4
        AttriValue=10-11
        NeiGongValue=7
        ReqLev=2999
        ReqItemType=234567
        """;

    [WindowsInnerPowerFact]
    public void DefinitionLoaderUsesRealIniOwnerAndPreservesUnequalArraysAndSpecialZeroPadding()
    {
        using var scope = new Scope(); Ini(scope, OneDefinition);
        Table.LoadDBInformation(); Assert.Equal(1u, Table.Count);
        var stage = Assert.Single(Table.Stages);
        Assert.Equal((ushort)1, stage.ID); Assert.Equal("Wolf Inner", stage.Name); Assert.Equal((byte)2, stage.NeiGongNum);
        Assert.Equal(new Attr[] { Attr.MaxHP, 0 }, stage.SpecialAtributesType);
        Assert.Equal(new uint[] { 99, 0 }, stage.AtributesValues);
        Assert.Equal(new byte[] { 8, 9 }, stage.NeiGongAtributes.Select(gong => gong.ID));
        var first = stage.NeiGongAtributes[0];
        Assert.Equal((byte)3, first.MaxLevel); Assert.Equal(new[] { Attr.PAttack, Attr.MAttack }, first.AtributesType);
        Assert.Equal(new uint[] { 8, 9 }, first.AtributesValues); Assert.Equal(new uint[] { 4, 5, 6 }, first.ProgressNeiGongValue);
        Assert.Equal(2130u, first.ReqLev); Assert.Equal(123456u, first.ItemID);
        Assert.Single(stage.NeiGongAtributes[1].AtributesType);
        Assert.Equal(new uint[] { 10, 11 }, stage.NeiGongAtributes[1].AtributesValues);
    }

    [WindowsInnerPowerFact]
    public void MissingFileAndMalformedNumericCountReplaceDefinitionCacheWithEmptyArray()
    {
        using var scope = new Scope(Definition(1, 1));
        Table.LoadDBInformation(); Assert.Equal(0u, Table.Count); Assert.Empty(Table.Stages);
        Table.Count = 1; Table.Stages = [Definition(1, 1)];
        Ini(scope, "[NeiGong]\nNum=not-numeric\n");
        Table.LoadDBInformation(); Assert.Equal(0u, Table.Count); Assert.Empty(Table.Stages);
    }

    [WindowsInnerPowerFact]
    public void NumericReadDefaultsDoNotRepairDirectStringArrayParsing()
    {
        using var scope = new Scope();
        Ini(scope, OneDefinition.Replace("id=1", "id=bad").Replace("MaxLev=3", "MaxLev=bad")
            .Replace("ReqLev=2130", "ReqLev=bad").Replace("ReqItemType=123456", "ReqItemType=bad"));
        Table.LoadDBInformation();
        Assert.Equal((ushort)0, Table.Stages[0].ID);
        var gong = Table.Stages[0].NeiGongAtributes[0];
        Assert.Equal((byte)0, gong.MaxLevel); Assert.Equal(0u, gong.ReqLev); Assert.Equal(0u, gong.ItemID);
        Ini(scope, OneDefinition.Replace("AttriType=2-3", "AttriType=2-"));
        Assert.Throws<FormatException>(() => Table.LoadDBInformation());
        Assert.Equal(1u, Table.Count); Assert.Single(Table.Stages); Assert.Null(Table.Stages[0]);
    }

    [WindowsInnerPowerFact]
    public void MoreSpecialTypesThanGongCountFaultsBeforePublishingStage()
    {
        using var scope = new Scope(Definition(1, 1));
        Ini(scope, OneDefinition.Replace("AttriType=1", "AttriType=1-2-3"));
        Assert.Throws<IndexOutOfRangeException>(() => Table.LoadDBInformation());
        Assert.Equal(1u, Table.Count); Assert.Single(Table.Stages); Assert.Null(Table.Stages[0]);
    }

    [WindowsInnerPowerFact]
    public void LaterDefinitionFailureRetainsEarlierPublishedStageAndNewCount()
    {
        using var scope = new Scope(Definition(9, 9));
        Ini(scope, OneDefinition.Replace("Num=1", "Num=2") + "\n[2]\nid=2\nNeiGongNum=1\nAttriType=bad\nAttriValue=1\n");
        Assert.Throws<FormatException>(() => Table.LoadDBInformation());
        Assert.Equal(2u, Table.Count); Assert.Equal(2, Table.Stages.Length);
        Assert.Equal((ushort)1, Table.Stages[0].ID); Assert.Null(Table.Stages[1]);
    }
}
