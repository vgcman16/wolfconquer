using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyTimedMagicShieldStatusFlag = 0x08;
    private const int LegacyTimedStigmaStatusFlag = 0x09;

    private async ValueTask SendDueLegacyStigmaMagicShieldExpirationsAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var playerCount = 0;
        var viewerCount = 0;
        foreach (var candidate in _sessions.Snapshot())
        {
            var uid = candidate.Session.Character.Uid;
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(candidate.Connection, uid, out var current))
                continue;
            var provider = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;
            // Native comparison time is per client, then GetFlags captures old objects before any await.
            var subjectNow = provider.GetUtcNow();
            var entries = CaptureLegacyRootItemEffectExpirations(
                candidate.Connection, current, provider, subjectNow, out var cleaned);
            if (!ReferenceEquals(current.Character, cleaned.Character))
                _sessions.Set(candidate.Connection, cleaned);
            if (entries.Count == 0) continue;
            playerCount++;
            foreach (var flag in entries)
            {
                if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(candidate.Connection, uid, out current)) break;
                var character = RetireLegacyRootItemEffectRegistration(
                    candidate.Connection, current.Character, flag, out var removed);
                var expired = current with { Character = character };
                _sessions.Set(candidate.Connection, expired);
                if (!removed) continue;
                var publication = await SendLegacyRootItemEffectExpiryVectorAsync(
                    candidate.Connection, uid, expired, cancellationToken).ConfigureAwait(false);
                viewerCount += publication.ViewerCount;
                if (publication.Session is null) break;
            }
        }
        if (playerCount > 0)
            await _log.WriteLineAsync("WorldServer expired legacy Stigma/MagicShield flags " +
                $"playerCount={playerCount} viewerCount={viewerCount}.").ConfigureAwait(false);
    }
}
