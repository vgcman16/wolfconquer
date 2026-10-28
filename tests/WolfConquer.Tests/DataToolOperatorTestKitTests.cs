using System.Runtime.CompilerServices;
using System.Text.Json;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class DataToolOperatorTestKitTests : IDisposable
{
    private const string Prefix = "wolf-operator-kit-cli-";
    private readonly string root = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
    private readonly Guid domain = Guid.NewGuid();
    private string Output => Path.Combine(root, "request.json");

    public DataToolOperatorTestKitTests() => Directory.CreateDirectory(root);

    [Theory]
    [InlineData("basic-trojan", 9)]
    [InlineData("socket-materials", 6)]
    public async Task ActualChildCommandCreatesOnlyTheStrictRequestAndReportsNoSend(string kit, int rows)
    {
        var result = await RunAsync(Output, domain.ToString("N"), "2", "1293600", kit);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        var bytes = File.ReadAllBytes(Output);
        var request = LocalOperatorMailPipe.ParseRequest(bytes);
        Assert.NotEqual(Guid.Empty, request.OperationId);
        Assert.Equal(domain, request.ExpectedDomain);
        Assert.Equal(rows, request.Items.Count);
        using var report = JsonDocument.Parse(result.Output);
        Assert.Equal(request.OperationId, report.RootElement.GetProperty("operationId").GetGuid());
        Assert.True(report.RootElement.GetProperty("requestCreated").GetBoolean());
        Assert.False(report.RootElement.GetProperty("sent").GetBoolean());
        Assert.Equal([Output], Directory.GetFiles(root));
        Assert.Empty(Directory.GetDirectories(root));
        Assert.DoesNotContain("password", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("itemUid", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActualChildRefusesToOverwriteAnyExistingOutputWithoutRegeneratingIt()
    {
        Assert.Equal(0, (await RunAsync(Output, domain.ToString("N"), "2", "1293600", "basic-trojan")).ExitCode);
        var bytes = File.ReadAllBytes(Output);
        var stamp = File.GetLastWriteTimeUtc(Output);
        var result = await RunAsync(Output, domain.ToString("N"), "2", "1293600", "socket-materials");
        AssertRefusal(result);
        Assert.Equal(bytes, File.ReadAllBytes(Output));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(Output));
        Assert.Equal([Output], Directory.GetFiles(root));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("missing-parent")]
    [InlineData("zero-domain")]
    [InlineData("uppercase-domain")]
    [InlineData("zero-account")]
    [InlineData("bad-character")]
    [InlineData("unknown-kit")]
    public async Task ActualChildRejectsInvalidArgumentsBeforePublishingAnything(string kind)
    {
        var path = kind == "relative" ? "request.json" : kind == "missing-parent" ? Path.Combine(root, "absent", "request.json") : Output;
        var domainText = kind == "zero-domain" ? Guid.Empty.ToString("N") :
            kind == "uppercase-domain" ? "abcdefabcdefabcdefabcdefabcdefab".ToUpperInvariant() : domain.ToString("N");
        var result = await RunAsync(path, domainText, kind == "zero-account" ? "0" : "2",
            kind == "bad-character" ? "-1" : "1293600", kind == "unknown-kit" ? "anything" : "basic-trojan");
        AssertRefusal(result);
        Assert.Empty(Directory.GetFileSystemEntries(root));
    }

    [Fact]
    public async Task TwoActualChildrenRacingOneDestinationPublishExactlyOneRetainedOperation()
    {
        var results = await Task.WhenAll(
            RunAsync(Output, domain.ToString("N"), "2", "1293600", "basic-trojan"),
            RunAsync(Output, domain.ToString("N"), "2", "1293600", "socket-materials"));
        var winner = Assert.Single(results, result => result.ExitCode == 0);
        AssertRefusal(Assert.Single(results, result => result.ExitCode != 0));
        var request = LocalOperatorMailPipe.ParseRequest(File.ReadAllBytes(Output));
        using var report = JsonDocument.Parse(winner.Output);
        Assert.Equal(request.OperationId, report.RootElement.GetProperty("operationId").GetGuid());
        Assert.Equal([Output], Directory.GetFiles(root));
    }

    private static void AssertRefusal(RunResult result)
    {
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("was not created", result.Error);
        Assert.DoesNotContain("Unhandled exception", result.Error);
    }

    private async Task<RunResult> RunAsync(params string[] arguments)
    {
        using var process = TestProjectProcess.Create(FindRepositoryRoot(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = root;
        process.StartInfo.ArgumentList.Add("create-operator-test-kit");
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "Could not start the actual kit request command.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException primary)
        {
            try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception cleanup) { throw new AggregateException("Owned CLI timeout and cleanup both failed.", primary, cleanup); }
            throw new TimeoutException("The actual kit request command timed out.", primary);
        }
        return new(process.ExitCode, await output, await error);
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
        Assert.Equal(parent, Path.GetDirectoryName(target), OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.StartsWith(Prefix, Path.GetFileName(target), StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(Path.GetFileName(target)[Prefix.Length..], "N", out _));
        if (!Directory.Exists(target)) return;
        Assert.False((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0);
        Assert.Empty(Directory.GetDirectories(target));
        Directory.Delete(target, recursive: true);
    }

    private sealed record RunResult(int ExitCode, string Output, string Error);
}
