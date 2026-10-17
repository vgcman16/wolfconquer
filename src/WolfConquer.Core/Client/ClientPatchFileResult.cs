namespace WolfConquer.Core.Client;

public sealed record ClientPatchFileResult(
    string RelativePath,
    int Applied,
    int Missing,
    bool Changed);
