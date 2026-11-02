using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        public async ValueTask<bool> RunVigorAndCreateSpawnPrefixAsync()
        {
            if (!Refresh()) return false;
            CaptureVigorQueryMetadata();
            var maximum = LegacyMaxHitpointsCalculator.CalculateQueryMaxVigor(
                CapturedEquipment, Context.WorldData);
            var vigor = LegacyEquipmentQueryBounds.ClampVigor(
                LegacyEquipmentQueryBounds.ReadVigor(Session.Character), maximum);
            if (!CommitManagedOrLegacyVigor(vigor))
                return false;
            if (!Request.CallItems) return true;
            if (!await SendAsync(new LegacyServerInfoPacket(vigor).ToFrameBytes(),
                    LegacyEquipmentQueryPublishedPhases.ServerInfo).ConfigureAwait(false))
                return false;
            return await SendPeersAsync().ConfigureAwait(false);
        }

        public async ValueTask<bool> SendPeersAsync()
        {
            if (!Refresh()) return false;
            var publication = await SendLegacyCurrentPlayerSpawnToPeersAsync(
                    Context, Session, Request.Scope, CancellationToken, SourceItems, CapturedAlternate)
                .ConfigureAwait(false);
            Adopt(publication.Current);
            if (!Current.Completed) return false;
            Mark(LegacyEquipmentQueryPublishedPhases.PeerSpawn);
            return true;
        }
    }
}
