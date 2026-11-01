namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    internal void FenceAdmissionContinuationQueryFailure(WorldPacketHandlers.PreparedAdmissionContinuationQuery query, Exception primary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        if (query.TryGetFailureOwner(out var recoverableContext, out var recoverableBinding) &&
            TryRetireRecoverableContinuation(recoverableContext, recoverableBinding, primary))
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        var errors = new List<Exception> { primary };
        try
        {
            if (query.TryGetFailureOwner(out var context, out var binding) && ReferenceEquals(binding.Store, this) &&
                framePersistenceOwner is { } frame && !IsCurrentThreadInsideAnyConnectionGate &&
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot))
            {
                lock (frame.RequireActionLifecycleGate(this))
                {
                    using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, context.Session);
                    if (frame.IsOrdinaryContinuationLoginContext(binding.Connection, context) &&
                        _connectionGates.TryGetValue(binding.Connection, out var gate))
                    {
                        lock (gate)
                        {
                            using var native = EnterNativeConnectionGate();
                            using var actors = new SpellActorScope(binding);
                            if (query.TryGetFailureOwner(out var retainedContext, out var retainedBinding) &&
                                ReferenceEquals(context, retainedContext) && ReferenceEquals(binding, retainedBinding) &&
                                frame.IsOrdinaryContinuationLoginContext(binding.Connection, context) &&
                                !binding.IsRetired && ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) &&
                                ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) &&
                                ReferenceEquals(binding.Client.Player, binding.Player) && ReferenceEquals(binding.Player.Owner, binding.Client) &&
                                _sessions.TryGetValue(binding.Connection, out var holder) && ReferenceEquals(holder.SpellBinding, binding) &&
                                ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity))
                            {
                                // Same admitted native lifetime, possibly after publication
                                // whose observer threw before the caller could adopt it.
                                try { binding.Recovery.Block("experienceAdmissionQueryRecoveryRequired:publicationUnproven"); }
                                catch (Exception error) { errors.Add(error); }
                                try { holder.Clear(); }
                                catch (Exception error) { errors.Add(error); }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        throw new AggregateException("Pending admission query and exact-owner cleanup failed.", errors);
    }
}
