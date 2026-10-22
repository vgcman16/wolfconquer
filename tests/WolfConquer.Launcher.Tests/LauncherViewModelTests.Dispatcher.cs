using System.IO;
using System.Windows.Threading;

namespace WolfConquer.Launcher.Tests;

public sealed partial class LauncherViewModelTests
{
    // Owned STA presentation dispatcher only: no Application, Window, update execution,
    // processes or native game components. Settings-display cases may read owned local JSON fixtures.
    private static async Task OnDispatcher(Func<Task> test)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                }));
                Dispatcher.Run();
            }
            catch (Exception error) { finished.TrySetException(error); }
        }) { IsBackground = true, Name = "WolfConquer owned presentation test" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await finished.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally
        {
            try
            {
                if (dispatcher is not null && !dispatcher.HasShutdownStarted)
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }
            catch (InvalidOperationException) when (dispatcher?.HasShutdownStarted == true) { }
            if (!thread.Join(TimeSpan.FromSeconds(1)))
                throw new TimeoutException("Owned presentation dispatcher did not shut down.");
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
