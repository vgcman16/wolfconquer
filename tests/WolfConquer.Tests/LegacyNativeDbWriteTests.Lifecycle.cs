using System.Text;
using WolfConquer.Core.Data;
using DbWrite = WolfConquer.Core.Data.LegacyNativeDbWrite;
using Mode = WolfConquer.Core.Data.LegacyNativeDbWriteMode;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDbWriteTests
{
    [Fact]
    public void DisposeDropsReferencesWithoutClearingEscapedListOrClosingHeldWriter()
    {
        using var files = new NativeRankFileScope();
        using var bytes = new MemoryStream();
        using var ownedStream = new StreamWriter(bytes, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true);
        var write = new DbWrite("inert.txt");
        write.Add("escaped");
        var list = Items(write);
        Slot("SW").SetValue(write, ownedStream);
        write.Dispose();
        write.Dispose();
        Assert.Null(Slot("Items").GetValue(write));
        Assert.Null(Slot("SW").GetValue(write));
        Assert.Equal("", Location(write));
        Assert.Equal(new[] { "escaped" }, list);
        ownedStream.Write("still open");
        ownedStream.Flush();
        Assert.Equal(Encoding.UTF8.GetBytes("still open"), bytes.ToArray());
        Assert.Throws<NullReferenceException>(() => write.Add("after dispose"));
        Assert.False(File.Exists(files.PathFor("inert.txt")));
    }

    [Fact]
    public void PostDisposeExecuteFaultsOnEmptyPathThenRecapturedPathIsTouchedBeforeNullItems()
    {
        using var files = new NativeRankFileScope();
        var write = new DbWrite("inert.txt");
        write.Dispose();
        Assert.Throws<ArgumentException>(() => write.Execute(Mode.Open));
        Assert.Empty(Directory.EnumerateFileSystemEntries(files.Database));

        string existing = files.PathFor("existing.txt");
        File.WriteAllText(existing, "will be truncated before Items.Count");
        write.ChangeLocation("existing.txt");
        Assert.Throws<NullReferenceException>(() => write.Execute(Mode.Open));
        Assert.Empty(File.ReadAllBytes(existing));
        var stream = Assert.IsType<StreamWriter>(Slot("SW").GetValue(write));
        Assert.Throws<ObjectDisposedException>(() => stream.Write("closed by using"));

        write.ChangeLocation("created.txt");
        Assert.Throws<NullReferenceException>(() => write.Execute(Mode.Create));
        Assert.True(File.Exists(files.PathFor("created.txt")));
        Assert.Empty(File.ReadAllBytes(files.PathFor("created.txt")));
        Assert.Null(Slot("Items").GetValue(write));
        write.Dispose();
    }
}
