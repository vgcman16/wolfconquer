using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSubClassTests
{
    [Theory]
    [InlineData(0, 4)]
    [InlineData(65533, 65535)]
    public async Task CooperatingStudyAddsSerializeActualPointAndFrameBatches(int before, int expected)
    {
        await RunOwnedStudyConcurrency(4, async owned =>
        {
            var scope = owned.Scope;
            scope.Runtime.StudyPoints = (ushort)before;
            var start = owned.StartBarrier ?? throw new InvalidOperationException("Study rendezvous was not created.");
            Task[] workers = owned.Packets.Select(packet => owned.StartWorker(() =>
            {
                if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Study workers did not rendezvous.");
                scope.Runtime.AddStudyPoints(scope.Client, 1, packet);
            })).ToArray();
            await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal((ushort)expected, scope.Runtime.StudyPoints);
            var frames = scope.Queue.ToArray();
            Assert.Equal(12, frames.Length);
            for (int index = 0; index < 4; index++)
            {
                Assert.Equal(StudyMessage("1"), frames[index * 3]);
                Assert.Equal(Effect(), frames[index * 3 + 1]);
                Assert.Equal(ListFrame(8, (ushort)Math.Min(65535, before + index + 1)), frames[index * 3 + 2]);
            }
        });
    }

    [Fact]
    public async Task RebindingBeforeActualClientGateAdmissionRejectsOldRuntimeWithoutPartialOutput()
    {
        await RunOwnedStudyConcurrency(0, async owned =>
        {
            var scope = owned.Scope;
            var before = StudyPhysicalBytes(scope.Packet);
            var held = owned.HoldGate(scope.Client.SpellAdmissionSyncRoot);
            await held.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            var attempted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task work = owned.StartWorker(() =>
            {
                attempted.TrySetResult(true);
                Assert.Throws<InvalidOperationException>(() =>
                    scope.Runtime.AddStudyPoints(scope.Client, 17, scope.Packet));
            });
            try
            {
                await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                scope.Client.Player.SubClass = new LegacyNativeSubClassRuntime();
            }
            finally { held.Release(); }
            await Task.WhenAll(work, held.Worker).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal((ushort)0, scope.Runtime.StudyPoints);
            Assert.Equal(before, StudyPhysicalBytes(scope.Packet));
            scope.Unwritten();
            Assert.Equal(2, LegacyNativePacketRecycle.Count);
        });
    }

    [Fact]
    public async Task ObservableRebindAfterPointCommitIsClassifiedCommittedWithoutRollbackOrDeliveryClaim()
    {
        await RunOwnedStudyConcurrency(0, async owned =>
        {
            var scope = owned.Scope;
            scope.Runtime.StudyPoints = 1;
            var original = StudyPhysicalBytes(scope.Packet);
            object sendGate = typeof(LegacyNativeSecuritySocket)
                .GetField("locckss", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scope.Client.Socket)!;
            var held = owned.HoldGate(sendGate);
            await held.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            LegacyNativeStudyNotificationException? observed = null;
            var started = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task work = owned.StartWorker(() =>
            {
                started.TrySetResult(Thread.CurrentThread);
                observed = Assert.Throws<LegacyNativeStudyNotificationException>(() =>
                    scope.Runtime.AddStudyPoints(scope.Client, 17, scope.Packet));
            });
            try
            {
                Thread workerThread = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await ObserveActualStudySendBoundary(scope, work, workerThread);
                Assert.False(work.IsCompleted);
                // A real noncooperating graph writer, not a sender callback.
                scope.Client.Player.SubClass = new LegacyNativeSubClassRuntime();
            }
            finally { held.Release(); }
            await Task.WhenAll(work, held.Worker).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.NotNull(observed);
            Assert.True(observed.ResourceCommitted);
            Assert.Equal((ushort)1, observed.PointsBefore);
            Assert.Equal((ushort)18, observed.PointsAfter);
            Assert.Equal("Effect", observed.NotificationPhase);
            Assert.IsType<InvalidOperationException>(observed.InnerException);
            Assert.Equal((ushort)18, scope.Runtime.StudyPoints);
            scope.Frames(StudyMessage("17"));
            scope.ChatWasDisposed(StudyMessage("17"));
            Assert.Equal(original, StudyPhysicalBytes(scope.Packet));
            Assert.Equal(321, scope.Packet.Size);
            Assert.Equal(237, scope.Packet.Position);
        });
    }

    [Fact]
    public Task NativeSilentTransportDropDoesNotPretendToRejectCommittedStudy() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(bindStudy: true);
        scope.Client.Socket!.Alive = false;
        scope.Runtime.AddStudyPoints(scope.Client, 17, scope.Packet);
        Assert.Equal((ushort)17, scope.Runtime.StudyPoints);
        Assert.Empty(scope.Queue);
        scope.CallerIs(ListFrame(8, 17));
        scope.ChatWasDisposed(StudyMessage("17"));
    });

    private static async Task ObserveActualStudySendBoundary(Scope scope, Task work, Thread workerThread)
    {
        var points = typeof(LegacyNativeSubClassRuntime).GetField("studyPoints", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(10))
        {
            Thread.MemoryBarrier();
            // With the private socket gate held, the actual postcommit worker's
            // only blocking boundary before Talk publication is that real monitor.
            if ((ushort)points.GetValue(scope.Runtime)! == 18 &&
                (workerThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0) return;
            if (work.IsCompleted)
            {
                await work;
                throw new InvalidOperationException("Study worker returned before the observed commit boundary.");
            }
            await Task.Delay(5);
        }
        throw new TimeoutException("Real study operation did not reach its sender boundary.");
    }

    private static async Task RunOwnedStudyConcurrency(int packetCount, Func<StudyConcurrencyInputs, Task> body)
    {
        StudyConcurrencyInputs? owned = null;
        // Actual Scope and Packet constructors retain the required fresh native stack.
        await NativePacketTestThread.Run(() => owned = new StudyConcurrencyInputs(packetCount));
        if (owned is null) throw new InvalidOperationException("Native study inputs were not created.");
        Exception? primary = null;
        try { await body(owned); }
        catch (Exception error) { primary = error; }
        List<Exception> cleanup = await owned.CleanupAsync();
        if (primary is not null)
        {
            cleanup.RemoveAll(error => ReferenceEquals(error, primary));
            cleanup.Insert(0, primary);
        }
        ThrowStudyFailures(cleanup);
    }

    private static void ThrowStudyFailures(List<Exception> errors)
    {
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Study test and owned cleanup failed.", errors);
    }

    private static void TryStudyCleanup(List<Exception> errors, Action cleanup)
    {
        try { cleanup(); }
        catch (Exception error) { errors.Add(error); }
    }

    private sealed class StudyConcurrencyInputs
    {
        internal Scope Scope { get; }
        internal LegacyNativePacket[] Packets { get; }
        internal Barrier? StartBarrier { get; }
        private readonly List<Task> workers = new(4);
        private readonly List<StudyGateOwner> gates = new(1);

        internal StudyConcurrencyInputs(int packetCount)
        {
            var packets = new List<LegacyNativePacket>(packetCount);
            Scope = new Scope(bindStudy: true);
            try
            {
                for (int index = 0; index < packetCount; index++)
                {
                    var packet = new LegacyNativePacket(128);
                    packets.Add(packet);
                    FillStudyPacket(packet);
                }
                Packets = packets.ToArray();
                if (packetCount > 0) StartBarrier = new Barrier(packetCount);
            }
            catch (Exception primary)
            {
                var errors = new List<Exception> { primary };
                if (StartBarrier is not null) TryStudyCleanup(errors, StartBarrier.Dispose);
                foreach (var packet in packets) TryStudyCleanup(errors, packet.Dispose);
                TryStudyCleanup(errors, Scope.Dispose);
                ThrowStudyFailures(errors);
                throw;
            }
        }

        internal Task StartWorker(Action action)
        {
            Task work = NativePacketTestThread.Run(action);
            workers.Add(work);
            return work;
        }

        internal StudyGateOwner HoldGate(object actualGate)
        {
            var held = new StudyGateOwner(actualGate);
            gates.Add(held);
            held.Worker = StartWorker(held.HoldOnWorker);
            return held;
        }

        internal async Task<List<Exception>> CleanupAsync()
        {
            var errors = new List<Exception>();
            foreach (var gate in gates) TryStudyCleanup(errors, gate.Release);
            try { await Task.WhenAll(workers); }
            catch (Exception completion)
            {
                bool recorded = false;
                foreach (var work in workers)
                {
                    if (work.Exception is not { } failure) continue;
                    errors.AddRange(failure.InnerExceptions);
                    recorded = true;
                }
                if (!recorded) errors.Add(completion);
            }
            // Every real worker is terminal before any unmanaged storage is freed.
            TryStudyCleanup(errors, () => Scope.Client.Player.SubClass = Scope.Runtime);
            foreach (var gate in gates) TryStudyCleanup(errors, gate.Dispose);
            if (StartBarrier is not null) TryStudyCleanup(errors, StartBarrier.Dispose);
            foreach (var packet in Packets) TryStudyCleanup(errors, packet.Dispose);
            TryStudyCleanup(errors, Scope.Dispose);
            return errors;
        }
    }

    private sealed class StudyGateOwner : IDisposable
    {
        private readonly object actualGate;
        private readonly ManualResetEventSlim release = new();
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Entered => entered.Task;
        internal Task Worker { get; set; } = null!;

        internal StudyGateOwner(object actualGate) => this.actualGate = actualGate;

        internal void HoldOnWorker()
        {
            // Monitor is acquired and released by this one genuine worker thread.
            lock (actualGate)
            {
                entered.TrySetResult(true);
                if (!release.Wait(TimeSpan.FromSeconds(20)))
                    throw new TimeoutException("Study gate owner was not released.");
            }
        }

        internal void Release() => release.Set();
        public void Dispose() => release.Dispose();
    }
}
