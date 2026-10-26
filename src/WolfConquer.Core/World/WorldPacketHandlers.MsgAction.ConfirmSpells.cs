namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleConfirmSpellsAsync(WorldPacketHandlerContext context,
        MsgActionPacket packet, CancellationToken cancellationToken)
    {
        if (context.Session is not { } session || context.Sessions is not { } sessions || context.Trades is not { } trades ||
            !LegacySpellPublicationPlan.TryPrepare(session.PlayerSpells, null, out var plan) || plan is null)
        {
            await context.Log.WriteLineAsync($"WorldServer skipped MsgAction confirm-spells {FormatSession(context)}; " +
                "a valid current learned-spell owner is unavailable.").ConfigureAwait(false);
            return;
        }
        // Prepare the complete response/echo before its first send. No save,
        // grant, debit or membership mutation occurs on this read-only action.
        var echo = packet.ToFrameBytes();
        if (!LegacySpellPublicationOwner.TryBind(sessions, trades, context.Connection, session, plan, out var owner) || owner is null ||
            !await owner.PublishAsync(echo, cancellationToken).ConfigureAwait(false)) return;
        await context.Log.WriteLineAsync("WorldServer sent LegacySpell confirm " +
            $"{FormatSession(context)} count={plan.Frames.Length}.").ConfigureAwait(false);
    }
}
