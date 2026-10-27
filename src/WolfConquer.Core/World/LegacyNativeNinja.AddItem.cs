using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void AddItem(uint ID, uint Level, uint Position, uint Points, uint UK)
    {
        var client = user ?? throw UnavailableRanking();
        RefuseNinjaPublicEntry(client);
        PreparedRankingItemFrame? publication = null;
        RankingChange? committed = null;
        try
        {
            lock (client.SpellAdmissionSyncRoot)
            {
                var player = client.Player ?? throw UnavailableRanking();
                lock (player.SpellAdmissionSyncRoot)
                {
                    RefuseNinjaPublicEntry(client);
                    lock (persistenceLoadGate)
                    {
                        var item = new Item { ItemID = ID, Level = Math.Clamp(Level, 1u, 9u),
                            Position = Position, Exp = Points, UK = UK };
                        item.UPDATE(); // Genuine source no-op on this still-private item.
                        var input = CaptureRankingChange(client, player, item);
                        using var ranks = LegacyNativeNinjaRank.PrepareRefresh(input.Uid, input.Replacements);
                        publication = PreparedRankingItemFrame.Create(input.Uid, item);
                        input.RequireCurrent();
                        publication.RentStorage();
                        input.RequireCurrentPlain();
                        publication.RequireCurrent();
                        ranks.RequireCurrentForPublication();
                        input.PublishCandidateState();
                        ranks.PublishPreparedMaps();
                        committed = input;
                    }
                }
            }
            publication.Publish(client, committed!);
        }
        finally { publication?.Dispose(); }
    }
}
