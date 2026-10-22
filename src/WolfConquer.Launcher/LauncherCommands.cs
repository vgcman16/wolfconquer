using System.Windows.Input;

namespace WolfConquer.Launcher;

internal sealed class LauncherCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) { if (CanExecute(parameter)) execute(); }
    public event EventHandler? CanExecuteChanged;
    internal void Refresh() => LauncherNotifications.Raise(CanExecuteChanged, this);
}

internal sealed class LauncherAsyncCommand(Func<Task> execute, Func<bool> canExecute,
    Action<Exception> reportFailure) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute();
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try { await execute(); }
        catch (Exception error)
        {
            try { reportFailure(error); }
            catch (Exception observerError) { LauncherNotifications.Observe(observerError); }
        } // ICommand's event boundary never leaks async-void errors.
    }
    public event EventHandler? CanExecuteChanged;
    internal void Refresh() => LauncherNotifications.Raise(CanExecuteChanged, this);
}

internal static class LauncherNotifications
{
    internal static void Observe(Exception error)
    {
        try { System.Diagnostics.Trace.WriteLine("Launcher UI observer: " + error.GetType().Name); }
        catch { } // A diagnostics listener must not terminate a workflow or a posted UI callback.
    }
    internal static void Raise(EventHandler? handlers, object sender)
    {
        if (handlers is null) return;
        foreach (EventHandler handler in handlers.GetInvocationList())
            try { handler(sender, EventArgs.Empty); } catch (Exception error) { Observe(error); }
    }
}
