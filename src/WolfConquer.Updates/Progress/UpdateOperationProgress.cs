using System.Diagnostics;

namespace WolfConquer.Updates.Progress;

public enum UpdateOperationPhase
{
    Checking, Preflight, Downloading, Verifying, BackingUp, Applying, RollingBack,
    Fetching, FastForwarding, Completed, Current, Skipped, Failed
}

public enum UpdateProgressUnit { Bytes, Files, Steps }

/// <summary>Phase-local measurements, never an estimated overall percentage. Null counts are indeterminate.</summary>
public sealed record UpdateOperationProgress(UpdateOperationPhase Phase, string Message,
    long? Completed = null, long? Total = null, UpdateProgressUnit Unit = UpdateProgressUnit.Steps,
    string? RelativePath = null);

// Observers are optional, untrusted notifications, not part of transaction control.
// A Progress<T> consumer must also handle exceptions inside its asynchronously posted
// callback; exceptions thrown synchronously by IProgress.Report cannot affect us.
internal sealed class UpdateProgressReporter(IProgress<UpdateOperationProgress>? observer)
{
    private UpdateOperationPhase? _phase;
    private long _timestamp;
    private long? _completed;
    private long? _total;
    private UpdateProgressUnit _unit;

    internal void Report(UpdateOperationPhase phase, string message, long? completed = null, long? total = null,
        UpdateProgressUnit unit = UpdateProgressUnit.Steps, string? relativePath = null)
    {
        if (observer is null) return;
        if (_phase == phase && completed == _completed && total == _total && unit == _unit) return;
        var now = Stopwatch.GetTimestamp();
        var final = completed.HasValue && total.HasValue && completed == total;
        var firstMeasurement = completed is > 0 && _completed.GetValueOrDefault() == 0;
        var chunk = unit == UpdateProgressUnit.Bytes ? 4L << 20 : 256;
        if (_phase == phase && !final && !firstMeasurement && Stopwatch.GetElapsedTime(_timestamp, now) < TimeSpan.FromMilliseconds(100) &&
            (!completed.HasValue || completed.Value - _completed.GetValueOrDefault() < chunk)) return;
        _phase = phase;
        _timestamp = now;
        _completed = completed;
        _total = total;
        _unit = unit;
        try { observer.Report(new(phase, message, completed, total, unit, relativePath)); }
        catch (Exception) { /* A failed UI/log observer must never interrupt install or rollback. */ }
    }
}
