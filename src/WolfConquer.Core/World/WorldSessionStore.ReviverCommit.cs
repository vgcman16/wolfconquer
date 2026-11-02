using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Inert without its real Store association. This is a real commit receipt,
    // not an output or authority for any detached receiver.
    internal sealed class CommittedOwnedReviver { }
    private sealed record OwnedReviverData(WorldServerFrameHandler Frame, ConquerTcpConnection Connection,
        object Identity, LegacyOrdinarySpellBinding Binding, WorldSession Before, WorldSession After,
        LegacyNativeMagicQueryClient.CompletedMaximums Query,
        LegacyNativeMagicQueryClient.RecordedReceiveValues Maximums,
        MonsterRuntimeStateStore.ClassicBuffTarget Monster, DateTimeOffset Now,
        WorldServerFrameHandler.PreparedReviverFrames Frames, WorldServerFrameHandler.ReviverAudience[] Audience)
    { internal int Started; }
    private readonly ConditionalWeakTable<CommittedOwnedReviver, OwnedReviverData> committedOwnedRevivers = new();

    // The real global reviver pass calls this only with its private active
    // admission. Canonical owner/animation/spawn frames are preflighted before
    // the actual writer; the returned receipt alone authorizes later output.
    internal bool TryCommitOwnedReviver(WorldServerFrameHandler frame, WorldServerFrameHandler.ReviverAdmission admission,
        ConquerTcpConnection connection, WorldSession expected, out CommittedOwnedReviver? receipt, out string reason)
    {
        receipt = null; reason = "owner-gate";
        RefuseBoundSpellReentry(connection);
        WorldSession? final = null; object identity; LegacyOrdinarySpellBinding? binding = null;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!WorldServerFrameHandler.TryReadReviverAdmission(admission, frame, this, connection, expected,
                        out identity, out var now, out var monster, out var audience) ||
                    !_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    !ReferenceEquals(holder.NativeClientIdentity, identity) || holder.SpellBinding is not { } actualBinding ||
                    !BindingIsCurrentPlain(actualBinding, holder, expected)) return false;
                binding = actualBinding;
                reason = "completed-query";
                using var actors = new SpellActorScope(binding);
                if (!LegacyNativeMagicQueryClient.TryCaptureRecordedReceive(binding, expected, out var query, out var maximums) ||
                    query is null || maximums.MaximumHitpoints == 0 || !query.IsCurrentPlain(binding, expected)) return false;
                reason = "source-maximum";
                try { if (frame.ComputeReviverSourceMaximum(expected) != maximums.MaximumHitpoints) return false; }
                catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                { return false; }
                reason = "revive-eligibility";
                var revive = LegacyPlayerDeathState.TryRevive(expected.Character, now, new LegacyPlayerReviveOptions
                {
                    IgnoreReviveDelay = true, ReviveDelay = TimeSpan.Zero,
                    MaximumHitpoints = maximums.MaximumHitpoints, MaximumMana = expected.Character.MaxMana,
                });
                if (!revive.Allowed) return false;
                var proposed = expected with { Character = revive.Character };
                reason = "mailbox-fence";
                if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                reason = "publication-preparation";
                using var publication = new PreparedSessionSpellPublication(this, connection, holder, expected, proposed);
                var vector = binding.Player.BitVector;
                if (vector is null) return false;
                lock (vector.SyncRoot)
                {
                    reason = "frame-preflight";
                    WorldServerFrameHandler.PreparedReviverFrames frames;
                    try
                    {
                        var spawn = publication.PrepareReviverSpawn(maximums.MaximumHitpoints);
                        frames = frame.PrepareReviverFramesWithPreparedSpawn(publication.FinalSession, binding.Player,
                            monster.Instance, maximums.MaximumHitpoints, spawn);
                        publication.RequireCurrentBeforeWriter();
                    }
                    catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                    { reason = "frame-preflight:" + error.GetType().Name + ":" + error.Message; return false; }
                    var prepared = new CommittedOwnedReviver();
                    var data = new OwnedReviverData(frame, connection, identity, binding, expected,
                        publication.FinalSession, query, maximums, monster, now, frames, audience);
                    committedOwnedRevivers.Add(prepared, data);
                    reason = "query-before-writer";
                    if (!query.IsCurrentPlain(binding, expected))
                    { committedOwnedRevivers.Remove(prepared); return false; }
                    reason = "admission-before-writer";
                    if (!WorldServerFrameHandler.ConsumeReviverAdmission(admission, frame, this, connection, expected))
                    { committedOwnedRevivers.Remove(prepared); return false; }
                    reason = "native-writer";
                    if (!TryPublishFrameOwnedHitpoints(publication, holder, out var nativeReason, monster: monster))
                    { reason = "native-writer:" + nativeReason; committedOwnedRevivers.Remove(prepared); return false; }
                    final = publication.FinalSession; receipt = prepared;
                    publication.Dispose();
                    PersistFrameOwnedSessionChange(connection, expected, final, identity, binding, publication.HitpointsWrite);
                }
            }
        }
        NotifyPublishedSessionChange(connection, expected, final!, identity, binding);
        reason = string.Empty; return true;
    }
}
