using System.Diagnostics;
using System.Reflection;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public static partial class LegacyNativeSmartThreadQueueChildScenarios
{
    private static void ThreadConstructor()
    {
        Action work = () => throw new InvalidOperationException("Constructor must not invoke Event.");
        Action<int> sleep = _ => throw new InvalidOperationException("Constructor must not invoke EventSleep.");
        using var owner = new ThreadProbeScope(123, work, sleep);
        var value = owner.Item;
        Require(ThreadField<int>(value, "Max_ProcessInterval") == 123, "Interval was not stored.");
        Require(ThreadField<string>(value, "Name") == owner.MutexName, "Mutex name was not stored.");
        Require(ReferenceEquals(work, ThreadField<Action>(value, "Event")) && ReferenceEquals(sleep, value.EventSleep),
            "Constructor did not retain the actual delegate references.");
        Require(ThreadField<Mutex>(value, "ThreadMutex").SafeWaitHandle.IsInvalid == false, "Actual named Mutex was not constructed.");
        Require(!ThreadField<bool>(value, "Alive"), "Constructor set Alive.");
        var thread = ThreadField<Thread>(value, "_thread");
        Require(thread.ThreadState == System.Threading.ThreadState.Unstarted && !thread.IsBackground,
            "Constructor started a worker or replaced the native foreground Thread default.");
        Action<int> replacement = _ => { };
        value.EventSleep = replacement;
        Require(ReferenceEquals(replacement, value.EventSleep), "EventSleep is not the replaceable native field.");

        AllowSourceField.SetValue(null, false);
        using var denied = new ThreadProbeScope(321, work, sleep);
        Require(ThreadField<int>(denied.Item, "Max_ProcessInterval") == 0 &&
            ThreadField<string?>(denied.Item, "Name") == null &&
            ThreadField<Mutex?>(denied.Item, "ThreadMutex") == null &&
            ThreadField<Action?>(denied.Item, "Event") == null && denied.Item.EventSleep == null &&
            ThreadField<Thread?>(denied.Item, "_thread") == null && !ThreadField<bool>(denied.Item, "Alive"),
            "False-gate construction allocated or assigned native body fields.");
    }

    private static void ProcessGate()
    {
        var count = 0;
        using var owner = new ThreadProbeScope(100, () => count++, _ => count++);
        AllowSourceField.SetValue(null, false);
        Require(!owner.Item.Process() && count == 0, "OnProcess did not return false before Event and EventSleep.");
        Require(!ThreadField<bool>(owner.Item, "Alive"), "Synchronous OnProcess changed Alive.");
    }

    private static void ProcessEventThenSleep()
    {
        var calls = new List<string>();
        var sleepValue = 0;
        var callThread = Environment.CurrentManagedThreadId;
        using var owner = new ThreadProbeScope(1000,
            () => { Require(Environment.CurrentManagedThreadId == callThread, "Event moved threads."); calls.Add("event"); },
            value => { Require(Environment.CurrentManagedThreadId == callThread, "EventSleep moved threads."); sleepValue = value; calls.Add("sleep"); });
        Require(owner.Item.Process(), "Allowed OnProcess did not return true.");
        Require(calls.SequenceEqual(new[] { "event", "sleep" }) && sleepValue > 0 && sleepValue <= 1000,
            "Event/Sleep order or native remaining interval differs.");
        Require(!ThreadField<bool>(owner.Item, "Alive"), "OnProcess began a lifecycle worker.");
    }

    private static void ProcessEventFault()
    {
        var calls = new List<string>();
        using var output = new ConsoleOutputScope();
        using var owner = new ThreadProbeScope(1000,
            () => { calls.Add("event"); throw new InvalidOperationException("owned event fault"); },
            _ => calls.Add("sleep"));
        Require(owner.Item.Process(), "Event exception escaped the native inner catch.");
        Require(calls.SequenceEqual(new[] { "event", "sleep" }), "Event fault skipped the native finally/Sleep callback.");
        Require(output.Text.Contains("System.InvalidOperationException: owned event fault", StringComparison.Ordinal),
            "Event fault did not go to the native Console.WriteLine path.");
    }

    private static void ProcessSleepFault()
    {
        var calls = new List<string>();
        using var output = new ConsoleOutputScope();
        using var owner = new ThreadProbeScope(1000,
            () => calls.Add("event"),
            _ => { calls.Add("sleep"); throw new InvalidOperationException("owned sleep fault"); });
        Require(owner.Item.Process(), "EventSleep exception escaped the native outer catch.");
        Require(calls.SequenceEqual(new[] { "event", "sleep" }), "EventSleep callback order differs.");
        Require(output.Text.Contains("System.InvalidOperationException: owned sleep fault", StringComparison.Ordinal),
            "EventSleep fault did not go to the native Console.WriteLine path.");
    }

    private static void ProcessWithoutEvent()
    {
        var count = 0;
        using var owner = new ThreadProbeScope(1000, null, value =>
        {
            Require(value > 0 && value <= 1000, "Null Event remaining interval differs.");
            count++;
        });
        Require(owner.Item.Process() && count == 1, "Null Event skipped native interval/Sleep behavior.");
    }

    private static void ProcessNoRemainingInterval()
    {
        foreach (var interval in new[] { 0, -1 })
        {
            var events = 0;
            var sleeps = 0;
            using var owner = new ThreadProbeScope(interval, () => events++, _ => sleeps++);
            Require(owner.Item.Process() && events == 1 && sleeps == 0,
                "Nonpositive remaining interval skipped Event or invoked Sleep.");
        }
    }

    private static void ProcessRealSleep()
    {
        using var owner = new ThreadProbeScope(60, null, null);
        var watch = Stopwatch.StartNew();
        Require(owner.Item.Process(), "Native default Thread.Sleep path did not return true.");
        Require(watch.ElapsedMilliseconds >= 35, "No-delegate OnProcess omitted the real remaining-interval sleep.");
    }

    private static void CloseUnopened()
    {
        using var owner = new ThreadProbeScope(1, null, null);
        var thread = ThreadField<Thread>(owner.Item, "_thread");
        owner.Item.Close();
        owner.Item.Close();
        Require(!ThreadField<bool>(owner.Item, "Alive") &&
            ReferenceEquals(thread, ThreadField<Thread>(owner.Item, "_thread")) &&
            thread.ThreadState == System.Threading.ThreadState.Unstarted,
            "Close on the native unopened state changed the worker or attempted Abort.");
        AllowSourceField.SetValue(null, false);
        using var denied = new ThreadProbeScope(1, null, null);
        denied.Item.Close();
        Require(ThreadField<Thread?>(denied.Item, "_thread") == null && !ThreadField<bool>(denied.Item, "Alive"),
            "Close dereferenced an unopened denied-constructor worker.");
    }

    private static T ThreadField<T>(LegacyNativeThreadItem item, string name) =>
        (T)typeof(LegacyNativeThreadItem).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(item)!;

    private sealed class ThreadProbe : LegacyNativeThreadItem
    {
        internal ThreadProbe(int interval, string mutexName, Action? work, Action<int>? sleep)
            : base(interval, mutexName, work!, sleep!) { }
        internal bool Process() => OnProcess();
    }

    private sealed class ThreadProbeScope : IDisposable
    {
        internal string MutexName { get; } = "wolfconquer-thread-probe-" + Guid.NewGuid().ToString("N");
        internal ThreadProbe Item { get; }
        internal ThreadProbeScope(int interval, Action? work, Action<int>? sleep) =>
            Item = new ThreadProbe(interval, MutexName, work, sleep);

        public void Dispose()
        {
            // The production lifecycle is never started, so no Thread.Abort or
            // worker termination is needed. Dispose only this owned BCL Mutex.
            var thread = ThreadField<Thread?>(Item, "_thread");
            Require(thread == null || thread.ThreadState == System.Threading.ThreadState.Unstarted,
                "A scenario unexpectedly started a worker.");
            ThreadField<Mutex?>(Item, "ThreadMutex")?.Dispose();
        }
    }

    private sealed class ConsoleOutputScope : IDisposable
    {
        private readonly TextWriter prior = Console.Out;
        private readonly StringWriter value = new();
        internal string Text => value.ToString();
        internal ConsoleOutputScope() => Console.SetOut(value);
        public void Dispose()
        {
            Console.SetOut(prior);
            value.Dispose();
        }
    }
}
