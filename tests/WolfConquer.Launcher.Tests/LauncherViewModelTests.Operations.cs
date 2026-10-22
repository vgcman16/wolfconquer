using System.IO;
using WolfConquer.Launcher;

namespace WolfConquer.Launcher.Tests;

public sealed partial class LauncherViewModelTests
{
    [Fact]
    public Task BusyCommandsCannotReenterOrChangeSelectedFlow() => OnDispatcher(async () =>
    {
        var model = ConfiguredClient();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        model.ExecuteAsync = (_, _, _, _) => { calls++; return release.Task; };
        model.PrimaryCommand.Execute(null);
        try
        {
            Assert.True(model.IsBusy); Assert.False(model.CanEditPaths);
            Assert.False(model.PrimaryCommand.CanExecute(null)); Assert.False(model.VerifyCommand.CanExecute(null));
            model.PrimaryCommand.Execute(null); model.VerifyCommand.Execute(null); model.LaunchCommand.Execute(null);
            model.SelectSourceCommand.Execute(null);
            Assert.Equal(1, calls); Assert.Equal(LauncherFlow.Client, model.SelectedFlow);
        }
        finally { release.TrySetResult(); }
        await DrainUntil(() => !model.IsBusy);
        Assert.Equal(1, calls); Assert.False(model.IsReady); Assert.False(model.LaunchCommand.CanExecute(null));
    });

    [Fact]
    public Task CancelReachesControllerButBusyStateLastsUntilControllerActuallyReturns() => OnDispatcher(async () =>
    {
        var model = ConfiguredClient();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken received = default;
        model.ExecuteAsync = async (_, _, progress, cancellation) =>
        {
            received = cancellation;
            progress.Report(new(IsReady: true));
            await release.Task;
            cancellation.ThrowIfCancellationRequested();
        };
        model.PrimaryCommand.Execute(null);
        try
        {
            await Drain(); Assert.True(model.IsBusy); Assert.True(received.CanBeCanceled);
            model.CancelCommand.Execute(null);
            Assert.True(received.IsCancellationRequested);
            Assert.True(model.IsBusy); Assert.False(model.CanEditPaths);
            Assert.False(model.CancelCommand.CanExecute(null)); Assert.False(model.LaunchCommand.CanExecute(null));
            Assert.Contains("Cancellation requested", model.StatusText);
            await Drain(); Assert.True(model.IsBusy);
        }
        finally { release.TrySetResult(); }
        await DrainUntil(() => !model.IsBusy);
        Assert.Equal("Cancelled", model.StatusText); Assert.False(model.IsReady);
        Assert.False(model.LaunchCommand.CanExecute(null));
    });

    [Fact]
    public Task ConfiguredPreviewRemainsExplicitlyLabelledAndEveryOperationalCommandIsDisabled() => OnDispatcher(async () =>
    {
        var model = LauncherViewModel.CreateVisualPreview();
        var calls = 0;
        model.ExecuteAsync = (_, _, _, _) => { calls++; return Task.CompletedTask; };
        var paths = ConfiguredClient();
        model.ConfigurationPath = paths.ConfigurationPath; model.ClientRoot = paths.ClientRoot;
        model.SourceRoot = Path.Combine(Path.GetTempPath(), "wolf-ui-preview-source");
        Assert.True(model.IsVisualPreview); Assert.True(model.IsAnimating);
        Assert.False(model.IsBusy); Assert.False(model.IsReady); Assert.False(model.CanEditPaths);
        Assert.Contains("VISUAL PREVIEW", model.StatusText);
        Assert.Contains("preview", model.StatusDetail, StringComparison.OrdinalIgnoreCase);
        foreach (var command in new[] { model.PrimaryCommand, model.VerifyCommand, model.LaunchCommand, model.CancelCommand,
            model.SelectClientCommand, model.SelectSourceCommand })
        { Assert.False(command.CanExecute(null)); command.Execute(null); }
        await Drain(); Assert.Equal(0, calls);
    });

    [Fact]
    public Task QueuedProgressFromFailedOperationCannotReplaceFinalErrorOrAuthorizeLaunch() => OnDispatcher(async () =>
    {
        var model = ConfiguredClient();
        model.ExecuteAsync = (_, _, progress, _) =>
        {
            // Queue a success-looking observation, then fail before the UI pumps it.
            progress.Report(new(Status: "stale success", StagePercent: 100, BytePercent: 100, IsReady: true));
            return Task.FromException(new IOException("Synthetic controller failure."));
        };
        model.PrimaryCommand.Execute(null);
        await DrainUntil(() => !model.IsBusy);
        await Drain();
        Assert.Equal("Could not complete", model.StatusText);
        Assert.False(model.IsReady); Assert.False(model.LaunchCommand.CanExecute(null));
        Assert.DoesNotContain("stale success", model.StatusDetail);
    });
}
