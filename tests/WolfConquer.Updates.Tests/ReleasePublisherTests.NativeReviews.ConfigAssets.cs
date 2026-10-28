using System.Text;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    private static NativeAssetReview ConfigReview(string path, string category, byte[] bytes) =>
        new(path, path, "file", category, bytes.LongLength, Fixture.Hash(bytes), "synthetic-review-01");

    [Theory]
    [InlineData("ini/userdata.lua", "native-server-type-lua")]
    [InlineData("ini/luacfg/userdata.dat", "native-luacfg-userdata-catalog")]
    public async Task ExactConfigReviewPublishesSyntheticBytesThroughSignedCommand(string path, string category)
    {
        using var f = new Fixture();
        const string content = "synthetic config fixture 123";
        byte[] expected = Encoding.UTF8.GetBytes(content);
        f.Put(path, content);
        var review = ConfigReview(path, category, expected);
        string prior = new('B', 64);
        string plan = WriteNativeReviewPlan(f, NativeReviewPlan(f, 2, [new(path, prior)], new[] { review }));
        string key = Path.Combine(f.Root, "ephemeral.pem");
        File.WriteAllText(key, f.Key.ExportPkcs8PrivateKeyPem(), new UTF8Encoding(false));
        using var output = new StringWriter();
        Assert.Equal(0, await NativeReviewCommand()("publish", ["--plan", plan, "--key", key], output, CancellationToken.None));
        var index = f.Verify(File.ReadAllBytes(Path.Combine(f.Publish, "feeds", "game-stable.json")), index: true);
        var full = f.Verify(File.ReadAllBytes(f.Blob(Assert.Single(index.Manifest.Files).Sha256)));
        var file = Assert.Single(full.Manifest.Files);
        Assert.Equal(path, file.Path); Assert.Equal(expected.LongLength, file.Length);
        Assert.Equal(Fixture.Hash(expected), file.Sha256); Assert.Equal(prior, file.BaseSha256);
        Assert.NotEqual(prior, file.Sha256); Assert.Equal(expected, File.ReadAllBytes(f.Blob(file.Sha256)));
        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(f.Source, path)));
    }

    [Theory]
    [InlineData("ini/userdata.lua", "native-server-type-lua", 65536)]
    [InlineData("ini/luacfg/userdata.dat", "native-luacfg-userdata-catalog", 1048576)]
    public void ConfigReviewAcceptsBoundaryLengthsAndRejectsOutsideBounds(string path, string category, int maximum)
    {
        foreach (int length in new[] { 0, 1, maximum, maximum + 1 })
        {
            using var f = new Fixture(); f.Put(path, new string('x', length));
            byte[] bytes = Encoding.UTF8.GetBytes(new string('x', length));
            var review = ConfigReview(path, category, bytes);
            if (length is 0 || length > maximum) AssertNativeReviewRefuses(f, [new(path, null)], [review]);
            else
            {
                var result = ReleasePublisher.Publish(f.Options(), [new(path, null)], f.Key, Fixture.KeyId, [review]);
                var file = Assert.Single(f.Verify(result.FullEnvelope).Manifest.Files);
                Assert.Equal(bytes, File.ReadAllBytes(f.Blob(file.Sha256)));
            }
        }
    }

    [Theory]
    [InlineData("ini/userdata.lua", "native-server-type-lua")]
    [InlineData("ini/luacfg/userdata.dat", "native-luacfg-userdata-catalog")]
    public void ConfigReviewRequiresExactIdentityAndBytes(string path, string category)
    {
        foreach (string fault in new[] { "missing", "case-path", "case-collision", "collision", "node", "category", "zero", "wrong-length", "digest", "evidence", "duplicate", "mutated" })
        {
            using var f = new Fixture(); f.Put(path, "ABCDEF");
            var review = ConfigReview(path, category, Encoding.UTF8.GetBytes("ABCDEF"));
            var reviews = new[] { review };
            switch (fault)
            {
                case "missing": reviews = []; break;
                case "case-path": review = review with { Path = path.ToUpperInvariant() }; break;
                case "case-collision": review = review with { CollisionPath = path.ToUpperInvariant() }; break;
                case "collision": review = review with { CollisionPath = "ini/other.dat" }; break;
                case "node": review = review with { CollisionNodeType = "directory" }; break;
                case "category": review = review with { Category = category == "native-server-type-lua" ? "native-luacfg-userdata-catalog" : "native-server-type-lua" }; break;
                case "zero": review = review with { Length = 0 }; break;
                case "wrong-length": review = review with { Length = 5 }; break;
                case "digest": review = review with { ExpectedSourceSha256 = new string('A', 64) }; break;
                case "evidence": review = review with { Evidence = "" }; break;
                case "duplicate": reviews = [review, review]; break;
                case "mutated":
                    string source = Path.Combine(f.Source, path); DateTime stamp = File.GetLastWriteTimeUtc(source);
                    f.Put(path, "UVWXYZ"); File.SetLastWriteTimeUtc(source, stamp); break;
            }
            if (fault is not ("missing" or "duplicate")) reviews = [review];
            AssertNativeReviewRefuses(f, [new(path, new string('B', 64))], reviews);
        }
        Assert.Throws<InvalidDataException>(() => PublicationPathPolicy.ValidateRelativePath(path));
    }

    [Theory]
    [InlineData("ini/token_type.dat")]
    [InlineData("ini/userdata/other.dat")]
    [InlineData("ini/userdata2.lua")]
    [InlineData("ini/account/userdata.lua")]
    [InlineData("ini/private/userdata.lua")]
    [InlineData("ini/userdata.lua.bak")]
    public void ConfigCategoriesDoNotWaiveOtherQuarantinedNames(string path)
    {
        using var f = new Fixture(); f.Put(path, "synthetic");
        var review = ConfigReview(path, "native-server-type-lua", Encoding.UTF8.GetBytes("synthetic"));
        AssertNativeReviewRefuses(f, [new(path, null)], [review]);
    }
}
