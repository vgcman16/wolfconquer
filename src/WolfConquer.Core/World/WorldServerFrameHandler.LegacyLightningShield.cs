namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyLightningShieldStatusFlag = 0xF0;

    private async ValueTask SendDueLegacyLightningShieldExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await SendDueLegacyLightningShieldActivatedExpirationsAsync(now, cancellationToken).ConfigureAwait(false);
        var playerCount = 0;
        foreach (var candidate in _sessions.Snapshot())
        {
            var uid = candidate.Session.Character.Uid;
            if (!_sessions.TryGet(candidate.Connection, out var current) || current is null ||
                current.Character.Uid != uid ||
                !TryExpireLegacyTimedStatusFlag(current.Character, now, LegacyLightningShieldStatusFlag,
                    WorldPacketHandlers.RuntimeLegacyLightningShieldExpiresAtUtcAttribute,
                    out var character, out var removed)) continue;

            // Generic ThreadPool expiry removes240 only.237 and Left are
            // independent; its specialized reset/removal owner ran above.
            var updated = current with { Character = character };
            _sessions.Set(candidate.Connection, updated);
            if (!removed) continue;
            var context = CreateMonsterRuntimeContext(candidate with { Session = updated });
            await WorldPacketHandlers.SendLegacyLightningShieldFamilyVectorAsync(
                    context, updated, uid, cancellationToken)
                .ConfigureAwait(false);
            playerCount++;
        }

        if (playerCount > 0)
            await _log.WriteLineAsync(
                    $"WorldServer expired legacy LightningShield flags playerCount={playerCount}.")
                .ConfigureAwait(false);
    }
}
