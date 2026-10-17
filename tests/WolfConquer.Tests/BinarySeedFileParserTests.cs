using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class BinarySeedFileParserTests
{
    [Fact]
    public void ParseFilePreservesSourceNameLengthHashAndBase64Payload()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "wolf-binary-seed-" + Guid.NewGuid());
        Directory.CreateDirectory(rootPath);
        var path = Path.Combine(rootPath, "ClaimContainer.bin");
        byte[] bytes = [0x00, 0x10, 0xff, 0x7e];
        File.WriteAllBytes(path, bytes);

        try
        {
            var file = BinarySeedFileParser.ParseFile(path);

            Assert.Equal("ClaimContainer.bin", file.SourceFile);
            Assert.Equal(bytes.Length, file.Length);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), file.Sha256);
            Assert.Equal(Convert.ToBase64String(bytes), file.Base64);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}
