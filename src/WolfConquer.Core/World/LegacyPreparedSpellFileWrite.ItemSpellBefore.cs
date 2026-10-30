namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    internal void RequireItemSpellBeforeOwner(WorldPacketHandlerContext context, WorldSession actual) =>
        store.RequireItemSpellBeforeOwner(context, actual, this);

    internal void RequireItemSpellBeforeOwnerPlain(WorldPacketHandlerContext context, WorldSession actual)
    {
        if (!ReferenceEquals(store, context.Sessions) || !ReferenceEquals(connection, context.Connection) ||
            !ReferenceEquals(expected, actual) || !OwnerIsCurrentPlain() ||
            !ReferenceEquals(actual.NativeGameClient, context.Connection.NativeGameClient)) throw Unavailable();
    }

    internal void RequireItemSpellBeforePaths(WorldPacketHandlerContext context, string[] paths)
    {
        if (paths.Length != 3 || string.IsNullOrWhiteSpace(context.PlayerItemsRoot) ||
            !Path.IsPathFullyQualified(context.PlayerItemsRoot) ||
            !Recovery.IsReadyForCharacterItemsSpells(UsersRoot, context.PlayerItemsRoot, SpellsRoot) ||
            !string.Equals(paths[0], CharacterPath, PathComparison) || !string.Equals(paths[2], SpellsPath, PathComparison) ||
            !string.Equals(paths[1], Path.Combine(Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.PlayerItemsRoot)),
                expected.Character.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin"), PathComparison)) throw Unavailable();
    }
}
