namespace WolfConquer.Updates.Publishing;

public static partial class PublicationPathPolicy
{
    // Shape-only admission is shared with CLI, before key or source reads.
    public static void ValidatePlan(IEnumerable<PublishEntry> plan,
        IEnumerable<NativeAssetReview>? nativeAssetReviews = null) =>
        _ = NativeAssetReviewSet.Create(plan, nativeAssetReviews);

    private static bool IsReviewedCollision(string path, string prefix, NativeAssetReview? review) =>
        review is not null && (review.Category == NativeAssetReviewSet.ObjectDirectory
            ? prefix.Equals(review.CollisionPath, StringComparison.Ordinal)
            : review.Category == NativeAssetReviewSet.LogsBitmap && prefix.Equals(path, StringComparison.Ordinal) &&
              path.Equals("data/main/LogS.BMP", StringComparison.Ordinal));
}
