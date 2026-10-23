using System.Reflection;
using System.Text.Json;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Tests;

public sealed class CharSuffixRemainderManifestTests
{
    [Theory]
    [InlineData("2026-10-23T03:00:00Z", true)]
    [InlineData("2026-10-23T03:00:00+00:00", true)]
    [InlineData("2026-10-23T03:00:00z", false)]
    [InlineData("2026-10-23T03:00:00+01:00", false)]
    [InlineData("2026-10-23T03:00:00", false)]
    [InlineData("2026-10-23T03:00:00Z ", false)]
    [InlineData("2026-10-23T03:00:00Ｚ", false)]
    [InlineData("not-a-dateZ", false)]
    public void ManifestAndPublicationTimestampsRetainExactUtcSuffixChecks(string value, bool accepted)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            productId = "wolf-prefix-fixture",
            channel = "stable",
            sequence = 1,
            createdUtc = value,
            expiresUtc = "2026-10-24T03:00:00Z",
            contentBaseUrl = "https://updates.example.test/releases/",
            files = Array.Empty<object>(),
        });
        // Only the actual pure private parser is invoked. PublishingCommand.Run,
        // key loading, signing, writing and publishing are never called here.
        var command = Assembly.Load("WolfConquer.Updater").GetType(
            "WolfConquer.Updater.PublishingCommand", throwOnError: true)!;
        var parseUtc = command.GetMethod("ParseUtc", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(parseUtc);

        if (accepted)
        {
            var expected = new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero);
            Assert.Equal(expected, ManifestJson.ReadPayload(payload).CreatedUtc);
            Assert.Equal(expected, Assert.IsType<DateTimeOffset>(parseUtc.Invoke(null, [value])));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => ManifestJson.ReadPayload(payload));
            var invocation = Assert.Throws<TargetInvocationException>(() => parseUtc.Invoke(null, [value]));
            Assert.IsType<InvalidDataException>(invocation.InnerException);
        }
    }

    [Theory]
    [InlineData("https://updates.example.test/releases/", true)]
    [InlineData("https://updates.example.test/releases", false)]
    [InlineData("https://updates.example.test/releases/%2F", false)]
    [InlineData("https://updates.example.test/releases/／", false)]
    [InlineData("https://updates.example.test/releases/?q=1", false)]
    [InlineData("https://updates.example.test/releases/#fragment", false)]
    public void ContentBaseRequiresLiteralDirectorySlashWithoutQueryOrFragment(string value, bool accepted)
    {
        var now = new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero);
        var manifest = new UpdateManifest(1, "wolf-prefix-fixture", "stable", 1,
            now, now.AddDays(1), new Uri(value), []);

        if (accepted)
            ManifestValidation.Validate(manifest, allowLoopbackHttp: false);
        else
            Assert.Throws<InvalidDataException>(() => ManifestValidation.Validate(manifest, allowLoopbackHttp: false));
    }
}
