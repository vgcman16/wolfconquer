namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPublishLegacyPlayerSpells(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession proposed, out WorldSession final)
    {
        final = proposed;
        if (context.Sessions is { } sessions)
        {
            if (!sessions.TryPublishPlayerSpells(context.Connection, expected, proposed, out var published) || published is null)
                return false;
            final = published;
            return true;
        }
        // Preserve genuinely unbound fixtures, without letting a missing/wrong
        // Store turn an already-issued native binding into detached authority.
        return context.Connection.NativeGameClient?.OrdinarySpellBinding is null &&
            proposed.NativeGameClient?.OrdinarySpellBinding is null;
    }
}
