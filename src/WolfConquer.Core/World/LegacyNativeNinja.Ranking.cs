using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void UpdateRank()
    {
        var client = user ?? throw UnavailableRanking();
        RefuseNinjaPublicEntry(client);
        lock (client.SpellAdmissionSyncRoot)
        {
            var player = client.Player ?? throw UnavailableRanking();
            lock (player.SpellAdmissionSyncRoot)
            {
                RefuseNinjaPublicEntry(client);
                lock (persistenceLoadGate)
                {
                    var input = CaptureRankingChange(client, player, added: null);
                    using var ranks = LegacyNativeNinjaRank.PrepareRefresh(input.Uid, input.Replacements);
                    input.RequireCurrent();
                    ranks.Publish();
                }
            }
        }
    }

    private static InvalidOperationException UnavailableRanking() =>
        new("Ninja ranking requires current owned player, item and metadata state.");
}
