using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly SemaphoreSlim basicPhysicalSignal = new(0, 1);
    private int basicPhysicalWorkerStarted, basicPhysicalDrainActive;

    // Called only after a real completed publication armed a native repeat.
    // One tracked Frame worker serves all entries; it is not a per-player1ms
    // loop, nor an accepting queue. Every due entry re-enters actual admission.
    private void NotifyBasicPhysicalRepeatArmed()
    {
        if (!_runtimeUpdateLoop.Enabled || _ownedAdmissionRuntime is not { } runtime) return;
        lock (runtime.LifetimeGate)
        {
            if (runtime.IsStopping) return;
            if (Interlocked.CompareExchange(ref basicPhysicalWorkerStarted, 1, 0) == 0)
            {
                var lifetime = LinkOwnedAdmissionWorker(CancellationToken.None) ??
                    throw new InvalidOperationException("basicPhysicalWorkerLifetimeUnavailable");
                TrackOwnedAdmissionWorker(SendBasicPhysicalRepeatsAsync(lifetime.Token), lifetime);
            }
            if (basicPhysicalSignal.CurrentCount == 0) basicPhysicalSignal.Release();
        }
    }

    private async Task SendBasicPhysicalRepeatsAsync(CancellationToken cancellationToken)
    {
        using var worker = _ownedAdmissionRuntime!.EnterWorker();
        try
        {
            while (true)
            {
                await basicPhysicalSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                do
                {
                    await SendDueBasicPhysicalAttacksAsync(cancellationToken).ConfigureAwait(false);
                    if (!HasLiveBasicPhysicalRepeats()) break;
                    // The source's300ms minimum is a permission gate, also
                    // rechecked with actual current speed before every attack.
                    await Task.Delay(TimeSpan.FromMilliseconds(300),
                        _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System, cancellationToken).ConfigureAwait(false);
                } while (!cancellationToken.IsCancellationRequested);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (_ownedAdmissionRuntime?.IsStopping == true) StopBasicPhysicalGenerations();
        }
        catch (Exception failure)
        {
            try { _tradeRecovery.Block("basicPhysicalWorkerFailed:" + failure.GetType().Name); }
            catch (Exception fence) { throw new AggregateException("Basic physical worker and recovery fencing failed.", failure, fence); }
            finally
            {
                StopBasicPhysicalGenerations();
            }
            throw;
        }
    }

    private void StopBasicPhysicalGenerations()
    {
        // Worker stop/failure starts outside Store connection gates. Serialize
        // its slot changes with hit/arm publication. Holder retirement keeps
        // its existing connection-owned path and never takes this higher gate.
        lock (_tradeStates.SyncRoot)
            foreach (var active in _sessions.Snapshot())
                if (active.Session.NativeGameClient?.OrdinarySpellBinding is { } binding)
                    RetireBasicPhysicalGeneration(binding);
    }

    private bool HasLiveBasicPhysicalRepeats()
    {
        lock (_tradeStates.SyncRoot)
            return !_tradeRecovery.IsBlocked && _ownedAdmissionRuntime is { IsStopping: false } &&
                _sessions.Snapshot().Any(active => basicPhysicalSlots.TryGetValue(active.Connection, out var slot) &&
                    slot.Repeat is not null && IsStaminaReadinessCurrent(slot.Readiness));
    }

    // Real test/runtime entry: no supplied UID, clock, damage, descriptor,
    // registration, roll or successful result. Disabled scheduling retains it.
    internal async ValueTask SendDueBasicPhysicalAttacksAsync(CancellationToken cancellationToken)
    {
        if (_ownedAdmissionRuntime is not { IsStopping: false } runtime ||
            Interlocked.CompareExchange(ref basicPhysicalDrainActive, 1, 0) != 0) return;
        try
        {
            using var worker = runtime.EnterWorker();
            using var operation = await _worldOperations.EnterSharedAsync(cancellationToken).ConfigureAwait(false);
            if (_tradeRecovery.IsBlocked || !_tradeRecovery.TryRequireLiveJournalClear(out _))
            { StopBasicPhysicalGenerations(); return; }
            foreach (var active in _sessions.Snapshot())
            {
                cancellationToken.ThrowIfCancellationRequested();
                BasicPhysicalRepeatTicket? ticket = null;
                try
                {
                    if (!_sessions.TryCaptureDueBasicPhysical(this, active.Connection, out var request, out var packet, out ticket) ||
                        request is null || packet is null) continue;
                    var bytes = packet.Value.ToFrameBytes();
                    if (!PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _))
                        throw new InvalidDataException("basicPhysicalNativeDescriptorNotRepresentable");
                    await HandleFrameCoreAsync(active.Connection, frame, cancellationToken, operation, request).ConfigureAwait(false);
                }
                finally
                {
                    // An exact old entry may stop after refusal/transport loss;
                    // it can never cancel a newly armed generation or holder.
                    if (ticket is not null) _sessions.StopBasicPhysicalRepeat(this, active.Connection, ticket);
                }
            }
        }
        finally { Volatile.Write(ref basicPhysicalDrainActive, 0); }
    }
}
