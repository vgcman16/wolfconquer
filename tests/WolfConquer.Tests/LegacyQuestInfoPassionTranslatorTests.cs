using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyQuestInfoPassionTranslatorTests
{
    [Fact]
    public void TranslateUsesExactMissionIdFieldsWithoutChangingGameplayValues()
    {
        var target =
            "; passion quests\r\n" +
            "[TaskType]\r\n" +
            "TypeId1=1\r\n" +
            "TypeName1=\u65b0\u624b\u4efb\u52a1\r\n" +
            "TaskNameColor=0xFFCFC058\r\n" +
            "[1]\r\n" +
            "MissionId=9001\r\n" +
            "Name = \u65e7\u4efb\u52a1\r\n" +
            "Content=\uFFFD\u574f\u5185\u5bb9\r\n" +
            "Prize=\u5956\u52b1\r\n" +
            "Intention1=\u627e\u5230\u536b\u5175\r\n" +
            "BeginNpcName=\u8001\u536b\u5175\r\n" +
            "FinishNpcName=Guide\r\n" +
            "BeginNpcId=100,1002,1,1,\u8001\u536b\u5175\r\n" +
            "Amount=\u4e00\u767e\r\n";
        var reference =
            "[TaskType]\r\n" +
            "TypeId1=1\r\n" +
            "TypeName1=Recruit Quest\r\n" +
            "TaskNameColor=0xFFFFFFFF\r\n" +
            "\r\n" +
            "[1]\r\n" +
            "MissionId=1234\r\n" +
            "Name=Wrong section-name match\r\n" +
            "\r\n" +
            "[77]\r\n" +
            "MissionId=9001\r\n" +
            "Name=Pilgrim's Trial\r\n" +
            "Content=Defeat 3 bandits near Twin City.\r\n" +
            "Prize=Silver and experience.\r\n" +
            "Intention1=Find the guard near the bridge.\r\n" +
            "BeginNpcName=Old Guard\r\n" +
            "FinishNpcName=Guard Captain\r\n" +
            "BeginNpcId=200,1002,1,1,Old Guard\r\n" +
            "Amount=One hundred\r\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, out var updated);

        Assert.Equal(
            "; passion quests\r\n" +
            "[TaskType]\r\n" +
            "TypeId1=1\r\n" +
            "TypeName1=Recruit Quest\r\n" +
            "TaskNameColor=0xFFCFC058\r\n" +
            "[1]\r\n" +
            "MissionId=9001\r\n" +
            "Name = Pilgrim's Trial\r\n" +
            "Content=Defeat 3 bandits near Twin City.\r\n" +
            "Prize=Silver and experience.\r\n" +
            "Intention1=Find the guard near the bridge.\r\n" +
            "BeginNpcName=Old Guard\r\n" +
            "FinishNpcName=Guide\r\n" +
            "BeginNpcId=100,1002,1,1,\u8001\u536b\u5175\r\n" +
            "Amount=\u4e00\u767e\r\n",
            updated);
        Assert.True(result.Changed);
        Assert.Equal(3, result.ReferenceSectionsScanned);
        Assert.Equal(2, result.ReferenceMissionIds);
        Assert.Equal(2, result.TargetSectionsScanned);
        Assert.Equal(1, result.TargetSectionsWithMissionId);
        Assert.Equal(7, result.TranslatableFields);
        Assert.Equal(6, result.MatchedFields);
        Assert.Equal(6, result.ChangedFields);
        Assert.Equal(0, result.AmbiguousFields);
        Assert.Equal(1, result.UnmatchedFields);
    }

    [Fact]
    public void TranslateReplacesCompoundNpcNamesOnlyWhenNumericAnchorMatches()
    {
        var target =
            "[1]\n" +
            "MissionId=650\n" +
            "BeginNpcId=2001,1002,439,464,\u53cc\u9f99\u57ce\u603b\u5175,\u53cc\u9f99\u57ce\n" +
            "FinishNpcId=0004,1002,413,303,\u88c5\u5907\u5e97\u8001\u677f,\u53cc\u9f99\u57ce\n" +
            "[2]\n" +
            "MissionId=651\n" +
            "BeginNpcId=9999,1002,413,303,\u88c5\u5907\u5e97\u8001\u677f,\u53cc\u9f99\u57ce\n";
        var reference =
            "[10]\n" +
            "MissionId=650\n" +
            "BeginNpcId=2001,1002,439,464,TCCaptain,TwinCity\n" +
            "FinishNpcId=4,1002,413,303,Armorer,TwinCity\n" +
            "[11]\n" +
            "MissionId=651\n" +
            "BeginNpcId=0004,1002,413,303,Armorer,TwinCity\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, out var updated);

        Assert.Equal(
            "[1]\n" +
            "MissionId=650\n" +
            "BeginNpcId=2001,1002,439,464,TCCaptain,TwinCity\n" +
            "FinishNpcId=0004,1002,413,303,Armorer,TwinCity\n" +
            "[2]\n" +
            "MissionId=651\n" +
            "BeginNpcId=9999,1002,413,303,\u88c5\u5907\u5e97\u8001\u677f,\u53cc\u9f99\u57ce\n",
            updated);
        Assert.True(result.Changed);
        Assert.Equal(3, result.TranslatableFields);
        Assert.Equal(2, result.MatchedFields);
        Assert.Equal(2, result.ChangedFields);
        Assert.Equal(1, result.UnmatchedFields);
    }

    [Fact]
    public void TranslateReusesUniqueCompoundNpcTupleTailsWithoutChangingEnglishTailParts()
    {
        var target =
            "[1]\n" +
            "MissionId=700\n" +
            "BeginNpcId=0031,1002,416,278,\u5a01\u6b66\u5927\u5c06\u519b,CentralPlain\n" +
            "FinishNpcId=20030,10088,126,116,SisterYung,\u94c1\u6247\u95e8\u5c71\u95e8\n" +
            "[2]\n" +
            "MissionId=701\n" +
            "BeginNpcId=2003,1020,0566,0620,\u9ed1\u864e\u57ce\u603b\u5175,Canyon\n";
        var reference =
            "[10]\n" +
            "MissionId=9000\n" +
            "BeginNpcId=31,1002,416,278,GeneralBravery,TwinCity\n" +
            "FinishNpcId=20030,10088,126,116,SisterYung,WindwalkerGate\n" +
            "[11]\n" +
            "MissionId=9001\n" +
            "BeginNpcId=2003,1020,566,620,ACCaptain,ApeCity\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, out var updated);
        var second = LegacyQuestInfoPassionTranslator.Translate(updated, reference, out var secondText);

        Assert.Equal(
            "[1]\n" +
            "MissionId=700\n" +
            "BeginNpcId=0031,1002,416,278,GeneralBravery,CentralPlain\n" +
            "FinishNpcId=20030,10088,126,116,SisterYung,WindwalkerGate\n" +
            "[2]\n" +
            "MissionId=701\n" +
            "BeginNpcId=2003,1020,0566,0620,ACCaptain,Canyon\n",
            updated);
        Assert.True(result.Changed);
        Assert.Equal(3, result.TranslatableFields);
        Assert.Equal(3, result.MatchedFields);
        Assert.Equal(3, result.ChangedFields);
        Assert.Equal(0, result.UnmatchedFields);
        Assert.False(second.Changed);
        Assert.Equal(0, second.ChangedFields);
        Assert.Equal(updated, secondText);
    }

    [Fact]
    public void TranslateSkipsAmbiguousCompoundNpcTupleTails()
    {
        var target =
            "[1]\n" +
            "MissionId=700\n" +
            "BeginNpcId=0031,1002,416,278,\u5a01\u6b66\u5927\u5c06\u519b,CentralPlain\n";
        var firstReference =
            "[10]\n" +
            "MissionId=9000\n" +
            "BeginNpcId=31,1002,416,278,GeneralBravery,TwinCity\n";
        var secondReference =
            "[11]\n" +
            "MissionId=9001\n" +
            "BeginNpcId=31,1002,416,278,MightyGeneral,TwinCity\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(
            target,
            new[] { firstReference, secondReference },
            out var updated);

        Assert.False(result.Changed);
        Assert.Equal(target, updated);
        Assert.Equal(1, result.TranslatableFields);
        Assert.Equal(0, result.MatchedFields);
        Assert.Equal(0, result.ChangedFields);
        Assert.Equal(1, result.UnmatchedFields);
    }

    [Fact]
    public void TranslateUsesSectionMatchedTaskTypeNamesWithoutChangingTaskTypeIds()
    {
        var target =
            "[TaskType]\n" +
            "Num=2\n" +
            "TypeId1=1\n" +
            "TypeName1=\u65b0\u624b\u4efb\u52a1\n" +
            "TaskNameColor=0xFFCFC058\n" +
            "TypeId2=2\n" +
            "TypeName2=\u6559\u7a0b\u4efb\u52a1\n";
        var reference =
            "[TaskType]\n" +
            "Num=12\n" +
            "TypeId1=1\n" +
            "TypeName1=Recruit Quest\n" +
            "TaskNameColor=0xFFFFFFFF\n" +
            "TypeId2=2\n" +
            "TypeName2=Tutorial Quest\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, out var updated);

        Assert.Equal(
            "[TaskType]\n" +
            "Num=2\n" +
            "TypeId1=1\n" +
            "TypeName1=Recruit Quest\n" +
            "TaskNameColor=0xFFCFC058\n" +
            "TypeId2=2\n" +
            "TypeName2=Tutorial Quest\n",
            updated);
        Assert.True(result.Changed);
        Assert.Equal(2, result.TranslatableFields);
        Assert.Equal(2, result.MatchedFields);
        Assert.Equal(2, result.ChangedFields);
    }

    [Fact]
    public void TranslateSkipsAmbiguousReferenceFieldButUsesAgreedFields()
    {
        var target =
            "[10]\n" +
            "MissionId=42\n" +
            "Name=\u65e7\u540d\u5b57\n" +
            "Content=\u65e7\u5185\u5bb9\n";
        var firstReference =
            "[500]\n" +
            "MissionId=42\n" +
            "Name=First Name\n" +
            "Content=Shared English content.\n";
        var secondReference =
            "[600]\n" +
            "MissionId=42\n" +
            "Name=Second Name\n" +
            "Content=Shared English content.\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(
            target,
            new[] { firstReference, secondReference },
            out var updated);

        Assert.Equal(
            "[10]\n" +
            "MissionId=42\n" +
            "Name=\u65e7\u540d\u5b57\n" +
            "Content=Shared English content.\n",
            updated);
        Assert.True(result.Changed);
        Assert.Equal(1, result.AmbiguousReferenceFields);
        Assert.Equal(2, result.TranslatableFields);
        Assert.Equal(1, result.MatchedFields);
        Assert.Equal(1, result.ChangedFields);
        Assert.Equal(1, result.AmbiguousFields);
        Assert.Equal(0, result.UnmatchedFields);
    }

    [Fact]
    public void TranslateUsesExactEnglishAnchorsWhenMissionIdFieldIsAmbiguous()
    {
        var target =
            "[11]\n" +
            "MissionId=706\n" +
            "Name=\u5929\u77f3\u548c\u5929\u77f3\u5546\u5e97\n" +
            "BeginNpcId=0432,1002,305,369,\u73cd\u54c1\u5546\u4eba,\u53cc\u9f99\u57ce\n" +
            "Prize=EXP: 1/12 of an EXP Ball\n" +
            "Intention1=Activate the Exp Potion [mob ExpPotion,1002,319,380][chk_quest 706,1,1,1]\n" +
            "Content=\u5728\u5929\u77f3\u5546\u5e97\u4e2d\u8d2d\u4e70\u73cd\u54c1\u3002\n";
        var wrongSectionReference =
            "[11]\n" +
            "MissionId=706\n" +
            "Name=Quiz Show\n" +
            "BeginNpcId=0432,1002,305,369,QuizHolder,Market\n" +
            "Prize=EXP: 2/15 or 11/60 of an EXP Ball\n" +
            "Intention1=Talk with [npc QuizHolder,6168,1036,316,246]\n" +
            "Content=We hold a Quiz Show every day.\n";
        var anchoredReference =
            "[12]\n" +
            "MissionId=706\n" +
            "Name=CP and Shopping Mall\n" +
            "BeginNpcId=0432,1002,319,380,GreatMerchant,Market\n" +
            "Prize=EXP: 1/12 of an EXP Ball\n" +
            "Intention1=Activate the Exp Potion [mob ExpPotion,1002,319,380][chk_quest 706,1,1,1]\n" +
            "Content=You can purchase useful items from the shopping mall with CPs.\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(
            target,
            new[] { wrongSectionReference, anchoredReference },
            out var updated);
        var second = LegacyQuestInfoPassionTranslator.Translate(
            updated,
            new[] { wrongSectionReference, anchoredReference },
            out var secondText);

        Assert.Equal(
            "[11]\n" +
            "MissionId=706\n" +
            "Name=CP and Shopping Mall\n" +
            "BeginNpcId=0432,1002,305,369,\u73cd\u54c1\u5546\u4eba,\u53cc\u9f99\u57ce\n" +
            "Prize=EXP: 1/12 of an EXP Ball\n" +
            "Intention1=Activate the Exp Potion [mob ExpPotion,1002,319,380][chk_quest 706,1,1,1]\n" +
            "Content=You can purchase useful items from the shopping mall with CPs.\n",
            updated);
        Assert.True(result.Changed);
        Assert.Equal(3, result.TranslatableFields);
        Assert.Equal(2, result.MatchedFields);
        Assert.Equal(2, result.ChangedFields);
        Assert.Equal(0, result.AmbiguousFields);
        Assert.Equal(1, result.UnmatchedFields);
        Assert.False(second.Changed);
        Assert.Equal(0, second.ChangedFields);
        Assert.Equal(updated, secondText);
    }

    [Fact]
    public void TranslateDoesNotUseSingleEnglishAnchorToBreakAmbiguity()
    {
        var target =
            "[20]\n" +
            "MissionId=8800\n" +
            "Name=\u672a\u77e5\u4efb\u52a1\n" +
            "Prize=EXP: 1/12 of an EXP Ball\n";
        var firstReference =
            "[200]\n" +
            "MissionId=8800\n" +
            "Name=First Source Name\n" +
            "Prize=EXP: 1/12 of an EXP Ball\n";
        var secondReference =
            "[201]\n" +
            "MissionId=8800\n" +
            "Name=Second Source Name\n" +
            "Prize=Different reward\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(
            target,
            new[] { firstReference, secondReference },
            out var updated);

        Assert.False(result.Changed);
        Assert.Equal(target, updated);
        Assert.Equal(1, result.TranslatableFields);
        Assert.Equal(0, result.MatchedFields);
        Assert.Equal(0, result.ChangedFields);
        Assert.Equal(1, result.AmbiguousFields);
        Assert.Equal(0, result.UnmatchedFields);
    }

    [Fact]
    public void TranslateUsesUniqueSourceBackedTextAsSecondAnchorInSamePass()
    {
        var target =
            "[121]\n" +
            "MissionId=1306\n" +
            "Name=\u71c3\u7fbd\u8f9f\u90aa\n" +
            "Prize=Some EXP,  1,500 Silver,  [item 720172,0,0,0,0,0,0]\n" +
            "IntentionDesp=\u9ed1\u864e\u57ce\u526f\u57ce\u4e3b\u5e0c\u671b\u4f60\u5c06\u4e00\u6839\u79c3\u9e70\u7fbd\u62ff\u5230\u57ce\u5899\u9876\u7684\u706b\u7089\u5904\u711a\u70e7\u3002\n" +
            "Intention1=\u83b7\u5f97\u79c3\u9e70\u7fbd\u5e76\u711a\u70e7\n" +
            "Content=\u9ed1\u864e\u57ce\u526f\u57ce\u4e3b\u8bf7\u4f60\u53bb\u6d88\u706d\u79c3\u9e70\u3002\n";
        var wrongReference =
            "[121]\n" +
            "MissionId=1306\n" +
            "Name=Chicken or the Egg?\n" +
            "Prize=Some EXP,  1,500 Silver,  [item 720172,0,0,0,0,0,0]\n" +
            "IntentionDesp=Gene and Randy are arguing over a question.\n" +
            "Intention1=\u83b7\u5f97\u79c3\u9e70\u7fbd\u5e76\u711a\u70e7\n" +
            "Content=Gene and Randy need help.\n";
        var anchoredReference =
            "[300]\n" +
            "MissionId=1306\n" +
            "Name=Vulture Feather\n" +
            "Prize=Some EXP,  1,500 Silver,  [item 720172,0,0,0,0,0,0]\n" +
            "IntentionDesp=AC-Castellan asked you to burn a Vulture Feather on the Ape City Wall.\n" +
            "Intention1=Go and get a Vulture Feather and burn it on the [mob ApeCityWall,1020,554,606][chk_quest 1306,1,1,1]\n" +
            "Content=AC-Castellan asked you to get a Vulture Feather and burn it on the Ape City Wall.\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(
            target,
            new[] { wrongReference, anchoredReference },
            out var updated);
        var second = LegacyQuestInfoPassionTranslator.Translate(
            updated,
            new[] { wrongReference, anchoredReference },
            out var secondText);

        Assert.Equal(
            "[121]\n" +
            "MissionId=1306\n" +
            "Name=Vulture Feather\n" +
            "Prize=Some EXP,  1,500 Silver,  [item 720172,0,0,0,0,0,0]\n" +
            "IntentionDesp=AC-Castellan asked you to burn a Vulture Feather on the Ape City Wall.\n" +
            "Intention1=Go and get a Vulture Feather and burn it on the [mob ApeCityWall,1020,554,606][chk_quest 1306,1,1,1]\n" +
            "Content=AC-Castellan asked you to get a Vulture Feather and burn it on the Ape City Wall.\n",
            updated);
        Assert.True(result.Changed);
        Assert.Equal(4, result.TranslatableFields);
        Assert.Equal(4, result.MatchedFields);
        Assert.Equal(4, result.ChangedFields);
        Assert.Equal(0, result.AmbiguousFields);
        Assert.Equal(0, result.UnmatchedFields);
        Assert.False(second.Changed);
        Assert.Equal(0, second.ChangedFields);
        Assert.Equal(updated, secondText);
    }

    [Fact]
    public void TranslateLeavesAlreadyEnglishTargetUnchanged()
    {
        var target =
            "[1]\r\n" +
            "MissionId=5000\r\n" +
            "Name=Already English\r\n" +
            "Content=Talk to the guard.\r\n";
        var reference =
            "[2]\r\n" +
            "MissionId=5000\r\n" +
            "Name=Different Source Name\r\n" +
            "Content=Different source content.\r\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(target, updated);
        Assert.Equal(0, result.TranslatableFields);
        Assert.Equal(0, result.MatchedFields);
        Assert.Equal(0, result.ChangedFields);
        Assert.Equal(0, result.UnmatchedFields);
    }

    [Fact]
    public void TranslateCleansEnglishLikeQuestMojibakeOnlyInVisibleTextFields()
    {
        var target =
            "[1]\n" +
            "MissionId=6100\n" +
            "Name=\u65e7\u540d\u5b57\n" +
            "Content=\u65e7\u5185\u5bb9\n" +
            "Intention1=Pay a visit to the [npc KungfuGirl,5674,1002,440,371]\u9286?\n" +
            "Prize=\u5956\u52b1NPC\u9286?\n" +
            "BeginNpcId=100,1002,1,1,Guide\u9286?\n" +
            "Map=1002\u9286?\n";
        var reference =
            "[2]\n" +
            "MissionId=6100\n" +
            "Name=Nezha\u9225\u6A9A Trial\u9286?\n" +
            "Content=The elder isn\u9225\u6A9B ready\u9286?\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, out var updated);
        var second = LegacyQuestInfoPassionTranslator.Translate(updated, reference, out var secondText);

        Assert.Equal(
            "[1]\n" +
            "MissionId=6100\n" +
            "Name=Nezha's Trial.\n" +
            "Content=The elder isn't ready.\n" +
            "Intention1=Pay a visit to the [npc KungfuGirl,5674,1002,440,371].\n" +
            "Prize=\u5956\u52b1NPC\u9286?\n" +
            "BeginNpcId=100,1002,1,1,Guide\u9286?\n" +
            "Map=1002\u9286?\n",
            updated);
        Assert.True(result.Changed);
        Assert.Equal(5, result.TranslatableFields);
        Assert.Equal(3, result.MatchedFields);
        Assert.Equal(3, result.ChangedFields);
        Assert.Equal(2, result.UnmatchedFields);
        Assert.False(second.Changed);
        Assert.Equal(0, second.ChangedFields);
        Assert.Equal(updated, secondText);
    }

    [Fact]
    public void TranslateReplacesEmbeddedNpcAndItemTokenNamesFromMissionReferences()
    {
        var target =
            "[23]\n" +
            "MissionId=607\n" +
            "IntentionDesp=\u4f60\u901a\u8fc7\u4e86[npc \u98de\u4e91\u5b50,5673,1002,432,397]\u7684\u8f7b\u529f\u8003\u6821\u3002\n" +
            "Intention1=\u4f7f\u7528[item_t \u5524\u9f99\u4ee4,10365,161,260]\u53ec\u5524\u90aa\u9f99\n";
        var reference =
            "[607]\n" +
            "MissionId=607\n" +
            "Content=Report to [npc KungfuBoy,5673,1002,432,397] and use [item_t DragonToken,10365,161,260].\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, out var updated);
        var second = LegacyQuestInfoPassionTranslator.Translate(updated, reference, out var secondText);

        Assert.Equal(
            "[23]\n" +
            "MissionId=607\n" +
            "IntentionDesp=\u4f60\u901a\u8fc7\u4e86[npc KungfuBoy,5673,1002,432,397]\u7684\u8f7b\u529f\u8003\u6821\u3002\n" +
            "Intention1=\u4f7f\u7528[item_t DragonToken,10365,161,260]\u53ec\u5524\u90aa\u9f99\n",
            updated);
        Assert.True(result.Changed);
        Assert.Equal(2, result.TranslatableFields);
        Assert.Equal(2, result.MatchedFields);
        Assert.Equal(2, result.ChangedFields);
        Assert.False(second.Changed);
        Assert.Equal(updated, secondText);
    }

    [Fact]
    public void TranslateUsesSourceBackedFallbacksForTokensCompoundNpcNamesAndRewardLabels()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "WolfConquerQuestInfoPassionFallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            File.WriteAllText(Path.Combine(tempDirectory, "itemtype.txt"), "720001@@DragonToken\n");
            File.WriteAllText(
                Path.Combine(tempDirectory, "NpcID.cs"),
                "public enum NpcID\n" +
                "{\n" +
                "    KungfuGirl = 5674u,\n" +
                "    Armorer = 4u,\n" +
                "}\n");
            File.WriteAllText(
                Path.Combine(tempDirectory, "GameMapEx.ini"),
                "[1002]\n" +
                "Name=TwinCity\n");
            var catalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths([tempDirectory]);
            var target =
                "[23]\n" +
                "MissionId=607\n" +
                "IntentionDesp=\u53bb\u627e[npc \u529f\u592b\u5973\u5b69,5674,1002,440,371]\u5e76\u4f7f\u7528[item_t \u5524\u9f99\u4ee4,720001,0,0]\n" +
                "Prize=\u7ecf\u9a8c: 5; \u94f6\u4e24: 100\n" +
                "FinishNpcId=0004,1002,413,303,\u88c5\u5907\u5e97\u8001\u677f,\u53cc\u9f99\u57ce\n";
            var reference =
                "[607]\n" +
                "MissionId=607\n";

            var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, catalog, out var updated);
            var second = LegacyQuestInfoPassionTranslator.Translate(updated, reference, catalog, out var secondText);

            Assert.Equal(
                "[23]\n" +
                "MissionId=607\n" +
                "IntentionDesp=\u53bb\u627e[npc KungfuGirl,5674,1002,440,371]\u5e76\u4f7f\u7528[item_t DragonToken,720001,0,0]\n" +
                "Prize=EXP: 5; Silver: 100\n" +
                "FinishNpcId=0004,1002,413,303,Armorer,TwinCity\n",
                updated);
            Assert.True(result.Changed);
            Assert.Equal(3, result.TranslatableFields);
            Assert.Equal(3, result.MatchedFields);
            Assert.Equal(3, result.ChangedFields);
            Assert.False(second.Changed);
            Assert.Equal(updated, secondText);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void TranslateUsesSourceBackedCheckItemAndRewardItemAnchors()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "WolfConquerQuestInfoPassionItemAnchorFallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDirectory, "itemtype.txt"),
                "711317@@ColoredBead-Yellow\n" +
                "1088001@@Meteor\n");
            var catalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths([tempDirectory]);
            var target =
                "[23]\n" +
                "MissionId=607\n" +
                "Intention2=\u00b7[item_t \u4e94\u5f69\u795e\u73e0\u00b7\u9ec4,1011,393,041][chk_item 711317,1]\n" +
                "Prize=Item\uff1a  \u6d41\u661f  [item 1088001,0,0,0,0,0,0]\n";
            var reference =
                "[607]\n" +
                "MissionId=607\n";

            var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, catalog, out var updated);
            var second = LegacyQuestInfoPassionTranslator.Translate(updated, reference, catalog, out var secondText);

            Assert.Equal(
                "[23]\n" +
                "MissionId=607\n" +
                "Intention2=\u00b7[item_t ColoredBead-Yellow,1011,393,041][chk_item 711317,1]\n" +
                "Prize=Item\uff1a  Meteor  [item 1088001,0,0,0,0,0,0]\n",
                updated);
            Assert.True(result.Changed);
            Assert.Equal(2, result.TranslatableFields);
            Assert.Equal(2, result.MatchedFields);
            Assert.Equal(2, result.ChangedFields);
            Assert.Equal(0, result.UnmatchedFields);
            Assert.False(second.Changed);
            Assert.Equal(updated, secondText);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void TranslateCountsMissingReferenceFieldAsUnmatched()
    {
        var target =
            "[3]\n" +
            "MissionId=7000\n" +
            "Name=\u672a\u77e5\u4efb\u52a1\n";
        var reference =
            "[4]\n" +
            "MissionId=7001\n" +
            "Name=Other Quest\n";

        var result = LegacyQuestInfoPassionTranslator.Translate(target, reference, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(target, updated);
        Assert.Equal(1, result.TranslatableFields);
        Assert.Equal(0, result.MatchedFields);
        Assert.Equal(0, result.ChangedFields);
        Assert.Equal(0, result.AmbiguousFields);
        Assert.Equal(1, result.UnmatchedFields);
    }
}
