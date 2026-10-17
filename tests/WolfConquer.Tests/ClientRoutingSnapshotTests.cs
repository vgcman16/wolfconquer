using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientRoutingSnapshotTests : IDisposable
{
    private readonly string _clientRoot = Path.Combine(Path.GetTempPath(), "wolf-client-routing-snapshot-" + Guid.NewGuid().ToString("N"));

    public ClientRoutingSnapshotTests()
    {
        Directory.CreateDirectory(_clientRoot);
    }

    [Fact]
    public void DefaultRelativePathsIncludeRoutingSensitiveAndParserCriticalClientFiles()
    {
        Assert.Contains("server38.dat", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("ini/ndloginServer.dat", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("ini/flashLogin.ini", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("ini/StrRes.ini", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("ini/region.ini", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("ini/restrain.ini", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("AutoPatch/SocketConfig.ini", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("GuardShield.dat", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("GuardShield.ini", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("Start-Client.cmd", ClientRoutingSnapshotter.DefaultRelativePaths);
        Assert.Contains("游蝶登录器.bat", ClientRoutingSnapshotter.DefaultRelativePaths);
    }

    [Fact]
    public void CreateSnapshotsExistingDefaultFilesInDefaultOrderAndSkipsMissingFiles()
    {
        var capturedAtUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var server38Bytes = Encoding.ASCII.GetBytes("server-list\r\n");
        var flashLoginBytes = new byte[] { 0x00, 0xab, 0xcd, 0xef };
        var regionBytes = Encoding.ASCII.GetBytes("1 1002 430 380 440 390 0 TwinCity 0 0 0 0 none none\r\n");
        var restrainBytes = Encoding.ASCII.GetBytes("1 2 2xDmg:TestMonster;NoStack\r\n");
        var socketConfigBytes = Encoding.ASCII.GetBytes("Server4=U4 198.51.100.11 9537 198.51.100.17 9532 1 1\r\n");
        var guardShieldIniBytes = Encoding.UTF8.GetBytes("enabled=true\r\n");
        var startClientBytes = Encoding.ASCII.GetBytes("@echo off\r\nstart Conquer.exe\r\n");

        WriteClientFile("ini/flashLogin.ini", flashLoginBytes);
        WriteClientFile("ini/region.ini", regionBytes);
        WriteClientFile("ini/restrain.ini", restrainBytes);
        WriteClientFile("AutoPatch/SocketConfig.ini", socketConfigBytes);
        WriteClientFile("Start-Client.cmd", startClientBytes);
        WriteClientFile("GuardShield.ini", guardShieldIniBytes);
        WriteClientFile("server38.dat", server38Bytes);

        var manifest = ClientRoutingSnapshotter.Create(_clientRoot, capturedAtUtc);

        Assert.Equal(capturedAtUtc, manifest.CapturedAtUtc);
        Assert.Equal(_clientRoot, manifest.ClientRoot);
        Assert.Equal(
            [
                "server38.dat",
                "ini/flashLogin.ini",
                "ini/region.ini",
                "ini/restrain.ini",
                "AutoPatch/SocketConfig.ini",
                "GuardShield.ini",
                "Start-Client.cmd",
            ],
            manifest.Files.Select(file => file.RelativePath));
        Assert.DoesNotContain(manifest.Files, file => file.RelativePath == "ini/ndloginServer.dat");
        Assert.DoesNotContain(manifest.Files, file => file.RelativePath == "ini/StrRes.ini");
        Assert.DoesNotContain(manifest.Files, file => file.RelativePath == "GuardShield.dat");
        Assert.DoesNotContain(manifest.Files, file => file.RelativePath == "游蝶登录器.bat");

        AssertSnapshotFile(manifest.Files[0], "server38.dat", server38Bytes);
        AssertSnapshotFile(manifest.Files[1], "ini/flashLogin.ini", flashLoginBytes);
        AssertSnapshotFile(manifest.Files[2], "ini/region.ini", regionBytes);
        AssertSnapshotFile(manifest.Files[3], "ini/restrain.ini", restrainBytes);
        AssertSnapshotFile(manifest.Files[4], "AutoPatch/SocketConfig.ini", socketConfigBytes);
        AssertSnapshotFile(manifest.Files[5], "GuardShield.ini", guardShieldIniBytes);
        AssertSnapshotFile(manifest.Files[6], "Start-Client.cmd", startClientBytes);
        Assert.Equal(server38Bytes, File.ReadAllBytes(Path.Combine(_clientRoot, "server38.dat")));
    }

    [Fact]
    public void WriteManifestWritesUtf8HeadersAndFileLines()
    {
        var capturedAtUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var manifest = new ClientRoutingSnapshotManifest(
            capturedAtUtc,
            @"C:\client root",
            [
                new ClientRoutingSnapshotFile("server38.dat", 11, "abc123", "def456"),
                new ClientRoutingSnapshotFile("ini/flashLogin.ini", 4, "0011aa", "bb22cc"),
            ]);
        var outputPath = Path.Combine(_clientRoot, "routing-manifest.txt");

        ClientRoutingSnapshotter.WriteManifest(outputPath, manifest);

        var bytes = File.ReadAllBytes(outputPath);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal(
            "CapturedAtUtc=2026-01-02T03:04:05.0000000+00:00\n" +
            "ClientRoot=C:\\client root\n" +
            "server38.dat | SHA256=abc123 | MD5=def456 | Length=11\n" +
            "ini/flashLogin.ini | SHA256=0011aa | MD5=bb22cc | Length=4\n",
            Encoding.UTF8.GetString(bytes));
    }

    public void Dispose()
    {
        if (Directory.Exists(_clientRoot))
        {
            Directory.Delete(_clientRoot, recursive: true);
        }
    }

    private void WriteClientFile(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(_clientRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static void AssertSnapshotFile(ClientRoutingSnapshotFile file, string relativePath, byte[] bytes)
    {
        Assert.Equal(relativePath, file.RelativePath);
        Assert.Equal(bytes.Length, file.Length);
        Assert.Equal(Sha256Hex(bytes), file.Sha256);
        Assert.Equal(Md5Hex(bytes), file.Md5);
        Assert.Equal(file.Sha256.ToLowerInvariant(), file.Sha256);
        Assert.Equal(file.Md5.ToLowerInvariant(), file.Md5);
        Assert.Matches("^[0-9a-f]{64}$", file.Sha256);
        Assert.Matches("^[0-9a-f]{32}$", file.Md5);
    }

    private static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string Md5Hex(byte[] bytes)
    {
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }
}
