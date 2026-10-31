using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryPrepareStaminaReadiness(WorldServerFrameHandler frame, ConquerTcpConnection connection,
        WorldSession expected, TimeProvider clock, out LegacyOwnedStaminaRuntime.Readiness? readiness)
    {
        readiness = null;
        RefuseBoundSpellReentry(connection);
        var runtime = frame.CurrentStaminaRuntime(this);
        var lifecycle = frame.RequireWindWalkerLifecycleGate(this);
        if (runtime is null || !ReferenceEquals(framePersistenceOwner, frame) || !Monitor.IsEntered(lifecycle) ||
            !_connectionGates.TryGetValue(connection, out var gate)) return false;
        lock (gate)
        {
            using var nativeGate = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) || holder.SpellBinding is not { } binding ||
                !BindingIsCurrentPlain(binding, holder, expected) || !IsFramePersistenceCurrent(frame, connection, expected)) return false;
            using var actors = new SpellActorScope(binding);
            var stamina = new LegacyStaminaState.NativeValueProof(expected.Character);
            if (binding.Client.Fake || binding.Player.Stamina != stamina.Value || !stamina.IsCurrentPlain() ||
                !binding.Items.Image.IsCurrentPlain() || !BindingIsCurrentPlain(binding, holder, expected)) return false;
            readiness = new(frame, runtime, this, connection, binding, clock, lifecycle);
            return true;
        }
    }

    internal bool TryCommitOwnedStaminaTick(LegacyOwnedStaminaRuntime.Readiness readiness,
        WorldPacketHandlerContext context, WorldSession expected, out StaminaPublication? notification)
    {
        notification = null;
        var frame = readiness.Frame;
        RefuseBoundSpellReentry(context.Connection);
        if (!ReferenceEquals(framePersistenceOwner, frame) || !frame.IsStaminaReadinessCurrent(readiness) ||
            !ReferenceEquals(readiness.Connection, context.Connection) ||
            !frame.TryPrepareWindWalkerContext(this, context, expected, out var ownedContext)) return false;
        context = ownedContext;
        WorldSession? written = null;
        LegacyOrdinarySpellBinding? issuedBinding = null;
        lock (readiness.LifecycleGate)
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || holder.SpellBinding is not { } binding ||
                    !ReferenceEquals(binding, readiness.Binding) || !BindingIsCurrentPlain(binding, holder, expected) ||
                    !frame.IsStaminaReadinessCurrent(readiness) || !frame.IsWindWalkerContext(this, context, expected)) return false;
                using var actors = new SpellActorScope(binding);
                var vector = binding.Player.BitVector;
                if (vector is null) return false;
                lock (vector.SyncRoot)
                {
                    PreparedStamina? prepared;
                    try { prepared = PreparedStamina.TryPrepare(this, context, holder, expected, readiness); }
                    catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or FormatException or OverflowException or ArgumentException)
                    { return false; } // Preparation only: no resource or stamp was published.
                    if (prepared is null) return false;
                    using (prepared)
                    {
                        prepared.Native.ArmFinalProof();
                        if (!prepared.Native.ConsumeBeforeWriterPlain() || !prepared.IsCurrentPlain() ||
                            !prepared.Native.IsCurrentPlain() || !prepared.IsCurrentPlain()) return false;
                        // No callback/clock/serializer/comparer remains after
                        // this final closed proof. This is one transient step.
                        prepared.Native.PublishNative();
                        prepared.Native.PublishHolder();
                        prepared.Step.PublishPrepared();
                        try { prepared.Output.ConfirmCommitted(); }
                        catch (Exception cause)
                        {
                            var failure = new WorldSessionPublicationException(this, context.Connection, binding.NativeIdentity,
                                binding, prepared.Native.FinalSession, WorldSessionPublicationStage.ProcContinuation, cause);
                            BlockPublishedRecovery(failure);
                            try { FencePublishedOwnerUnderGate(failure); }
                            catch (Exception secondary) { failure.RecordFencingFailure(secondary); }
                            throw failure;
                        }
                        written = prepared.Native.FinalSession; issuedBinding = binding;
                        notification = prepared.Output;
                        prepared.Dispose();
                        PersistFrameOwnedSessionChange(context.Connection, expected, written,
                            binding.NativeIdentity, binding);
                    }
                }
            }
        }
        NotifyPublishedSessionChange(context.Connection, expected, written!, issuedBinding!.NativeIdentity, issuedBinding);
        return true;
    }
}
