namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyManiacDanceAttacksAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var operation = await _worldOperations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false);
        if (_tradeRecovery.IsBlocked || !_tradeRecovery.TryRequireLiveJournalClear(out _)) return;
        await SendDueLegacyManiacDanceAttacksHeldAsync(now, cancellationToken, operation).ConfigureAwait(false);
    }

    private async ValueTask SendDueLegacyManiacDanceAttacksHeldAsync(
        DateTimeOffset now, CancellationToken cancellationToken, IDisposable operation)
    {
        if (!_worldOperations.OwnsActiveExclusiveLease(operation))
            throw new InvalidOperationException("periodicCombatOperationRequiresExclusiveLease");
        foreach (var active in _sessions.Snapshot())
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null)
            {
                continue;
            }

            var current = active with { Session = currentSession };
            var context = CreateMonsterRuntimeOperationContext(current, operation);
            await WorldPacketHandlers.TrySendDueLegacyManiacDanceAttackAsync(
                    context,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
