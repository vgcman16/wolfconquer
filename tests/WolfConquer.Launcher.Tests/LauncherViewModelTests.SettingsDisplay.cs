using System.IO;
using System.Text;
using WolfConquer.Updater;

namespace WolfConquer.Launcher.Tests;

public sealed partial class LauncherViewModelTests
{
    [Theory]
    [InlineData(null, "Working directory: Client folder root (default).")]
    [InlineData("Env_DX8", "Working directory: Env_DX8 (relative to client folder).")]
    public Task LocalSettingsWorkingDirectoryDisplayDistinguishesRootAndEngineDirectoryWithoutChangingInputs(
        string? relative, string expected) => OnDispatcher(async () =>
    {
        var model = ConfiguredClient();
        string selectedRoot = model.ClientRoot;
        string config = model.ConfigurationPath;
        var settings = new ClientSettingsSummary(config, "presentation-fixture", "preview",
            selectedRoot + "-configured-default", "Env_DX8/FixtureClient.exe", false, relative);

        model.DisplayClientSettings(settings);

        Assert.Equal(expected, model.LaunchWorkingDirectoryText);
        Assert.Equal(selectedRoot, model.ClientRoot); Assert.Equal(config, model.ConfigurationPath);
        Assert.False(model.IsReady); Assert.False(model.IsBusy); Assert.Empty(model.Activity);
        Assert.False(model.LaunchCommand.CanExecute(null));
        await Drain();
    });

    [Fact]
    public Task ConfigurationEditsInvalidateTheDisplayAndAnOldSummaryCannotRestoreIt() => OnDispatcher(async () =>
    {
        var model = ConfiguredClient();
        var settings = new ClientSettingsSummary(model.ConfigurationPath, "presentation-fixture", "preview",
            model.ClientRoot, "FixtureClient.exe", false, "Env_DX8");
        model.DisplayClientSettings(settings);
        Assert.Contains("Env_DX8", model.LaunchWorkingDirectoryText);

        model.ConfigurationPath += ".different.json";
        model.DisplayClientSettings(settings);

        Assert.Equal("Working directory: Unavailable — select a valid local configuration.", model.LaunchWorkingDirectoryText);
        Assert.False(model.IsReady); Assert.Empty(model.Activity);
        await Drain();
    });

    [Fact]
    public Task MissingLocalSettingsStayUnavailableWithoutChangingStatusOrReadiness() => OnDispatcher(async () =>
    {
        var model = ConfiguredClient(); // A GUID path that has never been created.
        model.ExecuteAsync = (_, _, _, _) => Task.CompletedTask;
        string status = model.StatusText;

        model.RefreshClientSettingsDisplay();

        Assert.Equal("Working directory: Unavailable — select a valid local configuration.", model.LaunchWorkingDirectoryText);
        Assert.Equal(status, model.StatusText);
        Assert.False(model.IsReady); Assert.False(model.IsBusy); Assert.Empty(model.Activity);
        Assert.True(model.PrimaryCommand.CanExecute(null)); // Existing syntactic command gating is unchanged.
        Assert.False(model.LaunchCommand.CanExecute(null));
        await Drain();
    });

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public Task InvalidLocalSettingsRemainUnavailableAndTheOwnedConfigurationIsUnchanged(string json) => OnDispatcher(async () =>
    {
        string root = Path.Combine(Path.GetTempPath(), "wolf-launcher-display-" + Guid.NewGuid().ToString("N"));
        string config = Path.Combine(root, "configuration.json");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(config, json, new UTF8Encoding(false));
            byte[] before = File.ReadAllBytes(config);
            var model = new LauncherViewModel { ConfigurationPath = config, ClientRoot = Path.Combine(root, "client") };
            string status = model.StatusText;

            model.RefreshClientSettingsDisplay();

            Assert.Equal("Working directory: Unavailable — select a valid local configuration.", model.LaunchWorkingDirectoryText);
            Assert.Equal(status, model.StatusText); Assert.False(model.IsReady); Assert.Empty(model.Activity);
            Assert.Equal(before, File.ReadAllBytes(config));
            Assert.False(Directory.Exists(model.ClientRoot));
            Assert.False(Directory.Exists(Path.Combine(root, ".wolfconquer-cache")));
            await Drain();
        }
        finally { File.Delete(config); Directory.Delete(root); } // Exact owned files only; unexpected contents are retained by failure.
    });
}
