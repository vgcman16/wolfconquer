using System.Diagnostics;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigDecodeCommandTests
{
    [Theory]
    [InlineData("input")]
    [InlineData("modulus")]
    [InlineData("words")]
    [InlineData("existing-file")]
    [InlineData("directory")]
    public async Task ExistingOutputsAndEveryInputAliasArePreserved(string kind)
    {
        var output = kind switch { "input" => Input, "modulus" => Modulus, "words" => Words, _ => Output };
        if (kind == "existing-file") File.WriteAllBytes(output, [8, 7, 6, 5]);
        if (kind == "directory") Directory.CreateDirectory(output);
        var before = CaptureFiles();
        var directories = Directory.GetDirectories(root);

        var result = await RunAsync(Replace(Arguments(), "--output-new", output));

        AssertFailure(result);
        AssertFiles(before);
        Assert.Equal(directories, Directory.GetDirectories(root));
    }

    [Theory]
    [InlineData("input")]
    [InlineData("public-data")]
    [InlineData("output")]
    public async Task LinkedAncestorsCannotRedirectTheExplicitReadOrWriteRoles(string role)
    {
        var physical = Path.Combine(root, "physical");
        var linked = Path.Combine(root, "linked");
        Directory.CreateDirectory(physical);
        var args = Arguments();
        string? physicalInput = null;
        if (role != "output")
        {
            var source = role == "input" ? Input : Modulus;
            physicalInput = Path.Combine(physical, Path.GetFileName(source));
            File.Copy(source, physicalInput);
            args = Replace(args, role == "input" ? "--input" : "--modulus-file", Path.Combine(linked, Path.GetFileName(source)));
        }
        else args = Replace(args, "--output-new", Path.Combine(linked, "must-not-exist.bin"));
        var physicalBefore = physicalInput is null ? null : File.ReadAllBytes(physicalInput);
        var before = CaptureFiles();
        await CreateDirectoryLink(linked, physical);
        Exception? primaryFailure = null;
        try
        {
            var result = await RunAsync(args);

            AssertFailure(result);
            Assert.False(File.Exists(Output));
            Assert.False(File.Exists(Path.Combine(physical, "must-not-exist.bin")));
            if (physicalInput is not null) Assert.Equal(physicalBefore, File.ReadAllBytes(physicalInput));
        }
        catch (Exception primary) { primaryFailure = primary; throw; }
        finally
        {
            try { Directory.Delete(linked); }
            catch (Exception cleanup) when (primaryFailure is not null)
            { throw new AggregateException("Linked-path test and owned link cleanup both failed.", primaryFailure, cleanup); }
        }
        AssertFiles(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualHardlinkedInputIsRefusedWithoutChangingEitherName(bool publicData)
    {
        var original = publicData ? Modulus : Input;
        var alias = Path.Combine(root, "hardlinked-input.bin");
        await CreateHardLink(alias, original);
        var before = CaptureFiles();

        var result = await RunAsync(Replace(Arguments(), publicData ? "--modulus-file" : "--input", alias));

        AssertFailure(result);
        AssertFiles(before);
        Assert.False(File.Exists(Output));
    }

    [Fact]
    public async Task RealDeniedInputReadCannotBecomeASuccessfulEmptyOutput()
    {
        var before = CaptureFiles();
        using (File.Open(Input, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await RunAsync(Arguments());
            AssertFailure(result);
            Assert.False(File.Exists(Output));
        }
        AssertFiles(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingInputOrOutputParentIsNotCreatedImplicitly(bool outputParent)
    {
        var before = CaptureFiles();
        var args = Replace(Arguments(), outputParent ? "--output-new" : "--input", Path.Combine(root, "missing-parent", "file.bin"));

        var result = await RunAsync(args);

        AssertFailure(result);
        AssertFiles(before);
        Assert.Empty(Directory.GetDirectories(root));
    }

    private static async Task CreateDirectoryLink(string link, string target)
    {
        if (OperatingSystem.IsWindows()) await RunLinkCommand("cmd.exe", ["/c", "mklink", "/J", link, target]);
        else Directory.CreateSymbolicLink(link, target);
    }

    private static Task CreateHardLink(string link, string target) => OperatingSystem.IsWindows()
        ? RunLinkCommand("cmd.exe", ["/c", "mklink", "/H", link, target])
        : RunLinkCommand("ln", ["--", target, link]);

    private static async Task RunLinkCommand(string command, string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(command)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true },
        };
        foreach (var value in arguments) process.StartInfo.ArgumentList.Add(value);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException primary)
        {
            try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception cleanup) { throw new AggregateException("Owned link creation timed out and cleanup failed.", primary, cleanup); }
            throw new TimeoutException("Owned link creation timed out.", primary);
        }
        Assert.True(process.ExitCode == 0, $"Owned link creation failed: {await output} {await error}");
    }
}
