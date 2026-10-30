using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

/// <summary>One bounded startup snapshot; policy data alone does not admit a live actor.</summary>
public static class LegacyGmPolicyConfiguration
{
    public const string FileEnvironmentVariable = "WOLFCONQUER_GM_POLICY_FILE";
    private const string Prefix = "gmPolicy";

    public static bool TryLoadFromEnvironment(out LegacyGmPolicy? policy, out string reason) =>
        TryLoad(Environment.GetEnvironmentVariable(FileEnvironmentVariable), out policy, out reason);

    public static bool TryLoad(string? configuredPath, out LegacyGmPolicy? policy, out string reason)
    {
        policy = null;
        reason = string.Empty;
        return string.IsNullOrEmpty(configuredPath) || TryReadFile(configuredPath, out policy, out reason);
    }

    public static bool TryReadFile(string path, out LegacyGmPolicy? policy, out string reason)
    {
        policy = null;
        reason = Prefix + "FileInvalid";
        try
        {
            if (!IsLocalAbsolutePath(path)) return false;
            var fullPath = Path.GetFullPath(path);
            if (!CharacterSnapshotFileBaseline.TryValidateSafePath(fullPath, Prefix, out reason)) return false;
            using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is < 1 or > LegacyGmPolicy.MaximumBytes)
            { reason = Prefix + "FileLengthInvalid"; return false; }
            var bytes = new byte[checked((int)input.Length)];
            input.ReadExactly(bytes);
            if (input.ReadByte() != -1 || input.Length != bytes.Length)
            { reason = Prefix + "FileLengthChanged"; return false; }
            // These pre/post path checks reject an observed reparse path. They do
            // not attest the host or prove exclusion of an uncooperative path race.
            if (!CharacterSnapshotFileBaseline.TryValidateSafePath(fullPath, Prefix, out reason)) return false;
            policy = LegacyGmPolicy.Parse(bytes);
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            reason = Prefix + "FileUnreadable:" + error.GetType().Name;
            return false;
        }
    }

    private static bool IsLocalAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || !Path.IsPathFullyQualified(path) ||
            path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            return false;
        foreach (var value in path)
            if (char.IsControl(value)) return false;
        // Windows drive designators are the only colon allowed; alternate data
        // streams are not independent ordinary policy files.
        return !OperatingSystem.IsWindows() || path.Length >= 3 &&
            (path[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z') && path[1] == ':' &&
            (path[2] is '\\' or '/') && path.IndexOf(':', 2) < 0;
    }
}
