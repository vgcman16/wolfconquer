using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class SourceBackedStrResUnicodeCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolfconquer-strres-unicode-command-" + Guid.NewGuid().ToString("N"));
    private readonly Encoding _encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);

    public SourceBackedStrResUnicodeCommandTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitSourceDirectoryDiscoversOnlyExistingUnicodeResourceCandidate(bool nested)
    {
        var targetRoot = Path.Combine(_root, "owned-target");
        var target = Path.Combine(targetRoot, "ini", "StrRes.ini");
        var sourceRoot = Path.Combine(_root, "owned-reference");
        var source = Path.Combine(nested ? Path.Combine(sourceRoot, "ini") : sourceRoot, "UStrRes.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllBytes(target, _encoding.GetBytes("90001=\u4e2d\r\n90002=Keep ASCII\n"));
        var sourceBytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("90001=Existing English\r\n90002=Do not overwrite English\r\n")).ToArray();
        File.WriteAllBytes(source, sourceBytes);
        var sourceTimestamp = File.GetLastWriteTimeUtc(source);

        var result = await RunAsync(targetRoot, sourceRoot);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Error);
        Assert.Contains("Changed rows: 1", result.Output, StringComparison.Ordinal);
        Assert.Equal(_encoding.GetBytes("90001=Existing English\r\n90002=Keep ASCII\n"), File.ReadAllBytes(target));
        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
        Assert.Equal(sourceTimestamp, File.GetLastWriteTimeUtc(source));
        Assert.Equal(2, Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Length);
        var repeat = await RunAsync(targetRoot, sourceRoot);
        Assert.Equal(0, repeat.ExitCode);
        Assert.Contains("Changed rows: 0", repeat.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MalformedUnicodeReturnsDiagnosticFailureWithoutPublishingOrUnhandledException()
    {
        var targetRoot = Path.Combine(_root, "owned-target");
        var target = Path.Combine(targetRoot, "ini", "StrRes.ini");
        var sourceRoot = Path.Combine(_root, "owned-reference");
        var source = Path.Combine(sourceRoot, "UStrRes.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.CreateDirectory(sourceRoot);
        var original = _encoding.GetBytes("90001=\u4e2d\r\n");
        File.WriteAllBytes(target, original);
        byte[] sourceBytes = [0xFF, 0xFE, 0x00, 0xD8];
        File.WriteAllBytes(source, sourceBytes);
        var timestamp = File.GetLastWriteTimeUtc(target);

        var result = await RunAsync(targetRoot, sourceRoot);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("", result.Output);
        Assert.Contains("StrRes translation failed", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled exception", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_root, result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, File.ReadAllBytes(target));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(target));
        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
        Assert.Equal(2, Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Length);
    }

    private async Task<RunResult> RunAsync(string targetRoot, string sourceRoot)
    {
        using var process = TestProjectProcess.Create(FindRepositoryRoot(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = _root;
        process.StartInfo.ArgumentList.Add("patch-client-str-res-source-backed");
        process.StartInfo.ArgumentList.Add(targetRoot);
        process.StartInfo.ArgumentList.Add(sourceRoot);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool timed out. stdout: {await output} stderr: {await error}");
        }
        return new RunResult(process.ExitCode, await output, await error);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(sourcePath)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Could not locate WolfConquer.sln.");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed record RunResult(int ExitCode, string Output, string Error);
}
