using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [Theory]
    [InlineData("wrong-path")]
    [InlineData("wrong-case-path")]
    [InlineData("wrong-case-collision")]
    [InlineData("wrong-node-type")]
    [InlineData("wrong-category")]
    [InlineData("outside-native-root")]
    [InlineData("traversal")]
    [InlineData("negative-length")]
    [InlineData("too-large-length")]
    [InlineData("short-digest")]
    [InlineData("nonhex-digest")]
    [InlineData("empty-evidence")]
    [InlineData("long-evidence")]
    [InlineData("path-evidence")]
    [InlineData("space-evidence")]
    [InlineData("null-path")]
    [InlineData("null-category")]
    [InlineData("null-digest")]
    public void MalformedNativeReviewCannotAdmitAnExistingQuarantinedAsset(string kind)
    {
        using var f = new Fixture(); f.Put(NativeObjLeaf, "synthetic reviewed asset");
        var review = MakeNativeReview(f, NativeObjLeaf);
        review = kind switch
        {
            "wrong-path" => review with { Path = NativeObjRoot + "/other.dds" },
            "wrong-case-path" => review with { Path = NativeObjLeaf.ToUpperInvariant() },
            "wrong-case-collision" => review with { CollisionPath = NativeObjRoot.Replace("/obj", "/Obj") },
            "wrong-node-type" => review with { CollisionNodeType = "file" },
            "wrong-category" => review with { Category = "approved-private-file" },
            "outside-native-root" => review with { CollisionPath = "other/obj" },
            "traversal" => review with { CollisionPath = "data/map/mapobj/../obj" },
            "negative-length" => review with { Length = -1 },
            "too-large-length" => review with { Length = 4_294_967_297L },
            "short-digest" => review with { ExpectedSourceSha256 = new string('A', 63) },
            "nonhex-digest" => review with { ExpectedSourceSha256 = new string('G', 64) },
            "empty-evidence" => review with { Evidence = "" },
            "long-evidence" => review with { Evidence = new string('A', 129) },
            "path-evidence" => review with { Evidence = "review/assets" },
            "space-evidence" => review with { Evidence = "review 001" },
            "null-path" => review with { Path = null! },
            "null-category" => review with { Category = null! },
            "null-digest" => review with { ExpectedSourceSha256 = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        AssertNativeReviewRefuses(f, [new(NativeObjLeaf, null)], [review]);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("case-alias")]
    [InlineData("unused")]
    [InlineData("null-row")]
    [InlineData("empty")]
    public void NativeReviewCollectionMustMatchThePayloadExactly(string kind)
    {
        using var f = new Fixture(); f.Put(NativeObjLeaf, "synthetic reviewed asset");
        var review = MakeNativeReview(f, NativeObjLeaf);
        f.Put(NativeLogsBitmap, "synthetic unlisted reviewed bitmap");
        NativeAssetReview[] reviews = kind switch
        {
            "duplicate" => [review, review],
            "case-alias" => [review, review with { Path = review.Path.Replace("/sprite.dds", "/SPRITE.DDS", StringComparison.Ordinal) }],
            "unused" => [review, MakeNativeReview(f, NativeLogsBitmap)],
            "null-row" => [null!],
            "empty" => [],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        AssertNativeReviewRefuses(f, [new(NativeObjLeaf, null)], reviews);
    }

    [Theory]
    [InlineData("data/map/mapobj/archer/obj/sprite.dds")]
    [InlineData("data/main/LogS.BMP")]
    public void LegacyPublisherArgumentsStillRefuseCollisionNamesWithoutReview(string path)
    {
        using var f = new Fixture(); f.Put(path, "synthetic quarantined asset");
        AssertNativeReviewRefuses(f, [new(path, null)], null);
    }

    [Fact]
    public void ReviewEnumerationStopsAtThe4097thRowBeforeAnySourceRead()
    {
        using var f = new Fixture();
        int read = 0;
        PublishEntry[] entries = Enumerable.Range(0, 4097)
            .Select(index => new PublishEntry(NativeObjRoot + $"/asset-{index:D4}.dds", null)).ToArray();
        IEnumerable<NativeAssetReview> Reviews()
        {
            foreach (var entry in entries)
            {
                read++;
                yield return new(entry.Path, NativeObjRoot, "directory", "native-map-object-directory",
                    1, new string('A', 64), "fixture-bound-review");
            }
            throw new InvalidOperationException("Publisher enumerated beyond its review bound.");
        }

        AssertNativeReviewRefuses(f, entries, Reviews());
        Assert.Equal(4097, read);
    }
}
