using System.Reflection;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeConsoleExceptionTests
{
    [Fact]
    public Task StartupPathUsesTheCurrentProcessModuleDirectoryAndCachesThatValue() => NativePacketTestThread.Run(() =>
    {
        var field = typeof(LegacyNativeApplication).GetField("startupPath", BindingFlags.NonPublic | BindingFlags.Static)!;
        var prior = field.GetValue(null);
        try
        {
            field.SetValue(null, null);
            var path = LegacyNativeApplication.StartupPath;
            Assert.Equal(Path.GetDirectoryName(Environment.ProcessPath), path);
            Assert.Equal(path, field.GetValue(null));
            Assert.Same(path, LegacyNativeApplication.StartupPath);
        }
        finally { field.SetValue(null, prior); }
    });

    [Fact]
    public Task SaveExceptionWritesNativeSectionsAndAdvancesTheSamePhysicalCounter() => NativePacketTestThread.Run(() =>
    {
        using var files = new OwnedLogScope();
        Exception error;
        try { ThrowOwnedError(); throw new Exception("Unreachable"); }
        catch (InvalidOperationException caught) { error = caught; }
        var counter = Assert.IsType<LegacyNativeCounter>(typeof(LegacyNativeConsole)
            .GetField("ExceptionsCounter", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null));
        var before = counter.Count;
        try
        {
            LegacyNativeConsole.SaveException(error);
            LegacyNativeConsole.WriteException(error);
            Assert.Equal(unchecked(before + 2), counter.Count);
            var logs = Directory.GetFiles(files.Root, "*.txt", SearchOption.AllDirectories);
            Assert.Equal(2, logs.Length);
            foreach (var path in logs)
            {
                Assert.Equal(nameof(ThrowOwnedError), Path.GetFileName(Path.GetDirectoryName(path)));
                var expectedText = string.Join(Environment.NewLine, new[] { "----Exception message----", error.Message,
                    "----End of exception message----\r\n", "----Stack trace----", error.StackTrace!,
                    "----End of stack trace----\r\n" }) + Environment.NewLine;
                Assert.Equal(expectedText, File.ReadAllText(path));
            }
        }
        finally { counter.Set(before); }
    });

    [Fact]
    public Task NeverThrownExceptionFaultsAfterDirectoryPrefixButBeforeCounterAndFilePublication() => NativePacketTestThread.Run(() =>
    {
        using var files = new OwnedLogScope();
        var counter = Assert.IsType<LegacyNativeCounter>(typeof(LegacyNativeConsole)
            .GetField("ExceptionsCounter", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null));
        var before = counter.Count;
        Assert.Throws<NullReferenceException>(() => LegacyNativeConsole.SaveException(new Exception("no TargetSite")));
        Assert.Equal(before, counter.Count);
        Assert.True(Directory.Exists(Path.Combine(files.Root, "Exceptions")));
        Assert.Empty(Directory.GetFiles(files.Root, "*.txt", SearchOption.AllDirectories));
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOwnedError() => throw new InvalidOperationException("owned native exception fixture");

    private sealed class OwnedLogScope : IDisposable
    {
        private readonly FieldInfo field = typeof(LegacyNativeApplication).GetField("startupPath", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? prior;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-log-" + Guid.NewGuid().ToString("N"));
        internal OwnedLogScope()
        {
            prior = field.GetValue(null);
            Directory.CreateDirectory(Root);
            field.SetValue(null, Root); // Owned test MODEL of the genuine cached path, not production config.
        }
        public void Dispose()
        {
            field.SetValue(null, prior);
            var full = Path.GetFullPath(Root);
            var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), expectedParent) ||
                !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full), "^wolf-native-log-[0-9a-f]{32}$"))
                throw new InvalidOperationException("Owned log fixture cleanup containment failed.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
