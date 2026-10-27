using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed record LegacyOwnedAdmissionFixtureResult(string RootPath, string ConfigurationPath,
    Guid DomainId, uint AccountKey, uint CharacterUid, uint ItemUid);

internal enum LegacyOwnedAdmissionFixtureInterruption { None, AfterSnapshots, AfterBootstrap }

// An offline creator of new synthetic data, never an importer or Host startup API.
public static partial class LegacyOwnedAdmissionFixture
{
    public const string DirectoryPrefix = "wolf-owned-admission-";
    public const string ConfigurationFileName = "owned-admission.json";
    public const string HistoricalEvidence = "synthetic owned fixture: exactly one initial item; no prior issuance";

    // Preserve the established compatibility fixture. Targeted fixtures require
    // an explicit profile; no ambient environment selects their saved class.
    public static bool TryCreate(string newRoot, uint accountKey, uint characterUid, uint itemUid,
        out LegacyOwnedAdmissionFixtureResult? fixture, out string reason) =>
        TryCreate(newRoot, accountKey, characterUid, itemUid, ConquerProtocolProfile.RootD11_7899,
            LegacyOwnedAdmissionFixtureInterruption.None, out fixture, out reason);

    public static bool TryCreate(string newRoot, uint accountKey, uint characterUid, uint itemUid,
        ConquerProtocolProfile protocolProfile, out LegacyOwnedAdmissionFixtureResult? fixture, out string reason) =>
        TryCreate(newRoot, accountKey, characterUid, itemUid, protocolProfile,
            LegacyOwnedAdmissionFixtureInterruption.None, out fixture, out reason);

    internal static bool TryCreate(string newRoot, uint accountKey, uint characterUid, uint itemUid,
        LegacyOwnedAdmissionFixtureInterruption interruption, out LegacyOwnedAdmissionFixtureResult? fixture,
        out string reason) =>
        TryCreate(newRoot, accountKey, characterUid, itemUid, ConquerProtocolProfile.RootD11_7899,
            interruption, out fixture, out reason);

    internal static bool TryCreate(string newRoot, uint accountKey, uint characterUid, uint itemUid,
        ConquerProtocolProfile protocolProfile, LegacyOwnedAdmissionFixtureInterruption interruption,
        out LegacyOwnedAdmissionFixtureResult? fixture, out string reason)
    {
        fixture = null; reason = "ownedAdmissionFixtureIdentifiersInvalid";
        if (accountKey == 0 || accountKey != characterUid || itemUid is 0 or uint.MaxValue ||
            itemUid == characterUid || !Enum.IsDefined(interruption)) return false;
        if (protocolProfile is not (ConquerProtocolProfile.RootD11_7899 or ConquerProtocolProfile.VisibleDx8D13))
        { reason = "ownedAdmissionFixtureProfileInvalid"; return false; }
        if (!TryValidateTarget(newRoot, out var root, out reason)) return false;
        var stage = Path.Combine(Path.GetDirectoryName(root)!, "." + Path.GetFileName(root) + ".staging");
        try
        {
            RequireAbsent(stage);
            CreateNewDirectory(stage);
            RequirePhysicalDirectoryChain(stage);
            WriteSnapshots(stage, accountKey, characterUid, itemUid, protocolProfile);
            // Rename refuses any target that appeared after preflight. Bootstrap
            // follows the rename, so its persisted absolute routes are FINAL.
            RequirePhysicalDirectoryChain(Path.GetDirectoryName(root)!);
            RequireAbsent(root);
            Directory.Move(stage, root);
            if (interruption == LegacyOwnedAdmissionFixtureInterruption.AfterSnapshots)
            { reason = "ownedAdmissionFixtureInterruptedAfterSnapshots"; return false; }
            var registryOptions = new LegacyItemUidRegistryOptions(Path.Combine(root, "Users"),
                Path.Combine(root, "PlayersItems"), Path.Combine(root, "PlayersMailList"),
                Path.Combine(root, "RedeemContainer.bin"), Path.Combine(root, "ClaimContainer.bin"),
                itemUid, HistoricalEvidence);
            Guid domain;
            if (!LegacyItemUidRegistry.TryBootstrap(registryOptions, out var registry, out reason) || registry is null)
            { reason = "ownedAdmissionFixtureBootstrapFailed:" + reason; return false; }
            using (registry) domain = registry.ReadSyntheticFixtureBootstrapDomain();
            if (interruption == LegacyOwnedAdmissionFixtureInterruption.AfterBootstrap)
            { reason = "ownedAdmissionFixtureInterruptedAfterBootstrap"; return false; }
            var options = new LegacyOwnedAdmissionOptions(domain, registryOptions.UsersRoot,
                registryOptions.ItemsRoot, Path.Combine(root, "PlayersSpells"), Path.Combine(root, "PlayersProfs"),
                Path.Combine(root, "Quests"), Path.Combine(root, "PlayersCombatGear"), registryOptions.MailboxRoot,
                registryOptions.RedeemFile, registryOptions.ClaimFile, itemUid, HistoricalEvidence);
            var config = Path.Combine(root, ConfigurationFileName);
            WriteConfigurationLast(config, options.ToConfigurationBytes());
            fixture = new(root, config, domain, accountKey, characterUid, itemUid);
            reason = string.Empty; return true;
        }
        catch (Exception error) when (IsFixtureFailure(error))
        {
            // Preserve the new root/stage for inspection; never recursively
            // delete or reuse a path after a partially completed writer.
            reason = "ownedAdmissionFixtureCreationFailed:" + error.GetType().Name;
            return false;
        }
    }

    private static void WriteConfigurationLast(string path, byte[] bytes)
    {
        var pending = path + ".pending";
        RequirePhysicalDirectoryChain(Path.GetDirectoryName(path)!);
        RequireAbsent(path); RequireAbsent(pending);
        using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                   4096, FileOptions.WriteThrough))
        { output.Write(bytes); output.Flush(flushToDisk: true); }
        RequirePhysicalDirectoryChain(Path.GetDirectoryName(path)!);
        File.Move(pending, path, overwrite: false);
    }

    private static bool IsFixtureFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        InvalidDataException or ArgumentException or InvalidOperationException or NotSupportedException or
        FormatException or OverflowException or System.Security.SecurityException or System.Text.Json.JsonException or
        DllNotFoundException or EntryPointNotFoundException or System.Runtime.InteropServices.MarshalDirectiveException;
}
