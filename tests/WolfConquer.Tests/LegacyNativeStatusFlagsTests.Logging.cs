using System.Reflection;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    [Fact]
    public Task OnlyGetFlagsCatchesAndLogsItsRefreshFaultWithoutClearingDirtyState() => NativePacketTestThread.Run(() =>
    {
        using var logs = new OwnedFlagLogs();
        var flags = new Flags(32);
        flags.TryAdd(6, 7, false, 0);
        var retained = flags.GetFlags();
        flags.TryAdd(7, 9, false, 0); // Cache is now dirty.
        var registrations = flags.ArrayFlags;
        flags.ArrayFlags = null!;
        Assert.Throws<NullReferenceException>(() => flags.ContainFlag(6));
        Assert.Equal(logs.Before, logs.Counter.Count);
        var first = flags.GetFlags();
        var second = flags.GetFlags();
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.NotSame(first, second);
        Assert.Equal(unchecked(logs.Before + 2), logs.Counter.Count);
        var paths = Directory.GetFiles(logs.Root, "*.txt", SearchOption.AllDirectories);
        Assert.Equal(2, paths.Length);
        foreach (var path in paths)
            Assert.Contains("LegacyNativeStatusFlagsBigVector32.GetFlags", File.ReadAllText(path));
        flags.ArrayFlags = registrations;
        var recovered = flags.GetFlags();
        Assert.NotSame(retained, recovered);
        Assert.Equal(2, recovered.Length); // Refresh fault did not clear Update.
    });

    private sealed class OwnedFlagLogs : IDisposable
    {
        private readonly FieldInfo pathField = typeof(LegacyNativeApplication)
            .GetField("startupPath", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? priorPath;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-status-flags-log-" + Guid.NewGuid().ToString("N"));
        internal LegacyNativeCounter Counter { get; }
        internal uint Before { get; }

        internal OwnedFlagLogs()
        {
            priorPath = pathField.GetValue(null);
            Directory.CreateDirectory(Root);
            pathField.SetValue(null, Root); // Owned MODEL of the genuine cached path.
            Counter = (LegacyNativeCounter)typeof(LegacyNativeConsole)
                .GetField("ExceptionsCounter", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            Before = Counter.Count;
        }

        public void Dispose()
        {
            pathField.SetValue(null, priorPath);
            Counter.Set(Before);
            var full = Path.GetFullPath(Root);
            var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), expectedParent) ||
                !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full), "^wolf-status-flags-log-[0-9a-f]{32}$"))
                throw new InvalidOperationException("Owned flag-log cleanup containment failed.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
