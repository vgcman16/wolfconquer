using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class Chinese7937XuanbaoInspectionCommandTests : IDisposable
{
    private const string Command = "client-xuanbao7937-inspect";
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-xuanbao-cli-" + Guid.NewGuid().ToString("N"));
    public Chinese7937XuanbaoInspectionCommandTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task SyntheticPinnedInputsProduceOnlyBoundedCountsAndHashes()
    {
        var fixture = WriteInputs();
        var result = await Run(Arguments(fixture));
        Assert.Equal(0, result.Exit); Assert.Equal("", result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var value = json.RootElement;
        Assert.Equal("accepted", value.GetProperty("Status").GetString());
        Assert.Equal(7937, value.GetProperty("ClientBuild").GetInt32());
        Assert.Equal(59, value.GetProperty("EchoFields").GetInt32());
        Assert.Equal(7, value.GetProperty("EchoCostRows").GetInt32());
        Assert.Equal(6, value.GetProperty("EchoOpeningRows").GetInt32());
        Assert.Equal(2, value.GetProperty("ScoreLimits").GetInt32());
        Assert.Equal(2, value.GetProperty("GoldLimitFlags").GetInt32());
        Assert.Equal(2, value.GetProperty("ComposeRows").GetInt32());
        Assert.Equal(3, value.GetProperty("RandomRows").GetInt32());
        Assert.Equal(Pin(fixture.Info), value.GetProperty("InfoSha256").GetString());
        Assert.Equal(Pin(fixture.Compose), value.GetProperty("ComposePackedSha256").GetString());
        Assert.Equal(Pin(fixture.Random), value.GetProperty("RandomPackedSha256").GetString());
        Assert.True(value.GetProperty("CatalogSyntaxAndPinsValidated").GetBoolean());
        foreach (var key in new[] { "MainSubReferencesValidated", "ServerBindingValidated", "RuntimeActivated", "LiveSupportValidated", "OutputFilesWritten" })
            Assert.False(value.GetProperty(key).GetBoolean());
        Assert.DoesNotContain(root, result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(fixture.Info, await File.ReadAllBytesAsync(fixture.InfoPath));
        Assert.Equal(fixture.Compose, await File.ReadAllBytesAsync(fixture.ComposePath));
        Assert.Equal(fixture.Random, await File.ReadAllBytesAsync(fixture.RandomPath));
        Assert.Equal(3, Directory.GetFiles(root).Length);
    }

    [Theory]
    [InlineData(3)] [InlineData(5)] [InlineData(7)]
    public async Task EveryPinMismatchRefusesWithoutRowsPathsOrWrites(int pinIndex)
    {
        var fixture = WriteInputs(); var args = Arguments(fixture); args[pinIndex] = new string('0', 64);
        var result = await Run(args);
        AssertRefusal(result, pinIndex == 3 ? "info" : "rules", "PinMismatch");
        Assert.Equal(3, Directory.GetFiles(root).Length);
        Assert.Equal(fixture.Info, await File.ReadAllBytesAsync(fixture.InfoPath));
        Assert.Equal(fixture.Compose, await File.ReadAllBytesAsync(fixture.ComposePath));
        Assert.Equal(fixture.Random, await File.ReadAllBytesAsync(fixture.RandomPath));
    }

    [Fact]
    public async Task MissingAndMalformedInputsAndClosedArgumentsRefuse()
    {
        var fixture = WriteInputs(); var args = Arguments(fixture);
        args[2] = Path.Combine(root, "missing-info");
        AssertRefusal(await Run(args), "info", "UnsafePath");
        args = Arguments(fixture); args[1] = "7931";
        AssertRefusal(await Run(args), "arguments", "ExpectedBuildAndThreePinnedFiles");
        args = Arguments(fixture); args[3] = "bad";
        AssertRefusal(await Run(args), "arguments", "InvalidSha256");
        var malformed = Encoding.ASCII.GetBytes("not a selected Lua table"); File.WriteAllBytes(fixture.InfoPath, malformed);
        args = Arguments(fixture); args[3] = Pin(malformed);
        var bad = await Run(args); Assert.Equal(1, bad.Exit); Assert.Equal("", bad.Error);
        using var json = JsonDocument.Parse(bad.Output);
        Assert.Equal("info", json.RootElement.GetProperty("Stage").GetString());
        Assert.DoesNotContain(root, bad.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, Directory.GetFiles(root).Length);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task MissingOrMalformedPackedRuleRefusesWithItsRealCoreFailure(bool compose)
    {
        var fixture = WriteInputs(); var args = Arguments(fixture);
        int pathIndex = compose ? 4 : 6, pinIndex = compose ? 5 : 7;
        args[pathIndex] = Path.Combine(root, "missing-packed");
        AssertRefusal(await Run(args), "rules", "UnsafePath");
        args = Arguments(fixture);
        var malformed = ClientItemCatalogCodec.Encode(Encoding.ASCII.GetBytes("1@@2@@3@@4@@\n"));
        File.WriteAllBytes(args[pathIndex], malformed); args[pinIndex] = Pin(malformed);
        AssertRefusal(await Run(args), "rules", "InvalidTable");
        Assert.Equal(3, Directory.GetFiles(root).Length);
    }
    private void AssertRefusal(Result result, string stage, string reason)
    {
        Assert.Equal(1, result.Exit); Assert.Equal("", result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal("refused", json.RootElement.GetProperty("Status").GetString());
        Assert.Equal(stage, json.RootElement.GetProperty("Stage").GetString());
        Assert.Equal(reason, json.RootElement.GetProperty("Reason").GetString());
        Assert.False(json.RootElement.GetProperty("OutputFilesWritten").GetBoolean());
        Assert.DoesNotContain(root, result.Output, StringComparison.OrdinalIgnoreCase);
    }

    private Fixture WriteInputs()
    {
        var f = new Fixture(Path.Combine(root, "info.lua"), Path.Combine(root, "compose.dat"), Path.Combine(root, "random.dat"),
            SyntheticInfo(), Chinese7937XuanbaoRulesFixture.ComposePacked(), Chinese7937XuanbaoRulesFixture.RandomPacked());
        File.WriteAllBytes(f.InfoPath, f.Info); File.WriteAllBytes(f.ComposePath, f.Compose); File.WriteAllBytes(f.RandomPath, f.Random); return f;
    }
    private static string[] Arguments(Fixture f) => [Command, "7937", f.InfoPath, Pin(f.Info), f.ComposePath, Pin(f.Compose), f.RandomPath, Pin(f.Random)];
    private static string Pin(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] SyntheticInfo()
    {
        var text = new StringBuilder("local XuanBaoEchoInfo = {\n[\"max_hole_num\"] = 5,\n[\"cost_num\"] = 0,\n");
        for (int hole = 1; hole <= 7; hole++)
        {
            foreach (var part in new[] { "cost_money_", "cost_itemtype_", "cost_itemnum_" })
                text.Append("[\"").Append(part).Append(hole.ToString(CultureInfo.InvariantCulture)).Append("\"] = 0,\n");
            if (hole == 1) continue;
            foreach (var part in new[] { "cost_num", "itemtype_1", "exp_1", "itemtype_2", "exp_2", "maxexp" })
                text.Append("[\"open_hole").Append(hole.ToString(CultureInfo.InvariantCulture)).Append('_').Append(part).Append("\"] = 0,\n");
        }
        text.Append("}\nlocal function GetXuanBaoEchoInfo(key)\nreturn XuanBaoEchoInfo[key]\nend\n")
            .Append("local XuanBaoScoreGradeStep = { [10] = { [1] = { ScoreLimit0=0, ScoreLimit8=100, }, }, }\n")
            .Append(Chinese7937XuanbaoAttributeFixture.ScoreGetter)
            .Append("local XuanBaoLimitAttri = { [10] = { [1] = true, [2] = false, }, }\n")
            .Append(Chinese7937XuanbaoAttributeFixture.GoldGetter)
            .Append("local function Unload()\nXuanBaoEchoInfo = nil\nXuanBaoScoreGradeStep = nil\nXuanBaoLimitAttri = nil\nend\n")
            .Append("local XuanBao = {\nGetXuanBaoEchoInfo = GetXuanBaoEchoInfo,\n")
            .Append("GetXuanBaoScoreGradeStep = GetXuanBaoScoreGradeStep,\nGetXuanBaoGoldLimitAttri = GetXuanBaoGoldLimitAttri,\nUnload = Unload,\n}\n")
            .Append("XuanBao_GetXuanBaoEchoInfo = XuanBao.GetXuanBaoEchoInfo\n")
            .Append(Chinese7937XuanbaoAttributeFixture.Aliases).Append("return XuanBao\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }

    private async Task<Result> Run(params string[] arguments)
    {
        using var process = TestProjectProcess.Create(Repository(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = root;
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start()); var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); Assert.Fail("Owned inspection command timed out."); }
        return new(process.ExitCode, await output, await error);
    }
    private static string Repository([CallerFilePath] string source = "")
    {
        for (DirectoryInfo? dir = new(Path.GetDirectoryName(source)!); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "WolfConquer.sln"))) return dir.FullName;
        throw new InvalidOperationException("Repository root missing.");
    }
    public void Dispose()
    {
        var full = Path.GetFullPath(root); var name = Path.GetFileName(full); const string prefix = "wolf-xuanbao-cli-";
        if (!string.Equals(Path.GetDirectoryName(full), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _) ||
            (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Invalid owned inspection cleanup root.");
        Directory.Delete(full, recursive: true);
    }
    private sealed record Fixture(string InfoPath, string ComposePath, string RandomPath, byte[] Info, byte[] Compose, byte[] Random);
    private sealed record Result(int Exit, string Output, string Error);
}
