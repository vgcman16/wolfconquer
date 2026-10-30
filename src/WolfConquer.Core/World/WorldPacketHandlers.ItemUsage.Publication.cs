namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static WorldSession PublishLegacyItemUsageSession(WorldPacketHandlerContext context,
        WorldSession proposed)
    {
        // Preserve the existing standalone managed fixture path. A real Store
        // performs its actual bound-owner preparation and returns its final image.
        if (context.Sessions is not { } sessions) return proposed;
        sessions.Set(context.Connection, proposed, out var final);
        return final;
    }
}
