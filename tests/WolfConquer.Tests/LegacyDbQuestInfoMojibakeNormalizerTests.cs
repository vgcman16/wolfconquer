using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class LegacyDbQuestInfoMojibakeNormalizerTests
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    [Fact]
    public void NormalizeCleansEnglishLikeMojibakeOnlyInVisibleQuestTextFields()
    {
        var targetText =
            "[6100]\r\n" +
            "MissionId=6100\r\n" +
            "Map=Twin City\u9286?\r\n" +
            "Prequest=Nezha\u9225\u6A9A Trial\u9286?\r\n" +
            "BeginNpcId=5674,1002,440,371,KungfuGirl\u9286?\r\n" +
            "FinishNpcId=8296,1020,272,438,MoonMaiden\u9286?\r\n" +
            "Color=0xffffffff\r\n" +
            "Count=3\r\n" +
            "Profession=Trojan\u9286?\r\n" +
            "Sex=Female\u9286?\r\n" +
            "TaskId1=Report\u9286?\r\n" +
            "FileName=quest\u9286?.ini\r\n" +
            "Intention1=Pay a visit to the [npc KungfuGirl,5674,1002,440,371]\u9286?\r\n" +
            "Content=The TC Warder likes Nezha\u9225\u6A9A birthday and isn\u9225\u6A9B late\u9286?\n" +
            "Intention2=Report back to [npc MoonMaiden,8296,1020,272,438]\u9286?\n" +
            "Prize=Nezha\u9225\u6A9A thanks\u9286?\n" +
            "Name=\u5bfb\u627eNPC\u9286?\n";

        var result = LegacyDbQuestInfoMojibakeNormalizer.Normalize(targetText, out var updatedText);
        var second = LegacyDbQuestInfoMojibakeNormalizer.Normalize(updatedText, out var secondText);

        Assert.True(result.Changed);
        Assert.Equal(17, result.LinesScanned);
        Assert.Equal(5, result.VisibleTextFieldsScanned);
        Assert.Equal(4, result.NormalizedFields);
        Assert.Contains("Map=Twin City\u9286?\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Prequest=Nezha\u9225\u6A9A Trial\u9286?\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("BeginNpcId=5674,1002,440,371,KungfuGirl\u9286?\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("FinishNpcId=8296,1020,272,438,MoonMaiden\u9286?\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("TaskId1=Report\u9286?\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("FileName=quest\u9286?.ini\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Intention1=Pay a visit to the [npc KungfuGirl,5674,1002,440,371].\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Content=The TC Warder likes Nezha's birthday and isn't late.\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Intention2=Report back to [npc MoonMaiden,8296,1020,272,438].\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Prize=Nezha's thanks.\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Name=\u5bfb\u627eNPC\u9286?\n", updatedText, StringComparison.Ordinal);
        Assert.False(second.Changed);
        Assert.Equal(0, second.NormalizedFields);
        Assert.Equal(updatedText, secondText);
    }

    [Fact]
    public void NormalizeCleansLeadingListMarkerResidueOnlyForEnglishLikeVisibleQuestText()
    {
        var targetText =
            "[7000]\n" +
            "MissionId=7000\n" +
            "BeginNpcId=\u8DEFEaster Flame Pack\n" +
            "FinishNpcId=\u8DEFSpeak with [npc ArtisanLuo,8900,1002,300,300].\n" +
            "Map=\u8DEFMarket\n" +
            "Prize=\u8DEFEaster Flame Pack\n" +
            "Intention1=\u8DEFSpeak with [npc ArtisanLuo,8900,1002,300,300].\n" +
            "Intention2=\u8DEFObtain 3 Vital Pills\n" +
            "Name=\u8DEF\u5bfb\u627eNPC\n";

        var result = LegacyDbQuestInfoMojibakeNormalizer.Normalize(targetText, out var updatedText);
        var second = LegacyDbQuestInfoMojibakeNormalizer.Normalize(updatedText, out var secondText);

        Assert.True(result.Changed);
        Assert.Equal(9, result.LinesScanned);
        Assert.Equal(4, result.VisibleTextFieldsScanned);
        Assert.Equal(3, result.NormalizedFields);
        Assert.Contains("BeginNpcId=\u8DEFEaster Flame Pack\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("FinishNpcId=\u8DEFSpeak with [npc ArtisanLuo,8900,1002,300,300].\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Map=\u8DEFMarket\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Prize=- Easter Flame Pack\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Intention1=- Speak with [npc ArtisanLuo,8900,1002,300,300].\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Intention2=- Obtain 3 Vital Pills\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Name=\u8DEF\u5bfb\u627eNPC\n", updatedText, StringComparison.Ordinal);
        Assert.False(second.Changed);
        Assert.Equal(0, second.NormalizedFields);
        Assert.Equal(updatedText, secondText);
    }

    [Fact]
    public void NormalizeCleansIdeographicSpaceAndMultilineListResidueInEnglishVisibleQuestText()
    {
        var targetText =
            "[7100]\r\n" +
            "MissionId=7100\r\n" +
            "Map=Twin City\u9286\u20AC\r\n" +
            "IntentionDesp=Register for the battle after 21:00.\u9286\u20AC\u9286\u20AC\r\n" +
            "Intention1=Visit [npc TaoistYu,8427,1000,519,649]\u9286\u20AC\r\n" +
            "Intention2=Collect 5 Soul Stones by killing monsters around the gate.\\n\u8DEFGive them to [mob SolarSaint,1002,0297,0146].\r\n" +
            "Prize=\u8DEF\r\n" +
            "Name=\u8DEF\u5bfb\u627eNPC\r\n";

        var result = LegacyDbQuestInfoMojibakeNormalizer.Normalize(targetText, out var updatedText);
        var second = LegacyDbQuestInfoMojibakeNormalizer.Normalize(updatedText, out var secondText);

        Assert.True(result.Changed);
        Assert.Equal(8, result.LinesScanned);
        Assert.Equal(5, result.VisibleTextFieldsScanned);
        Assert.Equal(3, result.NormalizedFields);
        Assert.Contains("Map=Twin City\u9286\u20AC\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("IntentionDesp=Register for the battle after 21:00.\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Intention1=Visit [npc TaoistYu,8427,1000,519,649]\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Intention2=Collect 5 Soul Stones by killing monsters around the gate.\\n- Give them to [mob SolarSaint,1002,0297,0146].\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Prize=\u8DEF\r\n", updatedText, StringComparison.Ordinal);
        Assert.Contains("Name=\u8DEF\u5bfb\u627eNPC\r\n", updatedText, StringComparison.Ordinal);
        Assert.False(second.Changed);
        Assert.Equal(0, second.NormalizedFields);
        Assert.Equal(updatedText, secondText);
    }

    [Fact]
    public async Task PatchDbQuestInfoMojibakeCommandPatchesTempQuestInfoAndPreservesUtf8NoBom()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "db-questinfo-mojibake-" + Guid.NewGuid());
        var questInfoPath = Path.Combine(tempRoot, "Questinfo.ini");
        Directory.CreateDirectory(tempRoot);
        try
        {
            await File.WriteAllTextAsync(
                questInfoPath,
                "[6101]\r\n" +
                "MissionId=6101\r\n" +
                "Intention1=Pay a visit to the [npc KungfuGirl,5674,1002,440,371]\u9286?\r\n" +
                "Content=Nezha\u9225\u6A9A trial isn\u9225\u6A9B done\u9286?\r\n" +
                "BeginNpcId=5674,1002,440,371,KungfuGirl\u9286?\r\n",
                Utf8NoBom);

            var firstRun = await RunDataToolAsync("patch-db-quest-info-mojibake", tempRoot);

            Assert.Equal(0, firstRun.ExitCode);
            Assert.Equal(string.Empty, firstRun.StandardError);
            Assert.Contains("DB Questinfo file:", firstRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Visible text fields scanned: 2", firstRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Normalized fields: 2", firstRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Changed file: True", firstRun.StandardOutput, StringComparison.Ordinal);

            var firstBytes = await File.ReadAllBytesAsync(questInfoPath);
            Assert.False(firstBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            var firstText = Utf8NoBom.GetString(firstBytes);
            Assert.Contains("Intention1=Pay a visit to the [npc KungfuGirl,5674,1002,440,371].\r\n", firstText, StringComparison.Ordinal);
            Assert.Contains("Content=Nezha's trial isn't done.\r\n", firstText, StringComparison.Ordinal);
            Assert.Contains("BeginNpcId=5674,1002,440,371,KungfuGirl\u9286?\r\n", firstText, StringComparison.Ordinal);

            var secondRun = await RunDataToolAsync("patch-db-quest-info-mojibake", questInfoPath);

            Assert.Equal(0, secondRun.ExitCode);
            Assert.Equal(string.Empty, secondRun.StandardError);
            Assert.Contains("Normalized fields: 0", secondRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Changed file: False", secondRun.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(firstText, Utf8NoBom.GetString(await File.ReadAllBytesAsync(questInfoPath)));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
