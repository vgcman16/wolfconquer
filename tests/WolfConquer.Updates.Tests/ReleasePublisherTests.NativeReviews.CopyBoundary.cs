using System.Reflection;
using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualCopyBoundaryRechecksReviewedBytesAfterGenuineSourcePreflight(bool changeBytes)
    {
        using var f = new Fixture();
        f.Put(NativeObjLeaf, "ABCDEF");
        string source = Path.Combine(f.Source, NativeObjLeaf);
        DateTime stamp = File.GetLastWriteTimeUtc(source);
        var review = MakeNativeReview(f, NativeObjLeaf);
        var entry = new PublishEntry(NativeObjLeaf, new string('B', 64));
        PublicationPathPolicy.ValidatePlan([entry], [review]);
        NativeAssetSource.ValidateMembership(f.Source, [review]);
        var inspect = typeof(ReleasePublisher).GetMethod("Inspect", BindingFlags.Static | BindingFlags.NonPublic);
        var copy = typeof(ReleasePublisher).GetMethod("CopySource", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(inspect);
        Assert.NotNull(copy);
        object? snapshot = inspect.Invoke(null, [f.Source, entry, review]);
        Assert.NotNull(snapshot);
        string feed = ExistingFeed(f);
        string blobs = Path.Combine(f.Publish, "blobs");
        Directory.CreateDirectory(blobs);

        if (changeBytes)
        {
            f.Put(NativeObjLeaf, "UVWXYZ");
            File.SetLastWriteTimeUtc(source, stamp);
            var invocation = Assert.Throws<TargetInvocationException>(() => copy.Invoke(null, [snapshot, blobs]));
            Assert.IsType<InvalidDataException>(invocation.InnerException);
            Assert.Empty(Directory.GetFiles(blobs));
        }
        else
        {
            var copied = Assert.IsType<UpdateFileEntry>(copy.Invoke(null, [snapshot, blobs]));
            Assert.Equal(review.ExpectedSourceSha256, copied.Sha256);
            Assert.Equal(entry.BaseSha256, copied.BaseSha256);
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(f.Blob(copied.Sha256)));
        }

        Assert.Equal(changeBytes ? "UVWXYZ" : "ABCDEF", File.ReadAllText(source));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(source));
        Assert.Equal("previous signed feed", File.ReadAllText(feed));
        Assert.Empty(Directory.GetFiles(f.Publish, "*.tmp", SearchOption.AllDirectories));
    }
}
