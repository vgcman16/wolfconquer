using WolfConquer.Core.Data;

namespace WolfConquer.Core.Runtime;

// Explicit host metadata owner. Construction is confined to the complete real
// staged publisher; ambient native globals cannot create a host capability.
public sealed partial class LegacyNativeCombatGearRuntime
{
    public const string MetadataRootEnvironmentVariable = "WOLFCONQUER_NATIVE_COMBAT_GEAR_ROOT";
    public const string RankPathEnvironmentVariable = "WOLFCONQUER_NATIVE_COMBAT_GEAR_RANK_PATH";
    private readonly LegacyNativeCombatGearTable.MailboxLoadProof proof;
    public string MetadataDirectory { get; }
    public string RankFilePath { get; }
    public bool IsCurrent => proof.IsCurrent();

    private LegacyNativeCombatGearRuntime(string directory, string rankFile,
        LegacyNativeCombatGearTable.MailboxLoadProof proof)
    { MetadataDirectory = directory; RankFilePath = rankFile; this.proof = proof; }

    public static bool TryLoadFromEnvironment(out LegacyNativeCombatGearRuntime? runtime, out string reason) =>
        TryLoad(Environment.GetEnvironmentVariable(MetadataRootEnvironmentVariable),
            Environment.GetEnvironmentVariable(RankPathEnvironmentVariable), out runtime, out reason);

    public static bool TryLoad(string? metadataDirectory, string? rankFilePath,
        out LegacyNativeCombatGearRuntime? runtime, out string reason)
    {
        runtime = null; reason = string.Empty;
        if (string.IsNullOrEmpty(metadataDirectory) && string.IsNullOrEmpty(rankFilePath)) return true;
        if (string.IsNullOrEmpty(metadataDirectory) || string.IsNullOrEmpty(rankFilePath))
        { reason = "nativeCombatGearConfigurationPairRequired"; return false; }
        try
        {
            if (string.IsNullOrWhiteSpace(metadataDirectory) || string.IsNullOrWhiteSpace(rankFilePath) ||
                !Path.IsPathFullyQualified(metadataDirectory) || !Path.IsPathFullyQualified(rankFilePath))
                throw new ArgumentException("Native CombatGear requires explicit absolute paths.");
            var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(metadataDirectory));
            var rankFile = Path.GetFullPath(rankFilePath);
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Native CombatGear metadata directory is absent.");
            // Public configuration is a directory. Only the preserved legacy
            // one-argument ABI interprets its argument as an arbitrary prefix.
            var prefix = Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar;
            var published = LegacyNativeCombatGearTable.MailboxLoadProof.LoadAndPublish(prefix, rankFile);
            var candidate = new LegacyNativeCombatGearRuntime(directory, rankFile, published);
            if (!candidate.IsCurrent) throw new InvalidDataException("Native CombatGear publication changed before acquisition.");
            runtime = candidate;
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException or OverflowException or FormatException or NotSupportedException or
            System.Security.SecurityException)
        {
            reason = error is InvalidDataException && error.Message == "nativeCombatGearSchema22Unresolved"
                ? error.Message : "nativeCombatGearStartupRefused:" + error.GetType().Name;
            return false;
        }
    }
}
