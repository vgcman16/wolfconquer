namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleClassicTitleChangeAsync(WorldPacketHandlerContext context,
        WorldSession session, object identity, byte[] titles, LegacyTitlePacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryValidateClassicTitleSelection(context, packet.Title, titles, out var reason))
        {
            await LogClassicTitleRefusalAsync(context, reason).ConfigureAwait(false);
            return;
        }
        var updated = session with { Character = WithCharacterAttribute(session.Character,
            LegacyClassicTitleState.SelectedKey, packet.Title) };
        var acknowledgement = new LegacyTitlePacket((uint)session.Character.Uid,
            packet.Title, LegacyTitleAction.Change).ToFrameBytes();
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryCommitClassicTitle(context, session, updated, identity, cancellationToken, out reason))
        {
            await LogClassicTitleRefusalAsync(context, reason).ConfigureAwait(false);
            return;
        }
        var delivered = await PublishClassicTitleAsync(context, updated, identity,
            [acknowledgement], cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer LegacyTitle outcome=committed delivery={(delivered ? "complete" : "stopped")} title={packet.Title}.")
            .ConfigureAwait(false);
    }

    private static bool TryValidateClassicTitleSelection(WorldPacketHandlerContext context,
        byte selected, byte[] owned, out string reason)
    {
        reason = "titleNotOwned";
        if (selected != 0 && !owned.Contains(selected)) return false;
        reason = "recoveryOwnerUnavailableOrBlocked";
        if (context.TradeRecovery is null || context.TradeRecovery.IsBlocked) return false;
        reason = string.Empty;
        return true;
    }
}
