using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeSharedPathLoggerCompatTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RealSaveAndWriteExceptionPublishThrownLambdaLogsWithFrameworkTargetSiteDirectory(bool writeException)
        => NativePacketTestThread.Run(() =>
    {
        Exception error = CaptureLambdaError();
        string site = error.TargetSite!.Name;
        Assert.Contains("<", site, StringComparison.Ordinal); Assert.Contains(">", site, StringComparison.Ordinal);
        string expectedSite = new(site.Select(c => c <= 31 || c is '"' or '<' or '>' or '|' ? '_' : c).ToArray());
        using var files = new OwnedLogScope(expectedSite);
        uint before = files.Counter.Count;
        if (writeException) LegacyNativeConsole.WriteException(error);
        else LegacyNativeConsole.SaveException(error);
        Assert.Equal(unchecked(before + 1), files.Counter.Count);
        string log = Assert.Single(files.ReadOwnedFiles(out _));
        Assert.Equal(expectedSite, Path.GetFileName(Path.GetDirectoryName(log)));
        Assert.EndsWith("E" + unchecked(before + 1) + ".txt", Path.GetFileName(log), StringComparison.Ordinal);
        string expected = string.Join(Environment.NewLine, new[]
        {
            "----Exception message----", error.Message, "----End of exception message----\r\n",
            "----Stack trace----", error.StackTrace!, "----End of stack trace----\r\n"
        }) + Environment.NewLine;
        Assert.Equal(expected, File.ReadAllText(log));
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CaptureLambdaError()
    {
        Action thrower = [MethodImpl(MethodImplOptions.NoInlining)] () =>
            throw new InvalidOperationException("owned path compatibility lambda fixture");
        try { thrower(); }
        catch (InvalidOperationException error) { return error; }
        throw new InvalidOperationException("Unreachable lambda control.");
    }

    private sealed class OwnedLogScope : IDisposable
    {
        private readonly FieldInfo startup = typeof(LegacyNativeApplication).GetField("startupPath", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? prior;
        private readonly string targetSite;
        private readonly uint before;
        private readonly ConsoleColor priorColor = Console.ForegroundColor;
        internal LegacyNativeCounter Counter { get; }
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-path-compat-" + Guid.NewGuid().ToString("N"));

        internal OwnedLogScope(string expectedSite)
        {
            targetSite = expectedSite; prior = startup.GetValue(null);
            ValidateRoot();
            if (Directory.Exists(Root) || File.Exists(Root)) throw new InvalidOperationException("Owned path already exists.");
            Directory.CreateDirectory(Root);
            try
            {
                startup.SetValue(null, Root);
                Counter = (LegacyNativeCounter)typeof(LegacyNativeConsole)
                    .GetField("ExceptionsCounter", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                before = Counter.Count;
            }
            catch
            {
                startup.SetValue(null, prior); Cleanup(); throw;
            }
        }

        private void ValidateRoot()
        {
            string full = Path.GetFullPath(Root);
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(parent) || !StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), parent) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-path-compat-[0-9a-f]{32}$") ||
                full.Any(c => c <= 31 || c is '"' or '<' or '>' or '|'))
                throw new InvalidOperationException("Exact temporary log-root containment failed.");
            for (string? at = full; at != null; at = Path.GetDirectoryName(at))
                if (Directory.Exists(at) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Owned log-root ancestor is a reparse point.");
        }

        private static string OwnedChild(string entry, string parent, bool directory)
        {
            string full = Path.GetFullPath(entry);
            FileAttributes attrs = File.GetAttributes(full);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), Path.GetFullPath(parent)) ||
                (attrs & FileAttributes.ReparsePoint) != 0 || attrs.HasFlag(FileAttributes.Directory) != directory)
                throw new InvalidOperationException("Unexpected owned log entry or reparse point.");
            return full;
        }

        internal string[] ReadOwnedFiles(out List<string> directories)
        {
            ValidateRoot(); directories = new List<string> { Path.GetFullPath(Root) };
            var logs = new List<string>();
            foreach (string exceptionsEntry in Directory.EnumerateFileSystemEntries(Root))
            {
                string exceptions = OwnedChild(exceptionsEntry, Root, directory: true);
                if (Path.GetFileName(exceptions) != "Exceptions") throw new InvalidOperationException("Unowned root entry.");
                directories.Add(exceptions);
                foreach (string dateEntry in Directory.EnumerateFileSystemEntries(exceptions))
                {
                    string date = OwnedChild(dateEntry, exceptions, directory: true);
                    if (!Regex.IsMatch(Path.GetFileName(date), "^[0-9]{1,2}-[0-9]{1,2}$"))
                        throw new InvalidOperationException("Unowned date directory.");
                    directories.Add(date);
                    foreach (string siteEntry in Directory.EnumerateFileSystemEntries(date))
                    {
                        string site = OwnedChild(siteEntry, date, directory: true);
                        if (Path.GetFileName(site) != targetSite) throw new InvalidOperationException("Unowned target-site directory.");
                        directories.Add(site);
                        foreach (string fileEntry in Directory.EnumerateFileSystemEntries(site))
                        {
                            string file = OwnedChild(fileEntry, site, directory: false);
                            if (!Regex.IsMatch(Path.GetFileName(file), "^[0-9]+-[0-9]+-[0-9]+-[0-9]+E[0-9]+\\.txt$"))
                                throw new InvalidOperationException("Unowned log file.");
                            logs.Add(file);
                        }
                    }
                }
            }
            return logs.ToArray();
        }

        private void Cleanup()
        {
            if (!Directory.Exists(Root)) return;
            string[] files = ReadOwnedFiles(out var directories); // Validate every entry before deleting any.
            foreach (string file in files) File.Delete(file);
            for (int i = directories.Count - 1; i >= 0; i--) Directory.Delete(directories[i], recursive: false);
        }

        public void Dispose()
        {
            startup.SetValue(null, prior); Counter.Set(before);
            try { Console.ForegroundColor = priorColor; }
            finally { Cleanup(); }
        }
    }
}
