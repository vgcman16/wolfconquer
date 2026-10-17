using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientFileDriftReporterTests : IDisposable
{
    private readonly string _workRoot = Path.Combine(Path.GetTempPath(), "wolf-client-file-drift-" + Guid.NewGuid().ToString("N"));
    private readonly string _originalRoot;
    private readonly string _patchedRoot;

    public ClientFileDriftReporterTests()
    {
        _originalRoot = Path.Combine(_workRoot, "original");
        _patchedRoot = Path.Combine(_workRoot, "patched");
        Directory.CreateDirectory(_originalRoot);
        Directory.CreateDirectory(_patchedRoot);
    }

    [Fact]
    public void CompareReportsChangedMissingAndExtraFilesWithLengthAndHashMetadata()
    {
        var sameBytes = Encoding.ASCII.GetBytes("same");
        var changedOriginalBytes = Encoding.ASCII.GetBytes("alpha");
        var changedPatchedBytes = Encoding.ASCII.GetBytes("bravo");
        var grownOriginalBytes = Encoding.ASCII.GetBytes("one");
        var grownPatchedBytes = Encoding.ASCII.GetBytes("three");
        var missingBytes = new byte[] { 1, 2, 3 };
        var extraBytes = new byte[] { 0xfe, 0xed };

        WriteOriginalFile("ini/same.ini", sameBytes);
        WritePatchedFile("ini/same.ini", sameBytes);
        WriteOriginalFile("ini/changed-same-length.ini", changedOriginalBytes);
        WritePatchedFile("ini/changed-same-length.ini", changedPatchedBytes);
        WriteOriginalFile("ini/changed-length.ini", grownOriginalBytes);
        WritePatchedFile("ini/changed-length.ini", grownPatchedBytes);
        WriteOriginalFile("ani/missing.c3", missingBytes);
        WritePatchedFile("data/extra.dat", extraBytes);

        var report = ClientFileDriftReporter.Compare(_originalRoot, _patchedRoot);

        Assert.True(report.HasDrift);
        Assert.Equal(Path.GetFullPath(_originalRoot), report.OriginalClientRoot);
        Assert.Equal(Path.GetFullPath(_patchedRoot), report.PatchedClientRoot);
        Assert.Collection(
            report.ChangedFiles,
            grown =>
            {
                Assert.Equal("ini/changed-length.ini", grown.RelativePath);
                AssertDriftFile(grown.OriginalFile, "ini/changed-length.ini", grownOriginalBytes);
                AssertDriftFile(grown.PatchedFile, "ini/changed-length.ini", grownPatchedBytes);
            },
            sameLength =>
            {
                Assert.Equal("ini/changed-same-length.ini", sameLength.RelativePath);
                Assert.Equal(sameLength.OriginalFile.Length, sameLength.PatchedFile.Length);
                Assert.NotEqual(sameLength.OriginalFile.Sha256, sameLength.PatchedFile.Sha256);
                AssertDriftFile(sameLength.OriginalFile, "ini/changed-same-length.ini", changedOriginalBytes);
                AssertDriftFile(sameLength.PatchedFile, "ini/changed-same-length.ini", changedPatchedBytes);
            });
        var missing = Assert.Single(report.MissingFiles);
        AssertDriftFile(missing, "ani/missing.c3", missingBytes);
        var extra = Assert.Single(report.ExtraFiles);
        AssertDriftFile(extra, "data/extra.dat", extraBytes);
    }

    [Fact]
    public void CompareDoesNotReportIdenticalRoots()
    {
        var configBytes = Encoding.ASCII.GetBytes("[Config]\r\nValue=1\r\n");
        var binaryBytes = new byte[] { 0x00, 0x10, 0x20 };
        WriteOriginalFile("ini/config.ini", configBytes);
        WritePatchedFile("ini/config.ini", configBytes);
        WriteOriginalFile("data/static.bin", binaryBytes);
        WritePatchedFile("data/static.bin", binaryBytes);

        var report = ClientFileDriftReporter.Compare(_originalRoot, _patchedRoot);

        Assert.False(report.HasDrift);
        Assert.Empty(report.ChangedFiles);
        Assert.Empty(report.MissingFiles);
        Assert.Empty(report.ExtraFiles);
    }

    [Fact]
    public void CompareCanExcludeLogAndDebugFiles()
    {
        WriteOriginalFile("debug/trace.txt", Encoding.ASCII.GetBytes("old trace"));
        WritePatchedFile("debug/trace.txt", Encoding.ASCII.GetBytes("new trace"));
        WriteOriginalFile("logs/client.log", Encoding.ASCII.GetBytes("old log"));
        WritePatchedFile("logs/client.log", Encoding.ASCII.GetBytes("new log"));
        WritePatchedFile("crash.dmp", new byte[] { 0xde, 0xad });
        WritePatchedFile("cache/local.tmp", Encoding.ASCII.GetBytes("temp"));
        WriteOriginalFile("ini/config.ini", Encoding.ASCII.GetBytes("before"));
        WritePatchedFile("ini/config.ini", Encoding.ASCII.GetBytes("after"));

        var report = ClientFileDriftReporter.Compare(
            _originalRoot,
            _patchedRoot,
            new ClientFileDriftOptions
            {
                ExcludeLogAndDebugFiles = true,
            });

        var changed = Assert.Single(report.ChangedFiles);
        Assert.Equal("ini/config.ini", changed.RelativePath);
        Assert.Empty(report.MissingFiles);
        Assert.Empty(report.ExtraFiles);
    }

    [Fact]
    public void CompareCanUseCustomRelativePathExclusion()
    {
        WriteOriginalFile("ini/keep.ini", Encoding.ASCII.GetBytes("before"));
        WritePatchedFile("ini/keep.ini", Encoding.ASCII.GetBytes("after"));
        WritePatchedFile("ini/generated.cache", Encoding.ASCII.GetBytes("generated"));

        var report = ClientFileDriftReporter.Compare(
            _originalRoot,
            _patchedRoot,
            new ClientFileDriftOptions
            {
                ExcludeRelativePath = relativePath => relativePath.EndsWith(".cache", StringComparison.OrdinalIgnoreCase),
            });

        var changed = Assert.Single(report.ChangedFiles);
        Assert.Equal("ini/keep.ini", changed.RelativePath);
        Assert.Empty(report.MissingFiles);
        Assert.Empty(report.ExtraFiles);
    }

    [Fact]
    public void CompareCanLimitSnapshotsToIncludedRelativePaths()
    {
        WriteOriginalFile("ini/owned.ini", Encoding.ASCII.GetBytes("before"));
        WritePatchedFile("ini/owned.ini", Encoding.ASCII.GetBytes("after"));
        WriteOriginalFile("ini/unowned.ini", Encoding.ASCII.GetBytes("old"));
        WritePatchedFile("ini/unowned.ini", Encoding.ASCII.GetBytes("new"));
        WritePatchedFile("data/unowned-extra.dat", Encoding.ASCII.GetBytes("extra"));

        var report = ClientFileDriftReporter.Compare(
            _originalRoot,
            _patchedRoot,
            new ClientFileDriftOptions
            {
                IncludeRelativePaths = ["ini/owned.ini"],
            });

        var changed = Assert.Single(report.ChangedFiles);
        Assert.Equal("ini/owned.ini", changed.RelativePath);
        Assert.Empty(report.MissingFiles);
        Assert.Empty(report.ExtraFiles);
    }

    [Fact]
    public void CompareReportsMissingRequiredPatchedFileEvenWhenOriginalAlsoLacksIt()
    {
        var stableBytes = Encoding.ASCII.GetBytes("unchanged");
        WriteOriginalFile("ini/stable.ini", stableBytes);
        WritePatchedFile("ini/stable.ini", stableBytes);

        var report = ClientFileDriftReporter.Compare(
            _originalRoot,
            _patchedRoot,
            new ClientFileDriftOptions
            {
                RequiredPatchedRelativePaths = ["ini/flashLogin.ini"],
            });

        Assert.True(report.HasDrift);
        Assert.Empty(report.ChangedFiles);
        Assert.Empty(report.MissingFiles);
        Assert.Empty(report.ExtraFiles);
        var missingRequired = Assert.Single(report.MissingRequiredPatchedFiles);
        Assert.Equal("ini/flashLogin.ini", missingRequired.RelativePath);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(_patchedRoot, "ini", "flashLogin.ini")),
            missingRequired.PatchedPath);
    }

    [Fact]
    public void CompareAcceptsRequiredPatchedFileOutsideIncludedSnapshotScope()
    {
        var stableBytes = Encoding.ASCII.GetBytes("unchanged");
        WriteOriginalFile("ini/stable.ini", stableBytes);
        WritePatchedFile("ini/stable.ini", stableBytes);
        WritePatchedFile("loader/cqimiver.txt", Encoding.ASCII.GetBytes("local loader route"));

        var report = ClientFileDriftReporter.Compare(
            _originalRoot,
            _patchedRoot,
            new ClientFileDriftOptions
            {
                IncludeRelativePaths = ["ini/stable.ini"],
                RequiredPatchedRelativePaths = ["loader/cqimiver.txt"],
            });

        Assert.False(report.HasDrift);
        Assert.Empty(report.ChangedFiles);
        Assert.Empty(report.MissingFiles);
        Assert.Empty(report.ExtraFiles);
        Assert.Empty(report.MissingRequiredPatchedFiles);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workRoot))
        {
            Directory.Delete(_workRoot, recursive: true);
        }
    }

    private void WriteOriginalFile(string relativePath, byte[] bytes)
    {
        WriteFile(_originalRoot, relativePath, bytes);
    }

    private void WritePatchedFile(string relativePath, byte[] bytes)
    {
        WriteFile(_patchedRoot, relativePath, bytes);
    }

    private static void WriteFile(string root, string relativePath, byte[] bytes)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static void AssertDriftFile(ClientFileDriftFile file, string relativePath, byte[] bytes)
    {
        Assert.Equal(relativePath, file.RelativePath);
        Assert.Equal(bytes.Length, file.Length);
        Assert.Equal(Sha256Hex(bytes), file.Sha256);
        Assert.Equal(file.Sha256.ToLowerInvariant(), file.Sha256);
        Assert.Matches("^[0-9a-f]{64}$", file.Sha256);
    }

    private static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
