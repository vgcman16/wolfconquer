namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // The real production due hook. Native preparation reads DateTime.Now and
    // its current actual row; no request/test-supplied expiry timestamp exists.
    internal async ValueTask SendDueLegacyWindWalkerFanExpirationsAsync(CancellationToken cancellationToken)
    {
        foreach (var candidate in _sessions.Snapshot())
        {
            cancellationToken.ThrowIfCancellationRequested();
            long uid = candidate.Session.Character.Uid;
            if (!_sessions.HasJoinedWindWalker(candidate.Session) ||
                !LegacyNativeClientPublication.TryCaptureIdentity(_sessions, candidate.Connection, uid, out var identity) ||
                !LegacyNativeClientPublication.TryCurrent(_sessions, candidate.Connection, uid, identity, out var current)) continue;
            var context = CreateMonsterRuntimeContext(candidate with { Session = current });
            if (!_sessions.TryExpireWindWalkerFan(context, current, identity, out _, out var publication)) continue;
            if (publication is not null) await publication.PublishAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
