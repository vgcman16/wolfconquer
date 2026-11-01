namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal readonly record struct BasicPhysicalFileRoots(string Users, string Items, string Spells);
    internal BasicPhysicalFileRoots RequireBasicPhysicalFileRoots(WorldSessionStore store, LegacyOrdinarySpellBinding binding)
    {
        if (!ReferenceEquals(store, _sessions) || !ReferenceEquals(binding.Store, store) || _snapshotLoader is null ||
            string.IsNullOrWhiteSpace(_snapshotLoader.PlayerItemsRoot) || string.IsNullOrWhiteSpace(_snapshotLoader.PlayerSpellsRoot))
            throw new InvalidOperationException("basicPhysicalConfiguredFileRootsUnavailable");
        var roots = new BasicPhysicalFileRoots(Normalize(_snapshotLoader.UsersRoot), Normalize(_snapshotLoader.PlayerItemsRoot),
            Normalize(_snapshotLoader.PlayerSpellsRoot));
        if (!string.Equals(roots.Users, binding.UsersRoot, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(roots.Spells, binding.SpellsRoot, StringComparison.OrdinalIgnoreCase) ||
            !binding.Recovery.IsReadyForCharacterItemsSpells(roots.Users, roots.Items, roots.Spells))
            throw new InvalidOperationException("basicPhysicalConfiguredFileDomainChanged");
        return roots;
        static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
