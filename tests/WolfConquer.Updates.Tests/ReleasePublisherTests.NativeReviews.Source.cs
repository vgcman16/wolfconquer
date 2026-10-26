using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [Theory]
    [InlineData("private-key.dds")]
    [InlineData("accounts/texture.dds")]
    [InlineData("token.dds")]
    [InlineData("cache/texture.dds")]
    [InlineData("nested/obj/texture.dds")]
    [InlineData("AutoP.exe")]
    public void ReviewedObjAncestorCannotHideAnotherRiskyDescendant(string suffix)
    {
        using var f = new Fixture();
        string second = NativeObjRoot + "/" + suffix;
        f.Put(NativeObjLeaf, "synthetic ordinary reviewed asset");
        f.Put(second, "synthetic risky descendant; no private material");
        AssertNativeReviewRefuses(f, [new(NativeObjLeaf, null), new(second, null)],
            [MakeNativeReview(f, NativeObjLeaf), MakeNativeReview(f, second)]);
    }

    [Theory]
    [InlineData("exe")]
    [InlineData("dat")]
    [InlineData("txt")]
    public void ObjReviewDoesNotAdmitAnArbitraryFileExtension(string extension)
    {
        using var f = new Fixture();
        string path = NativeObjRoot + "/asset." + extension;
        f.Put(path, "synthetic unsupported kind");
        AssertNativeReviewRefuses(f, [new(path, null)], [MakeNativeReview(f, path)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewUnreviewedFileOrEmptyDirectoryInvalidatesObjMembership(bool directory)
    {
        using var f = new Fixture(); f.Put(NativeObjLeaf, "synthetic reviewed asset");
        var review = MakeNativeReview(f, NativeObjLeaf);
        string extra = NativeObjRoot + (directory ? "/new-empty" : "/new.dds");
        if (directory) Directory.CreateDirectory(Path.Combine(f.Source, extra));
        else f.Put(extra, "new unreviewed synthetic sibling");

        AssertNativeReviewRefuses(f, [new(NativeObjLeaf, null)], [review]);
        Assert.True(directory ? Directory.Exists(Path.Combine(f.Source, extra)) : File.Exists(Path.Combine(f.Source, extra)));
    }

    [Theory]
    [InlineData("same-length-restored-time")]
    [InlineData("changed-length")]
    [InlineData("wrong-digest")]
    public void ReviewedSourceBytesMustMatchBeforeBlobsOrFeedChanges(string kind)
    {
        using var f = new Fixture(); f.Put(NativeObjLeaf, "ABCDEF");
        string path = Path.Combine(f.Source, NativeObjLeaf);
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        var review = MakeNativeReview(f, NativeObjLeaf);
        if (kind == "same-length-restored-time") { f.Put(NativeObjLeaf, "UVWXYZ"); File.SetLastWriteTimeUtc(path, stamp); }
        else if (kind == "changed-length") f.Put(NativeObjLeaf, "changed length");
        else review = review with { ExpectedSourceSha256 = new string('A', 64) };

        AssertNativeReviewRefuses(f, [new(NativeObjLeaf, null)], [review]);
    }

    [WindowsTheory]
    [InlineData("leaf")]
    [InlineData("collision")]
    public void ReviewedPathsMustMatchActualWindowsSpelling(string part)
    {
        using var f = new Fixture();
        string actual = part == "leaf" ? NativeObjRoot + "/Sprite.dds" : NativeObjLeaf.Replace("/obj/", "/Obj/");
        f.Put(actual, "synthetic case-sensitive review identity");
        var review = MakeNativeReview(f, actual) with { Path = NativeObjLeaf, CollisionPath = NativeObjRoot };
        AssertNativeReviewRefuses(f, [new(NativeObjLeaf, null)], [review]);
    }

    [Theory]
    [InlineData("data/main/logs.bmp")]
    [InlineData("data/main/LogS2.BMP")]
    [InlineData("other/LogS.BMP")]
    [InlineData("ini/userdata.lua")]
    [InlineData("ini/luacfg/userdata.dat")]
    [InlineData("ini/token_type.dat")]
    public void BitmapCategoryIsNotAGenericFilenameRiskWaiver(string path)
    {
        using var f = new Fixture(); f.Put(path, "synthetic nonapproved path");
        var review = MakeNativeReview(f, path, collision: path, category: "native-logs-bitmap", nodeType: "file");
        AssertNativeReviewRefuses(f, [new(path, null)], [review]);
    }

    [Theory]
    [InlineData("data/map/mapobj/obj/asset.dds", "data/map/mapobj/obj")]
    [InlineData("data/map/mapobj/archer/obj2/asset.dds", "data/map/mapobj/archer/obj2")]
    [InlineData("assets/mapobj/archer/obj/asset.dds", "assets/mapobj/archer/obj")]
    [InlineData("data/map/mapobj/archer/bin/asset.dds", "data/map/mapobj/archer/bin")]
    [InlineData("data/map/mapobj/archer/Obj/asset.dds", "data/map/mapobj/archer/Obj")]
    [InlineData("data/map/mapobj/private-keys/obj/asset.dds", "data/map/mapobj/private-keys/obj")]
    public void MapReviewRequiresTheExactNativeCollisionAndNoOtherAncestorRisk(string path, string collision)
    {
        using var f = new Fixture(); f.Put(path, "synthetic out-of-category asset");
        AssertNativeReviewRefuses(f, [new(path, null)], [MakeNativeReview(f, path, collision)]);
    }
}
