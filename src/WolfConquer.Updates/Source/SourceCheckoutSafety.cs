using System.Net;
using System.Net.Sockets;

namespace WolfConquer.Updates.Source;

internal static class SourceCheckoutSafety
{
    internal static string Root(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    internal static bool SamePath(string first, string second) => string.Equals(Root(first), Root(second),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    internal static bool IsAllowedRemote(string value, bool allowLocal)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            return false;
        if (allowLocal && Path.IsPathFullyQualified(value)) return true;
        if (value.Any(char.IsWhiteSpace)) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (allowLocal && uri.IsFile && string.IsNullOrEmpty(uri.UserInfo)) return true;
        if (uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || uri.Query.Length != 0 ||
            uri.Fragment.Length != 0 || uri.IsLoopback) return false;
        var host = uri.IdnHost.TrimEnd('.');
        if (IPAddress.TryParse(host, out var address)) return IsPublicAddress(address);
        return uri.HostNameType == UriHostNameType.Dns && host.Contains('.') &&
            !new[] { ".localhost", ".local", ".internal", ".lan", ".invalid", ".test" }
                .Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return false;
        if (address.IsIPv4MappedToIPv6) return IsPublicAddress(address.MapToIPv4());
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (bytes[0] & 0xE0) == 0x20 && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8);
        return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224 &&
            !(bytes[0] == 169 && bytes[1] == 254) && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
            !(bytes[0] == 192 && bytes[1] == 168) && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
    }

    internal static async Task<bool> ConfigurationIsSafeAsync(SourceUpdateGit git, string expected, CancellationToken token)
    {
        var urls = await git.RunAsync(token, "config", "--get-all", "remote.origin.url");
        if (urls.ExitCode != 0 || urls.Output.TrimEnd('\r', '\n') != expected) return false;
        // Local includes are evaluated too. Global/system config is isolated by the runner.
        var rewrites = await git.RunAsync(token, "config", "--get-regexp", "^url\\..*\\.insteadof$");
        if (rewrites.ExitCode != 1) return false;
        // Avoid executing configured clean/smudge/process filters while examining/updating files.
        var filters = await git.RunAsync(token, "config", "--get-regexp", "^filter\\..*\\.(clean|smudge|process|required)$");
        return filters.ExitCode == 1;
    }

    internal static async Task<bool> HasBlockedStateAsync(SourceUpdateGit git, string root, CancellationToken token)
    {
        var directory = (await git.RunAsync(token, "rev-parse", "--absolute-git-dir")).RequireSuccess();
        foreach (var name in new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply", "sequencer", "index.lock" })
            if (File.Exists(Path.Combine(directory, name)) || Directory.Exists(Path.Combine(directory, name))) return true;
        if (File.Exists(Path.Combine(root, ".gitmodules"))) return true;
        var entries = (await git.RunAsync(token, "ls-files", "--stage", "-z")).RequireSuccess();
        if (entries.Split('\0').Any(entry => entry.StartsWith("160000 ", StringComparison.Ordinal))) return true;
        // Status deliberately trusts these index bits, so it cannot establish
        // that hidden local edits are absent. Refuse instead of clearing bits.
        var flags = (await git.RunAsync(token, "ls-files", "-v", "-z")).RequireSuccess();
        return flags.Split('\0').Any(entry => entry.Length > 0 && (entry[0] == 'S' || char.IsLower(entry[0])));
    }

    internal static async Task<bool> TargetContainsSubmodulesAsync(SourceUpdateGit git, string commit, CancellationToken token)
    {
        var tree = (await git.RunAsync(token, "ls-tree", "-r", "--full-tree", "-z", commit)).RequireSuccess();
        return tree.Split('\0').Any(entry => entry.StartsWith("160000 ", StringComparison.Ordinal) ||
            entry.EndsWith("\t.gitmodules", StringComparison.Ordinal));
    }

    internal static async Task<bool> IsDirtyAsync(SourceUpdateGit git, CancellationToken token) =>
        (await git.RunAsync(token, "status", "--porcelain=v1", "--untracked-files=all", "--ignore-submodules=all", "-z"))
            .RequireSuccess().Length != 0;
}
