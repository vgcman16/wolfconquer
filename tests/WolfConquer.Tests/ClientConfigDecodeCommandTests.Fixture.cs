using System.Runtime.CompilerServices;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigDecodeCommandTests : IDisposable
{
    private const string Command = "client-config-decode";
    // Existing independently signed synthetic arithmetic vectors; no game data,
    // native key constants, private key or production encoder is used here.
    private const string ModulusHex = "FFFFFFFFFFFFFF720000000000001321";
    private const string RawBlockHex = "F422BEBCB2583EF983DF19472A45CC44";
    private const string GzipBlocksHex =
        "9F3C03CBBEB338D77BF1DAF2EAFB2BB20F24F0F7983280ABE67CCF05C2A41078CCCD23ABFF5E260449A8302953CD542B" +
        "570C820643C2E4A57645ADBA901AE81099E49592D6F9C467C84BBC927594184F";
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-config-decode-cli-" + Guid.NewGuid().ToString("N"));
    private string Input => Path.Combine(root, "synthetic-input.bin");
    private string Modulus => Path.Combine(root, "synthetic-public-modulus.bin");
    private string Words => Path.Combine(root, "synthetic-words.json");
    private string Output => Path.Combine(root, "owned-decoded.bin");

    public ClientConfigDecodeCommandTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Input, Convert.FromHexString(RawBlockHex));
        File.WriteAllBytes(Modulus, Convert.FromHexString(ModulusHex));
        // Independent sparse inverse golden for this synthetic modulus, retained
        // from the existing arithmetic-vector tests rather than production Encode.
        var words = new uint[64];
        Array.Fill(words, 0x13ACu, 0, 44);
        words[44] = 0xFFFFEC53;
        words[45] = words[46] = 0x1321;
        File.WriteAllBytes(Words, JsonSerializer.SerializeToUtf8Bytes(words));
    }

    private string[] Arguments(bool words = false, string? transformed = null, string? result = null) =>
    [
        "--profile", "d13-default", "--input", Input,
        words ? "--encoded-words-file" : "--modulus-file", words ? Words : Modulus,
        "--output-new", Output, "--max-transformed-bytes", transformed ?? "64", "--max-result-bytes", result ?? "64",
    ];

    private static string[] Replace(string[] args, string key, string value)
    {
        var result = args.ToArray();
        var index = Array.IndexOf(result, key);
        Assert.True(index >= 0);
        result[index + 1] = value;
        return result;
    }

    private Dictionary<string, (byte[] Bytes, DateTime Time)> CaptureFiles() =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => Path.GetRelativePath(root, path),
            path => (File.ReadAllBytes(path), File.GetLastWriteTimeUtc(path)), StringComparer.Ordinal);

    private void AssertFiles(Dictionary<string, (byte[] Bytes, DateTime Time)> expected)
    {
        var actual = CaptureFiles();
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value.Bytes, actual[pair.Key].Bytes);
            Assert.Equal(pair.Value.Time, actual[pair.Key].Time);
        }
    }

    private async Task<RunResult> RunAsync(params string[] arguments)
    {
        using var process = TestProjectProcess.Create(FindRepositoryRoot(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = root;
        process.StartInfo.ArgumentList.Add(Command);
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "Could not start the actual DataTool command.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException primary)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception cleanup) { throw new AggregateException("CLI timed out and process cleanup failed.", primary, cleanup); }
            throw new TimeoutException("The actual client-config-decode command timed out.", primary);
        }
        return new(process.ExitCode, await output, await error);
    }

    private void AssertFailure(RunResult result)
    {
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("Client config decode failed:", result.Error);
        Assert.Contains("OutputPublished=False", result.Error);
        Assert.DoesNotContain("Unhandled exception", result.Error);
        Assert.DoesNotContain(root, result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ModulusHex, result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot([CallerFilePath] string path = "")
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Could not locate WolfConquer.sln.");
    }

    public void Dispose()
    {
        var target = Path.GetFullPath(root);
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var name = Path.GetFileName(target);
        Assert.Equal(parent, Path.GetDirectoryName(target), OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.StartsWith("wolf-config-decode-cli-", name, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(name["wolf-config-decode-cli-".Length..], "N", out _));
        if (!Directory.Exists(target)) return;
        Assert.False((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0, "Owned fixture root was replaced by a link.");
        foreach (var directory in Directory.GetDirectories(target))
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) Directory.Delete(directory);
        Directory.Delete(target, recursive: true);
    }

    private sealed record RunResult(int ExitCode, string Output, string Error);
}
