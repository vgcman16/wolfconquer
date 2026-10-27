using System.Reflection;
using System.Text.RegularExpressions;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [WindowsPrestigeFact]
    public Task NonpositiveRankLimitRefusesBeforePublishingOrInvokingTheNativeDiagnostic() => NativePacketTestThread.Run(() =>
        RunPrestigeDiagnosticRefusal(files =>
    {
        var counter = (LegacyNativeCounter)typeof(LegacyNativeConsole)
            .GetField("ExceptionsCounter",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        uint before = counter.Count;
        foreach (int limit in new[] {0,-1})
        {
            var rank = new Ranking.Rank(Ranking.Type.Ninja) {MaxItems=30};
            rank.AddItem(Entry(17,40_000));
            rank.MaxItems = limit;
            var prior = new PrestigeMutationRankImage(rank);

            var error = Record.Exception(() => rank.AddItem(Entry()));

            prior.RequireUnchanged();
            Assert.Equal(before,counter.Count);
            Assert.False(Directory.Exists(Path.Combine(files.Root,"Exceptions")));
            Assert.IsType<InvalidOperationException>(error);
        }
        Assert.Empty(Directory.GetFiles(files.Root,"*",SearchOption.AllDirectories));
    }));

    [WindowsPrestigeFact]
    public Task MissingWorldBucketInvokesRealDiagnosticAndPreservesItsDirectoryOnlyFaultPrefix() => NativePacketTestThread.Run(() =>
    {
        using var state = new State();
        using var files = new OwnedDiagnosticDirectories();
        Assert.Empty(Ranking.Ranks);
        Assert.Throws<NullReferenceException>(()=>Ranking.BestOfTheWorld);
        Assert.Empty(Ranking.Ranks);
        Assert.True(Directory.Exists(Path.Combine(files.Root,"Exceptions")));
        Assert.Empty(Directory.GetFiles(files.Root,"*",SearchOption.AllDirectories));
    });

    [Theory]
    [InlineData("low-add")]
    [InlineData("eligible-add")]
    [InlineData("present-remove")]
    [InlineData("absent-remove")]
    public void NullSharedGateRejectsLowEligibleAndRemovalOperationsBeforeAnyRankMutation(string operation)
    {
        using var state = new State();
        var rank = new Ranking.Rank(Ranking.Type.Ninja){MaxItems=30};
        rank.AddItem(Entry(41,40_000));
        Ranking.SynRoot = null!;
        var prior = new PrestigeMutationRankImage(rank);

        var error = Record.Exception(() =>
        {
            switch (operation)
            {
                case "low-add": rank.AddItem(Entry(41,19_999)); break;
                case "eligible-add": rank.AddItem(Entry(41,50_000)); break;
                case "present-remove": rank.Remove(41); break;
                case "absent-remove": rank.Remove(99); break;
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }
        });

        prior.RequireUnchanged();
        Assert.IsType<ArgumentNullException>(error);
    }

    private static void RunPrestigeDiagnosticRefusal(Action<OwnedDiagnosticDirectories> action)
    {
        State? state = null;
        OwnedDiagnosticDirectories? files = null;
        var failures = new List<Exception>();
        try { state = new(); files = new(); action(files); } catch (Exception error) { failures.Add(error); }
        try { files?.Dispose(); } catch (Exception error) { failures.Add(error); }
        try { state?.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Prestige diagnostic assertion and owned cleanup failed.", failures);
    }

    private sealed class WindowsPrestigeFactAttribute : FactAttribute
    {
        public WindowsPrestigeFactAttribute()
        {
            if(!OperatingSystem.IsWindows()) Skip="The genuine native MyConsole diagnostic requires Windows handles and path syntax.";
        }
    }

    private sealed class OwnedDiagnosticDirectories : IDisposable
    {
        private readonly FieldInfo field=typeof(LegacyNativeApplication).GetField("startupPath",BindingFlags.NonPublic|BindingFlags.Static)!;
        private readonly object? prior;
        internal string Root {get;}=Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(),"native-prestige-diagnostic-"+Guid.NewGuid().ToString("N"));

        internal OwnedDiagnosticDirectories()
        {
            Guard();
            Assert.False(Directory.Exists(Root)||File.Exists(Root));
            Directory.CreateDirectory(Root);
            prior=field.GetValue(null);
            field.SetValue(null,Root); // Owned test model of the genuine private process cache only.
        }

        private void Guard()
        {
            Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(),Path.GetDirectoryName(Path.GetFullPath(Root)));
            Assert.True(Regex.IsMatch(Path.GetFileName(Root),"^native-prestige-diagnostic-[0-9a-f]{32}$"));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Root);
        }

        public void Dispose()
        {
            field.SetValue(null,prior);
            Guard();
            Assert.Empty(Directory.GetFiles(Root,"*",SearchOption.AllDirectories));
            var directories=Directory.GetDirectories(Root,"*",SearchOption.AllDirectories);
            foreach(string directory in directories)
            {
                Assert.StartsWith(Root+Path.DirectorySeparatorChar,Path.GetFullPath(directory),StringComparison.OrdinalIgnoreCase);
                LegacyNativeDbReadOwnedFiles.OrdinaryChain(directory);
            }
            foreach(string directory in directories.OrderByDescending(path=>path.Length)) Directory.Delete(directory,recursive:false);
            Directory.Delete(Root,recursive:false);
        }
    }
}
