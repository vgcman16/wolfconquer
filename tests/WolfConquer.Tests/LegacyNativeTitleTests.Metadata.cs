using System.Globalization;
using Metadata = WolfConquer.Core.Data.LegacyNativeTitleStorage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTitleTests
{
    [Fact]
    public Task MetadataDefaultsAndMissingFileDoNotReplaceOrClearCurrentDictionary() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var file = new MetadataFile();
            var value = new Metadata();
            Assert.Equal(0u, value.ID);
            Assert.Equal(0u, value.SubID);
            Assert.Equal(0u, value.Score);
            Assert.Equal("", value.Name);
            var retained = scope.Row(9, 9);
            Assert.Throws<FileNotFoundException>(() => Metadata.LoadDBInformation());
            Assert.Same(scope.Titles, Metadata.Titles);
            Assert.Same(retained, Assert.Single(scope.Titles).Value);
            Assert.False(File.Exists(file.PathName));
        });

    [Fact]
    public Task MetadataLoadUsesMixedDelimitersLiteralColumnsAndAppendsToSameDictionary() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var file = new MetadataFile();
            var retained = scope.Row(9, 9);
            const string input = "  1@@  2@@狼@@ignored@@not-number@@x@@y@@9  \r\n4294967295 7 Last a b c d 4294967295\r\n";
            file.Text(input);
            Metadata.LoadDBInformation();
            Assert.Same(scope.Titles, Metadata.Titles);
            Assert.Same(retained, scope.Titles[9]);
            Assert.Equal(3, scope.Titles.Count);
            Assert.Equal(1u, scope.Titles[1].ID);
            Assert.Equal(2u, scope.Titles[1].SubID);
            Assert.Equal("狼", scope.Titles[1].Name);
            Assert.Equal(9u, scope.Titles[1].Score);
            Assert.Equal(uint.MaxValue, scope.Titles[uint.MaxValue].ID);
            Assert.Equal(7u, scope.Titles[uint.MaxValue].SubID);
            Assert.Equal(uint.MaxValue, scope.Titles[uint.MaxValue].Score);
            Assert.Equal(input, File.ReadAllText(file.PathName));
        });

    [Theory]
    [InlineData("2 3 Name", "index")]
    [InlineData("bad 3 Name a b c d 9", "format")]
    [InlineData("2 3 Name a b c d bad", "format")]
    [InlineData("4294967296 3 Name a b c d 9", "overflow")]
    public Task MetadataParseFaultKeepsEarlierPublishedRowsAndDoesNotReadFollowingRow(
        string bad, string error) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var file = new MetadataFile();
        file.Text("1 2 First a b c d 9\n" + bad + "\n3 4 Last a b c d 8\n");
        Exception actual = error switch
        {
            "index" => Assert.Throws<IndexOutOfRangeException>(() => Metadata.LoadDBInformation()),
            "format" => Assert.Throws<FormatException>(() => Metadata.LoadDBInformation()),
            _ => Assert.Throws<OverflowException>(() => Metadata.LoadDBInformation()),
        };
        Assert.NotNull(actual);
        Assert.Single(scope.Titles);
        Assert.Equal("First", scope.Titles[1].Name);
        Assert.Equal(9u, scope.Titles[1].Score);
        Assert.False(scope.Titles.ContainsKey(3));
    });

    [Fact]
    public Task RepeatedMetadataLoadFailsAtFirstDuplicateWithoutReplacingOriginalRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var file = new MetadataFile();
            file.Text("1 2 First a b c d 9\n2 3 Second a b c d 10\n");
            Metadata.LoadDBInformation();
            var first = scope.Titles[1];
            var second = scope.Titles[2];
            Assert.Throws<ArgumentException>(() => Metadata.LoadDBInformation());
            Assert.Equal(2, scope.Titles.Count);
            Assert.Same(first, scope.Titles[1]);
            Assert.Same(second, scope.Titles[2]);
        });

    [Fact]
    public Task MetadataNumericParseUsesCurrentCulturePositiveSign() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var file = new MetadataFile();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        file.Text("p7 p2 Name a b c d p9\n");
        Metadata.LoadDBInformation();
        Assert.Equal(7u, scope.Titles[7].ID);
        Assert.Equal(2u, scope.Titles[7].SubID);
        Assert.Equal(9u, scope.Titles[7].Score);
    });

    [Fact]
    public Task MetadataLoaderReadsCurrentLocationWithoutFallingBackToPreviouslyOwnedFile() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var file = new MetadataFile();
            file.Text("1 2 First a b c d 9\n");
            // This explicit missing child remains inside the owned root; no original
            // or process-current directory is probed as an alternate database.
            WolfConquer.Core.World.LegacyNativeServerConfig.DbLocation =
                Path.Combine(file.Root, "absent") + Path.DirectorySeparatorChar;
            Assert.Throws<DirectoryNotFoundException>(() => Metadata.LoadDBInformation());
            Assert.Empty(scope.Titles);
            Assert.True(File.Exists(file.PathName));
        });
}
