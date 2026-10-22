using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.ClientLayout;

// This fixed local compatibility preset is not manifest-defined filesystem authority.
// Signed payload files remain ordinary files beneath their ordinary root directories.
public static partial class ClientAssetLayout
{
    public const string LegacyDx8V1 = "legacy-dx8-v1";
    private const string EngineDirectory = "Env_DX8";
    private static readonly IReadOnlyList<string> Directories = Array.AsReadOnly(new[]
    {
        "ani", "c3", "data", "data2", "Font", "Help", "ini", "map", "Script", "sound", "video"
    });

    public static IReadOnlyList<string> AssetDirectoryNames => Directories;

    public static void ValidateConfiguration(string? preset, string launchPath, string? workingDir)
    {
        if (preset is null) return;
        RequirePreset(preset);
        UpdatePathPolicy.ValidateRelativePath(launchPath);
        if (workingDir != EngineDirectory || !launchPath.StartsWith(EngineDirectory + "/", StringComparison.Ordinal))
            throw new InvalidDataException("The DX8 asset preset requires an Env_DX8 working directory and executable.");
        foreach (var name in Directories)
            if (IsAtOrBelow(launchPath, EngineDirectory + "/" + name))
                throw new InvalidDataException("The configured executable cannot be inside a shared asset alias.");
    }

    public static void ValidateManifest(VerifiedUpdateManifest verified, string preset)
    {
        ArgumentNullException.ThrowIfNull(verified);
        RequirePreset(preset);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool engineCovered = false;
        foreach (var file in verified.Manifest.Files)
        {
            UpdatePathPolicy.ValidateRelativePath(file.Path);
            if (!paths.Add(file.Path)) throw new InvalidDataException("Signed payload paths collide under Windows comparison.");
            if (file.Path.Equals(EngineDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The DX8 engine path must be a directory.");
            if (file.Path.StartsWith(EngineDirectory + "/", StringComparison.OrdinalIgnoreCase) &&
                !file.Path.StartsWith(EngineDirectory + "/", StringComparison.Ordinal))
                throw new InvalidDataException("The signed DX8 engine directory must use its canonical casing.");
            if (file.Path.StartsWith(EngineDirectory + "/", StringComparison.Ordinal)) engineCovered = true;
            foreach (var name in Directories)
            {
                if (file.Path.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    IsAtOrBelow(file.Path, EngineDirectory + "/" + name))
                    throw new InvalidDataException("Signed payload cannot occupy a fixed DX8 asset alias or directory.");
                if (file.Path.StartsWith(name + "/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!file.Path.StartsWith(name + "/", StringComparison.Ordinal))
                        throw new InvalidDataException("A signed root asset directory does not use the preset's canonical casing.");
                    covered.Add(name);
                }
            }
        }
        foreach (var path in paths)
            for (var slash = path.LastIndexOf('/'); slash >= 0; slash = path.LastIndexOf('/', slash - 1))
                if (paths.Contains(path[..slash]))
                    throw new InvalidDataException("A signed payload file is also a directory.");
        if (covered.Count != Directories.Count || !engineCovered)
            throw new InvalidDataException("The DX8 preset requires signed engine files and all eleven root asset directories.");
    }

    // Missing ordinary paths are allowed for a fresh destination; existing conflicts
    // are rejected before recovery, installation, or any local alias creation.
    public static void ValidateExistingLayout(string root, string preset)
    {
        RequirePreset(preset);
        Preflight(root, requireDirectories: false);
    }

    private static bool IsAtOrBelow(string path, string parent) =>
        path.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);

    private static void RequirePreset(string preset)
    {
        if (preset != LegacyDx8V1) throw new InvalidDataException("Unknown local client asset-layout preset.");
    }
}
