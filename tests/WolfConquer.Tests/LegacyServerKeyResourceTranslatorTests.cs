using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyServerKeyResourceTranslatorTests
{
    [Fact]
    public void TranslatesMatchedServerKeyResourcesWithoutChangingLuaStructure()
    {
        var serverKeyText =
            "local Text = {\r\n" +
            "\t[\"STR_NPC_ID_1\"] = \"\u6742\u8d27\u5e97\u8001\u677f\",\r\n" +
            "\t[\"STR_MONSTER_ID_7\"] = \"\u571f\u532a\",\r\n" +
            "\t[\"STR_MAGIC_TYPE_1000\"] = \"\u5929\u96f7\u843d\",\r\n" +
            "\t[\"STR_MAP_ID_1002\"] = \"\u6e05\u98ce\u539f\",\r\n" +
            "\t[\"STR_NPC_ID_999999\"] = \"\u672a\u77e5NPC\",\r\n" +
            "\t[\"STR_ITEM_TYPE_50000\"] = \"\u7a7f\u4e91\u7bad\",\r\n" +
            "}\r\n";

        var result = LegacyServerKeyResourceTranslator.Translate(
            serverKeyText,
            [
                Catalog("STR_NPC_ID_", [(1, "Grocery Store Owner")]),
                Catalog("STR_MONSTER_ID_", [(7, "Bandit")]),
                Catalog("STR_MAGIC_TYPE_", [(1000, "Thunder")]),
                Catalog("STR_MAP_ID_", [(1002, "Central Plain")]),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(5, result.KeyCount);
        Assert.Equal(4, result.TranslatedKeys);
        Assert.Equal(1, result.UnmatchedKeys);
        Assert.Equal(4, result.ChangedKeys);
        Assert.Contains("\t[\"STR_NPC_ID_1\"] = \"Grocery Store Owner\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_MONSTER_ID_7\"] = \"Bandit\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_MAGIC_TYPE_1000\"] = \"Thunder\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_MAP_ID_1002\"] = \"Central Plain\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_NPC_ID_999999\"] = \"\u672a\u77e5NPC\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_ITEM_TYPE_50000\"] = \"\u7a7f\u4e91\u7bad\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ReapplyingTranslatedServerKeyResourcesIsIdempotent()
    {
        var serverKeyText =
            "local Text = {\r\n" +
            "\t[\"STR_MAP_ID_1002\"] = \"Central Plain\",\r\n" +
            "}\r\n";

        var result = LegacyServerKeyResourceTranslator.Translate(
            serverKeyText,
            [Catalog("STR_MAP_ID_", [(1002, "Central Plain")])],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.KeyCount);
        Assert.Equal(1, result.TranslatedKeys);
        Assert.Equal(0, result.ChangedKeys);
        Assert.Equal(serverKeyText, updatedText);
    }

    [Fact]
    public void SkipsReferenceNamesThatStillContainChinese()
    {
        var serverKeyText = "\t[\"STR_MONSTER_ID_7\"] = \"\u571f\u532a\",\r\n";

        var result = LegacyServerKeyResourceTranslator.Translate(
            serverKeyText,
            [Catalog("STR_MONSTER_ID_", [(7, "\u571f\u532a")])],
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(1, result.KeyCount);
        Assert.Equal(0, result.TranslatedKeys);
        Assert.Equal(1, result.UnmatchedKeys);
        Assert.Equal(serverKeyText, updatedText);
    }

    [Fact]
    public void TranslatesNamedServerKeyResources()
    {
        var serverKeyText =
            "\t[\"STR_OFFICIAL_TYPE_KING\"] = \"\u56fd\u738b\",\r\n" +
            "\t[\"STR_OFFICIAL_TYPE_UNKNOWN\"] = \"\u672a\u77e5\",\r\n";

        var result = LegacyServerKeyResourceTranslator.Translate(
            serverKeyText,
            [],
            [new LegacyServerKeyNamedResourceCatalog(new Dictionary<string, string> { ["STR_OFFICIAL_TYPE_KING"] = "King" })],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(1, result.KeyCount);
        Assert.Equal(1, result.TranslatedKeys);
        Assert.Equal(1, result.ChangedKeys);
        Assert.Contains("\t[\"STR_OFFICIAL_TYPE_KING\"] = \"King\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_OFFICIAL_TYPE_UNKNOWN\"] = \"\u672a\u77e5\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceOnlyCjkCatalogsPreserveDifferentEnglishNames()
    {
        var serverKeyText =
            "\t[\"STR_MAP_ID_1002\"] = \"Existing English\",\r\n" +
            "\t[\"STR_MAP_ID_1003\"] = \"\u6e05\u98ce\u539f\",\r\n" +
            "\t[\"STR_OFFICIAL_TYPE_KING\"] = \"Existing King\",\r\n" +
            "\t[\"STR_OFFICIAL_TYPE_QUEEN\"] = \"\u738b\u540e\",\r\n";

        var result = LegacyServerKeyResourceTranslator.Translate(
            serverKeyText,
            [Catalog("STR_MAP_ID_", [(1002, "Central Plain"), (1003, "Mine Cave")], replaceOnlyCjk: true)],
            [
                new LegacyServerKeyNamedResourceCatalog(
                    new Dictionary<string, string>
                    {
                        ["STR_OFFICIAL_TYPE_KING"] = "King",
                        ["STR_OFFICIAL_TYPE_QUEEN"] = "Queen",
                    },
                    ReplaceOnlyCjk: true),
            ],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(4, result.KeyCount);
        Assert.Equal(2, result.TranslatedKeys);
        Assert.Equal(2, result.UnmatchedKeys);
        Assert.Equal(2, result.ChangedKeys);
        Assert.Contains("\t[\"STR_MAP_ID_1002\"] = \"Existing English\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_MAP_ID_1003\"] = \"Mine Cave\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_OFFICIAL_TYPE_KING\"] = \"Existing King\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_OFFICIAL_TYPE_QUEEN\"] = \"Queen\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslatesTrapResourceKeysWithGenericTrapFallback()
    {
        var serverKeyText =
            "\t[\"STR_TRAP_ID_1310\"] = \"\u91cd\u751f\u83b2\u82b1\",\r\n" +
            "\t[\"STR_TRAP_ID_319\"] = \"trap\",\r\n" +
            "\t[\"STR_TRAP_ID_999999\"] = \"\u672a\u77e5\u9677\u9631\",\r\n";

        var result = LegacyServerKeyResourceTranslator.Translate(
            serverKeyText,
            [Catalog("STR_TRAP_ID_", [(1310, "trap"), (319, "trap")])],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.KeyCount);
        Assert.Equal(2, result.TranslatedKeys);
        Assert.Equal(1, result.UnmatchedKeys);
        Assert.Equal(1, result.ChangedKeys);
        Assert.Contains("\t[\"STR_TRAP_ID_1310\"] = \"trap\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_TRAP_ID_319\"] = \"trap\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_TRAP_ID_999999\"] = \"\u672a\u77e5\u9677\u9631\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslatesShootMonsterKeysFromMonsterCatalogAndSkipsMissingSources()
    {
        var serverKeyText =
            "\t[\"STR_SHOOT_MONSTER_105\"] = \"\u9524\u5c71\u602a\",\r\n" +
            "\t[\"STR_SHOOT_MONSTER_106\"] = \"\u672a\u77e5\u602a\",\r\n" +
            "\t[\"STR_SHOOT_AWARD_TYPE_1\"] = \"\u94f6\u4e24\",\r\n";

        var result = LegacyServerKeyResourceTranslator.Translate(
            serverKeyText,
            [Catalog("STR_MONSTER_ID_", [(105, "RockMonsterL17")])],
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.KeyCount);
        Assert.Equal(1, result.TranslatedKeys);
        Assert.Equal(1, result.UnmatchedKeys);
        Assert.Equal(1, result.ChangedKeys);
        Assert.Contains("\t[\"STR_SHOOT_MONSTER_105\"] = \"RockMonsterL17\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_SHOOT_MONSTER_106\"] = \"\u672a\u77e5\u602a\",\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("\t[\"STR_SHOOT_AWARD_TYPE_1\"] = \"\u94f6\u4e24\",\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void QuestAnchorNpcFallbackTranslatesOnlyUnambiguousChineseNpcResources()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dbServerPath = Path.Combine(tempRoot, "DBServer");
            Directory.CreateDirectory(dbServerPath);
            var questInfoPath = Path.Combine(dbServerPath, "Questinfo.ini");
            File.WriteAllText(
                questInfoPath,
                "[100]\r\n" +
                "BeginNpcId=390,1002,332,547,MarriageStone,TwinCity\r\n" +
                "FinishNpcId=391,1002,333,548,FirstName,TwinCity\r\n" +
                "BeginNpcId_2=391,1002,333,548,OtherName,TwinCity\r\n" +
                "FinishNpcId=392,1002,334,549,\u59fb\u7f18\u77f3,TwinCity\r\n" +
                "BeginNpcId=393,1002,335,550,QuestOnly,TwinCity\r\n" +
                "FinishNpcId=394,1002,336,551,FallbackName,TwinCity\r\n" +
                "BeginNpcId_3=\"395,1002,337,552,QuotedAnchor,TwinCity\"\r\n");

            var serverKeyText =
                "\t[\"STR_NPC_ID_390\"] = \"\u59fb\u7f18\u77f3\",\r\n" +
                "\t[\"STR_NPC_ID_391\"] = \"\u51b2\u7a81NPC\",\r\n" +
                "\t[\"STR_NPC_ID_392\"] = \"\u4e2d\u6587NPC\",\r\n" +
                "\t[\"STR_NPC_ID_393\"] = \"Already English\",\r\n" +
                "\t[\"STR_NPC_ID_394\"] = \"\u5f3a\u6765\u6e90NPC\",\r\n" +
                "\t[\"STR_NPC_ID_395\"] = \"\u5f15\u53f7NPC\",\r\n";

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildQuestAnchorNpcFallbackCatalog([questInfoPath]);
            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [
                    Catalog("STR_NPC_ID_", [(394, "Strong Catalog Name")]),
                    fallbackCatalog,
                ],
                out var updatedText);

            Assert.True(result.Changed);
            Assert.Equal("MarriageStone", fallbackCatalog.Names[390]);
            Assert.Equal("QuotedAnchor", fallbackCatalog.Names[395]);
            Assert.False(fallbackCatalog.Names.ContainsKey(391));
            Assert.False(fallbackCatalog.Names.ContainsKey(392));
            Assert.Contains("\t[\"STR_NPC_ID_390\"] = \"MarriageStone\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_391\"] = \"\u51b2\u7a81NPC\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_392\"] = \"\u4e2d\u6587NPC\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_393\"] = \"Already English\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_394\"] = \"Strong Catalog Name\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_395\"] = \"QuotedAnchor\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void QuestLinkedNpcFallbackTranslatesOnlyUnambiguousChineseNpcResources()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dbServerPath = Path.Combine(tempRoot, "DBServer");
            Directory.CreateDirectory(dbServerPath);
            var questInfoPath = Path.Combine(dbServerPath, "Questinfo.ini");
            File.WriteAllText(
                questInfoPath,
                "[100]\r\n" +
                "Intention1=Ask [npc BianQing,4484,1002,425,331] in Twin City.\r\n" +
                "Intention2=Find [npc DailyQuestEnvoy,22050,1002,344,442][npc CharacterNameEditor,7935,1002,303,404]\r\n" +
                "Intention3=Talk to [npc YeSheng,4569,1015,324,400].\r\n" +
                "Intention4=Talk to [npc OtherName,4569,1015,324,400].\r\n" +
                "Intention5=Deliver the water to [npc \u9a7b\u519b\u504f\u5c06,8442,1000,490,328]\r\n" +
                "Intention6=Speak with [npc SkillTeamPKManager,8592,1002,313,141] in Twin City.\r\n" +
                "Intention7=Exchange from [npc MammonEnvoy,9623,3070,0048,0069] for rewards.\r\n");

            var serverKeyText =
                "\t[\"STR_NPC_ID_4484\"] = \"\u6241\u9752\",\r\n" +
                "\t[\"STR_NPC_ID_22050\"] = \"\u65e5\u5e38\u4efb\u52a1\u5927\u4f7f\",\r\n" +
                "\t[\"STR_NPC_ID_7935\"] = \"\u6237\u7c4d\u5b98\u5458\",\r\n" +
                "\t[\"STR_NPC_ID_4569\"] = \"\u591c\u91cc\u9f20\u53f6\u80dc\",\r\n" +
                "\t[\"STR_NPC_ID_8442\"] = \"\u9a7b\u519b\u504f\u5c06\",\r\n" +
                "\t[\"STR_NPC_ID_8592\"] = \"\u7ec4\u961f\u5927\u4f17PK\u8d5b\u5165\u573a\u4e13\u5458\",\r\n" +
                "\t[\"STR_NPC_ID_9623\"] = \"Already English\",\r\n";

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildQuestLinkedNpcFallbackCatalog([questInfoPath]);
            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [
                    Catalog("STR_NPC_ID_", [(8592, "Strong Catalog Name")]),
                    fallbackCatalog,
                ],
                out var updatedText);

            Assert.True(result.Changed);
            Assert.Equal("BianQing", fallbackCatalog.Names[4484]);
            Assert.Equal("DailyQuestEnvoy", fallbackCatalog.Names[22050]);
            Assert.Equal("CharacterNameEditor", fallbackCatalog.Names[7935]);
            Assert.False(fallbackCatalog.Names.ContainsKey(4569));
            Assert.False(fallbackCatalog.Names.ContainsKey(8442));
            Assert.Contains("\t[\"STR_NPC_ID_4484\"] = \"BianQing\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_22050\"] = \"DailyQuestEnvoy\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_7935\"] = \"CharacterNameEditor\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_4569\"] = \"\u591c\u91cc\u9f20\u53f6\u80dc\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_8442\"] = \"\u9a7b\u519b\u504f\u5c06\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_8592\"] = \"Strong Catalog Name\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_9623\"] = \"Already English\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void NpcIdEnumFallbackTranslatesOnlyConsistentChineseNpcResources()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dbServerPath = Path.Combine(tempRoot, "lqtxfwd", "GameServer", "DBServer");
            var sourcePath = Path.Combine(
                tempRoot,
                "lqtxfwd",
                "DecompiledSource",
                "GameServer",
                "MrRayzo.Game.MsgNpc");
            Directory.CreateDirectory(dbServerPath);
            Directory.CreateDirectory(sourcePath);
            var questInfoPath = Path.Combine(dbServerPath, "Questinfo.ini");
            File.WriteAllText(questInfoPath, "[100]\r\n");
            File.WriteAllText(
                Path.Combine(sourcePath, "NpcID.cs"),
                "namespace Source;\r\n" +
                "public enum NpcID : uint\r\n" +
                "{\r\n" +
                "\tPromotionWarrior = 10001u,\r\n" +
                "\tTowerofMysteryConductor = 19231u,\r\n" +
                "\tDuplicateOne = 7762u,\r\n" +
                "\tDuplicateTwo = 7762u,\r\n" +
                "\tTooLarge = 4294967294u,\r\n" +
                "\t\u4e2d\u6587 = 20000u,\r\n" +
                "}\r\n");

            var serverKeyText =
                "\t[\"STR_NPC_ID_10001\"] = \"\u6f47\u6e58\u6218\u795e\",\r\n" +
                "\t[\"STR_NPC_ID_19231\"] = \"Already English\",\r\n" +
                "\t[\"STR_NPC_ID_7762\"] = \"\u4ed3\u5e93\u8001\u677f\",\r\n";

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildQuestLinkedNpcFallbackCatalog([questInfoPath]);
            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [fallbackCatalog],
                out var updatedText);

            Assert.True(result.Changed);
            Assert.Equal("PromotionWarrior", fallbackCatalog.Names[10001]);
            Assert.Equal("TowerofMysteryConductor", fallbackCatalog.Names[19231]);
            Assert.False(fallbackCatalog.Names.ContainsKey(7762));
            Assert.False(fallbackCatalog.Names.ContainsKey(20000));
            Assert.Contains("\t[\"STR_NPC_ID_10001\"] = \"PromotionWarrior\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_19231\"] = \"Already English\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_7762\"] = \"\u4ed3\u5e93\u8001\u677f\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void NpcIdEnumFallbackSkipsQuestSourceConflicts()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dbServerPath = Path.Combine(tempRoot, "lqtxfwd", "GameServer", "DBServer");
            var sourcePath = Path.Combine(
                tempRoot,
                "lqtxfwd",
                "DecompiledSource",
                "GameServer",
                "MrRayzo.Game.MsgNpc");
            Directory.CreateDirectory(dbServerPath);
            Directory.CreateDirectory(sourcePath);
            var questInfoPath = Path.Combine(dbServerPath, "Questinfo.ini");
            File.WriteAllText(
                questInfoPath,
                "[100]\r\n" +
                "Intention1=Speak with [npc QuestName,10001,1002,439,464].\r\n" +
                "Intention2=Speak with [npc MatchedName,10002,1002,439,464].\r\n");
            File.WriteAllText(
                Path.Combine(sourcePath, "NpcID.cs"),
                "namespace Source;\r\n" +
                "public enum NpcID : uint\r\n" +
                "{\r\n" +
                "\tEnumName = 10001u,\r\n" +
                "\tMatchedName = 10002u,\r\n" +
                "}\r\n");

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildQuestLinkedNpcFallbackCatalog([questInfoPath]);

            Assert.False(fallbackCatalog.Names.ContainsKey(10001));
            Assert.Equal("MatchedName", fallbackCatalog.Names[10002]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SpellIdEnumFallbackTranslatesMagicTypeResourcesFromSource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-spells-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dbServerPath = Path.Combine(tempRoot, "lqtxfwd", "GameServer", "DBServer");
            var sourcePath = Path.Combine(
                tempRoot,
                "lqtxfwd",
                "DecompiledSource",
                "GameServer",
                "MrRayzo.Role");
            Directory.CreateDirectory(dbServerPath);
            Directory.CreateDirectory(sourcePath);
            File.WriteAllText(
                Path.Combine(sourcePath, "Flags.cs"),
                "namespace Source;\r\n" +
                "public static class Flags\r\n" +
                "{\r\n" +
                "\tpublic enum SpellID : ushort\r\n" +
                "\t{\r\n" +
                "\t\tWaterShockwave = 16310,\r\n" +
                "\t\tWaterShockwavePassive = 16320\r\n" +
                "\t}\r\n" +
                "}\r\n");

            var serverKeyText =
                "\t[\"STR_MAGIC_TYPE_16310\"] = \"\u6c34\u51b2\u51fb\u6ce2\",\r\n" +
                "\t[\"STR_MAGIC_TYPE_16320\"] = \"\u6c34\u51b2\u51fb\u6ce2\u88ab\u52a8\",\r\n";

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildSpellIdEnumFallbackCatalog([dbServerPath]);
            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [fallbackCatalog],
                out var updatedText);

            Assert.True(result.Changed);
            Assert.Equal("WaterShockwave", fallbackCatalog.Names[16310]);
            Assert.Equal("WaterShockwavePassive", fallbackCatalog.Names[16320]);
            Assert.Equal(2, result.KeyCount);
            Assert.Equal(2, result.TranslatedKeys);
            Assert.Equal(2, result.ChangedKeys);
            Assert.Contains("\t[\"STR_MAGIC_TYPE_16310\"] = \"WaterShockwave\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_MAGIC_TYPE_16320\"] = \"WaterShockwavePassive\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SpellIdEnumFallbackSkipsConflictsAndPreservesAlreadyEnglishMagicRows()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-spells-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            var firstPath = Path.Combine(tempRoot, "FirstFlags.cs");
            var secondPath = Path.Combine(tempRoot, "SecondFlags.cs");
            File.WriteAllText(
                firstPath,
                "namespace Source;\r\n" +
                "public enum SpellID : ushort\r\n" +
                "{\r\n" +
                "\tFirstConflict = 17000,\r\n" +
                "\tStableSpell = 17010,\r\n" +
                "}\r\n");
            File.WriteAllText(
                secondPath,
                "namespace Source;\r\n" +
                "public enum SpellID : ushort\r\n" +
                "{\r\n" +
                "\tSecondConflict = 17000,\r\n" +
                "\tStableSpell = 17010,\r\n" +
                "}\r\n");

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildSpellIdEnumFallbackCatalog(
                [firstPath, secondPath]);
            var serverKeyText =
                "\t[\"STR_MAGIC_TYPE_17000\"] = \"\u51b2\u7a81\u6280\u80fd\",\r\n" +
                "\t[\"STR_MAGIC_TYPE_17010\"] = \"StableSpell\",\r\n";

            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [fallbackCatalog],
                out var updatedText);

            Assert.False(result.Changed);
            Assert.False(fallbackCatalog.Names.ContainsKey(17000));
            Assert.Equal("StableSpell", fallbackCatalog.Names[17010]);
            Assert.Equal(2, result.KeyCount);
            Assert.Equal(1, result.TranslatedKeys);
            Assert.Equal(1, result.UnmatchedKeys);
            Assert.Equal(0, result.ChangedKeys);
            Assert.Equal(serverKeyText, updatedText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MapInfoFallbackTranslatesMapResourcesFromDbMaps()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-maps-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dbServerPath = Path.Combine(tempRoot, "GameServer", "DBServer");
            var mapsPath = Path.Combine(dbServerPath, "maps");
            Directory.CreateDirectory(mapsPath);
            File.WriteAllText(
                Path.Combine(mapsPath, "3826.ini"),
                "[info]\r\n" +
                "id=3826\r\n" +
                "name=EvilArray1\r\n");
            File.WriteAllText(
                Path.Combine(mapsPath, "6072.ini"),
                "[info]\r\n" +
                "id=6072\r\n" +
                "name=SubclassCenter\r\n");

            var serverKeyText =
                "\t[\"STR_MAP_ID_3826\"] = \"\u5c01\u9b54\u96351\",\r\n" +
                "\t[\"STR_MAP_ID_6072\"] = \"\u526f\u804c\u4e1a\u4e13\u5c5e\u5927\u5385\",\r\n";

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildMapInfoFallbackCatalog([dbServerPath]);
            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [fallbackCatalog],
                out var updatedText);

            Assert.True(result.Changed);
            Assert.Equal("EvilArray1", fallbackCatalog.Names[3826]);
            Assert.Equal("SubclassCenter", fallbackCatalog.Names[6072]);
            Assert.Equal(2, result.KeyCount);
            Assert.Equal(2, result.TranslatedKeys);
            Assert.Equal(2, result.ChangedKeys);
            Assert.Contains("\t[\"STR_MAP_ID_3826\"] = \"EvilArray1\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_MAP_ID_6072\"] = \"SubclassCenter\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MapInfoFallbackSkipsConflictsAndPreservesAlreadyEnglishMapRows()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-maps-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            var firstPath = Path.Combine(tempRoot, "FirstMap.ini");
            var secondPath = Path.Combine(tempRoot, "SecondMap.ini");
            var stablePath = Path.Combine(tempRoot, "StableMap.ini");
            File.WriteAllText(
                firstPath,
                "[info]\r\n" +
                "id=3826\r\n" +
                "name=FirstMap\r\n");
            File.WriteAllText(
                secondPath,
                "[info]\r\n" +
                "id=3826\r\n" +
                "name=SecondMap\r\n");
            File.WriteAllText(
                stablePath,
                "[info]\r\n" +
                "id=3830\r\n" +
                "name=TwinCity`sNight1\r\n");

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildMapInfoFallbackCatalog(
                [firstPath, secondPath, stablePath]);
            var serverKeyText =
                "\t[\"STR_MAP_ID_3826\"] = \"\u5c01\u9b54\u96351\",\r\n" +
                "\t[\"STR_MAP_ID_3830\"] = \"TwinCity`sNight1\",\r\n";

            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [fallbackCatalog],
                out var updatedText);

            Assert.False(result.Changed);
            Assert.False(fallbackCatalog.Names.ContainsKey(3826));
            Assert.Equal("TwinCity`sNight1", fallbackCatalog.Names[3830]);
            Assert.Equal(2, result.KeyCount);
            Assert.Equal(1, result.TranslatedKeys);
            Assert.Equal(1, result.UnmatchedKeys);
            Assert.Equal(0, result.ChangedKeys);
            Assert.Equal(serverKeyText, updatedText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ClientNpcNameFallbackTranslatesOnlyConsistentChineseNpcResources()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-client-npcs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var iniPath = Path.Combine(tempRoot, "ini");
            Directory.CreateDirectory(iniPath);
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            File.WriteAllBytes(
                Path.Combine(iniPath, "npc.ini"),
                encoding.GetBytes(
                    "[NpcType5000]\r\n" +
                    "Name=Battle God Xiaoxiang\r\n" +
                    "Note=[Skill Mentor]\r\n" +
                    "\r\n" +
                    "[NpcType5001]\r\n" +
                    "Name=First Conflict\r\n" +
                    "\r\n" +
                    "[NpcType5002]\r\n" +
                    "Name=\u4e2d\u6587\u6765\u6e90\r\n" +
                    "\r\n" +
                    "[NpcType5003]\r\n" +
                    "Name=12345\r\n"));
            File.WriteAllBytes(
                Path.Combine(iniPath, "NpcX.ini"),
                encoding.GetBytes(
                    "[5001]\r\n" +
                    "Name=Second Conflict\r\n" +
                    "\r\n" +
                    "[5004]\r\n" +
                    "Name=Divine Craftsman Lu Ban\r\n"));

            var serverKeyText =
                "\t[\"STR_NPC_ID_5000\"] = \"\u6f47\u6e58\u6218\u795e\",\r\n" +
                "\t[\"STR_NPC_ID_5001\"] = \"\u51b2\u7a81NPC\",\r\n" +
                "\t[\"STR_NPC_ID_5002\"] = \"\u4e2d\u6587NPC\",\r\n" +
                "\t[\"STR_NPC_ID_5003\"] = \"\u9519\u8befNPC\",\r\n" +
                "\t[\"STR_NPC_ID_5004\"] = \"Already English\",\r\n";

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildClientNpcNameFallbackCatalog([tempRoot]);
            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [fallbackCatalog],
                out var updatedText);

            Assert.True(result.Changed);
            Assert.Equal("Battle God Xiaoxiang", fallbackCatalog.Names[5000]);
            Assert.Equal("Divine Craftsman Lu Ban", fallbackCatalog.Names[5004]);
            Assert.False(fallbackCatalog.Names.ContainsKey(5001));
            Assert.False(fallbackCatalog.Names.ContainsKey(5002));
            Assert.False(fallbackCatalog.Names.ContainsKey(5003));
            Assert.Equal(5, result.KeyCount);
            Assert.Equal(1, result.TranslatedKeys);
            Assert.Equal(4, result.UnmatchedKeys);
            Assert.Equal(1, result.ChangedKeys);
            Assert.Contains("\t[\"STR_NPC_ID_5000\"] = \"Battle God Xiaoxiang\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_5001\"] = \"\u51b2\u7a81NPC\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_5002\"] = \"\u4e2d\u6587NPC\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_5003\"] = \"\u9519\u8befNPC\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_5004\"] = \"Already English\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExactServerKeyResourceCatalogsTranslateOnlyUnambiguousChineseResources()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-exact-" + Guid.NewGuid().ToString("N"));
        try
        {
            var scriptPath = Path.Combine(tempRoot, "Script");
            var iniPath = Path.Combine(tempRoot, "ini");
            Directory.CreateDirectory(scriptPath);
            Directory.CreateDirectory(iniPath);
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            File.WriteAllBytes(
                Path.Combine(scriptPath, "Server_Key.lua"),
                encoding.GetBytes(
                    "\t[\"STR_NPC_ID_9001\"] = \"ExactNpc\",\r\n" +
                    "\t[\"STR_MONSTER_ID_2001\"] = \"ExactMonster\",\r\n" +
                    "\t[\"STR_MAP_ID_3001\"] = \"Exact Map\",\r\n" +
                    "\t[\"STR_INSTANCE_ID_4001\"] = \"ExactInstance\",\r\n" +
                    "\t[\"STR_MAGIC_TYPE_5001\"] = \"ExactMagic\",\r\n" +
                    "\t[\"STR_ITEM_TYPE_6001\"] = \"ExactItem\",\r\n" +
                    "\t[\"STR_NPC_ID_9002\"] = \"FirstConflict\",\r\n" +
                    "\t[\"STR_NPC_ID_9003\"] = \"\u4e2d\u6587\u6765\u6e90\",\r\n"));
            File.WriteAllBytes(
                Path.Combine(iniPath, "Server_Key.lua"),
                encoding.GetBytes("\t[\"STR_NPC_ID_9002\"] = \"SecondConflict\",\r\n"));

            var catalogs = LegacyServerKeyResourceTranslator.BuildExactServerKeyResourceCatalogs([tempRoot]);
            var npcCatalog = catalogs.Single(catalog => catalog.KeyPrefix == "STR_NPC_ID_");
            var serverKeyText =
                "\t[\"STR_NPC_ID_9001\"] = \"\u4e2d\u6587NPC\",\r\n" +
                "\t[\"STR_MONSTER_ID_2001\"] = \"\u4e2d\u6587\u602a\",\r\n" +
                "\t[\"STR_MAP_ID_3001\"] = \"\u4e2d\u6587\u5730\u56fe\",\r\n" +
                "\t[\"STR_INSTANCE_ID_4001\"] = \"\u4e2d\u6587\u526f\u672c\",\r\n" +
                "\t[\"STR_MAGIC_TYPE_5001\"] = \"\u4e2d\u6587\u6280\u80fd\",\r\n" +
                "\t[\"STR_ITEM_TYPE_6001\"] = \"\u4e2d\u6587\u7269\u54c1\",\r\n" +
                "\t[\"STR_NPC_ID_9002\"] = \"\u51b2\u7a81NPC\",\r\n" +
                "\t[\"STR_NPC_ID_9003\"] = \"\u4e2d\u6587\u6765\u6e90NPC\",\r\n";

            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                catalogs,
                out var updatedText);

            Assert.Equal("ExactNpc", npcCatalog.Names[9001]);
            Assert.False(npcCatalog.Names.ContainsKey(9002));
            Assert.False(npcCatalog.Names.ContainsKey(9003));
            Assert.True(result.Changed);
            Assert.Equal(8, result.KeyCount);
            Assert.Equal(6, result.TranslatedKeys);
            Assert.Equal(2, result.UnmatchedKeys);
            Assert.Equal(6, result.ChangedKeys);
            Assert.Contains("\t[\"STR_NPC_ID_9001\"] = \"ExactNpc\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_MONSTER_ID_2001\"] = \"ExactMonster\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_MAP_ID_3001\"] = \"Exact Map\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_INSTANCE_ID_4001\"] = \"ExactInstance\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_MAGIC_TYPE_5001\"] = \"ExactMagic\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_ITEM_TYPE_6001\"] = \"ExactItem\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_9002\"] = \"\u51b2\u7a81NPC\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_NPC_ID_9003\"] = \"\u4e2d\u6587\u6765\u6e90NPC\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExactServerKeyResourceCatalogsPreserveExistingDifferentEnglishRows()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-exact-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            File.WriteAllBytes(
                Path.Combine(tempRoot, "Server_Key.lua"),
                encoding.GetBytes("\t[\"STR_MAP_ID_3001\"] = \"Exact Map\",\r\n"));

            var catalogs = LegacyServerKeyResourceTranslator.BuildExactServerKeyResourceCatalogs([tempRoot]);
            var serverKeyText = "\t[\"STR_MAP_ID_3001\"] = \"Already English\",\r\n";

            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                catalogs,
                out var updatedText);

            Assert.False(result.Changed);
            Assert.Equal(1, result.KeyCount);
            Assert.Equal(0, result.TranslatedKeys);
            Assert.Equal(1, result.UnmatchedKeys);
            Assert.Equal(serverKeyText, updatedText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SeedMonsterFallbackTranslatesChineseMonsterResourcesFromSource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-monsters-" + Guid.NewGuid().ToString("N"));
        try
        {
            var seedPath = Path.Combine(tempRoot, ".setup_work", "seed-json");
            Directory.CreateDirectory(seedPath);
            File.WriteAllText(
                Path.Combine(seedPath, "monsters.json"),
                "[\r\n" +
                "  { \"id\": 105, \"name\": \"RockMonsterL17\" }\r\n" +
                "]\r\n");

            var serverKeyText =
                "\t[\"STR_MONSTER_ID_105\"] = \"\u9524\u5c71\u602a\",\r\n" +
                "\t[\"STR_SHOOT_MONSTER_105\"] = \"\u9524\u5c71\u602a\",\r\n";

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildSeedMonsterFallbackCatalog([tempRoot]);
            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [fallbackCatalog],
                out var updatedText);

            Assert.True(result.Changed);
            Assert.Equal("RockMonsterL17", fallbackCatalog.Names[105]);
            Assert.Equal(2, result.KeyCount);
            Assert.Equal(2, result.TranslatedKeys);
            Assert.Equal(2, result.ChangedKeys);
            Assert.Contains("\t[\"STR_MONSTER_ID_105\"] = \"RockMonsterL17\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_SHOOT_MONSTER_105\"] = \"RockMonsterL17\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SeedMonsterFallbackSkipsConflictsAndUnsafeSourceNames()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-monsters-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(
                Path.Combine(tempRoot, "monsters.json"),
                "[\r\n" +
                "  { \"id\": 105, \"name\": \"FirstRockMonster\" },\r\n" +
                "  { \"id\": 105, \"name\": \"SecondRockMonster\" },\r\n" +
                "  { \"id\": 106, \"name\": \"\u9524\u5c71\u602a\" },\r\n" +
                "  { \"id\": 107, \"name\": \"Broken\uFFFDMonster\" },\r\n" +
                "  { \"id\": 108, \"name\": \"SafeMonster\" }\r\n" +
                "]\r\n");

            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildSeedMonsterFallbackCatalog([tempRoot]);
            var serverKeyText =
                "\t[\"STR_MONSTER_ID_105\"] = \"\u51b2\u7a81\u602a\",\r\n" +
                "\t[\"STR_MONSTER_ID_106\"] = \"\u4e2d\u6587\u602a\",\r\n" +
                "\t[\"STR_MONSTER_ID_107\"] = \"\u9519\u8bef\u602a\",\r\n" +
                "\t[\"STR_MONSTER_ID_108\"] = \"\u5b89\u5168\u602a\",\r\n";

            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [fallbackCatalog],
                out var updatedText);

            Assert.True(result.Changed);
            Assert.False(fallbackCatalog.Names.ContainsKey(105));
            Assert.False(fallbackCatalog.Names.ContainsKey(106));
            Assert.False(fallbackCatalog.Names.ContainsKey(107));
            Assert.Equal("SafeMonster", fallbackCatalog.Names[108]);
            Assert.Equal(4, result.KeyCount);
            Assert.Equal(1, result.TranslatedKeys);
            Assert.Equal(3, result.UnmatchedKeys);
            Assert.Equal(1, result.ChangedKeys);
            Assert.Contains("\t[\"STR_MONSTER_ID_105\"] = \"\u51b2\u7a81\u602a\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_MONSTER_ID_106\"] = \"\u4e2d\u6587\u602a\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_MONSTER_ID_107\"] = \"\u9519\u8bef\u602a\",\r\n", updatedText, StringComparison.Ordinal);
            Assert.Contains("\t[\"STR_MONSTER_ID_108\"] = \"SafeMonster\",\r\n", updatedText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SeedMonsterFallbackPreservesAlreadyEnglishMonsterRows()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-server-key-monsters-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(
                Path.Combine(tempRoot, "monsters.json"),
                "[{ \"id\": \"105\", \"name\": \"RockMonsterL17\" }]\r\n");

            var serverKeyText = "\t[\"STR_MONSTER_ID_105\"] = \"Existing English\",\r\n";
            var fallbackCatalog = LegacyServerKeyResourceTranslator.BuildSeedMonsterFallbackCatalog([tempRoot]);
            var result = LegacyServerKeyResourceTranslator.Translate(
                serverKeyText,
                [fallbackCatalog],
                out var updatedText);

            Assert.False(result.Changed);
            Assert.Equal("RockMonsterL17", fallbackCatalog.Names[105]);
            Assert.Equal(1, result.KeyCount);
            Assert.Equal(0, result.TranslatedKeys);
            Assert.Equal(1, result.UnmatchedKeys);
            Assert.Equal(serverKeyText, updatedText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static LegacyServerKeyResourceCatalog Catalog(
        string prefix,
        IReadOnlyList<(int Id, string Name)> rows,
        bool replaceOnlyCjk = false)
    {
        return new LegacyServerKeyResourceCatalog(
            prefix,
            rows.ToDictionary(row => row.Id, row => row.Name),
            replaceOnlyCjk);
    }
}
