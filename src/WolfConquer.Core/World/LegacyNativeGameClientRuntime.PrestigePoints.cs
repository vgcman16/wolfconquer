using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    internal void CreatePrestigePoints()
    {
        var prepared = PreparePrestigeRebuild();
        if (!prepared.PublishRanks)
        {
            // Foreign-server rebuilds never consult the unrelated ranking owner.
            RequireCurrentPrestigeOwners(prepared.Owners);
            CommitPreparedPrestige(prepared);
            return;
        }

        var gate = LegacyNativePrestigeRanking.SynRoot
            ?? throw new InvalidOperationException("Prestige publication requires its shared gate.");
        lock (gate)
        {
            if (!ReferenceEquals(gate, LegacyNativePrestigeRanking.SynRoot))
                throw new InvalidOperationException("The prestige publication gate changed.");
            RequireCurrentPrestigeOwners(prepared.Owners);
            LegacyNativePrestigeRanking.PublishPrestigeEntry(prepared.Entry, prepared.IncludeClass);
            CommitPreparedPrestige(prepared);
        }
    }

    private void CommitPreparedPrestige(PreparedPrestigeRebuild prepared)
    {
        // All allocations, metadata reads and admission precede these field stores.
        PrestigePoints = prepared.Points;
        MyPrestigePoints = prepared.Total;
        PrestrigeEntry = prepared.Entry;
        prepared.Owners.Effects.ApplyPrepared(prepared.Effects);
        if (prepared.Owners.GuildMember is { } member)
            member.PrestigePoints = prepared.Total;
    }
}
