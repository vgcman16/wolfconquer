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
        {
            // Prepare the complete immutable wire image before the first awaited
            // publication. Each send still revalidates the exact live owner.
            var frames = titles.Select(title => new LegacyTitlePacket((uint)session.Character.Uid,
                title, LegacyTitleAction.Enqueue).ToFrameBytes()).ToArray();
            await PublishClassicTitleAsync(context, session, identity, frames, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (packet.Title != 0 && !titles.Contains(packet.Title))
        {
            await LogClassicTitleRefusalAsync(context, "titleNotOwned").ConfigureAwait(false);
            return;
        }
        if (context.TradeRecovery is null || context.TradeRecovery.IsBlocked)
        {
            await LogClassicTitleRefusalAsync(context, "recoveryOwnerUnavailableOrBlocked").ConfigureAwait(false);
            return;
        }
        var updated = session with { Character = WithCharacterAttribute(session.Character,
            LegacyClassicTitleState.SelectedKey, packet.Title) };
        var acknowledgement = new LegacyTitlePacket((uint)session.Character.Uid,
            packet.Title, LegacyTitleAction.Change).ToFrameBytes();
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryCommitClassicTitle(context, session, updated, identity, cancellationToken, out var reason))
        {
            await LogClassicTitleRefusalAsync(context, reason).ConfigureAwait(false);
            return;
        }
        var delivered = await PublishClassicTitleAsync(context, updated, identity,
            [acknowledgement], cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer LegacyTitle outcome=committed delivery={(delivered ? "complete" : "stopped")} title={packet.Title}.")
            .ConfigureAwait(false);
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
