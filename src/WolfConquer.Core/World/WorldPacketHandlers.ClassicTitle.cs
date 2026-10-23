using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyTitle)]
    private static async ValueTask HandleLegacyTitleAsync(WorldPacketHandlerContext context,
        PacketFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!LegacyTitlePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync("WorldServer rejected malformed LegacyTitle.").ConfigureAwait(false);
            return;
        }
        if (packet.Action is not (LegacyTitleAction.List or LegacyTitleAction.Change)) return;
        if (!TryResolveClassicTitleOwner(context, out var session, out var identity) ||
            !LegacyClassicTitleState.TryRead(session.Character.Attributes, out _, out var titles))
        {
            await LogClassicTitleRefusalAsync(context, "currentOwnerUnavailableOrInvalid").ConfigureAwait(false);
            return;
        }
        if (packet.Action == LegacyTitleAction.List)
            await HandleClassicTitleListAsync(context, session, identity, titles, cancellationToken).ConfigureAwait(false);
        else
            await HandleClassicTitleChangeAsync(context, session, identity, titles, packet, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleClassicTitleListAsync(WorldPacketHandlerContext context,
        WorldSession session, object identity, byte[] titles, CancellationToken cancellationToken)
    {
        // Prepare every frame before the first await. Publication independently
        // checks the exact original owner before each prepared Enqueue response.
        var frames = titles.Select(title => new LegacyTitlePacket((uint)session.Character.Uid,
            title, LegacyTitleAction.Enqueue).ToFrameBytes()).ToArray();
        await PublishClassicTitleAsync(context, session, identity, frames, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryResolveClassicTitleOwner(WorldPacketHandlerContext context,
        out WorldSession session, out object identity)
    {
        session = null!;
        identity = null!;
        if (context.Sessions is not { } sessions || context.Session is not { } expected ||
            expected.AccountId <= 0 || expected.Character is not { } character ||
            character.Uid != expected.AccountId || character.Uid > uint.MaxValue ||
            !sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, character.Uid, out identity) ||
            !IsCurrentClassicTitleOwner(context, expected, identity)) return false;
        session = expected;
        return true;
    }

    private static Task LogClassicTitleRefusalAsync(WorldPacketHandlerContext context, string reason)
    {
        var outcome = reason.StartsWith(LegacyClassicTitlePersistence.RecoveryRequired, StringComparison.Ordinal)
            ? "indeterminate; recovery fence blocked" : "rejected without commit";
        return context.Log.WriteLineAsync($"WorldServer LegacyTitle outcome={outcome} reason={reason}.");
    }
}
