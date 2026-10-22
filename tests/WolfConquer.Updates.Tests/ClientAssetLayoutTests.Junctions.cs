using WolfConquer.Updates.ClientLayout;

namespace WolfConquer.Updates.Tests;

public sealed partial class ClientAssetLayoutTests
{
    [WindowsFact]
    public void NativeJunctionCreateExactDeletePreservesActualTargetBytesAndRefusesOverwrite()
    {
        using var f = new LayoutFixture();
        string target = Directory.CreateDirectory(Path.Combine(f.Root, "target")).FullName;
        string keep = Path.Combine(target, "keep.bin"); File.WriteAllBytes(keep, [1, 9, 3, 7]);
        string link = Path.Combine(f.Client, "new-link");
        Assert.False(ClientDirectoryJunction.IsExact(link, target));
        f.Junction(link, target);
        Assert.True(ExistsReparse(link));
        Assert.True(ClientDirectoryJunction.IsExact(link, target));
        Assert.Equal(new byte[] { 1, 9, 3, 7 }, File.ReadAllBytes(Path.Combine(link, "keep.bin")));
        Assert.ThrowsAny<IOException>(() => ClientDirectoryJunction.CreateNew(link, target));
        ClientDirectoryJunction.DeleteExact(link, target);
        Assert.False(Directory.Exists(link));
        Assert.Equal(new byte[] { 1, 9, 3, 7 }, File.ReadAllBytes(keep));
        Assert.Throws<DirectoryNotFoundException>(() => ClientDirectoryJunction.DeleteExact(link, target));
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryExistingFileOrDirectoryIsNeverAdoptedOrDeleted(bool directory)
    {
        using var f = new LayoutFixture();
        string target = Directory.CreateDirectory(Path.Combine(f.Root, "target")).FullName;
        string path = Path.Combine(f.Client, "ordinary");
        if (directory) Directory.CreateDirectory(path); else File.WriteAllText(path, "keep ordinary file");
        Assert.ThrowsAny<IOException>(() => ClientDirectoryJunction.CreateNew(path, target));
        Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.IsExact(path, target));
        Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.DeleteExact(path, target));
        Assert.Equal(directory, Directory.Exists(path));
        if (!directory) Assert.Equal("keep ordinary file", File.ReadAllText(path));
    }

    [WindowsFact]
    public void DifferentActualJunctionTargetIsNotRetargetedOrDeleted()
    {
        using var f = new LayoutFixture();
        string first = Directory.CreateDirectory(Path.Combine(f.Root, "first")).FullName;
        string other = Directory.CreateDirectory(Path.Combine(f.Root, "other")).FullName;
        File.WriteAllText(Path.Combine(first, "keep"), "first"); File.WriteAllText(Path.Combine(other, "keep"), "other");
        string link = Path.Combine(f.Client, "link"); f.Junction(link, first);
        Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.IsExact(link, other));
        Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.DeleteExact(link, other));
        Assert.True(ClientDirectoryJunction.IsExact(link, first));
        Assert.Equal("first", File.ReadAllText(Path.Combine(first, "keep")));
        Assert.Equal("other", File.ReadAllText(Path.Combine(other, "keep")));
    }

    [WindowsFact]
    public void LinkedTargetAndLinkedLinkAncestorAreRefusedWithoutCreatingAnotherEntry()
    {
        using var f = new LayoutFixture();
        string target = Directory.CreateDirectory(Path.Combine(f.Root, "target")).FullName;
        string other = Directory.CreateDirectory(Path.Combine(f.Root, "other")).FullName;
        string linkedTarget = Path.Combine(f.Client, "target-link"); f.Junction(linkedTarget, target);
        string proposed = Path.Combine(f.Client, "proposed");
        Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.CreateNew(proposed, linkedTarget));
        string linkedParent = Path.Combine(f.Client, "parent-link"); f.Junction(linkedParent, other);
        Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.CreateNew(Path.Combine(linkedParent, "proposed"), target));
        Assert.False(Directory.Exists(proposed));
        Assert.Empty(Directory.EnumerateFileSystemEntries(other));
    }

    [WindowsTheory]
    [InlineData("relative")]
    [InlineData("traversal")]
    [InlineData("overlap")]
    [InlineData("forward-slash")]
    public void LexicalEscapeAndCyclesAreRefusedBeforeCreation(string kind)
    {
        using var f = new LayoutFixture();
        string target = Directory.CreateDirectory(Path.Combine(f.Root, "target")).FullName;
        string link = kind switch
        {
            "relative" => "relative-junction",
            "traversal" => Path.Combine(f.Client, "..", "outside"),
            "forward-slash" => Path.Combine(f.Client, "refused-slash").Replace('\\', '/'),
            _ => Path.Combine(target, "cycle")
        };
        Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.CreateNew(link, target));
        if (kind == "forward-slash")
        {
            Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.IsExact(link, target));
            Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.DeleteExact(link, target));
            Assert.False(Directory.Exists(Path.Combine(f.Client, "refused-slash")));
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        Assert.False(Directory.Exists(Path.Combine(f.Root, "outside")));
    }

    [WindowsFact]
    public void DirectorySymbolicLinkIsNotAcceptedAsAMountPointJunction()
    {
        using var f = new LayoutFixture();
        string target = Directory.CreateDirectory(Path.Combine(f.Root, "target")).FullName;
        string link = Path.Combine(f.Client, "wrong-tag");
        // This intentionally needs existing Windows symlink privilege; the core junction tests do not.
        Directory.CreateSymbolicLink(link, target);
        try
        {
            Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.IsExact(link, target));
            Assert.Throws<InvalidDataException>(() => ClientDirectoryJunction.DeleteExact(link, target));
            Assert.True(ExistsReparse(link));
        }
        finally { Directory.Delete(link); } // Remove only the owned symbolic link, never target contents.
        Assert.True(Directory.Exists(target));
    }
}
