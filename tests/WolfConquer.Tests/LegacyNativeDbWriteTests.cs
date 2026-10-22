using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using DbWrite = WolfConquer.Core.Data.LegacyNativeDbWrite;
using Mode = WolfConquer.Core.Data.LegacyNativeDbWriteMode;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeDbWriteTests
{
    private static FieldInfo Slot(string name) => typeof(DbWrite).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static string Location(DbWrite write) => (string)Slot("location").GetValue(write)!;
    private static List<string> Items(DbWrite write) => (List<string>)Slot("Items").GetValue(write)!;

    [Fact]
    public void NativeMutableStorageEnumAndConstructorBirthRequireNoFileAccess()
    {
        using var files = new NativeRankFileScope();
        var fields = typeof(DbWrite).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "location", "Items", "SW" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(string), typeof(List<string>), typeof(StreamWriter) }, fields.Select(field => field.FieldType));
        Assert.All(fields, field => { Assert.True(field.IsPrivate); Assert.False(field.IsInitOnly); });
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(Mode)));
        Assert.Equal((byte)1, (byte)Mode.Open);
        Assert.Equal((byte)2, (byte)Mode.Create);
        using var first = new DbWrite("missing-parent/rows.txt");
        using var second = new DbWrite("missing-parent/rows.txt");
        Assert.Equal(LegacyNativeServerConfig.DbLocation + "missing-parent/rows.txt", Location(first));
        Assert.Empty(Items(first));
        Assert.NotSame(Items(first), Items(second));
        Assert.Null(Slot("SW").GetValue(first));
        Assert.Empty(Directory.EnumerateFileSystemEntries(files.Database));
    }

    [Fact]
    public void AddAndExecuteReturnSelfAndWriteListCountNullsAndEmbeddedNewlinesLiterally()
    {
        using var files = new NativeRankFileScope();
        using var write = new DbWrite("rows.txt");
        Assert.Same(write, write.Add("snow-\u96EA"));
        Assert.Same(write, write.Add(null!));
        Assert.Same(write, write.Add("part1\npart2"));
        Assert.Same(write, write.Execute(Mode.Open));
        string expected = "Count=3" + Environment.NewLine + "snow-\u96EA" + Environment.NewLine +
            Environment.NewLine + "part1\npart2" + Environment.NewLine;
        Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(files.PathFor("rows.txt")));
        Assert.Equal(3, Items(write).Count);
    }

    [Fact]
    public void OpenReplacesExistingBytesAndRepeatsAllRetainedItemsWithAClosedWriter()
    {
        using var files = new NativeRankFileScope();
        string path = files.PathFor("rows.txt");
        File.WriteAllText(path, "old bytes longer than replacement");
        using var write = new DbWrite("rows.txt");
        var list = Items(write);
        write.Add("one").Execute(Mode.Open);
        Assert.Equal(NativeRankFileScope.Text("Count=1", "one"), File.ReadAllText(path));
        var oldStream = Assert.IsType<StreamWriter>(Slot("SW").GetValue(write));
        Assert.Throws<ObjectDisposedException>(() => oldStream.Write("after close"));
        write.Add("two").Execute(Mode.Open);
        Assert.Equal(NativeRankFileScope.Text("Count=2", "one", "two"), File.ReadAllText(path));
        Assert.Same(list, Items(write));
        Assert.Equal(new[] { "one", "two" }, list);
        Assert.NotSame(oldStream, Slot("SW").GetValue(write));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(255)]
    public void CreateAndEveryUnknownModeAppendCompleteRepeatedBlocks(int rawMode)
    {
        using var files = new NativeRankFileScope();
        string path = files.PathFor("append.txt");
        string seed = "seed-without-newline";
        File.WriteAllText(path, seed);
        using var write = new DbWrite("append.txt");
        write.Add("one").Execute((Mode)rawMode);
        write.Execute((Mode)rawMode);
        string block = NativeRankFileScope.Text("Count=1", "one");
        Assert.Equal(seed + block + block, File.ReadAllText(path));
        Assert.Single(Items(write));
    }

    [Fact]
    public void ConstructorCapturesPrefixAndChangeLocationRecapturesCurrentPrefixWithoutClearingItems()
    {
        using var files = new NativeRankFileScope();
        using var write = new DbWrite("first.txt");
        var list = Items(write);
        write.Add("retained");
        string other = Path.Combine(files.Root, "other");
        Directory.CreateDirectory(other);
        LegacyNativeServerConfig.DbLocation = other + Path.DirectorySeparatorChar;
        write.Execute(Mode.Open);
        Assert.True(File.Exists(files.PathFor("first.txt")));
        Assert.False(File.Exists(Path.Combine(other, "first.txt")));
        write.ChangeLocation("second.txt");
        LegacyNativeServerConfig.DbLocation = "not-read-by-execute";
        write.Execute(Mode.Open);
        Assert.Equal(NativeRankFileScope.Text("Count=1", "retained"), File.ReadAllText(Path.Combine(other, "second.txt")));
        Assert.Same(list, Items(write));
    }

    [Fact]
    public void RawConcatenationDoesNotNormalizeAbsoluteArgumentsOrNullStrings()
    {
        using var files = new NativeRankFileScope();
        LegacyNativeServerConfig.DbLocation = "raw-prefix";
        using var write = new DbWrite(files.RankPath);
        Assert.Equal("raw-prefix" + files.RankPath, Location(write));
        write.ChangeLocation(null!);
        Assert.Equal("raw-prefix", Location(write));
        LegacyNativeServerConfig.DbLocation = null!;
        using var empty = new DbWrite(null!);
        Assert.Equal("", Location(empty));
        empty.ChangeLocation("relative-only");
        Assert.Equal("relative-only", Location(empty));
        Assert.Empty(Directory.EnumerateFileSystemEntries(files.Database));
    }

    [Fact]
    public void MissingParentIsNotCreatedOrRepaired()
    {
        using var files = new NativeRankFileScope();
        using var write = new DbWrite("missing/rows.txt");
        write.Add("one");
        Assert.Throws<DirectoryNotFoundException>(() => write.Execute(Mode.Open));
        Assert.False(Directory.Exists(files.PathFor("missing")));
        Assert.Equal(new[] { "one" }, Items(write));
    }

    [Fact]
    public void OpenLockedFileThrowsBeforeTruncationAndRetainsQueuedItems()
    {
        using var files = new NativeRankFileScope();
        string path = files.PathFor("locked.txt");
        byte[] before = Encoding.UTF8.GetBytes("original bytes");
        File.WriteAllBytes(path, before);
        using var write = new DbWrite("locked.txt");
        write.Add("one");
        using (var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.ThrowsAny<IOException>(() => write.Execute(Mode.Open));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(new[] { "one" }, Items(write));
    }
}
