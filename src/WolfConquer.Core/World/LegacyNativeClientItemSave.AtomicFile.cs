using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeClientItemSave
{
    // Operational image/read bound, not an item capacity or gameplay slot rule.
    private const int MaximumImageBytes = 64 * 1024 * 1024;
    private static readonly ConcurrentDictionary<string, string> IndeterminatePaths = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static Result SaveLocked(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, SaveLocation location)
    {
        if (IndeterminatePaths.TryGetValue(location.Path, out var blocked))
            return new(Outcome.RecoveryRequired, blocked);
        location.EnsureSafe();
        var before = ReadImage(location.Path);
        var graph = SaveGraph.Capture(client, player, location);
        var image = graph.PrepareImage();
        var stage = location.NewStagePath();
        bool attempted = false;
        bool replaced = false;
        bool ownsStage = false;
        try
        {
            using (var file = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsStage = true;
                file.Write(image);
                file.Flush(flushToDisk: true);
            }
            if (!EqualImage(ReadImage(stage), image))
                return new(Outcome.Rejected, "stagePostcondition");
            location.EnsureSafe();
            if (!graph.IsCurrent() || !EqualImage(ReadImage(location.Path), before))
                return new(Outcome.Rejected, "staleBaselineOrOwner");

            attempted = true;
            if (before is null) File.Move(stage, location.Path); // Never overwrite an unexpected new file.
            else File.Replace(stage, location.Path, null, ignoreMetadataErrors: false);
            replaced = true;
            return EqualImage(ReadImage(location.Path), image)
                ? new(Outcome.Committed, string.Empty)
                : Fence(location.Path, "postReplaceImageUnproven");
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            if (!attempted) return new(Outcome.Rejected, "stage:" + exception.GetType().Name);
            return ClassifyAttempt(location.Path, before, image, replaced, exception);
        }
        finally
        {
            // Delete only this owner's exact generated stage; never a glob or the destination.
            try
            {
                if (ownsStage)
                {
                    location.EnsureSafeStage(stage);
                    File.Delete(stage);
                }
            }
            catch (Exception exception) when (IsOrdinaryFailure(exception))
            {
                // A retained private stage is not permission to delete/overwrite a destination.
                // No false rollback or success conclusion is drawn from cleanup.
            }
        }
    }

    private static Result ClassifyAttempt(string path, byte[]? before, byte[] candidate, bool replaced, Exception failure)
    {
        try
        {
            var actual = ReadImage(path);
            if (!replaced && EqualImage(actual, before)) return new(Outcome.Rejected, "replaceUnchanged:" + failure.GetType().Name);
            if (EqualImage(actual, candidate)) return new(Outcome.Committed, "verifiedImageAfter:" + failure.GetType().Name);
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception)) { }
        return Fence(path, "replaceOutcomeUnproven:" + failure.GetType().Name);
    }

    private static Result Fence(string path, string reason)
    {
        IndeterminatePaths.TryAdd(path, reason);
        return new(Outcome.RecoveryRequired, reason);
    }

    private static bool EqualImage(byte[]? left, byte[]? right) => left is null
        ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

    private static byte[]? ReadImage(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaximumImageBytes) throw new InvalidDataException("Native item image exceeds the bounded save size.");
            var bytes = new byte[checked((int)file.Length)];
            file.ReadExactly(bytes);
            if (file.ReadByte() != -1) throw new InvalidDataException("Native item image changed while read.");
            return bytes;
        }
        catch (FileNotFoundException) { return null; }
    }
}
