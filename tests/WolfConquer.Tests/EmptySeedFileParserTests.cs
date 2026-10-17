using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class EmptySeedFileParserTests
{
    [Fact]
    public void ParseFilePreservesZeroByteMarkerMetadata()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "wolf-empty-seed-" + Guid.NewGuid());
        Directory.CreateDirectory(rootPath);
        var path = Path.Combine(rootPath, "HeroRewards.txt");
        File.WriteAllBytes(path, []);

        try
        {
            var file = EmptySeedFileParser.ParseFile(path);

            Assert.Equal("HeroRewards.txt", file.SourceFile);
            Assert.Equal(0, file.Length);
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", file.Sha256);
            Assert.Equal("", file.Base64);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void ParseFileRejectsNonEmptyFiles()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "wolf-empty-seed-" + Guid.NewGuid());
        Directory.CreateDirectory(rootPath);
        var path = Path.Combine(rootPath, "HeroRewards.txt");
        File.WriteAllBytes(path, [0x01]);

        try
        {
            var exception = Assert.Throws<InvalidDataException>(() => EmptySeedFileParser.ParseFile(path));

            Assert.Contains("HeroRewards.txt", exception.Message);
            Assert.Contains("expected to be empty", exception.Message);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
