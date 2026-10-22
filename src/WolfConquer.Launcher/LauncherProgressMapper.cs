using WolfConquer.Updates.Progress;

namespace WolfConquer.Launcher;

// A percentage describes only the measured phase, never an invented overall ETA.
internal sealed class LauncherProgressMapper(IProgress<LauncherUiUpdate> target) : IProgress<UpdateOperationProgress>
{
    private UpdateOperationPhase? _lastPhase;
    public void Report(UpdateOperationProgress progress)
    {
        var phaseChanged = progress.Phase != _lastPhase;
        _lastPhase = progress.Phase;
        var percent = Percentage(progress.Completed, progress.Total);
        var bytes = progress.Unit == UpdateProgressUnit.Bytes;
        var counts = progress.Completed is { } completed && progress.Total is { } total ?
            bytes ? $"{Size(completed)} / {Size(total)} transferred" : $"{completed:N0} / {total:N0} {progress.Unit.ToString().ToLowerInvariant()}" :
            "This step has no measurable percentage.";
        var label = progress.Phase switch
        {
            UpdateOperationPhase.Preflight => "Checking installed files", UpdateOperationPhase.BackingUp => "Backing up originals",
            UpdateOperationPhase.RollingBack => "Finishing safe recovery", UpdateOperationPhase.FastForwarding => "Fast-forwarding source",
            _ => progress.Phase.ToString()
        };
        try
        {
            target.Report(new LauncherUiUpdate(Status: label, Detail: progress.Message, Stage: label,
                StagePercent: bytes ? null : percent, BytePercent: bytes ? percent : null,
                ByteText: counts, Activity: phaseChanged ? progress.Message : null, IsReady: false));
        }
        catch (Exception) { /* A UI observer is never transaction control. */ }
    }

    internal static double? Percentage(long? completed, long? total) =>
        completed is { } count && total is > 0 && count >= 0 && count <= total
            ? (double)count / total.Value * 100 : null;
    private static string Size(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.00} GiB" :
        bytes >= 1L << 20 ? $"{bytes / (double)(1L << 20):0.0} MiB" : bytes >= 1024 ? $"{bytes / 1024d:0.0} KiB" : $"{bytes} B";
}
