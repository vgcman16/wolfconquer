using System.Net;
using WolfConquer.Updates.Hosting;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReadOnlyFeedHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootOrAncestorJunctionIsRefusedBeforeBinding(bool ancestor)
    {
        await using var f = await Fixture.StartAsync();
        var link = Path.Combine(f.Root, "linked-publish");
        await f.LinkAsync(link, f.Publish);
        var candidate = ancestor ? Path.Combine(link, "feeds") : link;
        await Assert.ThrowsAsync<ArgumentException>(() => ReadOnlyFeedHost.StartAsync(new(candidate, 0)));
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("health")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinkedRouteAncestorOrExactLeafCannotExposeOutsideFiles(bool leaf)
    {
        await using var f = await Fixture.StartAsync();
        var outside = Directory.CreateDirectory(Path.Combine(f.Root, "outside")).FullName;
        File.WriteAllBytes(Path.Combine(outside, f.Hash), f.BlobBytes);
        string link;
        if (leaf)
        {
            link = Path.Combine(f.Publish, "blobs", f.Hash);
            File.Delete(link);
        }
        else
        {
            link = Path.Combine(f.Publish, "blobs");
            Directory.Move(link, Path.Combine(f.Root, "original-blobs"));
        }
        await f.LinkAsync(link, outside);
        using var response = await f.Client.GetAsync("blobs/" + f.Hash);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(f.BlobBytes, File.ReadAllBytes(Path.Combine(outside, f.Hash)));
    }
}
