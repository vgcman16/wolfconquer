using WolfConquer.Updater;
using WolfConquer.Updates.Progress;
using WolfConquer.Updates.Source;

namespace WolfConquer.Launcher;

internal static class LauncherController
{
    internal static async Task ExecuteAsync(LauncherAction action, LauncherInputs inputs,
        IProgress<LauncherUiUpdate> ui, CancellationToken cancellation)
    {
        var measurements = new LauncherProgressMapper(ui);
        // Preflight file hashing and Git startup must not block the WPF dispatcher/animation.
        if (action == LauncherAction.UpdateSource)
        {
            var result = await Task.Run(() => new SourceCheckoutUpdater().UpdateAsync(inputs.SourceRoot,
                "https://github.com/vgcman16/wolfconquer.git", "main", cancellation, measurements), cancellation);
            var success = result.Status is "updated" or "current";
            Report(ui, new LauncherUiUpdate(Status: result.Status switch
            {
                "updated" => "Source updated", "current" => "Source current", "local-changes" => "Local edits preserved",
                "local-commits" => "Local commits preserved", _ => "Source update refused"
            }, Detail: result.Message + " No build, deployment or server restart was performed.",
                Stage: success ? "Source check complete" : "Manual attention required", StagePercent: success ? 100 : null,
                ByteText: "Git does not provide a reliable transfer percentage here.", Activity: result.Message, IsReady: false));
            return;
        }
        var resultClient = await Task.Run(() => LauncherWorkflow.RunClientAsync(inputs.ConfigurationPath,
            fresh: inputs.AllowFreshInstall, repair: action == LauncherAction.VerifyClient,
            launch: action == LauncherAction.LaunchClient, progress: measurements, cancellationToken: cancellation,
            installRootOverride: inputs.ClientRoot), cancellation);
        var detail = $"{resultClient.ProductId} / {resultClient.Channel} · release {resultClient.Sequence}. " +
            (resultClient.MetadataOnly ? "Release selection is current; files were not fully rehashed. Use Verify files before enabling launch." :
            resultClient.LaunchedProcessId is not null ? "Configured client started. Login remains manual." :
            "Signed update operation completed. This does not establish gameplay or login readiness.");
        if (!resultClient.MetadataOnly && !resultClient.LaunchCandidateAvailable)
            detail += " The configured launch path is absent from the signed release; launch is disabled.";
        Report(ui, new LauncherUiUpdate(Status: resultClient.Status switch
        {
            "launched" => "Client started", "unchanged" => "Release current", "verified" => "Files verified", _ => "Client files updated"
        }, Detail: detail, Stage: "Operation complete", StagePercent: 100,
            ByteText: resultClient.MetadataOnly ? "No payload download was needed." : "All required transfer and transaction steps finished.",
            Activity: detail, IsReady: resultClient.LaunchedProcessId is null && resultClient.LaunchCandidateAvailable));
    }

    private static void Report(IProgress<LauncherUiUpdate> ui, LauncherUiUpdate update)
    {
        try { ui.Report(update); }
        catch (Exception) { /* Reporting cannot change the result of a completed transaction. */ }
    }
}
