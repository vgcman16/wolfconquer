using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937ItemAlignmentCommandTests : IDisposable
{
    private const string Command = "client-item-catalog7937-align";
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-7937-align-" + Guid.NewGuid().ToString("N"));
    public Chinese7937ItemAlignmentCommandTests() => Directory.CreateDirectory(root);
    private sealed record Result(int Exit, string Output, string Error);
    private sealed record Inputs(string Reference, string Current, string ReferencePin, string CurrentPin, byte[] ReferenceBytes, byte[] CurrentBytes);

    private async Task<Inputs> WriteInputs(ClientItemCatalog7937AlignmentTests.CatalogPair bytes)
    {
        var reference = Path.Combine(root, "private-reference.decoded");
        var current = Path.Combine(root, "private-current.decoded");
        await File.WriteAllBytesAsync(reference, bytes.Reference); await File.WriteAllBytesAsync(current, bytes.Current);
        return new(reference, current, Pin(bytes.Reference), Pin(bytes.Current), bytes.Reference, bytes.Current);
    }

    private static string Pin(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private Task<Result> Inspect(Inputs inputs) => Run(Command, "7937", inputs.Reference, inputs.ReferencePin, inputs.Current, inputs.CurrentPin);

    private async Task<Result> Run(params string[] args)
    {
        using var process = TestProjectProcess.Create(Repository(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = root;
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); Assert.Fail("Owned item alignment command timed out."); }
        return new(process.ExitCode, await output, await error);
    }

    private async Task Preserved(Inputs inputs)
    {
        Assert.Equal(inputs.ReferenceBytes, await File.ReadAllBytesAsync(inputs.Reference));
        Assert.Equal(inputs.CurrentBytes, await File.ReadAllBytesAsync(inputs.Current));
        Assert.Equal(new[] { inputs.Current, inputs.Reference }.Order(), Directory.GetFiles(root).Order());
        Assert.Empty(Directory.GetDirectories(root));
    }

    private JsonDocument ReadResult(Result result, bool success)
    {
        Assert.Equal(success ? 0 : 1, result.Exit);
        Assert.Equal("", success ? result.Error : result.Output);
        var json = JsonDocument.Parse(success ? result.Output : result.Error);
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "FormatVersion", "ClientVersion", "Status", "Failure", "ReferenceRows", "CurrentRows", "SharedIdentities",
            "ReferenceOnlyIdentities", "CurrentOnlyIdentities", "FramedVectorBytes", "ReferenceFields", "CurrentFields",
            "ComparedReferenceColumns", "ColumnsWithoutExactCandidate", "ColumnsWithOneExactCandidate",
            "ColumnsWithMultipleExactCandidates", "ReferenceSha256", "CurrentSha256", "SemanticRolesProved", "RuntimeBinding",
            "GameplayActivated", "InputsWritten", "OutputFilesWritten",
        };
        foreach (var property in json.RootElement.EnumerateObject())
        {
            Assert.Contains(property.Name, allowed);
            Assert.DoesNotContain(property.Value.ValueKind, new[] { JsonValueKind.Array, JsonValueKind.Object });
            if (property.Value.ValueKind != JsonValueKind.String) continue;
            var text = property.Value.GetString()!;
            foreach (var forbidden in new[] { root, "private-source", "private-current", "private-reference", "secret-field-value", "Unhandled exception" })
                Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(1, json.RootElement.GetProperty("FormatVersion").GetInt32());
        Assert.Equal(7937, json.RootElement.GetProperty("ClientVersion").GetInt32());
        foreach (var flag in new[] { "SemanticRolesProved", "RuntimeBinding", "GameplayActivated", "InputsWritten", "OutputFilesWritten" })
            Assert.False(json.RootElement.GetProperty(flag).GetBoolean());
        return json;
    }

    private static string Repository([CallerFilePath] string file = "")
    {
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(file)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository root missing.");
    }

    public void Dispose()
    {
        var full = Path.GetFullPath(root); var name = Path.GetFileName(full); const string prefix = "wolf-7937-align-";
        if (!string.Equals(Path.GetDirectoryName(full), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _) ||
            (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Invalid owned alignment cleanup root.");
        Directory.Delete(full, recursive: true);
    }
}
