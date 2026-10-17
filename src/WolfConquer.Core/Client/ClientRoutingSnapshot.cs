using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.Client;

public sealed record ClientRoutingSnapshotFile(string RelativePath, long Length, string Sha256, string Md5);

public sealed record ClientRoutingSnapshotManifest(
    DateTimeOffset CapturedAtUtc,
    string ClientRoot,
    IReadOnlyList<ClientRoutingSnapshotFile> Files);

public static class ClientRoutingSnapshotter
{
    public static IReadOnlyList<string> DefaultRelativePaths { get; } =
    [
        "server38.dat",
        "ini/ndloginServer.dat",
        "ini/flashLogin.ini",
        "ini/StrRes.ini",
        "ini/region.ini",
        "ini/restrain.ini",
        "AutoPatch/SocketConfig.ini",
        "GuardShield.dat",
        "GuardShield.ini",
        "Start-Client.cmd",
        "游蝶登录器.bat",
    ];

    public static ClientRoutingSnapshotManifest Create(
        string clientRoot,
        DateTimeOffset capturedAtUtc,
        IReadOnlyList<string>? relativePaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientRoot);
        var fullClientRoot = Path.GetFullPath(clientRoot);
        var files = new List<ClientRoutingSnapshotFile>();

        foreach (var relativePath in relativePaths ?? DefaultRelativePaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
            var normalizedRelativePath = NormalizeRelativePath(relativePath);
            var fullPath = Path.Combine(
                fullClientRoot,
                normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(fullPath);
            files.Add(new ClientRoutingSnapshotFile(
                normalizedRelativePath,
                bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant()));
        }

        return new ClientRoutingSnapshotManifest(capturedAtUtc, fullClientRoot, files);
    }

    public static void WriteManifest(string outputPath, ClientRoutingSnapshotManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(manifest);

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var builder = new StringBuilder();
        builder.Append("CapturedAtUtc=");
        builder.Append(manifest.CapturedAtUtc.ToString("O"));
        builder.Append('\n');
        builder.Append("ClientRoot=");
        builder.Append(manifest.ClientRoot);
        builder.Append('\n');

        foreach (var file in manifest.Files)
        {
            builder.Append(file.RelativePath);
            builder.Append(" | SHA256=");
            builder.Append(file.Sha256);
            builder.Append(" | MD5=");
            builder.Append(file.Md5);
            builder.Append(" | Length=");
            builder.Append(file.Length.ToString());
            builder.Append('\n');
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
