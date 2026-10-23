using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    private sealed class TableFiles : IDisposable
    {
        private readonly string location=LegacyNativeServerConfig.DbLocation;
        private readonly Dictionary<uint,LegacyNativeProfessionTable.ProfLevelUp> levels=LegacyNativeProfessionTable.ProfLevelUPList;
        private readonly Dictionary<uint,LegacyNativeProfessionTable.TitleBenefit> titles=LegacyNativeProfessionTable.TitleBenefits;
        private readonly Dictionary<uint,uint> progresses=LegacyNativeItemRefineUpgrade.ProgresUpdates;
        private readonly Dictionary<uint,LegacyNativeItemRefineUpgrade.EffectEX> effectsEx=LegacyNativeItemRefineUpgrade.EffectsEX;
        private readonly Dictionary<uint,LegacyNativeSpecialEffectAttributeTable.Attribute>? special=LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute;
        private readonly string root=Path.Combine(Path.GetTempPath(),"wolf-equipment-tables-"+Guid.NewGuid().ToString("N"));
        internal TableFiles()
        {
            Directory.CreateDirectory(root); LegacyNativeServerConfig.DbLocation=root+Path.DirectorySeparatorChar;
            LegacyNativeProfessionTable.ProfLevelUPList=new(); LegacyNativeProfessionTable.TitleBenefits=new();
            LegacyNativeItemRefineUpgrade.ProgresUpdates=new(); LegacyNativeItemRefineUpgrade.EffectsEX=new(); LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute=new();
        }
        internal void Write(string leaf,string text)=>File.WriteAllText(Path.Combine(root,leaf),text);
        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation=location; LegacyNativeProfessionTable.ProfLevelUPList=levels; LegacyNativeProfessionTable.TitleBenefits=titles;
            LegacyNativeItemRefineUpgrade.ProgresUpdates=progresses; LegacyNativeItemRefineUpgrade.EffectsEX=effectsEx; LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute=special;
            string exact=Path.GetFullPath(root); string prefix=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar+"wolf-equipment-tables-";
            if(!exact.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Owned equipment table path escaped temporary root.");
            if(Directory.Exists(exact))Directory.Delete(exact,true);
        }
    }

    [Fact]
    public Task ProfessionLoadUsesNativeBenefitsThenLevelsThenTitlesAndExactDifferentKeys() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); using var files=new TableFiles();
        files.Write("prof_lev_benefit.txt","99@@1001@@12@@60011@@70012@@40013@@10014@@500");
        files.Write("prof_lev_up.txt","77@@1001@@2@@123@@456@@789");
        files.Write("prof_title_benefit.txt","88@@4@@5@@10001@@20002@@30003@@40004");
        LegacyNativeProfessionTable.Load();
        var benefit=LegacyNativeProfessionTable.Benefits[1001]; Assert.False(LegacyNativeProfessionTable.Benefits.ContainsKey(99));
        Assert.Equal(1001u,benefit.Class); Assert.Equal((byte)12,benefit.BattlePower); Assert.Equal(500u,benefit.PrestigeScore);
        Assert.Equal(new uint[]{11,12,13,14},benefit.AttributeValue.Select(attr=>attr.Value));
        var level=LegacyNativeProfessionTable.ProfLevelUPList[77]; Assert.Equal(1001u,level.Class); Assert.Equal((byte)2,level.Level);
        Assert.Equal(123u,level.QuestID); Assert.Equal(456u,level.PrestigeScore); Assert.Equal(789u,level.EXP);
        var title=LegacyNativeProfessionTable.TitleBenefits[88]; Assert.Equal((byte)4,title.Class); Assert.Equal((byte)5,title.Rank);
        Assert.Equal(new uint[]{1,2,3,4},title.AttributeValue.Select(attr=>attr.Value));
        Assert.Throws<ArgumentException>(()=>LegacyNativeProfessionTable.Load()); Assert.Same(benefit,LegacyNativeProfessionTable.Benefits[1001]);
    });

    [Fact]
    public Task ProfessionBadLevelRetainsBenefitsAndPreventsTitlePhaseWithoutSkippingMalformedInput() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); using var files=new TableFiles();
        files.Write("prof_lev_benefit.txt","99@@1001@@12@@60011@@70012@@40013@@10014@@500");
        files.Write("prof_lev_up.txt","77@@1001@@256@@123@@456@@789");
        files.Write("prof_title_benefit.txt","88@@4@@5@@10001@@20002@@30003@@40004");
        Assert.Throws<OverflowException>(()=>LegacyNativeProfessionTable.Load());
        Assert.Single(LegacyNativeProfessionTable.Benefits); Assert.Empty(LegacyNativeProfessionTable.ProfLevelUPList); Assert.Empty(LegacyNativeProfessionTable.TitleBenefits);
    });

    [Fact]
    public Task RefineLoadParsesAllThreeOwnersLiteralMixedDelimitersAndNonzeroEffectOrder() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); using var files=new TableFiles();
        files.Write("item_refine_upgrade.txt","0 ignored\r\n1@@7 123");
        files.Write("item_refine_effect_ex.txt","65535@@720027 9@@10 11");
        files.Write("item_refine_effect.txt","12@@100 200@@65537 65538@@0 7 0 8 7");
        LegacyNativeItemRefineUpgrade.Load(); Assert.Equal(123u,LegacyNativeItemRefineUpgrade.ProgresUpdates[7]);
        var ex=LegacyNativeItemRefineUpgrade.EffectsEX[65535]; Assert.Equal(65535u,ex.ID); Assert.Equal(720027u,ex.ItemID);
        Assert.Equal(9u,ex.StarReq); Assert.Equal(10u,ex.AttributeNum); Assert.Equal(11u,ex.Value);
        var effect=LegacyNativeItemRefineUpgrade.Effects[12]; Assert.Equal((ushort)12,effect.Stars); Assert.Equal(100u,effect.PAttack); Assert.Equal(200u,effect.MAttack);
        Assert.Equal(65537u,effect.PDefense); Assert.Equal(65538u,effect.MDefense); Assert.Equal(new uint[]{7,8,7},effect.Effects);
    });

    [Fact]
    public Task RefineLoadMissingSecondFilePreservesFirstOwnerWithoutCreatingLaterRows() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); using var files=new TableFiles(); files.Write("item_refine_upgrade.txt","1 7 123");
        Assert.Throws<FileNotFoundException>(()=>LegacyNativeItemRefineUpgrade.Load());
        Assert.Equal(123u,LegacyNativeItemRefineUpgrade.ProgresUpdates[7]); Assert.Empty(LegacyNativeItemRefineUpgrade.EffectsEX); Assert.Empty(LegacyNativeItemRefineUpgrade.Effects);
        files.Write("item_refine_upgrade.txt","1 8 456"); files.Write("item_refine_effect_ex.txt","65536 1 2 3 4");
        Assert.Throws<OverflowException>(()=>LegacyNativeItemRefineUpgrade.Load());
        Assert.Equal(2,LegacyNativeItemRefineUpgrade.ProgresUpdates.Count); Assert.Empty(LegacyNativeItemRefineUpgrade.EffectsEX);
    });

    [Fact]
    public Task SpecialEffectLoadReplacesOnlyOnExistingFileAndPreservesNativeCaughtErrorPrefix() => NativePacketTestThread.Run(()=>
    {
        using var s=new Scope(); using var files=new TableFiles();
        var old=LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute; old[99]=new(){ID=99};
        TextWriter previous=Console.Out; using var output=new StringWriter();
        try
        {
            Console.SetOut(output); LegacyNativeSpecialEffectAttributeTable.Load(); Assert.Same(old,LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute);
            Assert.Contains("!File.Exists:",output.ToString()); output.GetStringBuilder().Clear();
            files.Write("speffect_attribute.txt","7@@1@@11@@12@@13@@14@@15@@16\r\n8@@bad@@1@@2@@3@@4@@5@@6");
            LegacyNativeSpecialEffectAttributeTable.Load();
            Assert.NotSame(old,LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute); var row=Assert.Single(LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute).Value;
            Assert.Equal(7u,row.ID); Assert.Equal(1u,row.Type); Assert.Equal(11u,row.value); Assert.Equal(12u,row.Score); Assert.Equal(13u,row.Exp); Assert.Equal(14u,row.Points); Assert.Equal(15u,row.uk6); Assert.Equal(16u,row.uk7);
            Assert.Contains(nameof(FormatException),output.ToString()); Assert.Same(old[99],Assert.Single(old).Value);
        }
        finally{Console.SetOut(previous);}
    });
}
