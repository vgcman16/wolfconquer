using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Kind = WolfConquer.Core.World.LegacyNativeMsgChiInfo.ChiPowerType;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeChiTests
{
    private sealed class FateFiles : IDisposable
    {
        private readonly string previousLocation = LegacyNativeServerConfig.DbLocation;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-chi-fate-" + Guid.NewGuid().ToString("N"));
        internal string Location => Root + Path.DirectorySeparatorChar;
        internal string Pathname => Path.Combine(Root, "fate_exp.txt");
        internal FateFiles()
        {
            Directory.CreateDirectory(Root);
            LegacyNativeServerConfig.DbLocation = Location;
        }
        internal void Write(string text) => File.WriteAllText(Pathname, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = previousLocation;
            string exact = Path.GetFullPath(Root);
            string expectedPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "wolf-chi-fate-";
            if (!exact.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Owned fate fixture root escaped temporary directory.");
            if (Directory.Exists(exact)) Directory.Delete(exact, recursive: true);
        }
    }

    private static string FateRow(uint id = 7, byte type = 1, uint cost = 199) =>
        id + "@@" + type + "@@2@@123@@10011@@20012@@60013@@140014@@50@@999@@" + cost + "@@456@@789@@321@@720027";

    [Fact]
    public Task FateLoadParsesEveryNativeFieldPackedStatAndTruncatedCostFromOwnedBomFile() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FateFiles();
        files.Write(FateRow() + "@@\r\n"); // RemoveEmptyEntries accepts the trailing delimiter.
        LegacyNativeFateExpTable.Load();
        var row = Assert.Single(LegacyNativeFateExpTable.Values).Value;
        Assert.Equal(7u,row.Index); Assert.Equal(Kind.Dragon,row.ChiType); Assert.Equal((byte)2,row.Level);
        Assert.Equal(123u,row.RequiredPoints); Assert.Equal(50u,row.MinScore); Assert.Equal(999u,row.MDefense);
        Assert.Equal(1u,row.CostReduction); Assert.Equal(456u,row.StudyLuck); Assert.Equal(789u,row.DailyFreePoints);
        Assert.Equal(321u,row.MaxChiPoints); Assert.Equal(720027u,row.PrizeItemID);
        Assert.Equal(new[] {Stat.CriticalStrike,Stat.SkillCriticalStrike,Stat.HPAdd,Stat.LukyStrike}, row.Attributes!.Select(attr=>attr.Type));
        Assert.Equal(new ushort[] {11,12,13,14}, row.Attributes!.Select(attr=>attr.Value));
        Assert.All(row.Attributes!, attr=>Assert.False(attr.Locked));
        Assert.Equal(files.Location, LegacyNativeServerConfig.DbLocation);
    });

    [Fact]
    public Task FateLoadMissingFileDoesNotClearOrRepairAnExistingOrNullGlobalOwner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FateFiles();
        var retained = new LegacyNativeFateExpTable.Value {Index=99};
        var dictionary = LegacyNativeFateExpTable.Values; dictionary[55]=retained;
        LegacyNativeFateExpTable.Load();
        Assert.Same(dictionary,LegacyNativeFateExpTable.Values); Assert.Same(retained,dictionary[55]);
        LegacyNativeFateExpTable.Values=null!;
        LegacyNativeFateExpTable.Load(); Assert.Null(LegacyNativeFateExpTable.Values);
    });

    [Fact]
    public Task FateLoadKeepsExistingEntriesAndPrefixRowsWhenLaterParseFails() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FateFiles();
        var retained = new LegacyNativeFateExpTable.Value {Index=99}; LegacyNativeFateExpTable.Values[99]=retained;
        files.Write(FateRow(7) + "\r\n" + FateRow(8).Replace("@@123@@", "@@bad@@") + "\r\n" + FateRow(9));
        Assert.Throws<FormatException>(()=>LegacyNativeFateExpTable.Load());
        Assert.Equal(2,LegacyNativeFateExpTable.Values.Count); Assert.Same(retained,LegacyNativeFateExpTable.Values[99]);
        Assert.Equal(7u,LegacyNativeFateExpTable.Values[7].Index); Assert.False(LegacyNativeFateExpTable.Values.ContainsKey(8)); Assert.False(LegacyNativeFateExpTable.Values.ContainsKey(9));
        using var exclusive = File.Open(files.Pathname,FileMode.Open,FileAccess.ReadWrite,FileShare.None); // File.ReadAllLines owner is closed on parse failure.
    });

    [Fact]
    public Task FateLoadDuplicateIndexThrowsWithoutReplacingFirstPhysicalValueOrSortingRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FateFiles();
        files.Write(FateRow(50,1,199) + "\r\n" + FateRow(3,4,0)); LegacyNativeFateExpTable.Load();
        var first = LegacyNativeFateExpTable.Values[50]; var second = LegacyNativeFateExpTable.Values[3];
        Assert.Equal(new uint[] {50,3},LegacyNativeFateExpTable.Values.Keys);
        Assert.Equal(Kind.Turtle,second.ChiType); Assert.Equal(0u,second.CostReduction);
        Assert.Throws<ArgumentException>(()=>LegacyNativeFateExpTable.Load());
        Assert.Same(first,LegacyNativeFateExpTable.Values[50]); Assert.Same(second,LegacyNativeFateExpTable.Values[3]);
    });

    [Fact]
    public Task FateLoadUsesLiteralConcatenatedLocationAndDoesNotSearchAnInsertedSeparator() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FateFiles(); files.Write(FateRow());
        LegacyNativeServerConfig.DbLocation = files.Root;
        LegacyNativeFateExpTable.Load(); Assert.Empty(LegacyNativeFateExpTable.Values);
        LegacyNativeServerConfig.DbLocation = files.Location;
        LegacyNativeFateExpTable.Load(); Assert.Single(LegacyNativeFateExpTable.Values);
    });

    [Fact]
    public Task FateLoadDoesNotSkipBlankRowsOrDefaultInvalidUnsignedByteFields() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FateFiles();
        files.Write("\r\n"+FateRow()); Assert.Throws<IndexOutOfRangeException>(()=>LegacyNativeFateExpTable.Load());
        Assert.Empty(LegacyNativeFateExpTable.Values);
        files.Write(FateRow().Replace("@@1@@2@@","@@256@@2@@")); Assert.Throws<OverflowException>(()=>LegacyNativeFateExpTable.Load());
        Assert.Empty(LegacyNativeFateExpTable.Values);
        files.Write("7@@1"); Assert.Throws<IndexOutOfRangeException>(()=>LegacyNativeFateExpTable.Load());
        Assert.Empty(LegacyNativeFateExpTable.Values);
    });

    [Fact]
    public Task FateLoadKeepsFullUnsignedPackedTypeAndNativeIntegerCostDivision() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new FateFiles();
        files.Write(FateRow(cost:uint.MaxValue).Replace("@@10011@@","@@4294967295@@"));
        LegacyNativeFateExpTable.Load();
        var row=LegacyNativeFateExpTable.Values[7];
        Assert.Equal((Stat)429496,row.Attributes![0].Type); Assert.Equal((ushort)7295,row.Attributes[0].Value);
        Assert.Equal(42949672u,row.CostReduction);
    });
}
