using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private void CommitNonNinjaLoading(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
    {
        var input = CaptureRankingChange(client, player, null);
        using var ranks = LegacyNativeNinjaRank.PrepareRefresh(input.Uid, input.Replacements);
        input.RequireCurrent(); ranks.Publish();
    }

    private void CommitFullLoading(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        LegacyNativeSpellStorage storage, LoadingInput input, LegacyNativeSpellStorage.PreparedNinjaSpells spells,
        PreparedNinjaSequence sequence, LegacyOrdinarySpellBinding? binding,
        WorldSessionStore.NativeSpellMutationLease? lease)
    {
        var ranking = CaptureRankingChange(client, player, null);
        lock (storage.MutationGate)
        lock (binding?.Proficiencies.MutationGate ?? storage.MutationGate)
        {
            using var ranks = LegacyNativeNinjaRank.PrepareRefresh(ranking.Uid, ranking.Replacements);
            ranking.RequireCurrent(); input.RequireCurrent(); spells.RequireCurrent(); sequence.RentStorage();
            var profs = binding is null ? null : LegacyNativeProficiencyStorage.PrepareSessionSnapshot(
                client, player, lease!.ExpectedProficiencies, lease.ExpectedProficiencies);
            spells.ArmFinalProof();
            profs?.ArmFinalProof();
            if (lease is not null) lease.PreparePublication(spells.FinalSnapshot, profs!.FinalSnapshot);
            ranking.RequireCurrentPlain(); input.RequireCurrent();
            sequence.RequireStorage(); ranks.RequireCurrentForPublication();
            if (!spells.ConsumeBeforeWriterPlain() || profs is not null && !profs.ConsumeBeforeWriterPlain() ||
                !spells.ConsumeCurrentPlain() || profs is not null && !profs.ConsumeCurrentPlain() ||
                lease is not null && !lease.IsCurrentPlain()) throw UnavailableRanking();
            ranks.PublishPreparedMaps(); spells.PublishPreparedMap();
            profs?.PublishPreparedMap();
            if (input.BadgeChanges) player.ApplyPreparedNinjaBadge(input.Badge);
            lease?.PublishPrepared();
        }
    }

    private static void CommitAlternate(LegacyNativeSpellStorage storage, LoadingInput input,
        LegacyNativeSpellStorage.PreparedNinjaSpells spells, PreparedNinjaSequence sequence,
        LegacyOrdinarySpellBinding? binding, WorldSessionStore.NativeSpellMutationLease? lease)
    {
        lock (storage.MutationGate)
        lock (binding?.Proficiencies.MutationGate ?? storage.MutationGate)
        {
            input.RequireCurrent(); spells.RequireCurrent(); sequence.RentStorage();
            var profs = binding is null ? null : LegacyNativeProficiencyStorage.PrepareSessionSnapshot(
                binding.Client, binding.Player, lease!.ExpectedProficiencies, lease.ExpectedProficiencies);
            spells.ArmFinalProof();
            profs?.ArmFinalProof();
            if (lease is not null) lease.PreparePublication(spells.FinalSnapshot, profs!.FinalSnapshot);
            input.RequireCurrent(); sequence.RequireStorage();
            if (!spells.ConsumeBeforeWriterPlain() || profs is not null && !profs.ConsumeBeforeWriterPlain() ||
                !spells.ConsumeCurrentPlain() || profs is not null && !profs.ConsumeCurrentPlain() ||
                lease is not null && !lease.IsCurrentPlain()) throw UnavailableRanking();
            spells.PublishPreparedMap(); profs?.PublishPreparedMap(); lease?.PublishPrepared();
        }
    }

    private static void CommitBadge(LegacyNativePlayerRuntime player, LoadingInput input, PreparedNinjaSequence sequence)
    {
        input.RequireCurrent(); sequence.RentStorage(); input.RequireCurrent(); sequence.RequireStorage();
        if (input.BadgeChanges) player.ApplyPreparedNinjaBadge(input.Badge);
    }
}
