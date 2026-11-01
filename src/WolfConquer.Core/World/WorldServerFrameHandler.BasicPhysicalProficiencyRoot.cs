namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal string RequireBasicPhysicalProficiencyRoot(WorldSessionStore store, LegacyOrdinarySpellBinding binding)
    {
        if (!ReferenceEquals(store, _sessions) || !ReferenceEquals(binding.Store, store) || _snapshotLoader is null ||
            string.IsNullOrWhiteSpace(_snapshotLoader.PlayerProficienciesRoot) ||
            !binding.Recovery.IsReadyForUsers(binding.UsersRoot))
            throw new InvalidOperationException("basicPhysicalConfiguredProficiencyRootUnavailable");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(_snapshotLoader.PlayerProficienciesRoot));
    }
}
