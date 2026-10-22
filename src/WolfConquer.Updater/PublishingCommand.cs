using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updater;

internal sealed record PublicationPlan(int SchemaVersion, string SourceRoot, string PublishRoot,
    string ProductId, string Channel, long Sequence, string ContentBaseUrl, string CreatedUtc, string ExpiresUtc,
    string KeyId, PublishEntry[] Files, bool IncludeBundle = true, bool AllowLoopbackHttp = false);

internal static class PublishingCommand
{
    internal static async Task<int> Run(string command, string[] args, TextWriter output, CancellationToken cancellation)
    {
        var arguments = new CommandOptions(args, [], command == "publish" ? ["--plan", "--key"] :
            ["--plan", "--key", "--manifest", "--base"]);
        var plan = LocalFiles.Read<PublicationPlan>(arguments.Required("--plan"), 64 * 1024 * 1024);
        if (plan.SchemaVersion != 1 || plan.Files is null) throw new InvalidDataException("Unsupported curated publication plan.");
        var keyPath = Path.GetFullPath(arguments.Required("--key"));
        var publishedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(plan.PublishRoot));
        var sourceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(plan.SourceRoot));
        if (keyPath.StartsWith(publishedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            keyPath.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Signing key must be outside both curated source and served publication roots.");
        using var key = ECDsa.Create();
        var privateBytes = LocalFiles.Bytes(keyPath, 16384);
        try { key.ImportFromPem(System.Text.Encoding.UTF8.GetString(privateBytes)); }
        finally { CryptographicOperations.ZeroMemory(privateBytes); }
        cancellation.ThrowIfCancellationRequested();
        if (command == "publish")
        {
            var result = ReleasePublisher.Publish(new ReleasePublishOptions(plan.SourceRoot, plan.PublishRoot,
                plan.ProductId, plan.Channel, plan.Sequence, new Uri(plan.ContentBaseUrl),
                ParseUtc(plan.CreatedUtc), ParseUtc(plan.ExpiresUtc), plan.IncludeBundle, plan.AllowLoopbackHttp), plan.Files, key, plan.KeyId);
            await output.WriteLineAsync($"Published {result.Manifest.ProductId}/{result.Manifest.Channel} sequence {result.Manifest.Sequence}: {result.Manifest.Files.Count} curated files.");
        }
        else
        {
            var bytes = LocalFiles.Bytes(arguments.Required("--manifest"), SignedUpdateManifest.MaximumEnvelopeBytes);
            var index = ReleasePublisher.RepointIndex(bytes, plan.KeyId, key.ExportSubjectPublicKeyInfoPem(),
                new UpdateVerificationContext(plan.ProductId, plan.Channel, plan.Sequence, DateTimeOffset.UtcNow,
                    allowLoopbackHttp: plan.AllowLoopbackHttp), new Uri(arguments.Required("--base")), key);
            // Product/channel are checked by manifest verification before being used as a filename.
            LocalFiles.AtomicWrite(Path.Combine(plan.PublishRoot, "feeds", plan.ProductId + "-" + plan.Channel + ".json"), index);
            await output.WriteLineAsync("Repointed signed discovery; exact release bytes and sequence are unchanged.");
        }
        return 0;
    }

    private static DateTimeOffset ParseUtc(string value) => (value.EndsWith("Z", StringComparison.Ordinal) ||
        value.EndsWith("+00:00", StringComparison.Ordinal)) && DateTimeOffset.TryParse(value,
        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) &&
        parsed.Offset == TimeSpan.Zero ? parsed : throw new InvalidDataException("Publication timestamps must be explicit UTC.");
}
