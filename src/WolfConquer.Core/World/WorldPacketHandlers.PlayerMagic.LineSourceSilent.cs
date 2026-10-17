using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerLineSourceSilentMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyLineSourceSilentMagic(spell))
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled source-silent Line magic " +
                $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetUid={query.OpponentUid}; source=Line.cs missing case after CanUseSpell.Verified.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyLineSourceSilentMagic(LegacyMagicRuntimeSpell spell)
    {
        return spell.Sort == LegacyMagicSort.Line &&
            spell.Id is 13_450 or 13_460 or 18_003;
    }
}
