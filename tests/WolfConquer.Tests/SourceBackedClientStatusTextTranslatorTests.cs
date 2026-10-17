// CJK fixture text uses repeated authored marker characters, not client text.
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class SourceBackedClientStatusTextTranslatorTests
{
    [Fact]
    public void TranslatesOnlyExactSameSectionAndKeyStatusRows()
    {
        var targetText =
            "[90]\r\n" +
            "Type=1\r\n" +
            "Name=甲甲甲甲甲甲\r\n" +
            "Num=甲甲甲甲：%02u甲甲%02u甲%02u甲\r\n" +
            "Note=甲甲甲甲甲甲%d%%\r\n" +
            "Icon=Soldier_shield\r\n" +
            "\r\n" +
            "[91]\r\n" +
            "Name=甲甲甲甲-甲甲甲甲甲\r\n";
        var sourceText =
            "[90]\r\n" +
            "Name=ShieldBlock\r\n" +
            "Num=Remaining: %02u hr %02u min %02u sec\r\n" +
            "Note=Shield block rate +%d%%\r\n" +
            "\r\n" +
            "[91]\r\n" +
            "Name=甲甲甲甲\r\n";

        var sources = SourceBackedClientStatusTextTranslator.ReadIniStatusTextSourcesFromText(
            sourceText,
            "StatusTips.en.ini");
        var result = SourceBackedClientStatusTextTranslator.TranslateStatusTips(
            targetText,
            sources,
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.SourceRows);
        Assert.Equal(3, result.SourceKeys);
        Assert.Equal(4, result.TargetRows);
        Assert.Equal(4, result.RowsNeedingTranslation);
        Assert.Equal(3, result.MatchedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(3, result.ChangedRows);
        Assert.Equal(1, result.RemainingCjkRows);
        Assert.Empty(result.ConflictingSourceKeys);
        Assert.Contains("Name=ShieldBlock\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Num=Remaining: %02u hr %02u min %02u sec\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Note=Shield block rate +%d%%\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("[91]\r\nName=甲甲甲甲-甲甲甲甲甲\r\n", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsProseWhenFormatPlaceholdersWouldChange()
    {
        var targetText =
            "[90]\n" +
            "Note=甲甲甲甲甲甲%d%%\n";
        var sources = new[]
        {
            new SourceBackedClientStatusTextSource(
                "90",
                "Note",
                "Increase shield block rate by 10%.",
                "MagicEffect.ini:[1047000]")
        };

        var result = SourceBackedClientStatusTextTranslator.TranslateStatusTips(
            targetText,
            sources,
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(targetText, updatedText);
        Assert.Equal(1, result.PlaceholderMismatchRows);
        Assert.Equal(1, result.RemainingCjkRows);
    }

    [Fact]
    public void LeavesRowsUnchangedWhenExactSourcesConflict()
    {
        var targetText =
            "[90]\r\n" +
            "Name=甲甲甲甲甲甲\r\n";
        var sources = new[]
        {
            new SourceBackedClientStatusTextSource("90", "Name", "ShieldBlock", "StatusTips.a.ini"),
            new SourceBackedClientStatusTextSource("90", "Name", "ShieldParry", "StatusTips.b.ini")
        };

        var result = SourceBackedClientStatusTextTranslator.TranslateStatusTips(
            targetText,
            sources,
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(targetText, updatedText);
        Assert.Equal(["90:NAME"], result.ConflictingSourceKeys);
        Assert.Equal(1, result.UnmatchedRows);
    }

    [Fact]
    public void LeavesStatusRowsUnmatchedWhenSourcesAreMissing()
    {
        const string targetText = "// synthetic comment\r\n[31]\r\nName=\u7532\r\n" +
            "[44]\r\nName=\u7532\u7532\r\nNum=\u7532:%u/%u\r\nNote=\u7532 note\r\n";

        var result = SourceBackedClientStatusTextTranslator.TranslateStatusTips(targetText, [], out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(targetText, updatedText);
        Assert.Equal(0, result.SourceRows);
        Assert.Equal(0, result.SourceKeys);
        Assert.Equal(4, result.TargetRows);
        Assert.Equal(4, result.RowsNeedingTranslation);
        Assert.Equal(0, result.MatchedRows);
        Assert.Equal(4, result.UnmatchedRows);
        Assert.Equal(0, result.ChangedRows);
        Assert.Equal(4, result.RemainingCjkRows);
    }

    [Fact]
    public void ExplicitSyntheticStatusSourcesPreserveUnmatchedRowsAndAreIdempotent()
    {
        const string targetText = "// synthetic comment\r\n[31]\r\nName=\u7532\r\n" +
            "[44]\r\nName=\u7532\u7532\r\nNum=\u7532:%u/%u\r\nNote=\u7532 note\r\n";
        SourceBackedClientStatusTextSource[] sources =
        [
            new("31", "Name", "Sample status one", "Authored fixture"),
            new("44", "Name", "Sample status two", "Authored fixture"),
            new("44", "Num", "Sample count %u/%u", "Authored fixture"),
        ];

        var result = SourceBackedClientStatusTextTranslator.TranslateStatusTips(targetText, sources, out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(3, result.SourceRows);
        Assert.Equal(3, result.SourceKeys);
        Assert.Equal(4, result.TargetRows);
        Assert.Equal(3, result.MatchedRows);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(3, result.ChangedRows);
        Assert.Equal(1, result.RemainingCjkRows);
        Assert.Equal("// synthetic comment\r\n[31]\r\nName=Sample status one\r\n" +
            "[44]\r\nName=Sample status two\r\nNum=Sample count %u/%u\r\nNote=\u7532 note\r\n", updatedText);

        var repeated = SourceBackedClientStatusTextTranslator.TranslateStatusTips(updatedText, sources, out var unchanged);
        Assert.False(repeated.Changed);
        Assert.Equal(updatedText, unchanged);
        Assert.Equal(1, repeated.RowsNeedingTranslation);
        Assert.Equal(1, repeated.UnmatchedRows);
        Assert.Equal(1, repeated.RemainingCjkRows);
    }

    [Fact]
    public void ReadsGuardedMagicEffectStatusNameSourcesAndIsIdempotent()
    {
        var sourceText =
            "[1047000]\r\n" +
            "Name=ShieldBlock\r\n" +
            "ActOfAttacker=903\r\n" +
            "ClientRepresent=1\r\n" +
            "Status=90\r\n" +
            "CoolDown=500\r\n" +
            "TargetType=2\r\n" +
            "\r\n" +
            "[1030800]\r\n" +
            "Name=Thwart\r\n" +
            "ClientRepresent=2\r\n" +
            "Status=57\r\n" +
            "\r\n" +
            "[1030900]\r\n" +
            "Name=ChainBolt\r\n" +
            "ClientRepresent=103\r\n" +
            "Status=88\r\n";
        var sources = SourceBackedClientStatusTextTranslator.ReadIniStatusTextSourcesFromText(
            sourceText,
            "MagicEffect.ini");
        var targetText =
            "[90]\r\n" +
            "Name=甲甲甲甲甲甲\r\n" +
            "[57]\r\n" +
            "Name=甲甲甲甲甲甲\r\n" +
            "[88]\r\n" +
            "Name=甲甲甲甲\r\n";

        var result = SourceBackedClientStatusTextTranslator.TranslateStatusTips(
            targetText,
            sources,
            out var updatedText);
        var secondResult = SourceBackedClientStatusTextTranslator.TranslateStatusTips(
            updatedText,
            sources,
            out var secondUpdatedText);

        Assert.Contains(
            new SourceBackedClientStatusTextSource("90", "Name", "ShieldBlock", "MagicEffect.ini:[1047000] Status"),
            sources);
        Assert.DoesNotContain(
            new SourceBackedClientStatusTextSource("57", "Name", "Thwart", "MagicEffect.ini:[1030800] Status"),
            sources);
        Assert.DoesNotContain(
            new SourceBackedClientStatusTextSource("88", "Name", "ChainBolt", "MagicEffect.ini:[1030900] Status"),
            sources);
        Assert.True(result.Changed);
        Assert.Equal(4, result.SourceRows);
        Assert.Equal(4, result.SourceKeys);
        Assert.Equal(3, result.TargetRows);
        Assert.Equal(3, result.RowsNeedingTranslation);
        Assert.Equal(1, result.MatchedRows);
        Assert.Equal(2, result.UnmatchedRows);
        Assert.Equal(1, result.ChangedRows);
        Assert.Equal(2, result.RemainingCjkRows);
        Assert.Contains("Name=ShieldBlock\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("[57]\r\nName=甲甲甲甲甲甲\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("[88]\r\nName=甲甲甲甲\r\n", updatedText, StringComparison.Ordinal);
        Assert.False(secondResult.Changed);
        Assert.Equal(updatedText, secondUpdatedText);
        Assert.Equal(2, secondResult.RowsNeedingTranslation);
        Assert.Equal(2, secondResult.UnmatchedRows);
        Assert.Equal(2, secondResult.RemainingCjkRows);
    }

    [Fact]
    public void LeavesMagicEffectStatusNameSourceUnchangedWhenDerivedSourcesConflict()
    {
        var sourceText =
            "[1047000]\n" +
            "Name=ShieldBlock\n" +
            "ClientRepresent=1\n" +
            "Status=90\n" +
            "CoolDown=500\n" +
            "\n" +
            "[1047001]\n" +
            "Name=ShieldParry\n" +
            "ClientRepresent=1\n" +
            "Status=90\n" +
            "CoolDown=500\n";
        var sources = SourceBackedClientStatusTextTranslator.ReadIniStatusTextSourcesFromText(
            sourceText,
            "MagicEffect.ini");
        var targetText =
            "[90]\n" +
            "Name=甲甲甲甲甲甲\n";

        var result = SourceBackedClientStatusTextTranslator.TranslateStatusTips(
            targetText,
            sources,
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(targetText, updatedText);
        Assert.Equal(["90:NAME"], result.ConflictingSourceKeys);
        Assert.Equal(1, result.UnmatchedRows);
        Assert.Equal(1, result.RemainingCjkRows);
    }

    [Fact]
    public void TranslatesInfoLuaSkillNamesFromSameMagicTypeSkillId()
    {
        var infoText =
            "    [13]={\r\n" +
            "        [0] = {\r\n" +
            "            [\"Id\"] = 21190,--甲甲id\r\n" +
            "            [\"Skill\"] = \"甲甲甲甲（甲甲）\",--甲甲甲甲甲\r\n" +
            "            [\"Level\"] = 0,--甲甲甲甲甲甲甲\r\n" +
            "        },\r\n" +
            "        [1] = {\r\n" +
            "            [\"Id\"] = 21190,--甲甲id\r\n" +
            "            [\"Skill\"] = \"甲甲甲甲（甲甲）\",--甲甲甲甲甲\r\n" +
            "            [\"Level\"] = 1,--甲甲甲甲甲甲甲\r\n" +
            "        },\r\n" +
            "        [\"AwakeTip\"]=\"甲甲甲甲甲甲甲甲甲，甲甲甲甲甲甲甲（甲甲）\\n\",\r\n" +
            "    }\r\n";
        var magicTypeText =
            "21190@@21190@@6@@WaterAegis(Rebirth)@@0@@\r\n" +
            "21191@@21190@@6@@WaterAegis(Rebirth)@@0@@\r\n";
        var sources = SourceBackedClientStatusTextTranslator.ReadMagicTypeSkillNameSourcesFromText(
            magicTypeText,
            "magictype.txt");

        var result = SourceBackedClientStatusTextTranslator.TranslateInfoLuaSkillNames(
            infoText,
            sources,
            out var updatedText);

        Assert.True(result.Changed);
        Assert.Equal(2, result.SkillRows);
        Assert.Equal(1, result.UniqueSkillIds);
        Assert.Equal(1, result.SourceSkillNames);
        Assert.Equal(2, result.MatchedSkillRows);
        Assert.Equal(2, result.TranslatedRows);
        Assert.Equal(2, result.ChangedRows);
        Assert.Empty(result.ConflictingSourceIds);
        Assert.Contains("[\"Skill\"] = \"WaterAegis(Rebirth)\"", updatedText, StringComparison.Ordinal);
        Assert.Contains("[\"AwakeTip\"]=\"甲甲甲甲甲甲甲甲甲，甲甲甲甲甲甲甲（甲甲）\\n\"", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsInfoLuaSkillNamesWhenMagicTypeSourcesConflict()
    {
        var infoText =
            "[0] = {\n" +
            "    [\"Id\"] = 21190,--甲甲id\n" +
            "    [\"Skill\"] = \"甲甲甲甲（甲甲）\",--甲甲甲甲甲\n" +
            "}\n";
        var magicTypeText =
            "21190@@21190@@6@@WaterAegis(Rebirth)@@0@@\n" +
            "21191@@21190@@6@@OtherName@@0@@\n";
        var sources = SourceBackedClientStatusTextTranslator.ReadMagicTypeSkillNameSourcesFromText(
            magicTypeText,
            "magictype.txt");

        var result = SourceBackedClientStatusTextTranslator.TranslateInfoLuaSkillNames(
            infoText,
            sources,
            out var updatedText);

        Assert.False(result.Changed);
        Assert.Equal(infoText, updatedText);
        Assert.Equal([21190], result.ConflictingSourceIds);
        Assert.Equal(1, result.MissingSkillRows);
    }

    [Fact]
    public void PatchFilesPreserveCodePage936Encoding()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-status-text-translator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            var statusTipsPath = Path.Combine(root, "StatusTips.ini");
            var infoLuaPath = Path.Combine(root, "Info.lua");
            var magicTypePath = Path.Combine(root, "magictype.txt");
            File.WriteAllBytes(
                statusTipsPath,
                encoding.GetBytes("[90]\r\nName=甲甲甲甲甲甲\r\n"));
            File.WriteAllBytes(
                infoLuaPath,
                encoding.GetBytes(
                    "[0] = {\r\n" +
                    "    [\"Id\"] = 21190,--甲甲id\r\n" +
                    "    [\"Skill\"] = \"甲甲甲甲（甲甲）\",--甲甲甲甲甲\r\n" +
                    "}\r\n"));
            File.WriteAllBytes(
                magicTypePath,
                encoding.GetBytes("21190@@21190@@6@@WaterAegis(Rebirth)@@0@@\r\n"));

            var statusResult = SourceBackedClientStatusTextTranslator.PatchStatusTipsFile(
                statusTipsPath,
                [new SourceBackedClientStatusTextSource("90", "Name", "ShieldBlock", "MagicEffect.ini:[1047000]")]);
            var infoResult = SourceBackedClientStatusTextTranslator.PatchInfoLuaFile(
                infoLuaPath,
                [magicTypePath]);

            Assert.True(statusResult.Changed);
            Assert.True(infoResult.Changed);
            Assert.Contains("Name=ShieldBlock", encoding.GetString(File.ReadAllBytes(statusTipsPath)), StringComparison.Ordinal);
            Assert.Contains("[\"Skill\"] = \"WaterAegis(Rebirth)\"", encoding.GetString(File.ReadAllBytes(infoLuaPath)), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
