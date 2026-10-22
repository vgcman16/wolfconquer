using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using WolfConquer.Updates.Hosting;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReadOnlyFeedHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HealthAndFeedSupportGetAndHeadWithoutPublishingFilesystemPaths(bool head)
    {
        await using var f = await Fixture.StartAsync();
        using var health = await f.Client.SendAsync(new HttpRequestMessage(head ? HttpMethod.Head : HttpMethod.Get, "health"));
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(15, health.Content.Headers.ContentLength);
        Assert.Equal(head ? "" : "{\"status\":\"ok\"}", await health.Content.ReadAsStringAsync());
        using var feed = await f.Client.SendAsync(new HttpRequestMessage(head ? HttpMethod.Head : HttpMethod.Get, "feeds/client-stable.json"));
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        Assert.True(feed.Headers.CacheControl!.Private);
        Assert.True(feed.Headers.CacheControl.NoStore);
        Assert.Equal(f.FeedBytes.Length, feed.Content.Headers.ContentLength);
        Assert.Equal(head ? Array.Empty<byte>() : f.FeedBytes, await feed.Content.ReadAsByteArrayAsync());
        Assert.Equal("127.0.0.1", f.Host.BaseAddress.Host);
    }

    [Fact]
    public async Task BlobHasValidatedContentAddressImmutableCacheAndConditionalEtag()
    {
        await using var f = await Fixture.StartAsync();
        using var response = await f.Client.GetAsync("blobs/" + f.Hash);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(f.BlobBytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("\"" + f.Hash + "\"", response.Headers.ETag!.Tag);
        Assert.Contains("immutable", response.Headers.CacheControl!.ToString());
        using var conditional = new HttpRequestMessage(HttpMethod.Get, "blobs/" + f.Hash);
        conditional.Headers.IfNoneMatch.Add(response.Headers.ETag);
        using var unchanged = await f.Client.SendAsync(conditional);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        Assert.Empty(await unchanged.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("bytes=0-2", 0, 3)]
    [InlineData("bytes=60-", 60, 4)]
    [InlineData("bytes=-4", 60, 4)]
    public async Task BlobSingleRangesHaveExactBodyLengthAndContentRange(string range, int offset, int count)
    {
        await using var f = await Fixture.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "blobs/" + f.Hash);
        request.Headers.Range = RangeHeaderValue.Parse(range);
        using var response = await f.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(count, response.Content.Headers.ContentLength);
        Assert.Equal($"bytes {offset}-{offset + count - 1}/64", response.Content.Headers.ContentRange!.ToString());
        Assert.Equal(f.BlobBytes.AsSpan(offset, count).ToArray(), await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("bytes=999-")]
    [InlineData("bytes=3-2")]
    [InlineData("bytes=0-1,3-4")]
    [InlineData("bytes=-0")]
    [InlineData("items=0-3")]
    public async Task InvalidOrMultipleRangesAreBoundedAndRefused(string range)
    {
        await using var f = await Fixture.StartAsync();
        var response = await f.RawAsync("GET", "/blobs/" + f.Hash, "Range: " + range + "\r\n");
        Assert.Equal(416, response.Status);
        Assert.Contains("Content-Range: bytes */64\r\n", response.Headers);
        Assert.Empty(response.Body);
    }

    [Fact]
    public async Task AllowlistedBootstrapSupportsHeadAndRangesButOtherFilesStayUnavailable()
    {
        await using var f = await Fixture.StartAsync();
        using var head = await f.Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "bootstrap/setup.zip"));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(4, head.Content.Headers.ContentLength);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
        var ranged = await f.RawAsync("HEAD", "/bootstrap/setup.zip", "Range: bytes=1-2\r\n");
        Assert.Equal(206, ranged.Status);
        Assert.Contains("Content-Length: 2\r\n", ranged.Headers);
        Assert.Empty(ranged.Body);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.GetAsync("bootstrap/not-allowed.exe")).StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/private.txt")]
    [InlineData("/feeds/../private.txt")]
    [InlineData("/feeds/%2e%2e/private.txt")]
    [InlineData("/feeds/%252e%252e/private.txt")]
    [InlineData("/feeds\\client-stable.json")]
    [InlineData("/feeds/client-stable.json?download=1")]
    [InlineData("/feeds/client-stable.json#fragment")]
    [InlineData("/bootstrap/../private.txt")]
    public async Task RawUnsafeOrUnknownTargetsHaveNoListingOrPathDisclosure(string target)
    {
        await using var f = await Fixture.StartAsync();
        var response = await f.RawAsync("GET", target);
        Assert.Equal(404, response.Status);
        Assert.DoesNotContain(f.Root, response.Headers + System.Text.Encoding.UTF8.GetString(response.Body));
        Assert.DoesNotContain("private fixture", System.Text.Encoding.UTF8.GetString(response.Body));
    }

    [Fact]
    public async Task PostAndRequestBodiesNeverCreateOrModifyFiles()
    {
        await using var f = await Fixture.StartAsync();
        Assert.Equal(405, (await f.RawAsync("POST", "/feeds/client-stable.json", "Content-Length: 0\r\n")).Status);
        Assert.Equal(400, (await f.RawAsync("GET", "/health", "Transfer-Encoding: chunked\r\n")).Status);
        Assert.Equal(400, (await f.RawAsync("GET", "/health", "Content-Length: 3\r\n")).Status);
        Assert.Equal(f.FeedBytes, File.ReadAllBytes(Path.Combine(f.Publish, "feeds", "client-stable.json")));
    }

    [Fact]
    public async Task HeaderCountIsBoundedAndWrongBlobContentDoesNotGetAnImmutableEtag()
    {
        await using var f = await Fixture.StartAsync();
        var tooMany = string.Concat(Enumerable.Range(0, 70).Select(index => $"X-{index}: value\r\n"));
        Assert.Equal(431, (await f.RawAsync("GET", "/health", tooMany)).Status);
        File.WriteAllBytes(Path.Combine(f.Publish, "blobs", f.Hash), [1, 2, 3]);
        using var response = await f.Client.GetAsync("blobs/" + f.Hash);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public async Task DisposalCancelsAnIncompleteConnectionAndCompletesShutdown()
    {
        await using var f = await Fixture.StartAsync();
        using var pending = new TcpClient();
        await pending.ConnectAsync(IPAddress.Loopback, f.Host.BaseAddress.Port);
        await pending.GetStream().WriteAsync("GET /health HTTP/1.1\r\nHo"u8.ToArray());
        await f.Host.DisposeAsync();
        await f.Host.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(f.Host.Completion.IsCompletedSuccessfully);
        await f.Host.DisposeAsync();
    }
}
