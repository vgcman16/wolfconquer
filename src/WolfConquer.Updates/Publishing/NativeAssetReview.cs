namespace WolfConquer.Updates.Publishing;

// Local publication admission only; never part of signed manifest schema 1.
public sealed record NativeAssetReview(string Path, string CollisionPath, string CollisionNodeType,
    string Category, long Length, string ExpectedSourceSha256, string Evidence);
