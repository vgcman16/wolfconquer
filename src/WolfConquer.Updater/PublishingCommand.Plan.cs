using System.Text.Json;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updater;

internal static partial class PublishingCommand
{
    private static PublicationPlan ReadPlan(string path, bool publication)
    {
        // Presence and typed shape must describe the same bounded byte snapshot.
        byte[] bytes = LocalFiles.Bytes(path, 64 * 1024 * 1024);
        PublicationPlan plan = LocalFiles.Parse<PublicationPlan>(bytes);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        bool hasReviews = document.RootElement.TryGetProperty("nativeAssetReviews", out _);
        if (plan.Files is null || plan.SchemaVersion != 1 && (!publication || plan.SchemaVersion != 2) ||
            plan.SchemaVersion == 1 && hasReviews)
            throw new InvalidDataException("Unsupported curated publication plan.");
        if (plan.SchemaVersion == 2 && (!hasReviews || plan.NativeAssetReviews is null ||
            plan.NativeAssetReviews.Length is < 1 or > 4096))
            throw new InvalidDataException("Schema 2 requires bounded native asset reviews.");
        if (publication) PublicationPathPolicy.ValidatePlan(plan.Files, plan.NativeAssetReviews);
        return plan;
    }
}
