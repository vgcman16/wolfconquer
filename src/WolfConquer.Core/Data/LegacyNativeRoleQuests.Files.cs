using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRoleQuests
{
    private static readonly ConcurrentDictionary<string, string> IndeterminatePaths = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static Result LoadLocked(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        LegacyNativeQuestsRuntime book, QuestLocation location)
    {
        if (IndeterminatePaths.TryGetValue(location.Path, out var reason)) return new(Outcome.RecoveryRequired, reason);
        var graph = new BookGraph(client, player, book, location);
        try { location.EnsureSafe(); }
        catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException)
        { return new(Outcome.NotFound, "questDirectoryMissing"); }
        var image = ReadImage(location.Path);
        if (image is null) return new(Outcome.NotFound, "questFileMissing");
        var next = graph.Merge(Decode(image));
        location.EnsureSafe();
        if (!EqualImage(ReadImage(location.Path), image) || !graph.IsCurrent())
            return new(Outcome.Rejected, "staleBaselineOrOwner");
        // Cooperating readers/writers use this book gate. Two public map fields
        // cannot promise atomic observation to uncooperating raw readers.
        book.src = next.Source;
        book.AcceptedQuests = next.Accepted;
        return new(Outcome.Committed, string.Empty);
    }

    private static Result SaveLocked(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        LegacyNativeQuestsRuntime book, QuestLocation location)
    {
        if (IndeterminatePaths.TryGetValue(location.Path, out var reason)) return new(Outcome.RecoveryRequired, reason);
        location.EnsureSafe();
        var before = ReadImage(location.Path);
        var graph = new BookGraph(client, player, book, location);
        var image = Encode(graph);
        string stage = location.NewStagePath();
        bool attempted = false, replaced = false, ownsStage = false;
        try
        {
            using (var file = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsStage = true;
                file.Write(image);
                file.Flush(flushToDisk: true);
            }
            if (!EqualImage(ReadImage(stage), image)) return new(Outcome.Rejected, "stagePostcondition");
            location.EnsureSafe();
            if (!EqualImage(ReadImage(location.Path), before) || !graph.IsCurrent())
                return new(Outcome.Rejected, "staleBaselineOrOwner");
            attempted = true;
            if (before is null) File.Move(stage, location.Path);
            else File.Replace(stage, location.Path, null, ignoreMetadataErrors: false);
            replaced = true;
            return EqualImage(ReadImage(location.Path), image)
                ? new(Outcome.Committed, string.Empty) : Fence(location.Path, "postReplaceImageUnproven");
        }
        catch (Exception exception) when (IsOrdinaryFailure(exception))
        {
            if (!attempted) return new(Outcome.Rejected, "stage:" + exception.GetType().Name);
            return ClassifyAttempt(location.Path, before, image, replaced, exception);
        }
        finally
        {
            try
            {
                if (ownsStage) { location.EnsureSafeStage(stage); File.Delete(stage); }
            }
            catch (Exception exception) when (IsOrdinaryFailure(exception)) { }
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
            if (file.Length > MaximumImageBytes) throw new InvalidDataException("Quest image exceeds the operational bound.");
            var image = new byte[checked((int)file.Length)];
            file.ReadExactly(image);
            if (file.ReadByte() != -1) throw new InvalidDataException("Quest image changed while read.");
            return image;
        }
        catch (FileNotFoundException) { return null; }
    }
}
