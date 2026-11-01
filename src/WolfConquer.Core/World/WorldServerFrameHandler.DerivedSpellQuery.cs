namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal LegacyJournalInterruption? DerivedSpellQueryJournalInterruption { get; set; }

    internal string RequireDerivedSpellQueryItemsRoot(WorldSessionStore store, WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession expected)
    {
        if (!ReferenceEquals(_sessions, store) || !ReferenceEquals(binding.Store, store) ||
            !IsActionLifecycleCurrent(store, context) || !ReferenceEquals(context.Connection, binding.Connection) ||
            !ReferenceEquals(expected.NativeGameClient, binding.Client) ||
            _snapshotLoader?.PlayerItemsRoot is not { } configured || !Path.IsPathFullyQualified(configured) ||
            context.PlayerItemsRoot is not { } requested || !Path.IsPathFullyQualified(requested))
            throw new InvalidOperationException("derivedSpellQuery23FrameRootsUnavailable");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        if (!string.Equals(root, Path.TrimEndingDirectorySeparator(Path.GetFullPath(requested)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            !binding.Recovery.IsReadyForCharacterItemsSpells(binding.UsersRoot, root, binding.SpellsRoot))
            throw new InvalidOperationException("derivedSpellQuery23ConfiguredRootsChanged");
        return root;
    }
}
