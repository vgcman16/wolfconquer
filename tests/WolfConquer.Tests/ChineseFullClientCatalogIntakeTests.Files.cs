using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ChineseFullClientCatalogIntakeTests
{
    [Fact]
    public void RealOwnedDirectoryReadsFixedInputsAndReleasesEveryLeaseWithoutWriting()
    {
        using var fixture = new ReferenceFixture();
        var before = fixture.Files();
        var snapshot = ChineseFullClientCatalogIntake.ReadReferenceDirectory(fixture.Root, 7937);
        Assert.False(snapshot.HasUnsupportedItems); Assert.Equal(42U, snapshot.Findings[0].Items!.Rows[0].ItemId);
        Assert.Equal(before.Keys.Order(), fixture.Files().Keys.Order());
        foreach (var pair in before)
        {
            Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            using var released = new FileStream(pair.Key, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(released.CanWrite);
        }
    }

    [Fact]
    public void MissingFixedFileCannotProduceACompleteSnapshotOrFallback()
    {
        using var fixture = new ReferenceFixture();
        File.Delete(Path.Combine(fixture.Root, "ini", "ItemtypeSub.dat"));
        Assert.Throws<IOException>(() => ChineseFullClientCatalogIntake.ReadReferenceDirectory(fixture.Root, 7937));
        using var released = new FileStream(Path.Combine(fixture.Root, "version.dat"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(released.CanWrite);
    }

    [Theory]
    [InlineData("relative-client")] [InlineData("\\\\server\\share\\client")]
    [InlineData("//server/share/client")] [InlineData("\\\\?\\C:\\client")]
    public void RelativeNetworkAndDevicePathsRefuseBeforeLoading(string path) =>
        Assert.Throws<ArgumentException>(() => ChineseFullClientCatalogIntake.ReadReferenceDirectory(path, 7937));

    private sealed class ReferenceFixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "WolfConquer-full7937-intake-" + Guid.NewGuid().ToString("N"));
        internal ReferenceFixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, "ini", "luacfg"));
            File.WriteAllBytes(Path.Combine(Root, "version.dat"), "7937\r\n"u8.ToArray());
            File.WriteAllBytes(Path.Combine(Root, "ini", "itemtype.dat"), ClientItemCatalogCodec.Encode(ItemRows("42")));
            File.WriteAllBytes(Path.Combine(Root, "ini", "ItemtypeSub.dat"), ClientItemCatalogCodec.Encode(ItemRows("7")));
            File.WriteAllBytes(Path.Combine(Root, "ini", "luacfg", "XuanBaoEcho.dat"), [0xff, 0, 1]);
            File.WriteAllBytes(Path.Combine(Root, "ini", "luacfg", "XuanBaoInfo.dat"), [0xfe, 2, 3]);
        }
        internal Dictionary<string, byte[]> Files() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);
        public void Dispose()
        {
            if (!Directory.Exists(Root)) return;
            var directory = new DirectoryInfo(Root);
            if (directory.Parent?.FullName != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) ||
                !directory.Name.StartsWith("WolfConquer-full7937-intake-", StringComparison.Ordinal) ||
                (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Owned reference fixture cleanup refused.");
            Directory.Delete(Root, recursive: true);
        }
    }
}
