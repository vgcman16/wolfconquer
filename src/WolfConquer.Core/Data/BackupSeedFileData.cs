namespace WolfConquer.Core.Data;

public sealed record BackupSeedFile(
    string SourceFile,
    long Length,
    string Sha256,
    string Base64);
