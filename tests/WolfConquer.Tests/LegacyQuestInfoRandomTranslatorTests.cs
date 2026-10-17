using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyQuestInfoRandomTranslatorTests
{
    [Fact]
    public void TranslatesExactSameFormatSectionKeyRowsWhenSourceIsCleanEnglish()
    {
        var target =
            "local RandomDailyInfo = {\r\n" +
            "\t[80] = {\r\n" +
            "\t\tContent=\"\u6740\u602a\u4efb\u52a1\",\r\n" +
            "\t\tIntention1=\"\u6d88\u706d[mob \u571f\u5893\u8760,1001,309,597][chk_quest %d,1,1,%d]\",\r\n" +
            "\t},\r\n" +
            "}\r\n";
        var source =
            "local RandomDailyInfo = {\r\n" +
            "\t[80] = {\r\n" +
            "\t\tContent=\"Kill task\",\r\n" +
            "\t\tIntention1=\"Eliminate [mob TombBat,1001,309,597][chk_quest %d,1,1,%d]\",\r\n" +
            "\t},\r\n" +
            "}\r\n";

        var sourcePath = WriteTempSource("QuestinfoRandom.lua", source);
        try
        {
            var sources = LegacyQuestInfoRandomTranslator.ReadSourceRows(sourcePath);
            var result = LegacyQuestInfoRandomTranslator.Translate(target, sources, out var updated);

            Assert.True(result.Changed);
            Assert.Equal(1, result.TargetSections);
            Assert.Equal(2, result.TargetRows);
            Assert.Equal(2, result.RowsNeedingTranslation);
            Assert.Equal(2, result.SourceMatchedRows);
            Assert.Equal(2, result.SameFormatMatchedRows);
            Assert.Equal(0, result.CompoundNpcMatchedRows);
            Assert.Equal(0, result.AnchorMatchedRows);
            Assert.Equal(2, result.ChangedRows);
            Assert.Equal(0, result.RemainingCjkRows);
            Assert.Contains("\t\tContent=\"Kill task\",\r\n", updated, StringComparison.Ordinal);
            Assert.Contains(
                "\t\tIntention1=\"Eliminate [mob TombBat,1001,309,597][chk_quest %d,1,1,%d]\",\r\n",
                updated,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempSource(sourcePath);
        }
    }

    [Fact]
    public void UsesQuestInfoSourcesForCompoundNpcTuplesAndExactNpcAnchors()
    {
        var target =
            "local RandomDailyInfo = {\n" +
            "\t[80] = {\n" +
            "\t\tBeginNpcId=\"27267,1002,339,465,\u7504\u51e0\u9053,\u53cc\u9f99\u57ce\",\n" +
            "\t\tFinishNpcId=\"27267,1002,339,465,\u7504\u51e0\u9053,\u53cc\u9f99\u57ce\",\n" +
            "\t\tIntention1=\"\u901a\u8fc7[npc \u65e5\u5e38\u4efb\u52a1\u5927\u4f7f,9998,1002,275,467]\u6d88\u706d[mob \u571f\u5893\u8760,1001,309,597][chk_quest %d,1,1,%d]\",\n" +
            "\t\tContent=\"\u6740\u602a\u4efb\u52a1\",\n" +
            "\t},\n" +
            "}\n";
        var questInfoSource =
            "[1]\n" +
            "BeginNpcId=27267,1002,339,465,TaoistZhen,TwinCity\n" +
            "FinishNpcId=9998,1002,275,467,DailyQuestEnvoy,CentralPlain\n" +
            "Intention1=Speak with [npc DailyQuestEnvoy,9998,1002,275,467].\n";

        var sourcePath = WriteTempSource("Questinfo.ini", questInfoSource);
        try
        {
            var sources = LegacyQuestInfoRandomTranslator.ReadSourceRows(sourcePath);
            var result = LegacyQuestInfoRandomTranslator.Translate(target, sources, out var updated);

            Assert.True(result.Changed);
            Assert.Equal(4, result.TargetRows);
            Assert.Equal(4, result.RowsNeedingTranslation);
            Assert.Equal(3, result.SourceMatchedRows);
            Assert.Equal(0, result.SameFormatMatchedRows);
            Assert.Equal(2, result.CompoundNpcMatchedRows);
            Assert.Equal(1, result.AnchorMatchedRows);
            Assert.Equal(1, result.UnmatchedRows);
            Assert.Equal(2, result.RemainingCjkRows);
            Assert.Contains(
                "\t\tBeginNpcId=\"27267,1002,339,465,TaoistZhen,TwinCity\",\n",
                updated,
                StringComparison.Ordinal);
            Assert.Contains(
                "\t\tFinishNpcId=\"27267,1002,339,465,TaoistZhen,TwinCity\",\n",
                updated,
                StringComparison.Ordinal);
            Assert.Contains(
                "[npc DailyQuestEnvoy,9998,1002,275,467]",
                updated,
                StringComparison.Ordinal);
            Assert.Contains("[mob \u571f\u5893\u8760,1001,309,597]", updated, StringComparison.Ordinal);
            Assert.Contains("Content=\"\u6740\u602a\u4efb\u52a1\"", updated, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempSource(sourcePath);
        }
    }

    [Fact]
    public void ConflictingAnchorSourcesDoNotPatchRows()
    {
        var target =
            "local RandomDailyInfo = {\n" +
            "\t[80] = {\n" +
            "\t\tIntention1=\"\u627e[npc \u65e5\u5e38\u4efb\u52a1\u5927\u4f7f,9998,1002,275,467]\",\n" +
            "\t},\n" +
            "}\n";
        var sources = new[]
        {
            new LegacyQuestInfoRandomSourceRow("anchor|npc|9998,1002,275,467", "DailyQuestEnvoy", "first"),
            new LegacyQuestInfoRandomSourceRow("anchor|npc|9998,1002,275,467", "EventEnvoy", "second"),
        };

        var result = LegacyQuestInfoRandomTranslator.Translate(target, sources, out var updated);

        Assert.False(result.Changed);
        Assert.Equal(target, updated);
        Assert.Equal(1, result.ConflictRows);
        Assert.Equal(0, result.SourceMatchedRows);
        Assert.Equal(1, result.RemainingCjkRows);
        Assert.Contains("anchor|npc|9998,1002,275,467", result.ConflictingSourceKeys);
    }

    [Fact]
    public void UsesMonsterGeneratorSourcesForExactMonsterAnchors()
    {
        var target =
            "local RandomDailyInfo = {\n" +
            "\t[80] = {\n" +
            "\t\tIntention1=\"\u524d\u5f80[mob \u6566\u714c\u5730\u5bab,1000,80,320]\uff0c\u6d88\u706d[mob \u5de8\u578b\u571f\u5893\u8760,1001,309,597][chk_quest %d,1,1,%d]\",\n" +
            "\t},\n" +
            "}\n";
        var sourceDirectory = WriteTempDbServerSource(
            ("0080", "TombBatL103"),
            [("0075", "1001", "309", "597", "0080")]);
        try
        {
            var sources = LegacyQuestInfoRandomTranslator.ReadSourceRows(sourceDirectory);
            var result = LegacyQuestInfoRandomTranslator.Translate(target, sources, out var updated);

            Assert.True(result.Changed);
            Assert.Equal(1, result.SourceMatchedRows);
            Assert.Equal(1, result.AnchorMatchedRows);
            Assert.Equal(1, result.ChangedRows);
            Assert.Equal(1, result.RemainingCjkRows);
            Assert.Contains("[mob TombBatL103,1001,309,597]", updated, StringComparison.Ordinal);
            Assert.Contains("[mob \u6566\u714c\u5730\u5bab,1000,80,320]", updated, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempSource(Path.Combine(sourceDirectory, "Questinfo.ini"));
        }
    }

    [Fact]
    public void ConflictingMonsterGeneratorSourcesDoNotPatchAnchorRows()
    {
        var target =
            "local RandomDailyInfo = {\n" +
            "\t[80] = {\n" +
            "\t\tIntention1=\"\u6d88\u706d[mob \u86ee\u725b\u602a,1001,162,319][chk_quest %d,1,1,%d]\",\n" +
            "\t},\n" +
            "}\n";
        var sourceDirectory = WriteTempDbServerSource(
            ("0080", "BullMonster"),
            ("0081", "BullKing"),
            [("0075", "1001", "162", "319", "0080"), ("0076", "1001", "162", "319", "0081")]);
        try
        {
            var sources = LegacyQuestInfoRandomTranslator.ReadSourceRows(sourceDirectory);
            var result = LegacyQuestInfoRandomTranslator.Translate(target, sources, out var updated);

            Assert.False(result.Changed);
            Assert.Equal(target, updated);
            Assert.Equal(1, result.ConflictRows);
            Assert.Equal(0, result.SourceMatchedRows);
            Assert.Contains("anchor|mob|1001,162,319", result.ConflictingSourceKeys);
        }
        finally
        {
            DeleteTempSource(Path.Combine(sourceDirectory, "Questinfo.ini"));
        }
    }

    [Fact]
    public void UsesNpcSeedAndNpcIdEnumSourcesForExactNpcAnchors()
    {
        var target =
            "local RandomDailyInfo = {\n" +
            "\t[80] = {\n" +
            "\t\tIntention1=\"\u62dc\u8bbf[npc \u4e8e\u89c2\u6d77,20823,10137,453,480]\u540e\u53bb[npc \u4e8e\u89c2\u6d77,20823,10137,450,480]\",\n" +
            "\t},\n" +
            "}\n";
        var sourceDirectory = WriteTempNpcAnchorSource(
            "public enum NpcID\n" +
            "{\n" +
            "    DragonYuKoon = 20823u,\n" +
            "    OtherNpc = 20824u,\n" +
            "}\n",
            "20823,2,49397,10137,453,480\n" +
            "20824,2,49397,10137,450,480\n");

        try
        {
            var sources = LegacyQuestInfoRandomTranslator.ReadSourceRows(sourceDirectory);
            var result = LegacyQuestInfoRandomTranslator.Translate(target, sources, out var updated);
            var second = LegacyQuestInfoRandomTranslator.Translate(updated, sources, out var secondText);

            Assert.True(result.Changed);
            Assert.Equal(2, result.SourceRows);
            Assert.Equal(2, result.SourceKeys);
            Assert.Equal(1, result.SourceMatchedRows);
            Assert.Equal(1, result.AnchorMatchedRows);
            Assert.Equal(1, result.ChangedRows);
            Assert.Equal(1, result.RemainingCjkRows);
            Assert.Contains("[npc DragonYuKoon,20823,10137,453,480]", updated, StringComparison.Ordinal);
            Assert.Contains("[npc \u4e8e\u89c2\u6d77,20823,10137,450,480]", updated, StringComparison.Ordinal);
            Assert.False(second.Changed);
            Assert.Equal(updated, secondText);
        }
        finally
        {
            DeleteTempSource(Path.Combine(sourceDirectory, "Npcs.txt"));
        }
    }

    [Fact]
    public void ConflictingNpcSeedAndQuestInfoAnchorSourcesDoNotPatchRows()
    {
        var target =
            "local RandomDailyInfo = {\n" +
            "\t[80] = {\n" +
            "\t\tIntention1=\"\u62dc\u8bbf[npc \u4e8e\u89c2\u6d77,20823,10137,453,480]\",\n" +
            "\t},\n" +
            "}\n";
        var sourceDirectory = WriteTempNpcAnchorSource(
            "public enum NpcID\n" +
            "{\n" +
            "    DragonYuKoon = 20823u,\n" +
            "}\n",
            "20823,2,49397,10137,453,480\n",
            questInfoText:
            "[1]\n" +
            "Intention1=Visit [npc RivalName,20823,10137,453,480].\n");

        try
        {
            var sources = LegacyQuestInfoRandomTranslator.ReadSourceRows(sourceDirectory);
            var result = LegacyQuestInfoRandomTranslator.Translate(target, sources, out var updated);

            Assert.False(result.Changed);
            Assert.Equal(target, updated);
            Assert.Equal(1, result.ConflictRows);
            Assert.Equal(0, result.SourceMatchedRows);
            Assert.Equal(1, result.RemainingCjkRows);
            Assert.Contains("anchor|npc|20823,10137,453,480", result.ConflictingSourceKeys);
        }
        finally
        {
            DeleteTempSource(Path.Combine(sourceDirectory, "Npcs.txt"));
        }
    }

    [Fact]
    public void TranslatesRandomPassionOnlyWithExactAnchorsAndSourceBackedFallbacks()
    {
        var target =
            "local RandomDailyInfo = {\n" +
            "\t[5278] = {\n" +
            "\t\tBeginNpcId=\"27267,1036,243,249,\u7504\u51e0\u9053,\u5e02\u573a\",\n" +
            "\t\tPrize=\"\u7ecf\u9a8c: 5\",\n" +
            "\t\tIntention1=\"\u524d\u5f80\u53cc\u9f99\u57ce\u627e[npc \u4e91\u91cc\u7ffb,24171,10364,310,354]\u8fdb\u5165\u65e0\u95f4\u4e4b\u5819\uff0c\u6d88\u706d[mob 101\u7ea7\u80dc\u5bd2\u9e1f,10479,210,458][chk_quest %d,1,1,%d]\",\n" +
            "\t\tContent=\"\u6740\u602a\u4efb\u52a1\",\n" +
            "\t},\n" +
            "}\n";
        var reference =
            "[100]\n" +
            "Intention1=Enter [npc AgentCloud,24171,10364,310,354].\n" +
            "Intention2=Defeat [mob L101ChillyBird,10479,252,454][chk_quest 11413,1,1,20]\n";
        var sourcePath = WriteTempSource("Questinfo.ini", reference);
        var fallbackDirectory = Path.Combine(
            Path.GetTempPath(),
            "WolfConquerQuestInfoRandomFallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fallbackDirectory);
        File.WriteAllText(Path.Combine(fallbackDirectory, "itemtype.txt"), string.Empty, Encoding.UTF8);

        try
        {
            var sources = LegacyQuestInfoRandomTranslator.ReadSourceRows(sourcePath);
            var fallbackCatalog = LegacyQuestInfoNameFallbackCatalog.FromSourcePaths([fallbackDirectory]);
            var result = LegacyQuestInfoRandomTranslator.Translate(target, sources, fallbackCatalog, out var updated);
            var second = LegacyQuestInfoRandomTranslator.Translate(updated, sources, fallbackCatalog, out var secondText);

            Assert.True(result.Changed);
            Assert.Equal(4, result.RowsNeedingTranslation);
            Assert.Equal(2, result.SourceMatchedRows);
            Assert.Equal(1, result.AnchorMatchedRows);
            Assert.Equal(1, result.FallbackMatchedRows);
            Assert.Equal(2, result.UnmatchedRows);
            Assert.Contains("BeginNpcId=\"27267,1036,243,249,\u7504\u51e0\u9053,\u5e02\u573a\"", updated, StringComparison.Ordinal);
            Assert.Contains("Prize=\"EXP: 5\"", updated, StringComparison.Ordinal);
            Assert.Contains("[npc AgentCloud,24171,10364,310,354]", updated, StringComparison.Ordinal);
            Assert.Contains("[mob 101\u7ea7\u80dc\u5bd2\u9e1f,10479,210,458]", updated, StringComparison.Ordinal);
            Assert.Contains("Content=\"\u6740\u602a\u4efb\u52a1\"", updated, StringComparison.Ordinal);
            Assert.False(second.Changed);
            Assert.Equal(updated, secondText);
        }
        finally
        {
            DeleteTempSource(sourcePath);
            if (Directory.Exists(fallbackDirectory))
            {
                Directory.Delete(fallbackDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ReadSourceRowsBlocksWrongGamePaths()
    {
        var rows = LegacyQuestInfoRandomTranslator.ReadSourceRows(
            @"C:\fixtures\Conquer Source 7770\NpcDialog.cs");

        Assert.Empty(rows);
    }

    private static string WriteTempSource(string fileName, string text)
    {
        var directory = Path.Combine(Path.GetTempPath(), "WolfConquerQuestInfoRandom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, text, Encoding.UTF8);
        return path;
    }

    private static void DeleteTempSource(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string WriteTempDbServerSource(
        params (string TypeId, string Name)[] monsters)
    {
        return WriteTempDbServerSource(monsters, []);
    }

    private static string WriteTempDbServerSource(
        (string TypeId, string Name) monster,
        (string GeneratorId, string MapId, string X, string Y, string TypeId)[] generators)
    {
        return WriteTempDbServerSource([monster], generators);
    }

    private static string WriteTempDbServerSource(
        (string TypeId, string Name) monster1,
        (string TypeId, string Name) monster2,
        (string GeneratorId, string MapId, string X, string Y, string TypeId)[] generators)
    {
        return WriteTempDbServerSource([monster1, monster2], generators);
    }

    private static string WriteTempDbServerSource(
        (string TypeId, string Name)[] monsters,
        (string GeneratorId, string MapId, string X, string Y, string TypeId)[] generators)
    {
        var directory = Path.Combine(Path.GetTempPath(), "WolfConquerQuestInfoRandomDb-" + Guid.NewGuid().ToString("N"));
        var monsterDirectory = Path.Combine(directory, "Monsters");
        var generatorDirectory = Path.Combine(directory, "cq_generator");
        Directory.CreateDirectory(monsterDirectory);
        Directory.CreateDirectory(generatorDirectory);

        foreach (var monster in monsters)
        {
            File.WriteAllText(
                Path.Combine(monsterDirectory, monster.Name + ".ini"),
                "[cq_monstertype]\n" +
                "id=" + monster.TypeId + "\n" +
                "name=" + monster.Name + "\n",
                Encoding.UTF8);
        }

        foreach (var generator in generators)
        {
            File.WriteAllText(
                Path.Combine(generatorDirectory, generator.GeneratorId + ".ini"),
                "[cq_generator]\n" +
                "id=" + generator.GeneratorId + "\n" +
                "mapid=" + generator.MapId + "\n" +
                "bound_x=" + generator.X + "\n" +
                "bound_y=" + generator.Y + "\n" +
                "bound_cx=44\n" +
                "bound_cy=51\n" +
                "maxnpc=1\n" +
                "rest_secs=1\n" +
                "max_per_gen=1\n" +
                "npctype=" + generator.TypeId + "\n" +
                "timer_begin=0\n" +
                "timer_end=0\n" +
                "born_x=0\n" +
                "born_y=0\n",
                Encoding.UTF8);
        }

        return directory;
    }

    private static string WriteTempNpcAnchorSource(
        string npcIdEnumText,
        string npcRowsText,
        string? questInfoText = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "WolfConquerQuestInfoRandomNpc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "NpcID.cs"), npcIdEnumText, Encoding.UTF8);
        File.WriteAllText(Path.Combine(directory, "Npcs.txt"), npcRowsText, Encoding.UTF8);
        if (questInfoText is not null)
        {
            File.WriteAllText(Path.Combine(directory, "Questinfo.ini"), questInfoText, Encoding.UTF8);
        }

        return directory;
    }
}
