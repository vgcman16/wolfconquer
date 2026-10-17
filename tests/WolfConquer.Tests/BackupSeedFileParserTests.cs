using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class BackupSeedFileParserTests
{
    [Fact]
    public void ParseFilePreservesBackupFileMetadataAndPayload()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "wolf-backup-seed-" + Guid.NewGuid());
        Directory.CreateDirectory(rootPath);
        var path = Path.Combine(rootPath, "SobNpcs.txt.bak");
        byte[] bytes = [0xef, 0xbb, 0xbf, 0x31];
        File.WriteAllBytes(path, bytes);

        try
        {
            var file = BackupSeedFileParser.ParseFile(path);

            Assert.Equal("SobNpcs.txt.bak", file.SourceFile);
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
