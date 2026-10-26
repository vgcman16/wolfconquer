using System.Text;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [WindowsFact]
    public void ExclusiveReviewedSourceRefusesWithoutCreatingBlobsOrChangingFeed()
    {
        using var f = new Fixture(); f.Put(NativeObjLeaf, "synthetic sharing-protected reviewed asset");
        var review = MakeNativeReview(f, NativeObjLeaf);
        string source = Path.Combine(f.Source, NativeObjLeaf);
        byte[] before = File.ReadAllBytes(source);
        string feed = ExistingFeed(f);

        using (var held = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => ReleasePublisher.Publish(f.Options(),
                [new(NativeObjLeaf, null)], f.Key, Fixture.KeyId, [review]));

        AssertQuarantinePublicationUnchanged(f, feed);
        Assert.Equal(before, File.ReadAllBytes(source));
    }

    [WindowsFact]
    public void AReviewNeverAdmitsAReparsePayloadEvenWithMatchingTargetBytes()
    {
        using var f = new Fixture();
        string outside = Path.Combine(f.Root, "owned-link-target.dat");
        File.WriteAllText(outside, "synthetic owned target bytes", new UTF8Encoding(false));
        byte[] before = File.ReadAllBytes(outside);
        string link = Path.Combine(f.Source, NativeObjLeaf);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        try { File.CreateSymbolicLink(link, outside); }
        catch (UnauthorizedAccessException)
        {
            throw Xunit.Sdk.SkipException.ForSkip("Windows symlink privilege/developer mode is unavailable for this owned reviewed-asset fixture.");
        }
        try
        {
            string feed = ExistingFeed(f);
            var review = new NativeAssetReview(NativeObjLeaf, NativeObjRoot, "directory",
                "native-map-object-directory", before.LongLength, Fixture.Hash(before), "fixture-reparse-review");

            Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(f.Options(),
                [new(NativeObjLeaf, null)], f.Key, Fixture.KeyId, [review]));

            AssertQuarantinePublicationUnchanged(f, feed);
            Assert.Equal(before, File.ReadAllBytes(outside));
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
        }
        finally { File.Delete(link); }
    }
}
