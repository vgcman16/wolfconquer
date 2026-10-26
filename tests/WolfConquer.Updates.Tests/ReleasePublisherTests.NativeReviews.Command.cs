using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [Fact]
    public async Task ActualSchema2PublishCommandSignsExactlyReviewedOwnedFixtureBytes()
    {
        using var f = new Fixture(); f.Put(NativeObjLeaf, "synthetic CLI reviewed asset");
        var review = MakeNativeReview(f, NativeObjLeaf);
        string plan = WriteNativeReviewPlan(f, NativeReviewPlan(f, 2, [new(NativeObjLeaf, null)], new[] { review }));
        string key = Path.Combine(f.Root, "ephemeral-signing.pem");
        File.WriteAllText(key, f.Key.ExportPkcs8PrivateKeyPem(), new UTF8Encoding(false));
        byte[] keyBefore = File.ReadAllBytes(key);
        byte[] planBefore = File.ReadAllBytes(plan);
        byte[] sourceBefore = File.ReadAllBytes(Path.Combine(f.Source, NativeObjLeaf));
        using var output = new StringWriter();

        int result = await NativeReviewCommand()("publish", ["--plan", plan, "--key", key], output, CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Contains("Published game/stable sequence 1: 1 curated files.", output.ToString());
        string feed = Path.Combine(f.Publish, "feeds", "game-stable.json");
        var index = f.Verify(File.ReadAllBytes(feed), index: true);
        var pointer = Assert.Single(index.Manifest.Files);
        var manifest = f.Verify(File.ReadAllBytes(f.Blob(pointer.Sha256)));
        var file = Assert.Single(manifest.Manifest.Files);
        Assert.Equal(NativeObjLeaf, file.Path);
        Assert.Equal(review.Length, file.Length);
        Assert.Equal(review.ExpectedSourceSha256, file.Sha256);
        Assert.Equal(sourceBefore, File.ReadAllBytes(f.Blob(file.Sha256)));
        Assert.Equal(sourceBefore, File.ReadAllBytes(Path.Combine(f.Source, NativeObjLeaf)));
        Assert.Equal(planBefore, File.ReadAllBytes(plan));
        Assert.Equal(keyBefore, File.ReadAllBytes(key));
        Assert.Empty(Directory.GetFiles(f.Publish, "*.tmp", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("schema1-reviews")]
    [InlineData("schema1-null")]
    [InlineData("schema1-empty")]
    [InlineData("schema2-missing")]
    [InlineData("schema2-null")]
    [InlineData("schema2-empty")]
    [InlineData("schema2-extra-field")]
    [InlineData("schema2-missing-digest")]
    [InlineData("schema2-string-length")]
    [InlineData("schema2-duplicate-row")]
    public async Task InvalidReviewPlanShapeIsRejectedBeforeAnAbsentSigningKey(string kind)
    {
        using var f = new Fixture(); f.Put(NativeObjLeaf, "synthetic CLI refused asset");
        var review = MakeNativeReview(f, NativeObjLeaf);
        bool schema1 = kind.StartsWith("schema1", StringComparison.Ordinal);
        object? reviews = new[] { review };
        if (kind.EndsWith("-null", StringComparison.Ordinal)) reviews = null;
        if (kind.EndsWith("-empty", StringComparison.Ordinal)) reviews = Array.Empty<NativeAssetReview>();
        if (kind == "schema2-duplicate-row") reviews = new[] { review, review };
        var plan = NativeReviewPlan(f, schema1 ? 1 : 2, [new(NativeObjLeaf, null)], reviews,
            includeReviews: kind != "schema2-missing");
        if (kind is "schema2-extra-field" or "schema2-missing-digest" or "schema2-string-length")
        {
            var row = JsonSerializer.SerializeToNode(review,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsObject();
            if (kind == "schema2-extra-field") row["allowAll"] = true;
            else if (kind == "schema2-missing-digest") row.Remove("expectedSourceSha256");
            else row["length"] = "not-an-integer";
            plan["nativeAssetReviews"] = new JsonArray(row);
        }
        string planPath = WriteNativeReviewPlan(f, plan);
        string absentKey = Path.Combine(f.Root, "absent-ephemeral-signing.pem");
        byte[] planBefore = File.ReadAllBytes(planPath);
        byte[] sourceBefore = File.ReadAllBytes(Path.Combine(f.Source, NativeObjLeaf));
        string feed = ExistingFeed(f);
        using var output = new StringWriter();

        Exception error = await Assert.ThrowsAnyAsync<Exception>(() => NativeReviewCommand()("publish",
            ["--plan", planPath, "--key", absentKey], output, CancellationToken.None));

        Assert.True(error is InvalidDataException or JsonException, error.GetType().FullName);
        Assert.DoesNotContain(absentKey, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(absentKey));
        Assert.Empty(output.ToString());
        Assert.Equal(planBefore, File.ReadAllBytes(planPath));
        Assert.Equal(sourceBefore, File.ReadAllBytes(Path.Combine(f.Source, NativeObjLeaf)));
        AssertQuarantinePublicationUnchanged(f, feed);
    }
}
