using System.Reflection;
using System.Text;
using System.Text.Json;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    private const string NativeObjRoot = "data/map/mapobj/archer/obj";
    private const string NativeObjLeaf = NativeObjRoot + "/sprite.dds";
    private const string NativeLogsBitmap = "data/main/LogS.BMP";

    private static NativeAssetReview MakeNativeReview(Fixture f, string path,
        string? collision = null, string? category = null, string? nodeType = null)
    {
        bool bitmap = path == NativeLogsBitmap;
        byte[] bytes = File.ReadAllBytes(Path.Combine(f.Source, path));
        return new(path, collision ?? (bitmap ? path : NativeObjRoot),
            nodeType ?? (bitmap ? "file" : "directory"),
            category ?? (bitmap ? "native-logs-bitmap" : "native-map-object-directory"),
            bytes.LongLength, Fixture.Hash(bytes), "fixture-review-001");
    }

    private static void AssertNativeReviewRefuses(Fixture f, PublishEntry[] entries,
        IEnumerable<NativeAssetReview>? reviews)
    {
        string feed = ExistingFeed(f);
        var before = Directory.GetFiles(f.Source, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, path => (Bytes: File.ReadAllBytes(path), Stamp: File.GetLastWriteTimeUtc(path)));

        var error = Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(
            f.Options(bundle: true), entries, f.Key, Fixture.KeyId, reviews));

        Assert.DoesNotContain(NativeObjRoot, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(NativeLogsBitmap, error.Message, StringComparison.OrdinalIgnoreCase);
        AssertQuarantinePublicationUnchanged(f, feed);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(f.Source, "*", SearchOption.AllDirectories).Order());
        foreach (var pair in before)
        {
            Assert.Equal(pair.Value.Bytes, File.ReadAllBytes(pair.Key));
            Assert.Equal(pair.Value.Stamp, File.GetLastWriteTimeUtc(pair.Key));
        }
    }

    private static Dictionary<string, object?> NativeReviewPlan(Fixture f, int schema,
        PublishEntry[] entries, object? reviews, bool includeReviews = true)
    {
        var plan = new Dictionary<string, object?>
        {
            ["schemaVersion"] = schema, ["sourceRoot"] = f.Source, ["publishRoot"] = f.Publish,
            ["productId"] = "game", ["channel"] = "stable", ["sequence"] = 1,
            ["contentBaseUrl"] = "https://original.example.test/content/",
            ["createdUtc"] = Fixture.Now.AddMinutes(-1).ToString("O"),
            ["expiresUtc"] = Fixture.Now.AddHours(1).ToString("O"), ["keyId"] = Fixture.KeyId,
            ["files"] = entries, ["includeBundle"] = true, ["allowLoopbackHttp"] = false,
        };
        if (includeReviews) plan["nativeAssetReviews"] = reviews;
        return plan;
    }

    private static string WriteNativeReviewPlan(Fixture f, object plan)
    {
        string path = Path.Combine(f.Root, "native-review-plan.json");
        File.WriteAllText(path, JsonSerializer.Serialize(plan,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), new UTF8Encoding(false));
        return path;
    }

    private static Func<string, string[], TextWriter, CancellationToken, Task<int>> NativeReviewCommand()
    {
        var type = Assembly.Load("WolfConquer.Updater").GetType(
            "WolfConquer.Updater.PublishingCommand", throwOnError: true)!;
        var method = type.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.CreateDelegate<Func<string, string[], TextWriter, CancellationToken, Task<int>>>();
    }
}
