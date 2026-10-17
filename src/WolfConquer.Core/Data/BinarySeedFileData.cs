namespace WolfConquer.Core.Data;

public sealed record BinarySeedFile(
    string SourceFile,
    long Length,
    string Sha256,
    string Base64);
