using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // The real Frame postlude already owns its current Store publication gate.
    // Preserve a null admission origin until an actual writer creates the book;
    // never adopt an external insertion/deletion as a fresh saved baseline.
    private void PersistBoundSessionProficiencies(LegacyOrdinarySpellBinding binding,
        WorldSession previous, WorldSession current, string configuredRoot)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!ReferenceEquals(binding.Store, _sessions) || !ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) ||
            !_sessions.IsFramePersistenceCurrent(this, binding.Connection, current) ||
            !ReferenceEquals(binding.ProficiencySnapshot, current.PlayerProficiencies) ||
            !string.Equals(root, binding.ProficienciesRoot, comparison)) throw InvalidBoundProficiencyPersistence();
        var baseline = binding.SavedProficiencies;
        if (!baseline.MatchesSnapshot(previous.PlayerProficiencies)) throw InvalidBoundProficiencyPersistence();
        var image = baseline.ProjectNext(current.PlayerProficiencies);
        var prepared = new LegacySavedProficiencyImage(image);
        var path = Path.Combine(root, binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            if (!_sessions.IsFramePersistenceCurrent(this, binding.Connection, current) ||
                !ReferenceEquals(baseline, binding.SavedProficiencies) ||
                !baseline.Matches(LegacySavedProficiencyImage.Read(path)) ||
                !binding.Recovery.TryRequireLiveJournalClear(out _)) throw InvalidBoundProficiencyPersistence();
            SnapshotFilePersistence.WriteAllBytesAtomically(path, image, ".player-proficiency-file-");
            if (!prepared.Matches(LegacySavedProficiencyImage.Read(path))) throw InvalidBoundProficiencyPersistence();
            binding.PublishSavedProficiencies(prepared); // Only this actual proven writer advances the anchor.
            return true;
        });
    }

    private static InvalidOperationException InvalidBoundProficiencyPersistence() =>
        new("ordinaryProficiencySavedOriginChanged");
}
