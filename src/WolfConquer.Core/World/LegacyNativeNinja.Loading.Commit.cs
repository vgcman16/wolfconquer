using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private void CommitNonNinjaLoading(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
    {
        var input = CaptureRankingChange(client, player, null);
        using var ranks = LegacyNativeNinjaRank.PrepareRefresh(input.Uid, input.Replacements);
        input.RequireCurrent();
        ranks.Publish();
    }

    private void CommitFullLoading(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        LegacyNativeSpellStorage storage, LoadingInput input, LegacyNativeSpellStorage.PreparedNinjaSpells spells,
        PreparedNinjaSequence sequence)
    {
        var ranking = CaptureRankingChange(client, player, null);
        lock (storage.MutationGate)
        {
            using var ranks = LegacyNativeNinjaRank.PrepareRefresh(ranking.Uid, ranking.Replacements);
            ranking.RequireCurrent(); // Last comparer-capable appearance read.
            input.RequireCurrent(); spells.RequireCurrent();
            sequence.RentStorage();
            ranking.RequireCurrentPlain(); input.RequireCurrent(); spells.RequireCurrent();
            sequence.RequireStorage(); ranks.RequireCurrentForPublication();
            // Every preparation/recheck has completed. Only actual owner stores remain.
            ranks.PublishPreparedMaps();
            spells.PublishPreparedMap();
            if (input.BadgeChanges) player.ApplyPreparedNinjaBadge(input.Badge);
        }
    }

    private static void CommitAlternate(LegacyNativeSpellStorage storage, LoadingInput input,
        LegacyNativeSpellStorage.PreparedNinjaSpells spells, PreparedNinjaSequence sequence)
    {
        lock (storage.MutationGate)
        {
            input.RequireCurrent(); spells.RequireCurrent();
            sequence.RentStorage();
            input.RequireCurrent(); spells.RequireCurrent(); sequence.RequireStorage();
            spells.PublishPreparedMap();
        }
    }

    private static void CommitBadge(LegacyNativePlayerRuntime player, LoadingInput input, PreparedNinjaSequence sequence)
    {
        input.RequireCurrent(); sequence.RentStorage();
        input.RequireCurrent(); sequence.RequireStorage();
        if (input.BadgeChanges) player.ApplyPreparedNinjaBadge(input.Badge);
    }
}
