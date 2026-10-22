namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static object? CaptureLegacyNativeMagicQueryCaster(
        WorldPacketHandlerContext context, WorldSession caster) =>
        context.Sessions is { } world && LegacyNativeClientPublication.TryCaptureIdentity(
            world, context.Connection, caster.Character.Uid, out var identity) ? identity : null;

    private static bool TryRefreshLegacyNativeMagicQueryCaster(
        WorldPacketHandlerContext context, long originalUid, object identity, out WorldSession caster)
    {
        caster = null!;
        return context.Sessions is { } world && LegacyNativeClientPublication.TryCurrent(
            world, context.Connection, originalUid, identity, out caster);
    }

    private static LegacyNativeMagicQueryFields ReadLegacyNativeMagicQueryCaster(
        WorldPacketHandlerContext context, long originalUid, object identity) =>
        // Native new GameClient.Status is zero BEFORE query. No cache-present opt-in/fresh fallback.
        LegacyNativeMagicQueryClient.Read(context.Sessions, context.Connection, originalUid, identity);
}
