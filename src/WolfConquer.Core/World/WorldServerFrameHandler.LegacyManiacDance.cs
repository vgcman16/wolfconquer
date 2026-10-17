namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyManiacDanceAttacksAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var active in _sessions.Snapshot())
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null)
            {
                continue;
            }

            var current = active with { Session = currentSession };
            var context = CreateMonsterRuntimeContext(current);
            await WorldPacketHandlers.TrySendDueLegacyManiacDanceAttackAsync(
                    context,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
