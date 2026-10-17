// CJK fixture text uses repeated authored marker characters, not client text.
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientTextCjkDebtReporterTests
{
    [Fact]
    public void CreateGroupsServerKeyDebtAndSkipsPackedFiles()
    {
        using var temp = TemporaryDirectory.Create();
        var scriptDirectory = Path.Combine(temp.Path, "Script");
        var iniDirectory = Path.Combine(temp.Path, "ini");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(iniDirectory);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        File.WriteAllBytes(
            Path.Combine(scriptDirectory, "Server_Key.lua"),
            encoding.GetBytes(
                "return {\r\n" +
                "    [\"STR_NPC_ID_1000\"] = \"甲甲\",\r\n" +
                "    [\"STR_ITEM_TYPE_120319\"] = \"甲甲甲甲\",\r\n" +
                "    [\"STR_ITEM_TYPE_120320\"] = \"Phoenix Armor\",\r\n" +
                "}\r\n"));
        File.WriteAllBytes(
            Path.Combine(iniDirectory, "itemtype.dat"),
            [0x1F, 0x8A, 0x00, 0x03, 0x7F, 0xC1, 0x00, 0x14]);

        var report = ClientTextCjkDebtReporter.Create(temp.Path);

        Assert.Equal(2, report.CandidateFileCount);
        Assert.Equal(1, report.TextFileCount);
        Assert.Equal(1, report.PackedOrBinaryFileCount);
        Assert.Equal(1, report.FilesWithCjk);
        var file = Assert.Single(report.Files);
        Assert.Equal(Path.Combine("Script", "Server_Key.lua"), file.RelativePath);
        Assert.Equal(ClientTextEncodingKind.CodePage936, file.EncodingKind);
        Assert.Equal(2, file.CjkLineCount);
        Assert.Contains(file.Families, family =>
            family.Name == "STR_NPC_ID" &&
            family.CjkLineCount == 1 &&
            family.Sample.Contains("甲甲", StringComparison.Ordinal));
        Assert.Contains(file.Families, family =>
            family.Name == "STR_ITEM_TYPE" &&
            family.CjkLineCount == 1 &&
            family.Sample.Contains("甲甲甲甲", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateDetectsUtf8BomTextDebt()
    {
        using var temp = TemporaryDirectory.Create();
        var iniDirectory = Path.Combine(temp.Path, "ini");
        Directory.CreateDirectory(iniDirectory);

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
        var textBytes = encoding.GetPreamble()
            .Concat(encoding.GetBytes("Name=甲甲甲甲\r\nText=Welcome\r\n"))
            .ToArray();
        File.WriteAllBytes(Path.Combine(iniDirectory, "NpcName.ini"), textBytes);

        var report = ClientTextCjkDebtReporter.Create(temp.Path);

        var file = Assert.Single(report.Files);
        Assert.Equal(ClientTextEncodingKind.Utf8Bom, file.EncodingKind);
        Assert.Equal(1, file.CjkLineCount);
        Assert.Contains(file.Families, family =>
            family.Name == "Name" &&
            family.Sample == "Name=甲甲甲甲");
    }

    [Fact]
    public void CreateSeparatesActionableVisibleDebtFromCommentsSupportDataAndPlayerState()
    {
        using var temp = TemporaryDirectory.Create();
        var iniDirectory = Path.Combine(temp.Path, "ini");
        var scriptDirectory = Path.Combine(temp.Path, "Script");
        var usersDirectory = Path.Combine(temp.Path, "Users");
        var guildsDirectory = Path.Combine(temp.Path, "Guilds");
        var unionsDirectory = Path.Combine(temp.Path, "Unions");
        var rankDirectory = Path.Combine(temp.Path, "rank");
        Directory.CreateDirectory(iniDirectory);
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(usersDirectory);
        Directory.CreateDirectory(guildsDirectory);
        Directory.CreateDirectory(unionsDirectory);
        Directory.CreateDirectory(rankDirectory);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        WriteText(Path.Combine(iniDirectory, "GUI.ini"), encoding,
            "//甲甲\r\n" +
            "Text=甲甲\r\n" +
            "Label=甲甲\r\n");
        WriteText(Path.Combine(scriptDirectory, "Dialog.lua"), encoding,
            "--甲甲\r\n" +
            "return \"甲甲\"\r\n");
        WriteText(Path.Combine(iniDirectory, "pinyindata.ini"), encoding, "han=甲\r\n");
        WriteText(Path.Combine(usersDirectory, "1001.ini"), encoding, "Name=甲甲\r\n");
        WriteText(Path.Combine(guildsDirectory, "1.txt"), encoding, "Name=甲甲\r\n");
        WriteText(Path.Combine(unionsDirectory, "1.ini"), encoding, "Name=甲甲\r\n");
        WriteText(Path.Combine(temp.Path, "Associate.txt"), encoding, "Friend=甲甲\r\n");
        WriteText(Path.Combine(rankDirectory, "arena.txt"), encoding, "1 甲甲\r\n");

        var report = ClientTextCjkDebtReporter.Create(temp.Path);

        Assert.Equal(8, report.FilesWithCjk);
        Assert.Equal(11, report.TotalCjkLineCount);
        Assert.Equal(2, report.ActionableFilesWithCjk);
        Assert.Equal(3, report.TotalActionableCjkLineCount);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 3);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.Comment, cjkLineCount: 2);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.SupportData, cjkLineCount: 1);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.PlayerState, cjkLineCount: 5);

        var guiFile = AssertFile(report, Path.Combine("ini", "GUI.ini"));
        Assert.Equal(2, guiFile.ActionableCjkLineCount);
        AssertCategory(guiFile.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 2);
        AssertCategory(guiFile.Categories, ClientTextCjkDebtCategory.Comment, cjkLineCount: 1);

        var scriptFile = AssertFile(report, Path.Combine("Script", "Dialog.lua"));
        Assert.Equal(1, scriptFile.ActionableCjkLineCount);
        AssertCategory(scriptFile.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);
        AssertCategory(scriptFile.Categories, ClientTextCjkDebtCategory.Comment, cjkLineCount: 1);

        var supportFile = AssertFile(report, Path.Combine("ini", "pinyindata.ini"));
        Assert.Equal(0, supportFile.ActionableCjkLineCount);
        AssertCategory(supportFile.Categories, ClientTextCjkDebtCategory.SupportData, cjkLineCount: 1);

        var userFile = AssertFile(report, Path.Combine("Users", "1001.ini"));
        Assert.Equal(0, userFile.ActionableCjkLineCount);
        AssertCategory(userFile.Categories, ClientTextCjkDebtCategory.PlayerState, cjkLineCount: 1);
    }

    [Fact]
    public void CreateTreatsInlineCommentOnlyCjkAsComment()
    {
        using var temp = TemporaryDirectory.Create();
        var iniDirectory = Path.Combine(temp.Path, "ini");
        Directory.CreateDirectory(iniDirectory);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        WriteText(Path.Combine(iniDirectory, "GUI.ini"), encoding,
            "SubBg1=5,-3,256,32,1,Home_WZjzxx // 甲甲甲甲\r\n" +
            "Tooltip=English -- 甲甲\r\n" +
            "Path=asset; 甲甲甲甲\r\n" +
            "Name=甲甲 // still visible\r\n");

        var report = ClientTextCjkDebtReporter.Create(temp.Path);

        Assert.Equal(1, report.FilesWithCjk);
        Assert.Equal(4, report.TotalCjkLineCount);
        Assert.Equal(1, report.ActionableFilesWithCjk);
        Assert.Equal(1, report.TotalActionableCjkLineCount);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.Comment, cjkLineCount: 3);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);

        var guiFile = AssertFile(report, Path.Combine("ini", "GUI.ini"));
        Assert.Equal(1, guiFile.ActionableCjkLineCount);
        AssertCategory(guiFile.Categories, ClientTextCjkDebtCategory.Comment, cjkLineCount: 3);
        AssertCategory(guiFile.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);
    }

    [Fact]
    public void CreateTreatsItemLuaAnnotationCommentsAsCommentsWithoutHidingTableValues()
    {
        using var temp = TemporaryDirectory.Create();
        var iniDirectory = Path.Combine(temp.Path, "ini");
        Directory.CreateDirectory(iniDirectory);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        WriteText(Path.Combine(iniDirectory, "Item.lua"), encoding,
            "-- [ItemType / 1000] = { [甲甲] = { 甲甲甲, 甲甲甲甲, } };\r\n" +
            "local ShowName = {\r\n" +
            "\t[730001] = \"甲甲\",\r\n" +
            "}\r\n");

        var report = ClientTextCjkDebtReporter.Create(temp.Path);

        Assert.Equal(1, report.FilesWithCjk);
        Assert.Equal(2, report.TotalCjkLineCount);
        Assert.Equal(1, report.ActionableFilesWithCjk);
        Assert.Equal(1, report.TotalActionableCjkLineCount);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.Comment, cjkLineCount: 1);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);

        var itemFile = AssertFile(report, Path.Combine("ini", "Item.lua"));
        Assert.Equal(1, itemFile.ActionableCjkLineCount);
        AssertCategory(itemFile.Categories, ClientTextCjkDebtCategory.Comment, cjkLineCount: 1);
        AssertCategory(itemFile.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);
    }

    [Fact]
    public void CreateReclassifiesProvenSupportAndGuiCommentsWithoutHidingVisibleRows()
    {
        using var temp = TemporaryDirectory.Create();
        var iniDirectory = Path.Combine(temp.Path, "ini");
        Directory.CreateDirectory(iniDirectory);

        var codePage936 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var utf8 = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);
        WriteText(
            Path.Combine(iniDirectory, "tymgcjy.dat"),
            utf8,
            "2\r\n" +
            "甲甲甲甲甲甲甲甲甲甲甲甲甲甲甲甲甲甲甲\r\n" +
            "甲--甲--甲--甲--甲--甲--甲--甲\r\n");
        WriteText(
            Path.Combine(iniDirectory, "GUI.ini"),
            codePage936,
            "[130-5228]//甲甲甲甲\r\n" +
            "comment=甲甲甲甲-0\r\n" +
            "STR_GANG_TIPS_CLOSE=甲甲\r\n");
        WriteText(
            Path.Combine(iniDirectory, "GUI800X600.ini"),
            codePage936,
            "[130-5228]//甲甲甲甲\r\n" +
            "comment=甲甲甲甲-0\r\n" +
            "STR_GANG_TIPS_CLOSE=甲甲\r\n");
        WriteText(
            Path.Combine(iniDirectory, "Item.lua"),
            codePage936,
            "return \"甲甲甲：33.40%\"\r\n");
        WriteText(
            Path.Combine(iniDirectory, "BoYiTip.lua"),
            codePage936,
            "\t[0] = '甲甲甲甲甲-甲甲',\r\n");

        var report = ClientTextCjkDebtReporter.Create(temp.Path);

        Assert.Equal(5, report.FilesWithCjk);
        Assert.Equal(10, report.TotalCjkLineCount);
        Assert.Equal(4, report.ActionableFilesWithCjk);
        Assert.Equal(4, report.TotalActionableCjkLineCount);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 4);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.Comment, cjkLineCount: 4);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.SupportData, cjkLineCount: 2);

        var supportFile = AssertFile(report, Path.Combine("ini", "tymgcjy.dat"));
        Assert.Equal(0, supportFile.ActionableCjkLineCount);
        AssertCategory(supportFile.Categories, ClientTextCjkDebtCategory.SupportData, cjkLineCount: 2);

        foreach (var relativePath in new[] { Path.Combine("ini", "GUI.ini"), Path.Combine("ini", "GUI800X600.ini") })
        {
            var guiFile = AssertFile(report, relativePath);
            Assert.Equal(1, guiFile.ActionableCjkLineCount);
            AssertCategory(guiFile.Categories, ClientTextCjkDebtCategory.Comment, cjkLineCount: 2);
            AssertCategory(guiFile.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);
        }

        var itemFile = AssertFile(report, Path.Combine("ini", "Item.lua"));
        Assert.Equal(1, itemFile.ActionableCjkLineCount);
        AssertCategory(itemFile.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);

        var boYiTipFile = AssertFile(report, Path.Combine("ini", "BoYiTip.lua"));
        Assert.Equal(1, boYiTipFile.ActionableCjkLineCount);
        AssertCategory(boYiTipFile.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);
    }

    [Fact]
    public void CreateTreatsGeneratedRankTextFilesAsPlayerState()
    {
        using var temp = TemporaryDirectory.Create();
        var iniDirectory = Path.Combine(temp.Path, "ini");
        Directory.CreateDirectory(iniDirectory);

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        WriteText(Path.Combine(temp.Path, "CombatGearRank.txt"), encoding, "1 甲甲\r\n");
        WriteText(Path.Combine(temp.Path, "RuneRanking.txt"), encoding, "1 甲甲\r\n");
        WriteText(Path.Combine(temp.Path, "PrestigeRanking.txt"), encoding, "1 甲甲\r\n");
        WriteText(Path.Combine(iniDirectory, "RankConfig.ini"), encoding, "Title=甲甲\r\n");

        var report = ClientTextCjkDebtReporter.Create(temp.Path);

        Assert.Equal(4, report.FilesWithCjk);
        Assert.Equal(1, report.ActionableFilesWithCjk);
        Assert.Equal(1, report.TotalActionableCjkLineCount);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.PlayerState, cjkLineCount: 3);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);

        AssertRankStateFile(report, "CombatGearRank.txt");
        AssertRankStateFile(report, "RuneRanking.txt");
        AssertRankStateFile(report, "PrestigeRanking.txt");

        var staticConfigFile = AssertFile(report, Path.Combine("ini", "RankConfig.ini"));
        Assert.Equal(1, staticConfigFile.ActionableCjkLineCount);
        AssertCategory(staticConfigFile.Categories, ClientTextCjkDebtCategory.VisibleText, cjkLineCount: 1);
    }

    [Fact]
    public void CreateTreatsTopLevelDbArenaAndInnerPowerSnapshotsAsPlayerState()
    {
        using var temp = TemporaryDirectory.Create();

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        WriteText(Path.Combine(temp.Path, "Arena.ini"), encoding, "1296468/甲甲甲甲甲/100/9000/2422007/4000/20165/20165/0/0/0/0/4000/0/0/10/\r\n");
        WriteText(Path.Combine(temp.Path, "InnerPower.txt"), encoding, "甲甲甲甲甲/1296468/0/0/\r\n");

        var report = ClientTextCjkDebtReporter.Create(temp.Path);

        Assert.Equal(2, report.FilesWithCjk);
        Assert.Equal(0, report.ActionableFilesWithCjk);
        Assert.Equal(0, report.TotalActionableCjkLineCount);
        AssertCategory(report.Categories, ClientTextCjkDebtCategory.PlayerState, cjkLineCount: 2);

        AssertRankStateFile(report, "Arena.ini");
        AssertRankStateFile(report, "InnerPower.txt");
    }

    private static void WriteText(string path, Encoding encoding, string text)
    {
        File.WriteAllBytes(path, encoding.GetBytes(text));
    }

    private static ClientTextCjkDebtFile AssertFile(ClientTextCjkDebtReport report, string relativePath)
    {
        return Assert.Single(
            report.Files,
            file => string.Equals(file.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertRankStateFile(ClientTextCjkDebtReport report, string relativePath)
    {
        var file = AssertFile(report, relativePath);
        Assert.Equal(0, file.ActionableCjkLineCount);
        AssertCategory(file.Categories, ClientTextCjkDebtCategory.PlayerState, cjkLineCount: 1);
    }

    private static void AssertCategory(
        IReadOnlyList<ClientTextCjkDebtCategorySummary> categories,
        ClientTextCjkDebtCategory category,
        int cjkLineCount)
    {
        var summary = Assert.Single(categories, candidate => candidate.Category == category);
        Assert.Equal(cjkLineCount, summary.CjkLineCount);
        Assert.True(summary.CjkCharacterCount > 0);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "WolfConquerCjkDebtTests",
            Guid.NewGuid().ToString("N"));

        private TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public static TemporaryDirectory Create()
        {
            return new TemporaryDirectory();
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
