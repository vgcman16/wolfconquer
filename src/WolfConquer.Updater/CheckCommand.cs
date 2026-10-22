using System.Net;
using System.Text.Json;
using WolfConquer.Updates.Discovery;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updater;

internal sealed record UpdaterCheckReceipt(int SchemaVersion, string Outcome, DateTimeOffset CheckedAtUtc,
    string ProductId, string Channel, long? Sequence, bool IndexValid, bool ManifestValid,
    bool DownloadsReachable, string Reason);

internal static class CheckCommand
{
    internal static async Task<int> Run(string[] args, TextWriter output, CancellationToken cancellation)
    {
        var arguments = new CommandOptions(args, [], ["--config", "--receipt", "--watch-seconds"]);
        var seconds = arguments.Number("--watch-seconds", 0, 10, 3600);
        var (settings, _, _) = ClientConfiguration.Load(arguments.Required("--config"));
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var trust = new UpdateTrust(settings.ProductId, settings.Channel, settings.KeyId, settings.PublicKeySpkiPem, settings.AllowLoopbackHttp);
        var discovery = new UpdateDiscoveryClient(http);
        byte[]? cached = null;
        long minimum = 1;
        do
        {
            bool indexValid = false, manifestValid = false, reachable = false;
            long? sequence = null;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            try
            {
                var index = await discovery.FetchIndexAsync(new Uri(settings.FeedUrl), trust, minimum, deadline.Token);
                indexValid = true;
                sequence = index.Manifest.Sequence;
                var found = await discovery.FetchReleaseAsync(index, trust, minimum, cached, deadline.Token);
                manifestValid = true;
                cached = found.FullEnvelope;
                minimum = index.Manifest.Sequence;
                var manifest = found.Release.FullManifest.Manifest;
                // Reachability is intentionally distinct from downloading/hashing a multi-GB payload.
                if (manifest.Bundle is not { } bundle) throw new InvalidDataException("No full-install bundle is published.");
                await Head(http, found.Release.ContentBaseUrl, bundle.Sha256, bundle.Length, deadline.Token);
                var executable = manifest.Files.SingleOrDefault(file => file.Path == settings.LaunchRelativePath)
                    ?? throw new InvalidDataException("Configured executable is not in the signed release.");
                await Head(http, found.Release.ContentBaseUrl, executable.Sha256, executable.Length, deadline.Token);
                reachable = true;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
            catch (Exception error) when (error is IOException or InvalidDataException or HttpRequestException or OperationCanceledException or
                System.Security.Cryptography.CryptographicException or ArgumentException) { }
            var receipt = new UpdaterCheckReceipt(1, indexValid && manifestValid && reachable ? "passed" : "failed",
                DateTimeOffset.UtcNow, settings.ProductId, settings.Channel, sequence, indexValid, manifestValid, reachable,
                reachable ? "verified" : !indexValid ? "index-invalid" : !manifestValid ? "manifest-invalid" : "downloads-unreachable");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(receipt, LocalFiles.Json);
            if (arguments.Optional("--receipt") is { } receiptPath)
            {
                if (!Path.IsPathFullyQualified(receiptPath)) throw new InvalidDataException("Receipt path must be absolute.");
                LocalFiles.AtomicWrite(receiptPath, bytes);
            }
            await output.WriteLineAsync(System.Text.Encoding.UTF8.GetString(bytes));
            if (seconds == 0) return receipt.Outcome == "passed" ? 0 : 1;
            await Task.Delay(TimeSpan.FromSeconds(seconds), cancellation);
        } while (!cancellation.IsCancellationRequested);
        return 130;
    }

    private static async Task Head(HttpClient http, Uri contentBase, string hash, long length, CancellationToken cancellation)
    {
        var uri = new Uri(contentBase, "blobs/" + hash.ToLowerInvariant());
        using var request = new HttpRequestMessage(HttpMethod.Head, uri);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.StatusCode != HttpStatusCode.OK || response.RequestMessage?.RequestUri != uri || response.Content.Headers.ContentLength != length)
            throw new InvalidDataException("Expected signed payload endpoint is unreachable or has an invalid length.");
    }
}
