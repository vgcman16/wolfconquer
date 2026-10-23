using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRuntimePublicationGateTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static readonly ConcurrentBag<GateFixture> RetainedFixtures = new();

    private static async Task RunAsync(Func<GateFixture, Task> body)
    {
        var fixture = new GateFixture();
        var errors = new List<Exception>();
        try { await body(fixture); }
        catch (Exception error) { AddDistinct(errors, error); }
        await fixture.CleanupAsync(errors);
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private static void AddDistinct(List<Exception> errors, Exception error)
    {
        if (!errors.Any(existing => ReferenceEquals(existing, error))) errors.Add(error);
    }

    private static object Invoke(MethodInfo method, object target, object?[]? arguments)
    {
        try { return method.Invoke(target, arguments) ?? throw new InvalidOperationException("Missing reflection result."); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private sealed class GateFixture
    {
        private static readonly MethodInfo Enter = typeof(WorldServerFrameHandler).GetMethod(
            "EnterLegacyRuntimePublicationAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The actual publication gate is missing.");
        private readonly WorldServerFrameHandler _handler = new(
            snapshotLoader: null, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, log: TextWriter.Null);
        private readonly List<GateWait> _waits = [];

        public ConquerTcpConnection First { get; } = CreateConnection();
        public ConquerTcpConnection Second { get; } = CreateConnection();

        private static ConquerTcpConnection CreateConnection() => new(
            new IPEndPoint(IPAddress.Loopback, 5816), new IPEndPoint(IPAddress.Loopback, 49152));

        public GateWait Start(ConquerTcpConnection connection, bool preCanceled = false)
        {
            var wait = new GateWait();
            _waits.Add(wait);
            if (preCanceled) wait.Budget.Cancel();
            var actualValueTask = Invoke(Enter, _handler, [connection, wait.Budget.Token]);
            var asTask = actualValueTask.GetType().GetMethod("AsTask", Type.EmptyTypes)
                ?? throw new InvalidOperationException("The actual ValueTask has no AsTask method.");
            wait.Actual = (Task)Invoke(asTask, actualValueTask, null);
            return wait;
        }

        public async Task CleanupAsync(List<Exception> errors)
        {
            foreach (var wait in _waits)
            {
                try { wait.Budget.Cancel(); }
                catch (Exception error) { AddDistinct(errors, error); }
            }

            foreach (var wait in _waits) await wait.ObserveTerminalAsync(errors);
            if (_waits.Any(wait => !wait.TerminalObserved))
            {
                RetainedFixtures.Add(this);
                AddDistinct(errors, new InvalidOperationException("Gate task termination is unproven; complete fixture retained."));
                return;
            }

            foreach (var wait in _waits)
            {
                try { wait.Release(); }
                catch (Exception error) { AddDistinct(errors, error); }
                try { wait.Budget.Dispose(); }
                catch (Exception error) { AddDistinct(errors, error); }
            }
        }
    }

    private sealed class GateWait
    {
        public CancellationTokenSource Budget { get; } = new(Deadline);
        public Task? Actual { get; set; }
        public bool TerminalObserved { get; private set; }
        private IDisposable? _lease;
        private bool _released;

        public async Task<IDisposable> GetLeaseAsync()
        {
            var actual = Actual ?? throw new InvalidOperationException("No actual gate task was captured.");
            await actual.WaitAsync(Deadline);
            var lease = CaptureLease(actual);
            TerminalObserved = true;
            return lease;
        }

        private IDisposable CaptureLease(Task actual)
        {
            if (_lease is not null) return _lease;
            var getter = actual.GetType().GetProperty("Result")?.GetMethod
                ?? throw new InvalidOperationException("The actual gate task has no lease result.");
            _lease = (IDisposable)Invoke(getter, actual, null);
            return _lease;
        }

        public async Task AssertCanceledAsync()
        {
            var error = await Record.ExceptionAsync(async () =>
            {
                await GetLeaseAsync();
                Release(); // A broken acquisition must not leave its accidental grant held.
            });
            var canceled = Assert.IsAssignableFrom<OperationCanceledException>(error);
            Assert.Equal(Budget.Token, canceled.CancellationToken);
            Assert.True(Actual!.IsCanceled);
            TerminalObserved = true;
        }

        public async Task AssertPendingAsync()
        {
            var actual = Actual ?? throw new InvalidOperationException("No actual gate task was captured.");
            if (actual.IsCompletedSuccessfully)
            {
                await GetLeaseAsync();
                Release(); // Dispose an accidental grant before reporting the exclusion failure.
            }
            Assert.False(actual.IsCompleted);
        }

        public void Release()
        {
            if (_lease is null || _released) return;
            _released = true;
            _lease.Dispose();
        }

        public async Task ObserveTerminalAsync(List<Exception> errors)
        {
            if (TerminalObserved) return;
            var actual = Actual;
            if (actual is null)
            {
                // Reflection failed before a task handle could be captured: retain the whole owner.
                AddDistinct(errors, new InvalidOperationException("An acquisition has no actual task handle; fixture retained."));
                return;
            }

            try { await actual.WaitAsync(Deadline); }
            catch (OperationCanceledException) when (actual.IsCanceled && Budget.IsCancellationRequested) { }
            catch (Exception error) { AddDistinct(errors, error); }

            if (!actual.IsCompleted) return;
            // Re-await this SAME task if completion raced an observer timeout. Preserve both errors.
            try
            {
                await actual;
                CaptureLease(actual);
            }
            catch (OperationCanceledException) when (actual.IsCanceled && Budget.IsCancellationRequested) { }
            catch (Exception error) { AddDistinct(errors, error); }
            TerminalObserved = true;
        }
    }
}
