using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyGmPolicyConfigurationTests
{
    private const string DomainText = "5ea475af-f3ef-48b1-848e-e29dd06bcb87";
    private const string Document = "{\"schemaVersion\":1,\"expectedDomain\":\"" + DomainText +
        "\",\"grants\":[{\"accountId\":2,\"characterUid\":1293600,\"capabilities\":[\"inspect.self\"]}]}";
    private const string DirectoryPrefix = "wolf-gm-policy-";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingConfigurationDisablesWithoutInventingAGrant(string? path)
    {
        Assert.True(LegacyGmPolicyConfiguration.TryLoad(path, out var policy, out var reason));
        Assert.Null(policy);
        Assert.Empty(reason);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("relative.json")]
    [InlineData("../policy.json")]
    [InlineData("\\\\localhost\\share\\policy.json")]
    [InlineData("//localhost/share/policy.json")]
    [InlineData("\\/localhost/share/policy.json")]
    [InlineData("/\\localhost/share/policy.json")]
    [InlineData("\\\\?\\C:\\policy.json")]
    [InlineData("\\\\.\\pipe\\policy")]
    public void AnExplicitInvalidPathRefusesInsteadOfSilentlyDisabling(string path)
    {
        Assert.False(LegacyGmPolicyConfiguration.TryLoad(path, out var policy, out var reason));
        Assert.Null(policy);
        Assert.NotEmpty(reason);
        Assert.DoesNotContain(path, reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RealOrdinaryPolicyIsOneIndependentImmutableStartupSnapshot() => WithDirectory(root =>
    {
        var path = Path.Combine(root, "policy.json");
        var bytes = Encoding.UTF8.GetBytes(Document);
        File.WriteAllBytes(path, bytes);
        Assert.True(LegacyGmPolicyConfiguration.TryLoad(path, out var loaded, out var reason), reason);
        var policy = Assert.IsType<LegacyGmPolicy>(loaded);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.True(policy.Allows(Guid.Parse(DomainText), 2, 1293600, LegacyGmCapability.InspectSelf));
        Assert.False(policy.Allows(Guid.Parse(DomainText), 2, 1293600, LegacyGmCapability.MailTestKitSelf));
        File.WriteAllText(path, "malformed replacement", new UTF8Encoding(false));
        Assert.True(policy.Allows(Guid.Parse(DomainText), 2, 1293600, LegacyGmCapability.InspectSelf));
        Assert.False(LegacyGmPolicyConfiguration.TryReadFile(path, out var replacement, out reason));
        Assert.Null(replacement);
        Assert.NotEmpty(reason);
    });

    [Fact]
    public void MissingFileAndDirectoryRefuseWithoutCreatingState() => WithDirectory(root =>
    {
        foreach (var path in new[] { Path.Combine(root, "missing.json"), root })
        {
            Assert.False(LegacyGmPolicyConfiguration.TryReadFile(path, out var policy, out var reason));
            Assert.Null(policy);
            Assert.NotEmpty(reason);
        }
        Assert.Empty(Directory.GetFileSystemEntries(root));
    });

    [Theory]
    [InlineData(0, false)]
    [InlineData(LegacyGmPolicy.MaximumBytes, true)]
    [InlineData(LegacyGmPolicy.MaximumBytes + 1, false)]
    public void FileByteLimitIsExactAndReadingDoesNotChangeInput(int length, bool accepted) => WithDirectory(root =>
    {
        var path = Path.Combine(root, "policy.json");
        var bytes = Encoding.UTF8.GetBytes(length == 0 ? "" : Document.PadRight(length));
        File.WriteAllBytes(path, bytes);
        Assert.Equal(accepted, LegacyGmPolicyConfiguration.TryReadFile(path, out var policy, out var reason));
        Assert.Equal(accepted, policy is not null);
        Assert.Equal(accepted, string.IsNullOrEmpty(reason));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(new[] { "policy.json" }, Directory.GetFiles(root).Select(Path.GetFileName));
    });

    [Theory]
    [InlineData("malformed")]
    [InlineData("bom")]
    [InlineData("utf8")]
    [InlineData("unknown")]
    public void BadDocumentCannotBecomeADefaultPolicy(string fault) => WithDirectory(root =>
    {
        var path = Path.Combine(root, "policy.json");
        byte[] bytes = fault switch
        {
            "bom" => new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(Document)).ToArray(),
            "utf8" => [0xff, 0xfe, 0xfd],
            "unknown" => Encoding.UTF8.GetBytes(Document[..^1] + ",\"role\":\"Admin\"}"),
            _ => Encoding.UTF8.GetBytes("{")
        };
        File.WriteAllBytes(path, bytes);
        Assert.False(LegacyGmPolicyConfiguration.TryReadFile(path, out var policy, out var reason));
        Assert.Null(policy);
        Assert.NotEmpty(reason);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    });

    private static void WithDirectory(Action<string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), DirectoryPrefix + Guid.NewGuid().ToString("N"));
        Exception? primary = null;
        try { Directory.CreateDirectory(root); test(root); }
        catch (Exception error) { primary = error; }
        try { DeleteOwnedDirectory(root); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException("GM policy test and owned cleanup failed.", primary, cleanup);
            throw;
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    private static async Task WithDirectoryAsync(Func<string, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), DirectoryPrefix + Guid.NewGuid().ToString("N"));
        Exception? primary = null;
        try { Directory.CreateDirectory(root); await test(root); }
        catch (Exception error) { primary = error; }
        try { DeleteOwnedDirectory(root); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException("GM policy test and owned cleanup failed.", primary, cleanup);
            throw;
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    private static void DeleteOwnedDirectory(string root)
    {
        var full = ValidateOwnedDirectoryRoot(root);
        if (!Directory.Exists(full)) return;
        var pending = new Stack<(string Path, bool ChildrenDeleted)>();
        pending.Push((full, false));
        while (pending.TryPop(out var entry))
        {
            var path = Path.GetFullPath(entry.Path);
            if (!SamePolicyPath(path, full) && !path.StartsWith(full + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the owned GM policy directory.");
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Refusing to follow a remaining GM policy fixture link.");
            if ((attributes & FileAttributes.Directory) == 0) { File.Delete(path); continue; }
            if (entry.ChildrenDeleted) { Directory.Delete(path, recursive: false); continue; }
            pending.Push((path, true));
            foreach (var child in Directory.GetFileSystemEntries(path)) pending.Push((child, false));
        }
    }

    private static string ValidateOwnedDirectoryRoot(string root)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var name = Path.GetFileName(full);
        if (!string.Equals(Path.GetDirectoryName(full), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            !name.StartsWith(DirectoryPrefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[DirectoryPrefix.Length..], "N", out _) ||
            Directory.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing cleanup outside the owned GM policy directory.");
        return full;
    }

    private static bool SamePolicyPath(string first, string second) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
