using System.Text;

namespace WolfConquer.Core.Web;

public static class LoginLoaderHttpResponder
{
    public const string LoginLoaderHost = "p.99.com";
    public const string LauncherVersionHost = "wjdown.99.com";
    public const string LauncherSkinHost = "antistat.99.com";
    public const string LauncherDriverConfigHost = "antistat.91.com";
    public const string LauncherUpdateHost = "safelogin.99.com";
    public const string CachedClientGmHost = "gm.99.com";
    public const string VersionPath = "/lsj/games/63/cqimiver.txt";
    public const string ManifestPath = "/lsj/games/63/cqimi.txt";
    public const string LauncherVersionPath = "/games/zf/reg/version.shtml";
    public const string LauncherSkinPath = "/ndlogin/skin/zf.dat";
    public const string LauncherDriverConfigPath = "/driver/config.txt";
    public const string LauncherUpdateManifestPath = "/ndlogin/zfplay/update.txt";
    public const string LauncherAutoPatchPath = "/ndlogin/zfplay/autopatch.dat";
    public const string CachedClientTelProgramPath = "/telprogram.aspx";
    public const string VersionPathEnvironmentVariable = "WOLFCONQUER_LOGINWEB_VERSION_FILE";
    public const string ManifestPathEnvironmentVariable = "WOLFCONQUER_LOGINWEB_MANIFEST_FILE";
    public const string LauncherVersionPathEnvironmentVariable = "WOLFCONQUER_LOGINWEB_REGISTRATION_VERSION_FILE";
    public const string LauncherSkinPathEnvironmentVariable = "WOLFCONQUER_LOGINWEB_SKIN_ZF_DAT";
    public const string LauncherDriverConfigPathEnvironmentVariable = "WOLFCONQUER_LOGINWEB_DRIVER_CONFIG_FILE";
    public const string LauncherUpdateManifestPathEnvironmentVariable = "WOLFCONQUER_LOGINWEB_UPDATE_TXT";
    public const string LauncherAutoPatchPathEnvironmentVariable = "WOLFCONQUER_LOGINWEB_AUTOPATCH_DAT";
    public const string CachedClientTelProgramPathEnvironmentVariable = "WOLFCONQUER_LOGINWEB_SUPPORT_HTML_FILE";

    // Payloads are operator-supplied files. No launcher content is bundled.
    public static IReadOnlyList<LoginLoaderPayloadRoute> Routes { get; } = Array.AsReadOnly(new[]
    {
        new LoginLoaderPayloadRoute(VersionPath, VersionPathEnvironmentVariable, "text/plain; charset=us-ascii"),
        new LoginLoaderPayloadRoute(ManifestPath, ManifestPathEnvironmentVariable, "application/octet-stream"),
        new LoginLoaderPayloadRoute(LauncherVersionPath, LauncherVersionPathEnvironmentVariable, "text/html; charset=us-ascii"),
        new LoginLoaderPayloadRoute(LauncherSkinPath, LauncherSkinPathEnvironmentVariable, "application/octet-stream"),
        new LoginLoaderPayloadRoute(LauncherDriverConfigPath, LauncherDriverConfigPathEnvironmentVariable, "text/plain; charset=us-ascii"),
        new LoginLoaderPayloadRoute(LauncherUpdateManifestPath, LauncherUpdateManifestPathEnvironmentVariable, "text/plain; charset=us-ascii"),
        new LoginLoaderPayloadRoute(LauncherAutoPatchPath, LauncherAutoPatchPathEnvironmentVariable, "application/octet-stream"),
        new LoginLoaderPayloadRoute(CachedClientTelProgramPath, CachedClientTelProgramPathEnvironmentVariable, "text/html; charset=utf-8")
    });

    public static LoginLoaderHttpResponse Get(string method, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return new LoginLoaderHttpResponse(405, "Method Not Allowed", "text/plain; charset=us-ascii", []);
        }

        var normalizedPath = NormalizePath(path);
        var route = Routes.FirstOrDefault(item =>
            string.Equals(item.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (route is null)
        {
            return new LoginLoaderHttpResponse(404, "Not Found", "text/plain; charset=us-ascii", []);
        }

        var payload = TryReadConfiguredPayload(route.EnvironmentVariable);
        return payload is not null
            ? new LoginLoaderHttpResponse(200, "OK", route.ContentType, payload)
            : new LoginLoaderHttpResponse(
                503,
                "Service Unavailable",
                "text/plain; charset=us-ascii",
                Encoding.ASCII.GetBytes($"{route.EnvironmentVariable} is not configured or its file is unavailable."));
    }

    private static byte[]? TryReadConfiguredPayload(string environmentVariable)
    {
        var configuredPath = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(Environment.ExpandEnvironmentVariables(configuredPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Keep missing/unreadable files unavailable, without exposing local paths.
            return null;
        }
    }

    private static string NormalizePath(string path)
    {
        var queryStart = path.IndexOf('?');
        var pathOnly = queryStart < 0 ? path : path[..queryStart];
        return pathOnly.StartsWith('/') ? pathOnly : "/" + pathOnly;
    }
}

public sealed record LoginLoaderPayloadRoute(string Path, string EnvironmentVariable, string ContentType);
