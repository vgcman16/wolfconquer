using System.IO;
using WolfConquer.Launcher;

namespace WolfConquer.Launcher.Tests;

public sealed partial class LauncherViewModelTests
{
    [Fact]
    public Task NormalStartupHasNoClaimedPercentagesReadinessOrRunnableCommands() => OnDispatcher(async () =>
    {
        var model = new LauncherViewModel();
        Assert.Equal("Not configured", model.StatusText);
        Assert.False(model.IsBusy); Assert.False(model.IsReady); Assert.False(model.IsVisualPreview);
        Assert.False(model.IsAnimating); Assert.Empty(model.Activity);
        Assert.Equal("Not reported", model.StageProgressText); Assert.Equal("—", model.ByteProgressText);
        Assert.Equal(0, model.StageProgress); Assert.Equal(0, model.ByteProgress);
        Assert.False(model.PrimaryCommand.CanExecute(null)); Assert.False(model.LaunchCommand.CanExecute(null));
        Assert.False(model.CancelCommand.CanExecute(null));
        await Drain();
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ClientRequiresBothExplicitConfigurationAndClientRoot(bool config, bool root) => OnDispatcher(async () =>
    {
        var model = ConfiguredClient();
        if (!config) model.ConfigurationPath = "";
        if (!root) model.ClientRoot = "";
        model.ExecuteAsync = (_, _, _, _) => Task.CompletedTask;
        Assert.Equal(config && root, model.PrimaryCommand.CanExecute(null));
        Assert.Equal(config && root, model.VerifyCommand.CanExecute(null));
        Assert.False(model.LaunchCommand.CanExecute(null));
        await Drain();
    });

    [Fact]
    public Task SourceNeedsOnlySourceRootAndHandlerNotClientConfiguration() => OnDispatcher(async () =>
    {
        var model = new LauncherViewModel { SelectedFlow = LauncherFlow.Source,
            SourceRoot = Path.Combine(Path.GetTempPath(), "wolf-ui-source-state") };
        var calls = 0;
        LauncherAction? observedAction = null;
        LauncherInputs? observedInputs = null;
        Assert.False(model.PrimaryCommand.CanExecute(null));
        model.ExecuteAsync = (action, inputs, _, _) =>
        {
            calls++; observedAction = action; observedInputs = inputs;
            return Task.CompletedTask;
        };
        Assert.True(model.PrimaryCommand.CanExecute(null));
        Assert.False(model.VerifyCommand.CanExecute(null)); Assert.False(model.LaunchCommand.CanExecute(null));
        model.PrimaryCommand.Execute(null);
        await DrainUntil(() => !model.IsBusy);
        Assert.Equal(1, calls); Assert.False(model.IsReady);
        Assert.Equal(LauncherAction.UpdateSource, observedAction);
        Assert.NotNull(observedInputs);
        Assert.Equal(model.SourceRoot, observedInputs.SourceRoot); Assert.Equal("", observedInputs.ConfigurationPath);
        Assert.Equal("Completed", model.StatusText);
    });

    [Fact]
    public Task ReportedHundredPercentDoesNotAuthorizeLaunchWithoutExplicitReadiness() => OnDispatcher(async () =>
    {
        var model = ConfiguredClient();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProgress<LauncherUiUpdate>? progress = null;
        model.ExecuteAsync = (_, _, observer, _) => { progress = observer; return release.Task; };
        model.PrimaryCommand.Execute(null);
        try
        {
            Assert.True(model.IsBusy); Assert.NotNull(progress);
            await Task.Run(() => progress.Report(new(StagePercent: 100, BytePercent: 100)));
            await Drain();
            Assert.Equal(100, model.StageProgress); Assert.Equal(100, model.ByteProgress);
            Assert.False(model.IsReady); Assert.False(model.LaunchCommand.CanExecute(null));
            progress.Report(new(StagePercent: 100, BytePercent: 100, IsReady: true));
            await Drain();
            Assert.True(model.IsReady); Assert.False(model.LaunchCommand.CanExecute(null)); // Still busy.
        }
        finally { release.TrySetResult(); }
        await DrainUntil(() => !model.IsBusy);
        Assert.True(model.IsReady); Assert.True(model.LaunchCommand.CanExecute(null));
    });

    [Fact]
    public Task UnexpectedFailureClearsPreviouslyReportedReadinessAndDoesNotExposeExceptionDetails() => OnDispatcher(async () =>
    {
        var model = ConfiguredClient();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProgress<LauncherUiUpdate>? progress = null;
        model.ExecuteAsync = (_, _, observer, _) => { progress = observer; return release.Task; };
        model.PrimaryCommand.Execute(null);
        progress!.Report(new(IsReady: true)); await Drain();
        Assert.True(model.IsReady);
        release.SetException(new InvalidOperationException("sensitive synthetic debug details"));
        await DrainUntil(() => !model.IsBusy);
        Assert.Equal("Could not complete", model.StatusText);
        Assert.False(model.IsReady); Assert.False(model.LaunchCommand.CanExecute(null));
        Assert.DoesNotContain("sensitive synthetic", model.StatusDetail);
    });

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1d)]
    [InlineData(101d)]
    public Task InvalidPercentagesReplacePriorValuesWithUnknown(double value) => OnDispatcher(async () =>
    {
        var model = new LauncherViewModel();
        model.Report(new(StagePercent: 50, BytePercent: 50));
        model.Report(new(StagePercent: value, BytePercent: value));
        Assert.Equal(0, model.StageProgress); Assert.Equal(0, model.ByteProgress);
        Assert.Equal("Not reported", model.StageProgressText); Assert.Equal("—", model.ByteProgressText);
        Assert.False(model.IsReady);
        await Drain();
    });

    [Fact]
    public Task ActivityRetainsOnlyTheNewestTwoHundredEntries() => OnDispatcher(async () =>
    {
        var model = new LauncherViewModel();
        for (var index = 0; index < 250; index++) model.Report(new(Activity: "activity-" + index));
        Assert.True(model.HasActivity); Assert.Equal(200, model.Activity.Count);
        Assert.Equal("activity-50", model.Activity[0].Message); Assert.Equal("activity-249", model.Activity[^1].Message);
        await Drain();
    });
}
