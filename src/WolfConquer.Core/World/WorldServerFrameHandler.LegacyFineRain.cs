using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyFineRain1StatusFlag = 0xD4;
    private const int LegacyFineRain2StatusFlag = 0xD5;
    private const string RuntimeLegacyFineRain1RemoveOnDeadAttribute = "LegacyFineRain1RemoveOnDead";
    private const string RuntimeLegacyFineRain2RemoveOnDeadAttribute = "LegacyFineRain2RemoveOnDead";

    private async ValueTask SendDueLegacyFineRainExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var playerCount = 0;
        var viewerCount = 0;
        var queryCount = 0;
        foreach (var candidate in _sessions.Snapshot())
        {
            var ownerUid = candidate.Session.Character.Uid;
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(candidate.Connection, ownerUid, out var current))
                continue;

            // Source BuffersCallback captures its comparison time separately for EACH client.
            var subjectNow = (_runtimeUpdateLoop.TimeProvider ?? TimeProvider.System).GetUtcNow();
            // GetFlags captures both expiry AND invocation state before the first await.
            var expirations = CaptureLegacyFineRainExpiryEntries(current, subjectNow, out var cleaned);
            var entries = CaptureLegacyFineRainRuntimeEntries(current, expirations);
            if (!ReferenceEquals(current.Character, cleaned.Character))
                _sessions.Set(candidate.Connection, cleaned);
            if (entries.Count == 0) continue;
            if (expirations.Count > 0) playerCount++;

            foreach (var entry in entries)
            {
                if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(candidate.Connection, ownerUid, out current))
                    break;
                if (entry.Expiry is not null)
                {
                    var expiredCharacter = RetireLegacyFineRainRegistration(
                        current.Character, entry.Flag, out var removedVisibleFlag);
                    var expired = current with { Character = expiredCharacter };
                    _sessions.Set(candidate.Connection, expired);

                    if (removedVisibleFlag)
                    {
                        var publication = await SendLegacyFineRainExpiryVectorAsync(
                                candidate.Connection, ownerUid, expired, cancellationToken).ConfigureAwait(false);
                        viewerCount += publication.ViewerCount;
                        if (publication.Session is null) break;
                    }

                    // Superseding a vector or removing the current flag never cancels this old entry's query.
                    if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(candidate.Connection, ownerUid, out current))
                        break;
                    var context = CreateMonsterRuntimeContext(candidate with { Session = current });
                    queryCount++;
                    var query = await WorldPacketHandlers.ExecuteLegacyEquipmentQueryAsync(
                            context, current,
                            new LegacyEquipmentQueryRequest(
                                LegacyEquipmentRebuildReason.FineRainChange,
                                new LegacyEquipmentQueryPublicationScope(ownerUid)),
                            cancellationToken).ConfigureAwait(false);
                    if (!query.Completed) break;
                }

                // This is a separate source if-chain AFTER expiry/query, even for a removed old212.
                if (entry.Invocation is { } invocation)
                {
                    if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(candidate.Connection, ownerUid, out current))
                        break;
                    var context = CreateMonsterRuntimeContext(candidate with { Session = current });
                    var invoked = await WorldPacketHandlers.ExecuteLegacyFineRain212InvocationAsync(
                            context, current, invocation, subjectNow,
                            _runtimeUpdateLoop.LegacyEnvironmentClock ?? LegacyEnvironmentMillisecondClock.System,
                            cancellationToken).ConfigureAwait(false);
                    if (invoked is null) break;
                }
            }
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer expired legacy FineRain flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount} queryCount={queryCount}.")
                .ConfigureAwait(false);
        }
    }
}
