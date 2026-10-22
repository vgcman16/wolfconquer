using System.Security.Cryptography;
using System.Text;
using WolfConquer.Updates.Publishing;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [Theory]
    [InlineData("../outside.dat")]
    [InlineData("/rooted.dat")]
    [InlineData("ini\\file.dat")]
    [InlineData(".git/config")]
    [InlineData("trusted-release-keys.json")]
    [InlineData("a/CON.txt")]
    public void InvalidCuratedPlanCannotReplaceAnExistingFeed(string path)
    {
        using var f = new Fixture();
        string feed = ExistingFeed(f);
        Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(f.Options(),
            [new(path, null)], f.Key, Fixture.KeyId));
        Assert.Equal("previous signed feed", File.ReadAllText(feed));
        Assert.False(Directory.Exists(Path.Combine(f.Publish, "blobs")));
    }

    [Theory]
    [InlineData("same.dat", "SAME.dat")]
    [InlineData("ini", "ini/file.dat")]
    public void CaseCollisionsAndFileDirectoryConflictsFailBeforeAnyBlobWrite(string first, string second)
    {
        using var f = new Fixture();
        Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(f.Options(),
            [new(first, null), new(second, null)], f.Key, Fixture.KeyId));
        Assert.False(Directory.Exists(f.Publish));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EqualAndNestedRootsAreRejectedWithoutWritingContent(int mode)
    {
        using var f = new Fixture();
        string source = f.Source;
        string publish = mode == 0 ? f.Source : mode == 1 ? Path.Combine(f.Source, "child") : f.Publish;
        if (mode == 2) { source = Path.Combine(f.Publish, "child"); Directory.CreateDirectory(source); }
        Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(f.Options(source: source, publish: publish),
            [], f.Key, Fixture.KeyId));
        Assert.Empty(Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void AllSourcesAndBaseHashesArePreflightedBeforeAnyOutputWrites()
    {
        using var f = new Fixture(); f.Put("first.dat", "first");
        Assert.Throws<FileNotFoundException>(() => ReleasePublisher.Publish(f.Options(),
            [new("first.dat", null), new("missing.dat", null)], f.Key, Fixture.KeyId));
        Assert.False(Directory.Exists(f.Publish));
        Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(f.Options(),
            [new("first.dat", "bad-base-hash")], f.Key, Fixture.KeyId));
        Assert.False(Directory.Exists(f.Publish));
        using var publicOnly = ECDsa.Create();
        publicOnly.ImportSubjectPublicKeyInfo(f.Key.ExportSubjectPublicKeyInfo(), out _);
        Assert.Throws<CryptographicException>(() => ReleasePublisher.Publish(f.Options(),
            [new("first.dat", null)], publicOnly, Fixture.KeyId));
        Assert.False(Directory.Exists(f.Publish));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BadExistingHashBlobIsNeverOverwrittenAndPriorFeedSurvives(bool sameLength)
    {
        using var f = new Fixture();
        const string source = "the expected immutable content!";
        string existing = sameLength ? new string('X', source.Length) : "short";
        f.Put("one.dat", source);
        string hash = Fixture.Hash(Encoding.UTF8.GetBytes(source));
        string feed = ExistingFeed(f);
        Directory.CreateDirectory(Path.Combine(f.Publish, "blobs"));
        File.WriteAllText(f.Blob(hash), existing, new UTF8Encoding(false));
        Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(f.Options(),
            [new("one.dat", null)], f.Key, Fixture.KeyId));
        Assert.Equal(existing, File.ReadAllText(f.Blob(hash)));
        Assert.Equal("previous signed feed", File.ReadAllText(feed));
        Assert.Empty(Directory.GetFiles(f.Publish, "*.tmp", SearchOption.AllDirectories));
    }

    [WindowsFact]
    public void AtomicFeedReplacementFailureKeepsPriorFeedAfterBlobsAreReady()
    {
        using var f = new Fixture(); f.Put("one.dat", "one");
        string feed = ExistingFeed(f);
        using (var held = new FileStream(feed, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => ReleasePublisher.Publish(f.Options(bundle: true),
                [new("one.dat", null)], f.Key, Fixture.KeyId));
        Assert.Equal("previous signed feed", File.ReadAllText(feed));
        Assert.True(File.Exists(f.Blob(Fixture.Hash(Encoding.UTF8.GetBytes("one")))));
        Assert.Empty(Directory.GetFiles(f.Publish, "*.tmp", SearchOption.AllDirectories));
    }

    [WindowsTheory]
    [InlineData("source-root")]
    [InlineData("source-ancestor")]
    [InlineData("source-file")]
    [InlineData("publish-root")]
    public void ReparseRootsAncestorsAndFilesAreRejected(string kind)
    {
        using var f = new Fixture(); f.Put("real.dat", "preserved");
        string link = Path.Combine(f.Root, "link");
        bool fileLink = kind == "source-file";
        try
        {
            if (fileLink) File.CreateSymbolicLink(link, Path.Combine(f.Source, "real.dat"));
            else Directory.CreateSymbolicLink(link, f.Source);
        }
        catch (UnauthorizedAccessException)
        {
            throw Xunit.Sdk.SkipException.ForSkip("Windows symlink privilege/developer mode is unavailable for this owned reparse fixture.");
        }
        try
        {
            string source = kind == "source-root" ? link : f.Source;
            if (kind == "source-ancestor") { Directory.CreateDirectory(Path.Combine(f.Source, "child")); source = Path.Combine(link, "child"); }
            if (fileLink) { File.Move(link, Path.Combine(f.Source, "alias.dat")); link = Path.Combine(f.Source, "alias.dat"); }
            var options = f.Options(source: source, publish: kind == "publish-root" ? link : f.Publish);
            Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(options,
                [new(fileLink ? "alias.dat" : "real.dat", null)], f.Key, Fixture.KeyId));
            Assert.Equal("preserved", File.ReadAllText(Path.Combine(f.Source, "real.dat")));
        }
        finally { if (fileLink) File.Delete(link); else Directory.Delete(link); }
    }

    private static string ExistingFeed(Fixture f)
    {
        string path = Path.Combine(f.Publish, "feeds", "game-stable.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "previous signed feed", new UTF8Encoding(false));
        return path;
    }
}
