using System.Collections.Concurrent;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace WolfConquer.Launcher.Tests;

public sealed partial class LauncherViewModelTests
{
    // Owned STA presentation dispatcher only: no Application, Window, update execution,
    // processes or native game components. Settings-display cases may read owned local JSON fixtures.
    private static async Task OnDispatcher(Func<Task> test)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupFailures = new ConcurrentQueue<Exception>();
        Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            try
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try { await test(); finished.TrySetResult(); }
                    catch (Exception error) { finished.TrySetException(error); }
                    finally { RequestShutdown(); }
                }));
                Dispatcher.Run();
            }
            catch (Exception error)
            {
                if (!finished.TrySetException(error)) cleanupFailures.Enqueue(error);
            }
        }) { IsBackground = true, Name = "WolfConquer owned presentation test" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var failures = new List<Exception>();
        try { await finished.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (Exception error) { failures.Add(error); }

        RequestShutdown();
        try
        {
            if (!thread.Join(TimeSpan.FromSeconds(1)))
                throw new TimeoutException("Owned presentation dispatcher did not shut down.");
        }
        catch (Exception error) { cleanupFailures.Enqueue(error); }

        // Include a body failure that completed during cleanup after the bounded wait expired.
        if (finished.Task.IsFaulted) failures.AddRange(finished.Task.Exception!.InnerExceptions);
        failures.AddRange(cleanupFailures);
        var distinctFailures = failures.Distinct<Exception>(ReferenceEqualityComparer.Instance).ToArray();
        if (distinctFailures.Length > 1)
            throw new AggregateException("Owned presentation test and/or dispatcher cleanup failed; preserve all diagnostics.", distinctFailures);
        if (distinctFailures.Length == 1)
            ExceptionDispatchInfo.Capture(distinctFailures[0]).Throw();

        void RequestShutdown()
        {
            try
            {
                if (dispatcher is not null && !dispatcher.HasShutdownStarted)
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }
            catch (InvalidOperationException) when (dispatcher?.HasShutdownStarted == true) { }
            catch (Exception error) { cleanupFailures.Enqueue(error); }
        }
    }

    private static async Task Drain() => await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

    private static async Task DrainUntil(Func<bool> complete)
    {
        for (var attempt = 0; attempt < 32 && !complete(); attempt++) await Drain();
        Assert.True(complete(), "Presentation operation did not reach its expected state.");
    }

    private static LauncherViewModel ConfiguredClient()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-ui-state-" + Guid.NewGuid().ToString("N"));
        return new LauncherViewModel
        {
            ConfigurationPath = Path.Combine(root, "bootstrap", "configuration.json"),
            ClientRoot = Path.Combine(root, "client")
        };
    }
}
