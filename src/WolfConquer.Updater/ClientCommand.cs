using System.Text.Json;

namespace WolfConquer.Updater;

// Presentation only: both the console and Windows launcher use the same typed workflow.
internal static class ClientCommand
{
    internal static async Task<int> Run(string command, CommandOptions arguments, TextWriter output, CancellationToken cancellation)
    {
        if (command == "recover" && (arguments.Has("--launch") || arguments.Has("--install") || arguments.Has("--repair")))
            throw new ArgumentException("Recovery accepts only --config.");
        var config = arguments.Required("--config");
        var result = command == "recover"
            ? await LauncherWorkflow.RecoverClientAsync(config, cancellationToken: cancellation)
            : await LauncherWorkflow.RunClientAsync(config, fresh: arguments.Has("--install"),
                repair: arguments.Has("--repair"), launch: arguments.Has("--launch"), cancellationToken: cancellation);
        if (command == "recover") await output.WriteLineAsync("Recovered pending transactions: " + result.RecoveredTransactions);
        else if (result.MetadataOnly)
            await output.WriteLineAsync("Selected release unchanged (metadata-only; use --repair for full integrity checks).");
        if (result.Installation is { } installed) await output.WriteLineAsync(JsonSerializer.Serialize(installed));
        if (result.LaunchedProcessId is { } processId)
            await output.WriteLineAsync("Started configured WolfConquer client process " + processId + "; login remains manual.");
        return 0;
    }
}
