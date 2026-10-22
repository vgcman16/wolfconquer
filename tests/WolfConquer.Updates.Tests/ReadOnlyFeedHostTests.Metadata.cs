using WolfConquer.Updates.Hosting;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReadOnlyFeedHostTests
{
    [Fact]
    public async Task HeadIsMetadataOnlyEvenForColdCorruptSameLengthBlobAndDoesNotWarmGetCache()
    {
        await using var f = await Fixture.StartAsync();
        File.WriteAllBytes(Path.Combine(f.Publish, "blobs", f.Hash), new byte[f.BlobBytes.Length]);
        var head = await f.RawAsync("HEAD", "/blobs/" + f.Hash);
        Assert.Equal(200, head.Status);
        Assert.Contains("Content-Length: 64\r\n", head.Headers);
        Assert.Contains("ETag: \"" + f.Hash + "\"\r\n", head.Headers);
        Assert.Empty(head.Body);
        var conditional = await f.RawAsync("HEAD", "/blobs/" + f.Hash, "If-None-Match: \"" + f.Hash + "\"\r\n");
        Assert.Equal(304, conditional.Status);
        Assert.Equal(0, f.Host.BlobHashPassCount);
        var get = await f.RawAsync("GET", "/blobs/" + f.Hash);
        Assert.Equal(404, get.Status);
        Assert.DoesNotContain("ETag:", get.Headers);
        Assert.Equal(1, f.Host.BlobHashPassCount);
    }

    [Fact]
    public async Task SuccessfulGetAndSubsequentRangeAndConditionalGetReuseStableWindowsIdentity()
    {
        await using var f = await Fixture.StartAsync();
        var first = await f.RawAsync("GET", "/blobs/" + f.Hash);
        Assert.Equal(200, first.Status);
        Assert.Equal(f.BlobBytes, first.Body);
        var range = await f.RawAsync("GET", "/blobs/" + f.Hash, "Range: bytes=2-4\r\n");
        Assert.Equal(206, range.Status);
        Assert.Equal(new byte[] { 2, 3, 4 }, range.Body);
        var conditional = await f.RawAsync("GET", "/blobs/" + f.Hash, "If-None-Match: \"" + f.Hash + "\"\r\n");
        Assert.Equal(304, conditional.Status);
        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 3, f.Host.BlobHashPassCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedLengthOrWriteTimeInvalidatesSuccessfulVerification(bool sameLength)
    {
        await using var f = await Fixture.StartAsync();
        Assert.Equal(200, (await f.RawAsync("GET", "/blobs/" + f.Hash)).Status);
        var path = Path.Combine(f.Publish, "blobs", f.Hash);
        var originalTime = File.GetLastWriteTimeUtc(path);
        File.WriteAllBytes(path, new byte[sameLength ? 64 : 63]);
        File.SetLastWriteTimeUtc(path, originalTime.AddMinutes(1));
        var corrupt = await f.RawAsync("GET", "/blobs/" + f.Hash);
        Assert.Equal(404, corrupt.Status);
        Assert.DoesNotContain("ETag:", corrupt.Headers);
        Assert.Equal(2, f.Host.BlobHashPassCount);
    }

    [Fact]
    public async Task ReplacedFileWithSameLengthAndWriteTimeCannotReusePreviousFileIdentity()
    {
        await using var f = await Fixture.StartAsync();
        Assert.Equal(200, (await f.RawAsync("GET", "/blobs/" + f.Hash)).Status);
        var path = Path.Combine(f.Publish, "blobs", f.Hash);
        var writeTime = File.GetLastWriteTimeUtc(path);
        var replacement = Path.Combine(f.Root, "replacement blob");
        File.WriteAllBytes(replacement, new byte[64]);
        File.SetLastWriteTimeUtc(replacement, writeTime);
        File.Move(replacement, path, overwrite: true);
        Assert.Equal(writeTime, File.GetLastWriteTimeUtc(path));
        Assert.Equal(404, (await f.RawAsync("GET", "/blobs/" + f.Hash)).Status);
        Assert.Equal(2, f.Host.BlobHashPassCount);
    }

    [Theory]
    [InlineData("relative-publish")]
    [InlineData("\\\\server\\share\\curated")]
    [InlineData("//server/share/curated")]
    public async Task PublishRootMustBeExplicitAbsoluteLocalCuratedDirectory(string root)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ReadOnlyFeedHost.StartAsync(new(root, 0)));
    }

    [Fact]
    public async Task VolumeRootIsNotACuratedPublishDirectory()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))!;
        await Assert.ThrowsAsync<ArgumentException>(() => ReadOnlyFeedHost.StartAsync(new(root, 0)));
        await Assert.ThrowsAsync<ArgumentException>(() => ReadOnlyFeedHost.StartAsync(new(Path.Combine(root, "."), 0)));
    }

    [Theory]
    [InlineData("WolfConquer-preview_test.json")]
    [InlineData("wolf..client.preview_test.json")]
    public async Task PublisherIdentifierGrammarAllowsCaseUnderscoresAndFilenameDots(string basename)
    {
        await using var f = await Fixture.StartAsync();
        File.WriteAllBytes(Path.Combine(f.Publish, "feeds", basename), f.FeedBytes);
        var response = await f.RawAsync("GET", "/feeds/" + basename);
        Assert.Equal(200, response.Status);
        Assert.Equal(f.FeedBytes, response.Body);
    }
}
