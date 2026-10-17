namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> RejectLegacyRootFirstFamilyWithDiagnosticAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        string reason)
    {
        var uid = session.Character.Uid;
        await context.Log.WriteLineAsync(
                "WorldServer skipped legacy player magic " +
                $"{FormatSession(context with { Session = session })} spellId={spell.Id} {reason}.")
            .ConfigureAwait(false);
        // A diagnostic writer is awaited too. Do not continue a retired owner's invocation.
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session))
        {
            return null;
        }

        return null; // These original rejection branches never resume gameplay after the log.
    }
}
