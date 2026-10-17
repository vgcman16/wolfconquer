using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyTransformSessionMergeAttemptLimit = 4;

    private async ValueTask SendDueLegacyTransformExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var playerCount = 0;
        var viewerCount = 0;
        foreach (var active in _sessions.Snapshot())
        {
            var ownerUid = active.Session.Character.Uid;
            LegacyPlayerTransformFinishPreparation? committed = null;
            for (var attempt = 0; attempt < LegacyTransformSessionMergeAttemptLimit; attempt++)
            {
                if (!_sessions.TryGet(active.Connection, out var current) || current is null ||
                    current.Character.Uid != ownerUid ||
                    !TryPrepareLegacyTransformExpiration(
                        current, _worldData, now,
                        _runtimeUpdateLoop.LegacyClock ?? LegacySignedMillisecondClock.System,
                        out var prepared, out _))
                {
                    break;
                }

                if (_sessions.TryCompareAndSet(active.Connection, current, prepared.Session))
                {
                    committed = prepared;
                    break;
                }
            }

            if (committed is not { } query)
            {
                continue;
            }

            var context = CreateMonsterRuntimeContext(active with { Session = query.Session });
            var finished = await WorldPacketHandlers.PublishLegacyPlayerTransformFinishAsync(
                    context, query, cancellationToken)
                .ConfigureAwait(false);
            viewerCount += finished.ViewerCount;
            if (finished.Session is not null)
            {
                playerCount++;
            }
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer expired legacy Transform state " +
                    $"playerCount={playerCount} viewerCount={viewerCount}; " +
                    "source=ClientTransform.CheckUp/FinishTransform.")
                .ConfigureAwait(false);
        }
    }

    internal static bool TryPrepareLegacyTransformExpiration(
        WorldSession session,
        WorldDataCatalog? worldData,
        DateTimeOffset now,
        ILegacySignedMillisecondClock clock,
        out LegacyPlayerTransformFinishPreparation prepared,
        out uint maxHitpoints)
    {
        ArgumentNullException.ThrowIfNull(session);
        prepared = new(session);
        maxHitpoints = 0;
        if (!LegacyTransformState.IsExpired(session.Character, now) ||
            !WorldPacketHandlers.TryPrepareLegacyPlayerTransformFinish(session, worldData, clock, out prepared))
        {
            return false;
        }

        LegacyEquipmentHitpointsStatus.TryGetForOwner(prepared.Session, out maxHitpoints);
        return true;
    }
}
