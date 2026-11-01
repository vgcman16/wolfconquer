using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitDragonhowlFlashingExpiry(WorldServerFrameHandler frame,
        WorldServerFrameHandler.OwnedDragonhowlFlashingExpiry purpose, out DragonhowlFlashingExpiryOutput? output)
    {
        output = null;
        if (!ReferenceEquals(framePersistenceOwner, frame) ||
            !frame.TryReadDragonhowlFlashingExpiryParticipants(purpose, this, out var context, out var binding, out var before)) return false;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(binding.Connection));
            if (!_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                using var actors = new SpellActorScope(binding);
                lock (binding.Player.BitVector.SyncRoot)
                {
                    if (!_sessions.TryGetValue(binding.Connection, out var holder) || !ReferenceEquals(holder.Read(), before) ||
                        !BindingIsCurrentPlain(binding, holder, before) ||
                        !frame.TryReadDragonhowlFlashingExpiry(purpose, out _, out _, out _) ||
                        !LegacyNativeStatusFlagsBigVector32.TryCaptureOwnedDragonhowlFlashing(binding, before, out var lease) ||
                        lease is null || !lease.IsDueNow) return false;
                    var proposed = ProjectDragonhowlFlashingRemoval(before);
                    var removal = lease.PrepareExpiry(frame, purpose, before, proposed);
                    using var publication = new PreparedSessionSpellPublication(this, binding.Connection, holder, before, proposed,
                        dragonhowlFlashingExpiry: removal);
                    if (publication.HitpointsWrite is not null) throw InvalidSpellPublication();
                    var preparedOutput = new DragonhowlFlashingExpiryOutput(this, frame, context, binding,
                        publication.FinalSession, holder.NativeClientIdentity, removal);
                    publication.ArmFinalProof();
                    if (!publication.ConsumeBeforeWriterPlain() || !publication.IsCurrentPlain() ||
                        !removal.IsPreparedFor(binding, before, publication.FinalSession) || !removal.ConsumeBeforePublication()) return false;
                    try
                    {
                        publication.PublishNative(); removal.PublishPrepared(); publication.PublishHolder();
                        preparedOutput.ConfirmCommitted();
                    }
                    catch (Exception error) { preparedOutput.Fence(error); throw; }
                    output = preparedOutput;
                }
            }
        }
        // Actual successor is retained in output before any callback/notification.
        try { NotifyPublishedSessionChange(binding.Connection, before, output.Session, binding.NativeIdentity, binding); }
        catch (Exception error) { output.Fence(error); throw; }
        return true;
    }

    private static WorldSession ProjectDragonhowlFlashingRemoval(WorldSession before)
    {
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(before.Character.Attributes);
        var words = LegacyWindWalkerStatusProjection.Read(before.Character);
        words[0] &= ~1UL;
        var attributes = new Dictionary<string, string>(proof.Fields, proof.CapturedComparer);
        foreach (var key in attributes.Keys.Where(key => key.Equals("StatusFlags", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("BitVector", StringComparison.OrdinalIgnoreCase)).ToArray()) attributes[key] = string.Join('/', words) + "/";
        var policies = attributes.Keys.Where(key => key.Equals(LegacyStatusFlagDeathRegistration.Key(0), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (policies.Length != 1 || !proof.IsCurrentPlain()) throw InvalidSpellPublication();
        attributes.Remove(policies[0]);
        return before with { Character = before.Character with { Attributes = attributes }, CanAttackFlashingRegistration = null };
    }
}
