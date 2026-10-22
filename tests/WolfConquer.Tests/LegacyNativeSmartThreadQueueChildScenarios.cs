using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// BCL assertions only: the fresh PowerShell child need not load an xUnit runner.
public static partial class LegacyNativeSmartThreadQueueChildScenarios
{
    private static readonly FieldInfo AllowSourceField = typeof(LegacyNativeConsole)
        .GetField("AllowSourcelOGIN", BindingFlags.NonPublic | BindingFlags.Static)!;

    public static string Execute(string scenario, string guard, string coreHash, string testsHash)
    {
        Require(LegacyNativeConsole.AllowSource(), "Cold native console source gate was not true.");
        var priorGate = (bool)AllowSourceField.GetValue(null)!;
        try
        {
            switch (scenario)
            {
                case "Constructor": Constructor(); break;
                case "EnqueueGateAndNull": EnqueueGateAndNull(); break;
                case "FifoAndLiveEnqueue": FifoAndLiveEnqueue(); break;
                case "WorkIgnoresAdmissionGate": WorkIgnoresAdmissionGate(); break;
                case "CapturedTimestamp": CapturedTimestamp(); break;
                case "CaughtCallbackFault": CaughtCallbackFault(); break;
                case "EmptyFinish": EmptyFinish(); break;
                case "ThreadConstructor": ThreadConstructor(); break;
                case "ProcessGate": ProcessGate(); break;
                case "ProcessEventThenSleep": ProcessEventThenSleep(); break;
                case "ProcessEventFault": ProcessEventFault(); break;
                case "ProcessSleepFault": ProcessSleepFault(); break;
                case "ProcessWithoutEvent": ProcessWithoutEvent(); break;
                case "ProcessNoRemainingInterval": ProcessNoRemainingInterval(); break;
                case "ProcessRealSleep": ProcessRealSleep(); break;
                case "CloseUnopened": CloseUnopened(); break;
                default: throw new InvalidOperationException("Unknown native queue scenario.");
            }
        }
        finally
        {
            AllowSourceField.SetValue(null, priorGate);
        }
        Require(LegacyNativeConsole.AllowSource() == priorGate, "Native source gate restoration failed.");
        return JsonSerializer.Serialize(new
        {
            Guard = guard, Scenario = scenario, CoreSha256 = coreHash, TestsSha256 = testsHash,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            Completed = true, ConsoleGateRestored = true, WorkerStarted = false,
        });
    }

    private static void Constructor()
    {
        var queue = new QueueProbe<object>(int.MinValue);
        Require(queue.Queues != null && queue.Count == 0 && queue.thread == null, "Native base constructor state differs.");
        var old = queue.Queues;
        var item = new object();
        var replacement = new ConcurrentQueue<object>();
        replacement.Enqueue(item);
        queue.Queues = replacement;
        Require(ReferenceEquals(queue.Queues, replacement) && queue.Count == 1 && old.Count == 0,
            "Queue replacement was normalized or copied.");
        queue.Queues = null!;
        Expect<NullReferenceException>(() => _ = queue.Count);
        queue.Queues = replacement;
        Require(queue.Queues.TryPeek(out var actual) && ReferenceEquals(item, actual), "Replacement item alias differs.");
        var independent = new QueueProbe<object>(int.MaxValue);
        Require(!ReferenceEquals(queue.Queues, independent.Queues) && independent.Count == 0,
            "Processors changed construction or owners share queue storage.");
    }

    private static void EnqueueGateAndNull()
    {
        var queue = new QueueProbe<object?>();
        queue.Enqueue(null);
        Require(queue.Count == 1 && queue.Queues.TryPeek(out var first) && first == null, "Allowed null was not enqueued.");
        AllowSourceField.SetValue(null, false);
        queue.Enqueue(new object());
        Require(queue.Count == 1, "False source gate admitted an item.");
        var realQueue = queue.Queues;
        queue.Queues = null!;
        queue.Enqueue(null); // The source gate must precede queue dereference.
        AllowSourceField.SetValue(null, true);
        Expect<NullReferenceException>(() => queue.Enqueue(null));
        queue.Queues = realQueue;
        var second = new object();
        queue.Enqueue(second);
        Require(queue.Queues.TryDequeue(out first) && first == null &&
            queue.Queues.TryDequeue(out var actual) && ReferenceEquals(second, actual) && queue.Count == 0,
            "Native queue lost null or reference identity after reopening admission.");
    }

    private static void FifoAndLiveEnqueue()
    {
        var values = new List<string?>();
        var counts = new List<int>();
        var queue = new QueueProbe<string?>();
        queue.OnItem = (value, _) =>
        {
            values.Add(value);
            counts.Add(queue.Count);
            if (value == "first") queue.Enqueue("live");
        };
        queue.Enqueue("first"); queue.Enqueue(null); queue.Enqueue("last");
        queue.Work();
        Require(values.SequenceEqual(new string?[] { "first", null, "last", "live" }), "Work changed FIFO or snapshotted the original queue.");
        Require(counts.SequenceEqual(new[] { 2, 2, 1, 0 }) && queue.Count == 0,
            "Callback did not observe the already-dequeued real queue.");
        queue.Work();
        Require(values.Count == 4, "Empty Work called OnDequeue.");
    }

