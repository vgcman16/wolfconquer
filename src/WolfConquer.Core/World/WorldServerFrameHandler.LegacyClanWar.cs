namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyClanWarCurrentScorePanelFramesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var active = activeSessions[0];
        var context = new WorldPacketHandlerContext(
            active.Connection,
            _log,
            active.Session,
            _arenaStatistics,
            _arenaStates,
            _worldData,
            _sessions,
            _teamArenaStatistics,
            _teamArenaStates,
            _teamStates,
            _monsterStates,
            _sobNpcStates,
            _floorItems,
            _terrain,
            _guildWarStates,
            _snapshotLoader?.PlayerSpellsRoot,
            _snapshotLoader?.PlayerItemsRoot,
            _mailboxes,
            _snapshotLoader?.PlayerProficienciesRoot,
            _elitePkStates,
            _teamPkStates,
            _dragonWarStates,
            PlayerQuestsRoot: _snapshotLoader?.PlayerQuestsRoot,
            ClientCompatibility: _clientCompatibility,
            TeleportTerrain: _teleportTerrain);

        await WorldPacketHandlers.SendDueLegacyClanWarCurrentScorePanelFramesAsync(
                context,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        await WorldPacketHandlers.SendDueLegacyClanWarCurrentCompleteWarFramesAsync(
                context,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        await WorldPacketHandlers.SendDueLegacyClanWarCurrentOpenWarFramesAsync(
                context,
                now,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
