using System.Text.Json;
using WolfConquer.Updates.Hosting;
using WolfConquer.Updates.Source;

namespace WolfConquer.Updater;

public static class UpdaterApplication
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Length == 0 || args[0] is "help" or "--help") { await output.WriteLineAsync(Help); return 0; }
            var rest = args[1..];
            switch (args[0])
            {
                case "client":
                case "recover":
                    var client = new CommandOptions(rest, ["--install", "--repair", "--launch"], ["--config"]);
                    return await ClientCommand.Run(args[0], client, output, cancellationToken);
                case "source":
                    return await Source(rest, output, cancellationToken);
                case "check":
                    return await CheckCommand.Run(rest, output, cancellationToken);
                case "serve":
                    var serve = new CommandOptions(rest, [], ["--root", "--port", "--bootstrap"]);
                    await using (var host = await ReadOnlyFeedHost.StartAsync(new FeedHostOptions(serve.Required("--root"),
                        serve.Number("--port", 48237, 1, 65535), serve.Optional("--bootstrap") is { } file ? [file] : null), cancellationToken))
                    {
                        await output.WriteLineAsync("WolfConquer read-only downloads: " + host.BaseAddress);
                        await host.Completion.WaitAsync(cancellationToken);
                    }
                    return 0;
                case "publish":
                case "repoint":
                    return await PublishingCommand.Run(args[0], rest, output, cancellationToken);
                default: throw new ArgumentException("Unknown command. Use help.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return 130; }
        catch (OperationCanceledException) { await error.WriteLineAsync("WolfConquer updater request timed out; no client was launched."); return 1; }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or FormatException or NotSupportedException or JsonException or System.Security.Cryptography.CryptographicException or
            System.Net.Http.HttpRequestException or System.ComponentModel.Win32Exception)
        {
            await error.WriteLineAsync("WolfConquer updater refused: " + exception.Message);
            return 1;
        }
    }

    private static async Task<int> Source(string[] args, TextWriter output, CancellationToken cancellation)
    {
        var options = new CommandOptions(args, [], ["--root", "--remote", "--branch", "--watch-minutes"]);
        var minutes = options.Number("--watch-minutes", 0, 1, 1440);
        var updater = new SourceCheckoutUpdater();
        SourceUpdateResult? previous = null;
        do
        {
            var result = await updater.UpdateAsync(options.Required("--root"),
                options.Optional("--remote") ?? "https://github.com/vgcman16/wolfconquer.git",
                options.Optional("--branch") ?? "main", cancellation);
            if (previous is null || previous.Status != result.Status || previous.AfterCommit != result.AfterCommit)
                await output.WriteLineAsync(JsonSerializer.Serialize(result));
            previous = result;
            if (minutes == 0) return result.Status is "current" or "updated" ? 0 : 2;
            await Task.Delay(TimeSpan.FromMinutes(minutes), cancellation);
        } while (!cancellation.IsCancellationRequested);
        return 130;
    }

    private const string Help = """
        WolfConquer download and source tools (Windows, .NET 9)
          client --config <local-json> [--install] [--repair] [--launch]
          recover --config <local-json>
          check --config <local-json> [--receipt <local-json>] [--watch-seconds 60]
          source --root <clean-checkout> [--watch-minutes 5] [--remote <trusted-https>] [--branch main]
          serve --root <curated-publish-root> [--port 48237] [--bootstrap <allowed-filename>]
          publish --plan <curated-json> --key <private-pem>
          repoint --plan <curated-json> --key <private-pem> --manifest <signed-envelope> --base <https-directory>
        --install permits a fresh updater-only/empty destination, never an arbitrary existing client.
        --repair verifies all signed files; modified files are preserved/refused, not silently repaired over.
        Normal unchanged-release checks are metadata-only, not an integrity certification.
        Source updates never reset/stash, build, deploy, or restart a server. Watch is explicitly opt-in.
        """;
}
