using System.Net;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Web;

public static class LoginLoaderWebServerRunner
{
    public const string DefaultAddress = "127.0.0.1";
    public const int DefaultPort = 80;

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter? output = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        output ??= Console.Out;

        var runOptions = ServerRunOptions.Parse(args);

        output.WriteLine($"LoginWeb for {ConquerRuntimeDefaults.ServerName}");
        output.WriteLine($"Host header: {LoginLoaderHttpResponder.LoginLoaderHost}");
        output.WriteLine($"Planned listen endpoint: {runOptions.Address}:{runOptions.Port}");
        foreach (var route in LoginLoaderHttpResponder.Routes)
        {
            var response = LoginLoaderHttpResponder.Get("GET", route.Path);
            var status = response.StatusCode == 200 ? "configured" : "unavailable";
            output.WriteLine($"{route.Path} -> {status} (file from {route.EnvironmentVariable})");
        }
        if (runOptions.AutoPatchSocketListen)
        {
            output.WriteLine(
                $"AutoPatch socket sink planned endpoint: {runOptions.AutoPatchSocketAddress}:{string.Join(',', runOptions.AutoPatchSocketPorts)}");
        }

        if (!runOptions.Listen)
        {
            output.WriteLine("Pass --listen to start accepting HTTP clients.");
            return 0;
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stop.Cancel();
        };

        await using var server = LoginLoaderHttpServer.Start(
            IPAddress.Parse(runOptions.Address),
            runOptions.Port,
            output);
        output.WriteLine($"HTTP listening on {server.BoundEndPoint}");

        AutoPatchSocketSinkServer? autoPatchSocketSink = null;
        if (runOptions.AutoPatchSocketListen)
        {
            autoPatchSocketSink = AutoPatchSocketSinkServer.Start(
                IPAddress.Parse(runOptions.AutoPatchSocketAddress),
                runOptions.AutoPatchSocketPorts,
                output);
            foreach (var endPoint in autoPatchSocketSink.BoundEndPoints)
            {
                output.WriteLine($"AutoPatch socket sink listening on {endPoint}");
            }
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
        finally
        {
            if (autoPatchSocketSink is not null)
            {
                await autoPatchSocketSink.DisposeAsync().ConfigureAwait(false);
            }
        }

        return 0;
    }

    private sealed record ServerRunOptions(
        bool Listen,
        string Address,
        int Port,
        bool AutoPatchSocketListen,
        string AutoPatchSocketAddress,
        IReadOnlyList<int> AutoPatchSocketPorts)
    {
        public static ServerRunOptions Parse(string[] args)
        {
            var listen = args.Any(arg => string.Equals(arg, "--listen", StringComparison.OrdinalIgnoreCase));
            var address = GetOption(args, "--address") ?? DefaultAddress;
            var portText = GetOption(args, "--port");
            var port = portText is null ? DefaultPort : ParsePort(portText);
            var autoPatchSocketListen = args.Any(arg =>
                string.Equals(arg, "--autopatch-socket-listen", StringComparison.OrdinalIgnoreCase));
            var autoPatchSocketAddress = GetOption(args, "--autopatch-socket-address") ?? address;
            var autoPatchSocketPorts = ParsePorts(GetOption(args, "--autopatch-socket-ports") ?? "9532,9536,9537");
            return new ServerRunOptions(
                listen,
                address,
                port,
                autoPatchSocketListen,
                autoPatchSocketAddress,
                autoPatchSocketPorts);
        }

        private static string? GetOption(string[] args, string name)
        {
            for (var index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index == args.Length - 1)
                {
                    throw new ArgumentException($"Missing value for {name}.");
                }

                return args[index + 1];
            }

            return null;
        }

        private static int ParsePort(string value)
        {
            if (!int.TryParse(value, out var port) || port is < 0 or > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Port must be between 0 and 65535.");
            }

            return port;
        }

        private static IReadOnlyList<int> ParsePorts(string value)
        {
            var ports = new List<int>();
            foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                ports.Add(ParsePort(item));
            }

            if (ports.Count == 0)
            {
                throw new ArgumentException("At least one AutoPatch socket port is required.");
            }

            return ports;
        }
    }
}
