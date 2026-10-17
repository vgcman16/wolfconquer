namespace WolfConquer.Core.Data;

public sealed record EmptySeedFile(
    string SourceFile,
    long Length,
    string Sha256,
    string Base64);
