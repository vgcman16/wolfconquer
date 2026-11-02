using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyTimedMagicShieldStatusFlag = 0x08;
    private const int LegacyTimedStigmaStatusFlag = 0x09;

    // Each actual capture/commit owns a real short Shared lease. No such lease
    // survives into an awaited owner/viewer transport send.
    private async ValueTask SendDueLegacyStigmaMagicShieldExpirationsAsync(
        RootExpiryRun run, CancellationToken cancellationToken)
    {
        if (!RootExpiryRunIsCurrent(run, out _)) return;
        WorldActiveSession[] snapshot;
        using (var operation = await _worldOperations.EnterSharedAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!RootExpiryRunIsCurrent(run, out _) || !_worldOperations.OwnsActiveLease(operation)) return;
            snapshot = _sessions.Snapshot().ToArray();
        }
        var playerCount = 0;
        var viewerCount = 0;
        foreach (var candidate in snapshot)
        {
            RootItemEffectDue? due = null;
            var initial = candidate.Session;
            var uid = initial.Character.Uid;
            using (var operation = await _worldOperations.EnterSharedAsync(cancellationToken).ConfigureAwait(false))
            {
                var tick = IssueRootExpiryTick(run, operation);
                if (tick is null) return;
                try
                {
                    try { due = CaptureLegacyRootItemEffectExpirations(tick, candidate.Connection, initial); }
                    catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                    { continue; }
                }
                finally { RootExpiryTicks.Remove(tick); }
            }
            if (due is null || due.Entries.Length == 0) continue;
            var expected = due.Initial;
            var expectedMap = due.Map;
            bool any = false;
            for (int index = 0; index < due.Entries.Length; index++)
            {
                WorldSession? expired = null; WorldSessionStore.RootExpiryOutputReceipt? output = null;
                LegacyNativeAttributeMapProof? successorMap = null;
                bool removed = false, committed = false;
                using (var operation = await _worldOperations.EnterSharedAsync(cancellationToken).ConfigureAwait(false))
                {
                    var tick = IssueRootExpiryTick(run, operation);
                    if (tick is null) return;
                    try
                    {
                        var authorization = new RootExpiryAuthorization();
                        RootExpiryAuthorizations.Add(authorization, new(this, tick, _sessions, candidate.Connection,
                            due.Identity, expected, expectedMap, due.Entries[index], due.SubjectNow, index));
                        try { committed = _sessions.TryCommitRootItemEffectExpiry(candidate.Connection, due.Identity,
                            expected, authorization, out expired, out removed, out output, out successorMap); }
                        finally { RootExpiryAuthorizations.Remove(authorization); }
                    }
                    finally { RootExpiryTicks.Remove(tick); }
                }
                if (!committed || expired is null || successorMap is null) break;
                expected = expired;
                expectedMap = successorMap;
                if (!removed) continue;
                if (output is null) break;
                if (!any) { playerCount++; any = true; }
                // The exact committed holder/identity is rechecked around each
                // actual send. A changed holder is never adopted by UID.
                var publication = await SendLegacyRootItemEffectExpiryVectorAsync(
                    candidate.Connection, uid, due.Identity, expired, output, cancellationToken).ConfigureAwait(false);
                viewerCount += publication.ViewerCount;
                if (!publication.Current || !RootExpiryRunIsCurrent(run, out _)) break;
            }
        }
        if (playerCount > 0)
            await _log.WriteLineAsync("WorldServer expired legacy Stigma/MagicShield flags " +
                $"playerCount={playerCount} viewerCount={viewerCount}.").ConfigureAwait(false);
    }
}
