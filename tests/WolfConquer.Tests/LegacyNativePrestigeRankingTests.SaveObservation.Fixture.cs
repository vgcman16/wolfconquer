using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    private static void RunOwnedPrestigeSaveObservation(Action<PrestigeSaveObservationFiles> action)
    {
        var location = LegacyNativeServerConfig.DbLocation;
        State? state = null;
        OwnedRankFile? owned = null;
        PrestigeSaveObservationFiles? observation = null;
        var failures = new List<Exception>();
        try
        {
            state = new();
            owned = new();
            observation = new(owned);
            action(observation);
        }
        catch (Exception error) { failures.Add(error); }
        try { observation?.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (observation is null || observation.PriorClosed)
            try { owned?.Dispose(); } catch (Exception error) { failures.Add(error); }
        else
            failures.Add(new IOException($"Owned observation root retained at '{owned!.Root}'; prior stream closure is unconfirmed."));
        LegacyNativeServerConfig.DbLocation = location;
        try { state?.Dispose(); } catch (Exception error) { failures.Add(error); }
        ThrowPrestigeObservationFailures(failures);
    }

    private static void ThrowPrestigeObservationFailures(List<Exception> failures)
    {
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Prestige Save observation assertion and owned cleanup failed; unexpected files remain preserved.", failures);
    }

    private sealed class PrestigeSaveObservationFiles : IDisposable
    {
        private readonly OwnedRankFile owned;
        private readonly string token = Guid.NewGuid().ToString("N");
        private readonly FileStream before;
        private byte[]? stageImage;
        private bool ownsStage;
        private bool ownsBackup;
        private bool replaceAttempted;
        internal string PriorText { get; } = Text(FullRow);
        internal byte[] PriorImage { get; }
        internal string Destination { get; }
        internal string Stage { get; }
        internal string Backup { get; }
        internal bool PriorClosed { get; private set; }

        internal PrestigeSaveObservationFiles(OwnedRankFile owned)
        {
            this.owned = owned;
            Destination = Path.GetFullPath(owned.PathFor());
            var prefix = Path.Combine(owned.Root, ".PrestigeRanking.txt.prestige-save-" + token);
            Stage = prefix + ".tmp";
            Backup = prefix + ".bak";
            GuardPaths();
            owned.Seed(PriorText);
            PriorImage = File.ReadAllBytes(Destination);
            before = new FileStream(Destination, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }

        internal void PrepareSidecars(string candidate)
        {
            stageImage = new UTF8Encoding(false, true).GetBytes(candidate);
            GuardPaths();
            using (var stream = new FileStream(Stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsStage = true;
                stream.Write(stageImage);
                stream.Flush(flushToDisk: true);
            }
            using (var stream = new FileStream(Backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsBackup = true;
                stream.Flush(flushToDisk: true);
            }
            Assert.Equal(stageImage, File.ReadAllBytes(Stage));
            Assert.Empty(File.ReadAllBytes(Backup));
        }

        internal void ReplaceTarget(string candidate)
        {
            GuardPaths();
            Assert.Equal(PriorImage, File.ReadAllBytes(Destination));
            replaceAttempted = true;
            File.Replace(Stage, Destination, Backup, ignoreMetadataErrors: false);
            owned.Expect(candidate);
            Assert.False(File.Exists(Stage));
            Assert.Equal(PriorImage, File.ReadAllBytes(Backup));
        }

        internal void RequireUnacknowledgedOutcome(string expected)
        {
            GuardPaths();
            var pathsType = typeof(Ranking).GetNestedType("PrestigeSavePaths", BindingFlags.NonPublic);
            Assert.NotNull(pathsType);
            var constructor = pathsType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: [typeof(string), typeof(string), typeof(string)], modifiers: null);
            Assert.NotNull(constructor);
            var paths = constructor.Invoke([Destination, Stage, Backup]);
            var method = typeof(Ranking).GetMethod("ReconcilePrestigeSave", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            object?[] arguments = [paths, before, stageImage!, false, null];
            object? result;
            try { result = method.Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
            Assert.Equal(expected, result?.ToString());
            Assert.Null(arguments[4]);
        }

        private void GuardPaths()
        {
            var root = Path.GetFullPath(owned.Root);
            Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), Path.GetDirectoryName(root));
            Assert.Matches("^native-prestige-rank-[0-9a-f]{32}$", Path.GetFileName(root));
            Assert.Matches("^[0-9a-f]{32}$", token);
            Assert.Equal(Path.Combine(root, "PrestigeRanking.txt"), Destination);
            var prefix = Path.Combine(root, ".PrestigeRanking.txt.prestige-save-" + token);
            Assert.Equal(prefix + ".tmp", Stage);
            Assert.Equal(prefix + ".bak", Backup);
            foreach (var path in new[] { Destination, Stage, Backup })
                LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        }

        private void DeleteKnownSidecar(string path, bool stage)
        {
            GuardPaths();
            FileAttributes attributes;
            try { attributes = File.GetAttributes(path); }
            catch (FileNotFoundException) when (stage && replaceAttempted) { return; }
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new IOException($"Unexpected owned observation entry preserved at '{path}'.");
            var bytes = File.ReadAllBytes(path);
            var known = stage ? bytes.AsSpan().SequenceEqual(stageImage!) :
                bytes.Length == 0 || bytes.AsSpan().SequenceEqual(PriorImage);
            if (!known) throw new IOException($"Changed owned observation sidecar preserved at '{path}'.");
            File.Delete(path);
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            try { before.Dispose(); PriorClosed = true; } catch (Exception error) { failures.Add(error); }
            if (PriorClosed)
            {
                if (ownsStage)
                    try { DeleteKnownSidecar(Stage, stage: true); ownsStage = false; }
                    catch (Exception error) { failures.Add(error); }
                if (ownsBackup)
                    try { DeleteKnownSidecar(Backup, stage: false); ownsBackup = false; }
                    catch (Exception error) { failures.Add(error); }
            }
            else
                failures.Add(new IOException($"Owned observation artifacts retained at '{owned.Root}'; prior stream closure is unconfirmed."));
            ThrowPrestigeObservationFailures(failures);
        }
    }
}