    private static void WorkIgnoresAdmissionGate()
    {
        var values = new List<int>();
        var queue = new QueueProbe<int> { OnItem = (value, _) => values.Add(value) };
        queue.Queues.Enqueue(19);
        AllowSourceField.SetValue(null, false);
        queue.Work();
        Require(values.SequenceEqual(new[] { 19 }) && queue.Count == 0,
            "Work incorrectly reused the Enqueue/Start source gate.");
    }

    private static void CapturedTimestamp()
    {
        var times = new List<int>();
        var elapsedAtCallback = new List<long>();
        var clock = Stopwatch.StartNew();
        var queue = new QueueProbe<int>();
        queue.OnItem = (value, time) =>
        {
            times.Add(time);
            elapsedAtCallback.Add(clock.ElapsedMilliseconds);
            if (value == 1) Thread.Sleep(180);
        };
        queue.Enqueue(1); queue.Enqueue(2);
        queue.Work();
        Require(times.Count == 2 && elapsedAtCallback[1] - elapsedAtCallback[0] >= 150,
            "The real callback delay was not observed.");
        Require(Math.Abs((long)unchecked(times[1] - times[0])) <= 50,
            "Work did not preserve its single pre-loop DateTime sample.");
    }

    private static void CaughtCallbackFault()
    {
        using var logs = new OwnedExceptionLogs();
        var values = new List<int>();
        var counts = new List<int>();
        var queue = new QueueProbe<int>();
        queue.OnItem = (value, _) =>
        {
            values.Add(value); counts.Add(queue.Count);
            ThrowOwnedCallbackError();
        };
        queue.Enqueue(31); queue.Enqueue(32);
        queue.Work();
        Require(values.SequenceEqual(new[] { 31 }) && counts.SequenceEqual(new[] { 1 }) && queue.Count == 1,
            "A caught callback fault retried the removed item or continued draining.");
        Require(queue.Queues.TryPeek(out var pending) && pending == 32, "Remaining item changed after callback fault.");
        var files = Directory.GetFiles(logs.Root, "*.txt", SearchOption.AllDirectories);
        Require(files.Length == 1 && File.ReadAllText(files[0]).Contains("owned native queue callback fault", StringComparison.Ordinal),
            "Work did not use the genuine native exception-file path.");
        Require(logs.Counter.Count == unchecked(logs.PriorCounter + 1), "Caught fault did not advance the real exception counter once.");
        queue.OnItem = (value, _) => { values.Add(value); counts.Add(queue.Count); };
        queue.Work();
        Require(values.SequenceEqual(new[] { 31, 32 }) && counts.SequenceEqual(new[] { 1, 0 }) && queue.Count == 0,
            "The next Work call did not resume with the pending item.");
        Require(Directory.GetFiles(logs.Root, "*.txt", SearchOption.AllDirectories).Length == 1,
            "Successful resumed Work emitted another exception file.");
    }

    private static void EmptyFinish()
    {
        var queue = new QueueProbe<int>();
        var elapsed = Stopwatch.StartNew();
        Require(queue.Finish(), "Native Finish did not return true.");
        Require(elapsed.ElapsedMilliseconds >= 950, "Empty Finish omitted its unconditional final one-second sleep.");
        Require(queue.Count == 0 && queue.thread == null, "Finish started a worker or changed queue state.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOwnedCallbackError() => throw new InvalidOperationException("owned native queue callback fault");

    private sealed class QueueProbe<T> : LegacyNativeConcurrentSmartThreadQueue<T>
    {
        internal Action<T, int>? OnItem;
        internal QueueProbe(int processors = 1) : base(processors) { }
        protected override void OnDequeue(T Value, int time) => OnItem?.Invoke(Value, time);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static T Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T error) { return error; }
        throw new InvalidOperationException("Expected " + typeof(T).FullName);
    }

    private sealed class OwnedExceptionLogs : IDisposable
    {
        private readonly FieldInfo startup = typeof(LegacyNativeApplication)
            .GetField("startupPath", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? priorPath;
        internal LegacyNativeCounter Counter { get; }
        internal uint PriorCounter { get; }
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-queue-" + Guid.NewGuid().ToString("N"));

        internal OwnedExceptionLogs()
        {
            priorPath = startup.GetValue(null);
            Counter = (LegacyNativeCounter)typeof(LegacyNativeConsole)
                .GetField("ExceptionsCounter", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            PriorCounter = Counter.Count;
            Directory.CreateDirectory(Root);
            startup.SetValue(null, Root);
        }

        public void Dispose()
        {
            startup.SetValue(null, priorPath);
            Counter.Set(PriorCounter);
            var full = Path.GetFullPath(Root);
            var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), parent) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-queue-[0-9a-f]{32}$"))
                throw new InvalidOperationException("Owned exception-file cleanup containment failed.");
            if (Directory.Exists(full))
            {
                Require((File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0, "Owned root became a reparse point.");
                var pending = new Queue<string>();
                pending.Enqueue(full);
                while (pending.TryDequeue(out var current))
                {
                    foreach (var directory in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                    {
                        Require((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, "Owned child directory became a reparse point.");
                        pending.Enqueue(directory);
                    }
                }
                Directory.Delete(full, recursive: true);
            }
        }
    }
}
