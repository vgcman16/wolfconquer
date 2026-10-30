namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRuneDropSource = "MsgItemUsuagePacket.DropRune";
    private const string LegacyRuneDropLines = "2070-2073";

    private static async ValueTask HandleLegacyDropRuneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryGetRuneItem(session.PlayerItems, packet.Id, out var rune))
        {
            // The original missing-UID request still refreshes the actual
            // ranking cache; it changes no files, resources or outgoing frames.
            UpdateLegacyRuneRanking(context, session);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage drop rune " +
                    $"{FormatSession(context)} source={LegacyRuneDropSource} " +
                    $"sourceLines={LegacyRuneDropLines} itemUid={packet.Id}; rune item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryCommitLegacyItemSpell(context, session,
                (stable, identity) => PrepareOwnedRuneContainerProjection(context, stable, identity,
                    LegacyRuneContainerKind.Drop, packet.Id, storageType: null),
                cancellationToken, out var updatedSession, out var frames, out var publicationIdentity, out var reason))
        {
            await context.Log.WriteLineAsync($"WorldServer held MsgItemUsage drop rune reason={reason}; no success was published.").ConfigureAwait(false);
            return;
        }
        await PublishItemSpellFramesAsync(context, updatedSession, frames, publicationIdentity, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer committed MsgItemUsage drop rune " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyRuneDropSource} " +
                $"sourceLines={LegacyRuneDropLines} itemUid={rune.Uid} itemId={rune.ItemId} " +
                $"position={rune.Position}.")
            .ConfigureAwait(false);
    }
}
