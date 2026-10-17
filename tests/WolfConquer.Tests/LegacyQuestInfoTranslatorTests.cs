using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyQuestInfoTranslatorTests
{
    [Fact]
    public void TranslatesVisibleQuestTextWhilePreservingStructure()
    {
        var clientText =
            "; header\r\n" +
            "[TaskTab]\r\n" +
            "TabName1=\u4e3b\u7ebf\r\n" +
            "TabId1=1\r\n" +
            "TabIcon1=ini\\Quest.dds\r\n" +
            "\r\n" +
            "// task types\n" +
            "[TaskType]\n" +
            "Num=1\n" +
            "TypeId1=5\n" +
            "TypeName1=\u65e5\u5e38\n" +
            "TaskNameColor=0xffffffff\n" +
            "\r" +
            "[1001]\r" +
            "MissionId=1001\r" +
            "Name = \u65e7\u4efb\u52a1\r" +
            "TaskName=\u65e7\u6807\u9898\r" +
            "Content=\u65e7\u5185\u5bb9\r" +
            "Desc1=\u65e7\u63cf\u8ff0\r" +
            "Text2=\u4fdd\u7559\u6211\r" +
            "Title=999\r" +
            "FileName=quest_zh.ini\r" +
            "Map=1002\r" +
            "Prize=1,2,3\r" +
            "BeginNpcId=100,1002,1,1,\u65e7NPC\r" +
            "UnknownText=\u672a\u77e5\r" +
            "\r\n" +
            "[LocalOnly]\r\n" +
            "Name=\u672c\u5730\u6d3b\u52a8\r\n" +
            "Text1=\u672c\u5730\u63d0\u793a";

        var referenceText =
            "[TaskTab]\r\n" +
            "TabName1=Main Quests\r\n" +
            "TabName2=Side Quests\r\n" +
            "TabId1=999\r\n" +
            "\r\n" +
            "[TaskType]\r\n" +
            "Num=1\r\n" +
            "TypeId1=7\r\n" +
            "TypeName1=Daily Quests\r\n" +
            "TaskNameColor=0xff000000\r\n" +
            "\r\n" +
            "[1001]\r\n" +
            "MissionId=1001\r\n" +
            "Name=Pilgrim's Trial\r\n" +
            "TaskName=Pilgrim Trial\r\n" +
            "Content=Defeat 3 bandits near Twin City.\r\n" +
            "Desc1=Speak with the guard after victory.\r\n" +
            "Text1=Reference text not present in the client.\r\n" +
            "Title=888\r\n" +
            "FileName=quest_en.ini\r\n" +
            "Map=2000\r\n" +
            "Prize=9,9,9\r\n" +
            "BeginNpcId=200,1002,1,1,Guard\r\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);

        Assert.Equal(
            "; header\r\n" +
            "[TaskTab]\r\n" +
            "TabName1=Main Quests\r\n" +
            "TabId1=1\r\n" +
            "TabIcon1=ini\\Quest.dds\r\n" +
            "\r\n" +
            "// task types\n" +
            "[TaskType]\n" +
            "Num=1\n" +
            "TypeId1=5\n" +
            "TypeName1=Daily Quests\n" +
            "TaskNameColor=0xffffffff\n" +
            "\r" +
            "[1001]\r" +
            "MissionId=1001\r" +
            "Name = Pilgrim's Trial\r" +
            "TaskName=Pilgrim Trial\r" +
            "Content=Defeat 3 bandits near Twin City.\r" +
            "Desc1=Speak with the guard after victory.\r" +
            "Text2=\u4fdd\u7559\u6211\r" +
            "Title=999\r" +
            "FileName=quest_zh.ini\r" +
            "Map=1002\r" +
            "Prize=1,2,3\r" +
            "BeginNpcId=100,1002,1,1,\u65e7NPC\r" +
            "UnknownText=\u672a\u77e5\r" +
            "\r\n" +
            "[LocalOnly]\r\n" +
            "Name=\u672c\u5730\u6d3b\u52a8\r\n" +
            "Text1=\u672c\u5730\u63d0\u793a",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(4, result.SectionsScanned);
        Assert.Equal(3, result.MatchedSections);
        Assert.Equal(6, result.TranslatedKeys);
        Assert.Equal(6, result.ChangedKeys);
        Assert.Equal(3, result.UnmatchedKeys);
        Assert.Equal(1, result.UnmatchedSections);
    }

    [Fact]
    public void TranslatesPrizeAndIntentionTextWhilePreservingStructuralRows()
    {
        var clientText =
            "[3001]\n" +
            "MissionId=3001\n" +
            "Map=1002\n" +
            "Color=0xffffffff\n" +
            "Count=3\n" +
            "BeginNpcId=100,1002,1,1\n" +
            "Prize=\u5956\u52b1\uff1a\u53cc\u500d\u7ecf\u9a8c\n" +
            "Intention1=\u627e\u5230\u8001\u5175\n";
        var referenceText =
            "[3001]\n" +
            "MissionId=9999\n" +
            "Map=2000\n" +
            "Color=0xff000000\n" +
            "Count=99\n" +
            "BeginNpcId=999,2000,9,9\n" +
            "Prize=Reward: Double EXP\n" +
            "Intention1=Find the veteran.\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);

        Assert.Equal(
            "[3001]\n" +
            "MissionId=3001\n" +
            "Map=1002\n" +
            "Color=0xffffffff\n" +
            "Count=3\n" +
            "BeginNpcId=100,1002,1,1\n" +
            "Prize=Reward: Double EXP\n" +
            "Intention1=Find the veteran.\n",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(2, result.TranslatedKeys);
        Assert.Equal(2, result.ChangedKeys);
        Assert.Equal(0, result.UnmatchedKeys);
    }

    [Fact]
    public void TranslatesSuffixedIntentionTextFromMatchingReferenceKey()
    {
        var clientText =
            "[3002]\n" +
            "MissionId=3002\n" +
            "Intention1_16=\u56de\u53bb\u627e\u961f\u957f\n";
        var referenceText =
            "[3002]\n" +
            "MissionId=3002\n" +
            "Intention1_16=Return to the captain.\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);

        Assert.Equal(
            "[3002]\n" +
            "MissionId=3002\n" +
            "Intention1_16=Return to the captain.\n",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(1, result.TranslatedKeys);
        Assert.Equal(1, result.ChangedKeys);
        Assert.Equal(0, result.UnmatchedKeys);
    }

    [Fact]
    public void LeavesAlreadyEnglishPrizeAndIntentionTextUnchanged()
    {
        var clientText =
            "[3003]\n" +
            "MissionId=3003\n" +
            "Prize=Reward: Meteor Scroll\n" +
            "Intention1=Talk with the guard.\n" +
            "Intention1_16=Report back to the guard.\n";
        var referenceText = clientText;

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);

        Assert.Equal(clientText, updatedText);
        Assert.False(result.Changed);
        Assert.Equal(3, result.TranslatedKeys);
        Assert.Equal(0, result.ChangedKeys);
        Assert.Equal(0, result.UnmatchedKeys);
    }

    [Fact]
    public void CleansAlreadyEnglishVisibleQuestMojibakeWithoutReference()
    {
        var clientText =
            "[3100]\n" +
            "MissionId=3100\n" +
            "Intention1=Pay a visit to the [npc KungfuGirl,5674,1002,440,371]\u9286?\n" +
            "Content=Nezha\u9225\u6A9A trial isn\u9225\u6A9B over\u9286?\n" +
            "Name=\u5bfb\u627eNPC\u9286?\n" +
            "BeginNpcId=100,1002,1,1,Guard\u9286?\n" +
            "Map=1002\n";
        var referenceText =
            "[3100]\n" +
            "MissionId=3100\n" +
            "Map=2000\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);
        var second = LegacyQuestInfoTranslator.Translate(updatedText, referenceText, out var secondText);

        Assert.Equal(
            "[3100]\n" +
            "MissionId=3100\n" +
            "Intention1=Pay a visit to the [npc KungfuGirl,5674,1002,440,371].\n" +
            "Content=Nezha's trial isn't over.\n" +
            "Name=\u5bfb\u627eNPC\u9286?\n" +
            "BeginNpcId=100,1002,1,1,Guard\u9286?\n" +
            "Map=1002\n",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(2, result.TranslatedKeys);
        Assert.Equal(2, result.ChangedKeys);
        Assert.False(second.Changed);
        Assert.Equal(0, second.ChangedKeys);
        Assert.Equal(updatedText, secondText);
    }

    [Fact]
    public void TranslatesCompoundNpcAnchorsWhenNumericPrefixMatches()
    {
        var clientText =
            "[1001]\n" +
            "MissionId=1001\n" +
            "BeginNpcId=2001,1002,439,464,\u53cc\u9f99\u57ce\u603b\u5175,\u53cc\u9f99\u57ce\n" +
            "FinishNpcId=0004,1002,413,303,\u88c5\u5907\u5e97\u8001\u677f,\u53cc\u9f99\u57ce\n" +
            "[1002]\n" +
            "MissionId=1002\n" +
            "BeginNpcId=9999,1002,413,303,\u88c5\u5907\u5e97\u8001\u677f,\u53cc\u9f99\u57ce\n";
        var referenceText =
            "[1001]\n" +
            "MissionId=1001\n" +
            "BeginNpcId=2001,1002,439,464,TCCaptain,TwinCity\n" +
            "FinishNpcId=4,1002,413,303,Armorer,TwinCity\n" +
            "[1002]\n" +
            "MissionId=1002\n" +
            "BeginNpcId=0004,1002,413,303,Armorer,TwinCity\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);

        Assert.Equal(
            "[1001]\n" +
            "MissionId=1001\n" +
            "BeginNpcId=2001,1002,439,464,TCCaptain,TwinCity\n" +
            "FinishNpcId=0004,1002,413,303,Armorer,TwinCity\n" +
            "[1002]\n" +
            "MissionId=1002\n" +
            "BeginNpcId=9999,1002,413,303,\u88c5\u5907\u5e97\u8001\u677f,\u53cc\u9f99\u57ce\n",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(2, result.TranslatedKeys);
        Assert.Equal(2, result.ChangedKeys);
        Assert.Equal(1, result.UnmatchedKeys);
    }

    [Fact]
    public void TranslatesSuffixedCompoundNpcAnchorsFromMissionIdFallback()
    {
        var clientText =
            "[1291]\n" +
            "MissionId=11469\n" +
            "BeginNpcId_0=2001,1002,439,464,\u53cc\u9f99\u57ce\u603b\u5175,\u6e05\u98ce\u539f\n" +
            "FinishNpcId_4=9999,1000,548,618,\u8ddb\u817f\u8001\u5175,\u4e91\u95e8\u5173\n";
        var referenceText =
            "[1368]\n" +
            "MissionId=11469\n" +
            "BeginNpcId_0=2001,1002,439,464,TCCaptain,WindPlain\n" +
            "FinishNpcId_4=27591,1000,548,618,LameVeteran,Desert\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);

        Assert.Equal(
            "[1291]\n" +
            "MissionId=11469\n" +
            "BeginNpcId_0=2001,1002,439,464,TCCaptain,WindPlain\n" +
            "FinishNpcId_4=9999,1000,548,618,\u8ddb\u817f\u8001\u5175,\u4e91\u95e8\u5173\n",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(1, result.TranslatedKeys);
        Assert.Equal(1, result.ChangedKeys);
        Assert.Equal(1, result.UnmatchedKeys);
    }

    [Fact]
    public void ReusesExactCompoundNpcTupleFromOtherReferenceMissions()
    {
        var clientText =
            "[2000]\n" +
            "MissionId=700\n" +
            "BeginNpcId=0031,1002,416,278,\u5a01\u6b66\u5927\u5c06\u519b,CentralPlain\n" +
            "FinishNpcId=4972,1004,33,41,\u96fe\u5f71\u5c0a\u8005,JobCenter\n";
        var referenceText =
            "[10]\n" +
            "MissionId=9000\n" +
            "BeginNpcId=31,1002,416,278,GeneralBravery,TwinCity\n" +
            "[11]\n" +
            "MissionId=9001\n" +
            "FinishNpcId=4972,1004,33,41,WindSage,JobCenter\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);
        var second = LegacyQuestInfoTranslator.Translate(updatedText, referenceText, out var secondText);

        Assert.Equal(
            "[2000]\n" +
            "MissionId=700\n" +
            "BeginNpcId=0031,1002,416,278,GeneralBravery,TwinCity\n" +
            "FinishNpcId=4972,1004,33,41,WindSage,JobCenter\n",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(2, result.TranslatedKeys);
        Assert.Equal(2, result.ChangedKeys);
        Assert.Equal(0, result.UnmatchedKeys);
        Assert.False(second.Changed);
        Assert.Equal(updatedText, secondText);
    }

    [Fact]
    public void SkipsExactCompoundNpcTupleWhenReferenceTupleIsAmbiguous()
    {
        var clientText =
            "[2000]\n" +
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

        var result = LegacyQuestInfoTranslator.Translate(
            clientText,
            new[] { firstReference, secondReference },
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(clientText, updatedText);
        Assert.Equal(0, result.TranslatedKeys);
        Assert.Equal(0, result.ChangedKeys);
        Assert.Equal(0, result.UnmatchedKeys);
    }

    [Fact]
    public void TranslatesSuffixedCompoundNpcAnchorsFromBaseReferenceWhenNumericPrefixMatches()
    {
        var clientText =
            "[1167]\n" +
            "MissionId=793\n" +
            "BeginNpcId_16=23965,10365,161,260,\u795e\u9f99\u4f7f,\u67ab\u6eaa\u6797\n" +
            "FinishNpcId_16=23965,10365,161,260,\u795e\u9f99\u4f7f,\u67ab\u6eaa\u6797\n" +
            "BeginNpcId_4=23965,10364,161,260,\u795e\u9f99\u4f7f,\u67ab\u6eaa\u6797\n";
        var referenceText =
            "[962]\n" +
            "MissionId=793\n" +
            "BeginNpcId=23965,10365,161,260,DragonEnvoy,MapleForest\n" +
            "FinishNpcId=23965,10365,161,260,DragonEnvoy,MapleForest\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);

        Assert.Equal(
            "[1167]\n" +
            "MissionId=793\n" +
            "BeginNpcId_16=23965,10365,161,260,DragonEnvoy,MapleForest\n" +
            "FinishNpcId_16=23965,10365,161,260,DragonEnvoy,MapleForest\n" +
            "BeginNpcId_4=23965,10364,161,260,\u795e\u9f99\u4f7f,\u67ab\u6eaa\u6797\n",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(2, result.TranslatedKeys);
        Assert.Equal(2, result.ChangedKeys);
        Assert.Equal(1, result.UnmatchedKeys);
    }

    [Fact]
    public void ReplacesEmbeddedNpcAndItemTokensWhenMissionIdTokenPayloadMatches()
    {
        var clientText =
            "[1167]\n" +
            "MissionId=793\n" +
            "Intention1_16=\u524d\u5f80\u53cc\u9f99\u57ce\u796d\u575b\u4f7f\u7528[item_t \u5524\u9f99\u4ee4,10365,161,260]\u53ec\u5524\u90aa\u9f99\n" +
            "Intention2_16=\u4e0e[npc \u795e\u9f99\u4f7f,23965,10365,161,260]\u5bf9\u8bdd\n";
        var referenceText =
            "[962]\n" +
            "MissionId=793\n" +
            "Intention1=Use the [item_t DragonToken,10365,161,260] to call out the Wicked Dragon.\n" +
            "Intention2=Talk with [npc DragonEnvoy,23965,10365,161,260].\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);
        var second = LegacyQuestInfoTranslator.Translate(updatedText, referenceText, out var secondText);

        Assert.Equal(
            "[1167]\n" +
            "MissionId=793\n" +
            "Intention1_16=\u524d\u5f80\u53cc\u9f99\u57ce\u796d\u575b\u4f7f\u7528[item_t DragonToken,10365,161,260]\u53ec\u5524\u90aa\u9f99\n" +
            "Intention2_16=\u4e0e[npc DragonEnvoy,23965,10365,161,260]\u5bf9\u8bdd\n",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(2, result.TranslatedKeys);
        Assert.Equal(2, result.ChangedKeys);
        Assert.False(second.Changed);
        Assert.Equal(updatedText, secondText);
    }

    [Fact]
    public void SourceBackedFallbackReplacesEmbeddedItemAndNpcTokensAndRewardLabels()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "WolfConquerQuestInfoFallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            File.WriteAllText(Path.Combine(tempDirectory, "itemtype.txt"), "720001@@DragonToken\n");
            File.WriteAllText(
                Path.Combine(tempDirectory, "Server_Key.lua"),
                "[\"STR_NPC_ID_5673\"] = \"KungfuBoy\", 0\n" +
                "[\"STR_ITEM_TYPE_720002\"] = \"ServerToken\", 0\n");
            var catalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths([tempDirectory]);
            var clientText =
                "[23]\n" +
                "MissionId=607\n" +
                "IntentionDesp=\u4f60\u901a\u8fc7\u4e86[npc \u98de\u4e91\u5b50,5673,1002,432,397]\u7684\u8f7b\u529f\u8003\u6821\uff0c\u5e76\u4f7f\u7528[item_t \u5524\u9f99\u4ee4,720001,0,0]\u3002\n" +
                "Prize=\u7ecf\u9a8c: 100, \u7269\u54c1: [item_t \u5907\u7528\u4ee4,720002,0,0], \u94f6\u4e24: 500\n";
            var referenceText =
                "[607]\n" +
                "MissionId=607\n";

            var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, catalog, out var updatedText);
            var second = LegacyQuestInfoTranslator.Translate(updatedText, referenceText, catalog, out var secondText);

            Assert.Equal(
                "[23]\n" +
                "MissionId=607\n" +
                "IntentionDesp=\u4f60\u901a\u8fc7\u4e86[npc KungfuBoy,5673,1002,432,397]\u7684\u8f7b\u529f\u8003\u6821\uff0c\u5e76\u4f7f\u7528[item_t DragonToken,720001,0,0]\u3002\n" +
                "Prize=EXP: 100, Item: [item_t ServerToken,720002,0,0], Silver: 500\n",
                updatedText);
            Assert.True(result.Changed);
            Assert.Equal(2, result.TranslatedKeys);
            Assert.Equal(2, result.ChangedKeys);
            Assert.False(second.Changed);
            Assert.Equal(updatedText, secondText);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SourceBackedFallbackReplacesMobMapAnchorsFromMapNamesOnlyAfterMonsterLookup()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "WolfConquerQuestInfoMobMapFallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDirectory, "Server_Key.lua"),
                "[\"STR_MONSTER_ID_9001\"] = \"HillBandit\", 0\n");
            File.WriteAllText(
                Path.Combine(tempDirectory, "GameMapEx.ini"),
                "[1015]\n" +
                "Name=BirdIslandWest\n" +
                "[9001]\n" +
                "Name=ShouldNotReplaceMonster\n");
            var catalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths([tempDirectory]);
            var clientText =
                "[1590]\n" +
                "MissionId=1589\n" +
                "Intention2=\u524d\u5f80[mob \u767d\u9e6d\u57ce\u897f\u533a,1015,774,606][chk_quest 1589,0,1,2]\n" +
                "Intention3=\u524d\u5f80[mob \u767d\u9e6d\u57ce\u5357\u533a,2020,693,606][chk_quest 1589,0,1,3]\n" +
                "Intention4=\u51fb\u8d25[mob \u5c71\u8d3c,9001,1002,439,464][chk_quest 1589,0,1,4]\n";
            var referenceText =
                "[1590]\n" +
                "MissionId=1589\n";

            var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, catalog, out var updatedText);
            var second = LegacyQuestInfoTranslator.Translate(updatedText, referenceText, catalog, out var secondText);

            Assert.Equal(
                "[1590]\n" +
                "MissionId=1589\n" +
                "Intention2=\u524d\u5f80[mob BirdIslandWest,1015,774,606][chk_quest 1589,0,1,2]\n" +
                "Intention3=\u524d\u5f80[mob \u767d\u9e6d\u57ce\u5357\u533a,2020,693,606][chk_quest 1589,0,1,3]\n" +
                "Intention4=\u51fb\u8d25[mob HillBandit,9001,1002,439,464][chk_quest 1589,0,1,4]\n",
                updatedText);
            Assert.True(result.Changed);
            Assert.Equal(2, result.TranslatedKeys);
            Assert.Equal(2, result.ChangedKeys);
            Assert.Equal(1, result.UnmatchedKeys);
            Assert.DoesNotContain("ShouldNotReplaceMonster", updatedText, StringComparison.Ordinal);
            Assert.False(second.Changed);
            Assert.Equal(updatedText, secondText);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SourceBackedFallbackReplacesCompoundNpcTrailingNamesWhenIdsAreExact()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "WolfConquerQuestInfoCompoundFallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDirectory, "Server_Key.lua"),
                "[\"STR_NPC_ID_2001\"] = \"TCCaptain\", 0\n" +
                "[\"STR_NPC_ID_4\"] = \"Armorer\", 0\n" +
                "[\"STR_NPC_ID_3000\"] = \"FirstName\", 0\n" +
                "[\"STR_NPC_ID_3000\"] = \"SecondName\", 0\n");
            File.WriteAllText(
                Path.Combine(tempDirectory, "GameMapEx.ini"),
                "[1002]\n" +
                "Name=TwinCity\n");
            var catalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths([tempDirectory]);
            var clientText =
                "[1001]\n" +
                "MissionId=1001\n" +
                "BeginNpcId=2001,1002,439,464,\u53cc\u9f99\u57ce\u603b\u5175,\u53cc\u9f99\u57ce\n" +
                "FinishNpcId=0004,1002,413,303,\u88c5\u5907\u5e97\u8001\u677f,\u53cc\u9f99\u57ce\n" +
                "BeginNpcId_4=3000,1002,413,303,\u51b2\u7a81NPC,\u53cc\u9f99\u57ce\n";
            var referenceText =
                "[1001]\n" +
                "MissionId=1001\n";

            var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, catalog, out var updatedText);
            var second = LegacyQuestInfoTranslator.Translate(updatedText, referenceText, catalog, out var secondText);

            Assert.Equal(
                "[1001]\n" +
                "MissionId=1001\n" +
                "BeginNpcId=2001,1002,439,464,TCCaptain,TwinCity\n" +
                "FinishNpcId=0004,1002,413,303,Armorer,TwinCity\n" +
                "BeginNpcId_4=3000,1002,413,303,\u51b2\u7a81NPC,TwinCity\n",
                updatedText);
            Assert.True(result.Changed);
            Assert.Equal(3, result.TranslatedKeys);
            Assert.Equal(3, result.ChangedKeys);
            Assert.DoesNotContain("FirstName", updatedText, StringComparison.Ordinal);
            Assert.DoesNotContain("SecondName", updatedText, StringComparison.Ordinal);
            Assert.False(second.Changed);
            Assert.Equal(updatedText, secondText);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void SourceBackedFallbackReplacesCheckItemBackedItemTokensAndSimpleRewardItemNames()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "WolfConquerQuestInfoItemAnchorFallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            File.WriteAllText(
                Path.Combine(tempDirectory, "itemtype.txt"),
                "711317@@ColoredBead-Yellow\n" +
                "721128@@BrokenSword\n" +
                "1088001@@Meteor\n" +
                "730002@@Stone\n");
            var catalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths([tempDirectory]);
            var clientText =
                "[23]\n" +
                "MissionId=607\n" +
                "Intention2=\u00b7[item_t \u4e94\u5f69\u795e\u73e0\u00b7\u9ec4,1011,393,041][chk_item 711317,1]\n" +
                "Intention3=\u00b7\u51fb\u8d25\u77f3\u532a\u83b7\u5f97[item_t \u6b8b\u5251,1000,262,341][chk_item 721128,1]\n" +
                "Prize=Item\uff1a  \u6d41\u661f  [item 1088001,0,0,0,0,0,0]\n" +
                "Prize=\u5927\u91cf\u7ecf\u9a8c\u503c\u3001+2\u8d64\u70bc\u77f3\uff08\u8d60\uff09*1[item 730002,0,0,0,3,0,0]\n";
            var referenceText =
                "[607]\n" +
                "MissionId=607\n";

            var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, catalog, out var updatedText);
            var second = LegacyQuestInfoTranslator.Translate(updatedText, referenceText, catalog, out var secondText);

            Assert.Equal(
                "[23]\n" +
                "MissionId=607\n" +
                "Intention2=\u00b7[item_t ColoredBead-Yellow,1011,393,041][chk_item 711317,1]\n" +
                "Intention3=\u00b7\u51fb\u8d25\u77f3\u532a\u83b7\u5f97[item_t BrokenSword,1000,262,341][chk_item 721128,1]\n" +
                "Prize=Item\uff1a  Meteor  [item 1088001,0,0,0,0,0,0]\n" +
                "Prize=\u5927\u91cf\u7ecf\u9a8c\u503c\u3001+2\u8d64\u70bc\u77f3\uff08\u8d60\uff09*1[item 730002,0,0,0,3,0,0]\n",
                updatedText);
            Assert.True(result.Changed);
            Assert.Equal(3, result.TranslatedKeys);
            Assert.Equal(3, result.ChangedKeys);
            Assert.Equal(1, result.UnmatchedKeys);
            Assert.False(second.Changed);
            Assert.Equal(updatedText, secondText);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void UsesMissionIdFallbackWhenSectionIdDiffers()
    {
        var clientText =
            "[21]\n" +
            "MissionId=606\n" +
            "Map=1002\n" +
            "Name=\u65e7\u4efb\u52a1\n" +
            "Content=\u627e\u5230\u53cc\u9f99\u57ce\u603b\u5175\u3002\n" +
            "Intention1=\u53bb\u627e\u603b\u5175\n" +
            "BeginNpcId=2001,1002,439,464,\u53cc\u9f99\u57ce\u603b\u5175,\u53cc\u9f99\u57ce\n" +
            "FinishNpcId=0004,1002,413,303,\u88c5\u5907\u5e97\u8001\u677f,\u53cc\u9f99\u57ce\n";
        var referenceText =
            "[606]\n" +
            "MissionId=606\n" +
            "Map=2000\n" +
            "Name=First Visit\n" +
            "Content=Speak with the Twin City Captain.\n" +
            "Intention1=Pay a visit to the [npc TCCaptain,2001,1002,439,464]\n" +
            "BeginNpcId=2001,1002,439,464,TCCaptain,TwinCity\n" +
            "FinishNpcId=4,1002,413,303,Armorer,TwinCity\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);

        Assert.Equal(
            "[21]\n" +
            "MissionId=606\n" +
            "Map=1002\n" +
            "Name=First Visit\n" +
            "Content=Speak with the Twin City Captain.\n" +
            "Intention1=Pay a visit to the [npc TCCaptain,2001,1002,439,464]\n" +
            "BeginNpcId=2001,1002,439,464,TCCaptain,TwinCity\n" +
            "FinishNpcId=0004,1002,413,303,Armorer,TwinCity\n",
            updatedText);
        Assert.True(result.Changed);
        Assert.Equal(5, result.TranslatedKeys);
        Assert.Equal(5, result.ChangedKeys);
        Assert.Equal(0, result.MatchedSections);
        Assert.Equal(1, result.UnmatchedSections);
    }

    [Fact]
    public void MissionIdFallbackSkipsAmbiguousTextAndMismatchedNpcAnchors()
    {
        var clientText =
            "[21]\n" +
            "MissionId=606\n" +
            "Content=\u627e\u5230\u603b\u5175\u3002\n" +
            "BeginNpcId=9999,1002,439,464,\u53cc\u9f99\u57ce\u603b\u5175,\u53cc\u9f99\u57ce\n";
        var firstReference =
            "[606]\n" +
            "MissionId=606\n" +
            "Content=Speak with the Twin City Captain.\n" +
            "BeginNpcId=2001,1002,439,464,TCCaptain,TwinCity\n";
        var secondReference =
            "[1606]\n" +
            "MissionId=606\n" +
            "Content=Talk to the Captain in Twin City.\n" +
            "BeginNpcId=2001,1002,439,464,TwinCityCaptain,TwinCity\n";

        var result = LegacyQuestInfoTranslator.Translate(
            clientText,
            new[] { firstReference, secondReference },
            out var updatedText);

        Assert.Equal(clientText, updatedText);
        Assert.False(result.Changed);
        Assert.Equal(0, result.TranslatedKeys);
        Assert.Equal(0, result.ChangedKeys);
        Assert.Equal(2, result.UnmatchedKeys);
    }

    [Fact]
    public void MissionIdFallbackDoesNotRewriteAlreadyEnglishTargetText()
    {
        var clientText =
            "[21]\n" +
            "MissionId=606\n" +
            "Content=Already reviewed English content.\n";
        var referenceText =
            "[606]\n" +
            "MissionId=606\n" +
            "Content=Different source-backed English content.\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);

        Assert.Equal(clientText, updatedText);
        Assert.False(result.Changed);
        Assert.Equal(0, result.TranslatedKeys);
        Assert.Equal(0, result.ChangedKeys);
    }

    [Fact]
    public void TranslatingAlreadyTranslatedQuestInfoIsIdempotent()
    {
        var clientText =
            "[TaskTab]\r\n" +
            "TabName1=\u4e3b\u7ebf\r\n" +
            "[TaskType]\r\n" +
            "TypeName1=\u65e5\u5e38\r\n" +
            "[2000]\r\n" +
            "Name=\u65e7\u4efb\u52a1\r\n" +
            "Content=\u65e7\u5185\u5bb9\r\n";
        var referenceText =
            "[TaskTab]\r\n" +
            "TabName1=Main Quests\r\n" +
            "[TaskType]\r\n" +
            "TypeName1=Daily Quests\r\n" +
            "[2000]\r\n" +
            "Name=Lost Supplies\r\n" +
            "Content=Recover the supplies near the bridge.\r\n";

        var first = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var firstText);
        var second = LegacyQuestInfoTranslator.Translate(firstText, referenceText, out var secondText);

        Assert.True(first.Changed);
        Assert.Equal(4, first.ChangedKeys);
        Assert.False(second.Changed);
        Assert.Equal(0, second.ChangedKeys);
        Assert.Equal(first.TranslatedKeys, second.TranslatedKeys);
        Assert.Equal(firstText, secondText);
    }

    [Fact]
    public void TranslatesKnownTaskTabLabelsWhenReferenceDoesNotHaveClientOnlySection()
    {
        var clientText =
            "[TaskTab]\r\n" +
            "Num=7\r\n" +
            "TabName0=\u5168\u90e8\r\n" +
            "TabName1=\u4e3b\u7ebf\r\n" +
            "TabName2=\u652f\u7ebf\r\n" +
            "TabName3=\u65e5\u5e38\r\n" +
            "TabName4=\u8de8\u670d\r\n" +
            "TabName5=\u6d3b\u52a8\r\n" +
            "TabName6=\u5468\u5e38\r\n" +
            "TabId6=6\r\n";
        var referenceText =
            "[TaskType]\r\n" +
            "TypeName1=Daily Quests\r\n";

        var result = LegacyQuestInfoTranslator.Translate(clientText, referenceText, out var updatedText);
        var second = LegacyQuestInfoTranslator.Translate(updatedText, referenceText, out var secondText);

        Assert.True(result.Changed);
        Assert.Equal(7, result.TranslatedKeys);
        Assert.Equal(7, result.ChangedKeys);
        Assert.Equal(1, result.UnmatchedSections);
        Assert.Contains("TabName0=All\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("TabName1=Main\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("TabName2=Side\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("TabName3=Daily\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("TabName4=Cross-server\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("TabName5=Events\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("TabName6=Weekly\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("TabId6=6\r\n", updatedText, StringComparison.Ordinal);
        Assert.False(second.Changed);
        Assert.Equal(0, second.ChangedKeys);
        Assert.Equal(updatedText, secondText);
    }
}
