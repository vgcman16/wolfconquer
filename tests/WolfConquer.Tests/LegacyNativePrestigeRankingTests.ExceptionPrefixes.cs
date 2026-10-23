using System.Reflection;
using System.Text.RegularExpressions;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [WindowsPrestigeFact]
    public Task EmptyRankFinallyPreservesNativeDiagnosticFaultAfterPublishingEmptySortedOwner() => NativePacketTestThread.Run(() =>
    {
        using var state = new State();
        using var files = new OwnedDiagnosticDirectories();
        var counter = (LegacyNativeCounter)typeof(LegacyNativeConsole)
            .GetField("ExceptionsCounter",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        uint before = counter.Count;
        foreach (int limit in new[] {0,-1})
        {
            var rank = new Ranking.Rank(Ranking.Type.Ninja) {MaxItems=limit};
            var old = rank.Items;
            var row = Entry();
            // Source creates a never-thrown Exception; its real logger faults on TargetSite.
            Assert.Throws<NullReferenceException>(()=>rank.AddItem(row));
            Assert.Empty(rank.Items);
            Assert.NotSame(old,rank.Items);
            Assert.Same(row,old[41]);
            Assert.Null(rank.BestOfClass);
            Assert.Equal(before,counter.Count);
        }
        Assert.True(Directory.Exists(Path.Combine(files.Root,"Exceptions")));
        Assert.Empty(Directory.GetFiles(files.Root,"*",SearchOption.AllDirectories));
    });

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

    [Fact]
    public void NullSharedGateIsNotReadForLowScoreButFaultsBeforeEligibleRankMutation()
    {
        using var state = new State();
        Ranking.SynRoot = null!;
        var rank = new Ranking.Rank(Ranking.Type.Ninja){MaxItems=30};
        var escaped = rank.Items;
        rank.AddItem(Entry(41,19999));
        Assert.Throws<ArgumentNullException>(()=>rank.AddItem(Entry()));
        Assert.Throws<ArgumentNullException>(()=>rank.Remove(41));
        Assert.Empty(rank.Items);
        Assert.Same(escaped,rank.Items);
        Assert.Null(rank.BestOfClass);
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
