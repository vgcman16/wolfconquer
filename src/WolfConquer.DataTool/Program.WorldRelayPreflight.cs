using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

internal static partial class Program
{
    static int WorldRelayCapturePreflight(string[] args)
    {
        string clientExePath;
        try
        {
            clientExePath = ResolveWorldRelayCapturePreflightClientExePath(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var processes = EnumerateWorldRelayCaptureClientProcesses(clientExePath);
            var connections = EnumerateWindowsIPv4TcpConnections();
            var report = WorldRelayCapturePreflightLogic.CreateReport(clientExePath, processes, connections);

            Console.Write(WorldRelayCapturePreflightLogic.FormatReport(report));
            return report.Ready ? 0 : 2;
        }
        catch (Exception ex) when (
            ex is Win32Exception or
                PlatformNotSupportedException or
                InvalidOperationException or
                NotSupportedException)
        {
            Console.Error.WriteLine($"World relay capture preflight failed: {ex.Message}");
            return 1;
        }
    }

    static int OriginalFlowCapturePreflight(string[] args)
    {
        string clientExePath;
        try
        {
            clientExePath = ResolveOriginalFlowCapturePreflightClientExePath(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var processes = EnumerateWorldRelayCaptureClientProcesses(clientExePath);
            var connections = EnumerateWindowsIPv4TcpConnections();
            var report = OriginalFlowCapturePreflightLogic.CreateReport(clientExePath, processes, connections);

            Console.Write(OriginalFlowCapturePreflightLogic.FormatReport(report));
            return OriginalFlowCapturePreflightLogic.GetExitCode(report);
        }
        catch (Exception ex) when (
            ex is Win32Exception or
                PlatformNotSupportedException or
                InvalidOperationException or
                NotSupportedException)
        {
            Console.Error.WriteLine($"Original-flow capture preflight failed: {ex.Message}");
            return 1;
        }
    }

    static string ResolveWorldRelayCapturePreflightClientExePath(string[] args)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException("World relay capture preflight accepts at most one client executable path.");
        }

        var clientExePath = args.Length > 1
            ? args[1]
            : WorldRelayCapturePreflightLogic.DefaultClientExePath;
        return WorldRelayCapturePreflightLogic.NormalizeClientExePath(clientExePath);
    }

    static string ResolveOriginalFlowCapturePreflightClientExePath(string[] args)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException("Original-flow capture preflight accepts at most one client executable path.");
        }

        var clientExePath = args.Length > 1
            ? args[1]
            : OriginalFlowCapturePreflightLogic.DefaultClientExePath;
        return WorldRelayCapturePreflightLogic.NormalizeClientExePath(clientExePath);
    }

    static string QuotePowerShellSingle(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    static string FormatAccountMySqlEnvironmentCommand()
    {
        var connectionString = TryReadBundledAccountMySqlConnectionString();
        return connectionString is null
            ? "$env:WOLFCONQUER_ACCOUNT_MYSQL = '<live MySQL connection string>'"
            : $"$env:WOLFCONQUER_ACCOUNT_MYSQL = {QuotePowerShellSingle(connectionString)}";
    }

    static string? TryReadBundledAccountMySqlConnectionString()
    {
        const string bundledGuardShieldPath = @"config/GuardShield.ini";
        if (!File.Exists(bundledGuardShieldPath))
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(bundledGuardShieldPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 ||
                line.StartsWith(';') ||
                line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length > 0)
            {
                values[key] = value;
            }
        }

        if (!values.TryGetValue("Host", out var host) ||
            !values.TryGetValue("Username", out var username) ||
            !values.TryGetValue("Password", out var password) ||
            !values.TryGetValue("Database", out var database) ||
            string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(database))
        {
            return null;
        }

        return "Server=" + host +
            ";Port=3306;Database=" + database +
            ";User ID=" + username +
            ";Password=" + password +
            ";SslMode=None;AllowPublicKeyRetrieval=True;";
    }
}

internal static class WorldRelayCapturePreflightLogic
{
    public const string DefaultClientExePath = @"client\Env_DX8\Conquer.exe";

    private static readonly int[] KeyPorts = [2021, 9960, 5818, 15819, 80];
    private static readonly int[] BlockingWorldPorts = [5818, 15819];

    public static string NormalizeClientExePath(string clientExePath)
    {
        if (string.IsNullOrWhiteSpace(clientExePath))
        {
            throw new ArgumentException("Client executable path is required.", nameof(clientExePath));
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(clientExePath));
    }

    public static bool PathsEqual(string left, string right)
    {
        try
        {
            return PathComparer.Equals(NormalizeClientExePath(left), NormalizeClientExePath(right));
        }
        catch (Exception ex) when (
            ex is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return false;
        }
    }

    public static WorldRelayCapturePreflightReport CreateReport(
        string clientExePath,
        IEnumerable<WorldRelayCapturePreflightProcess> processes,
        IEnumerable<WorldRelayCaptureTcpConnection> connections)
    {
        var fullClientExePath = NormalizeClientExePath(clientExePath);
        var clientProcesses = processes
            .Where(process => PathsEqual(process.ExecutablePath, fullClientExePath))
            .GroupBy(process => process.Id)
            .Select(group => group.First())
            .OrderBy(process => process.Id)
            .ToArray();
        var clientProcessIds = clientProcesses
            .Select(process => process.Id)
            .ToHashSet();
        var keyConnections = connections
            .Where(connection =>
                clientProcessIds.Contains(connection.OwningProcessId) &&
                (KeyPorts.Contains(connection.LocalPort) || KeyPorts.Contains(connection.RemotePort)))
            .OrderBy(connection => connection.OwningProcessId)
            .ThenBy(connection => connection.RemoteAddress, StringComparer.OrdinalIgnoreCase)
            .ThenBy(connection => connection.RemotePort)
            .ThenBy(connection => connection.LocalPort)
            .ToArray();
        var blockingConnections = keyConnections
            .Where(IsBlockingWorldRelayConnection)
            .ToArray();

        return new WorldRelayCapturePreflightReport(
            fullClientExePath,
            KeyPorts,
            clientProcesses,
            keyConnections,
            blockingConnections);
    }

    public static string FormatReport(WorldRelayCapturePreflightReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Real-client world relay capture preflight:");
        builder.AppendLine($"Client: {report.ClientExePath}");
        builder.AppendLine($"Key ports: {string.Join(", ", report.KeyPorts.Select(FormatKeyPort))}");
        builder.AppendLine($"Client running: {report.ClientRunning}");
        builder.AppendLine($"Client PID(s): {FormatClientProcessIds(report.ClientProcesses)}");
        builder.AppendLine("Key TCP connections:");

        if (report.KeyConnections.Count == 0)
        {
            builder.AppendLine("  none");
        }
        else
        {
            foreach (var connection in report.KeyConnections)
            {
                builder.AppendLine($"  {FormatConnection(connection)}");
            }
        }

        if (report.BlockingConnections.Count > 0)
        {
            builder.AppendLine(
                $"WARNING: active world/relay TCP connection(s) already open: {report.BlockingConnections.Count}");
            builder.AppendLine("Status: FAIL - relay capture needs a fresh world-entry connection.");
        }
        else if (!report.ClientRunning)
        {
            builder.AppendLine("Status: PASS - client is not running; start it only after the relay and forwarded AccountServer are ready.");
        }
        else
        {
            builder.AppendLine("Status: PASS - no active client TCP connection to world/relay ports 5818/15819.");
        }

        builder.AppendLine($"Ready: {report.Ready}");
        return builder.ToString();
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static bool IsBlockingWorldRelayConnection(WorldRelayCaptureTcpConnection connection)
    {
        return (BlockingWorldPorts.Contains(connection.LocalPort) || BlockingWorldPorts.Contains(connection.RemotePort)) &&
            IsActiveTcpState(connection.State);
    }

    internal static bool IsActiveTcpState(string state)
    {
        var normalized = state.Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        return string.Equals(normalized, "SynSent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "SynReceived", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Established", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "FinWait1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "FinWait2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "CloseWait", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Closing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "LastAck", StringComparison.OrdinalIgnoreCase);
    }

    internal static string FormatClientProcessIds(IReadOnlyList<WorldRelayCapturePreflightProcess> processes)
    {
        return processes.Count == 0
            ? "<none>"
            : string.Join(", ", processes.Select(process => process.Id));
    }

    private static string FormatConnection(WorldRelayCaptureTcpConnection connection)
    {
        return $"pid={connection.OwningProcessId} state={connection.State} " +
            $"local={connection.LocalAddress}:{connection.LocalPort} " +
            $"remote={connection.RemoteAddress}:{connection.RemotePort} " +
            $"keyPorts={FormatMatchedKeyPorts(connection)}";
    }

    private static string FormatMatchedKeyPorts(WorldRelayCaptureTcpConnection connection)
    {
        var ports = KeyPorts
            .Where(port => port == connection.LocalPort || port == connection.RemotePort)
            .Select(FormatKeyPort)
            .ToArray();

        return ports.Length == 0 ? "<none>" : string.Join(",", ports);
    }

    internal static string FormatKeyPort(int port)
    {
        return port switch
        {
            2021 => "2021(patch)",
            9960 => "9960(account)",
            5818 => "5818(world)",
            15819 => "15819(relay)",
            80 => "80(http)",
            _ => port.ToString()
        };
    }
}

internal static class OriginalFlowCapturePreflightLogic
{
    public const string DefaultClientExePath = WorldRelayCapturePreflightLogic.DefaultClientExePath;

    private static readonly int[] WatchedServicePorts = [2021, 9960, 5818];

    public static OriginalFlowCapturePreflightReport CreateReport(
        string clientExePath,
        IEnumerable<WorldRelayCapturePreflightProcess> processes,
        IEnumerable<WorldRelayCaptureTcpConnection> connections)
    {
        var fullClientExePath = WorldRelayCapturePreflightLogic.NormalizeClientExePath(clientExePath);
        var clientProcesses = processes
            .Where(process => WorldRelayCapturePreflightLogic.PathsEqual(process.ExecutablePath, fullClientExePath))
            .GroupBy(process => process.Id)
            .Select(group => group.First())
            .OrderBy(process => process.Id)
            .ToArray();
        var clientProcessIds = clientProcesses
            .Select(process => process.Id)
            .ToHashSet();
        var connectionArray = connections.ToArray();
        var serviceListeners = connectionArray
            .Where(connection =>
                WatchedServicePorts.Contains(connection.LocalPort) &&
                IsListeningTcpState(connection.State))
            .OrderBy(connection => connection.LocalPort)
            .ThenBy(connection => connection.LocalAddress, StringComparer.OrdinalIgnoreCase)
            .ThenBy(connection => connection.OwningProcessId)
            .ToArray();
        var missingServicePorts = WatchedServicePorts
            .Where(port => !serviceListeners.Any(connection => connection.LocalPort == port))
            .ToArray();
        var clientWatchedConnections = connectionArray
            .Where(connection =>
                clientProcessIds.Contains(connection.OwningProcessId) &&
                InvolvesWatchedServicePort(connection))
            .OrderBy(connection => connection.OwningProcessId)
            .ThenBy(connection => connection.RemoteAddress, StringComparer.OrdinalIgnoreCase)
            .ThenBy(connection => connection.RemotePort)
            .ThenBy(connection => connection.LocalPort)
            .ToArray();
        var blockingClientConnections = clientWatchedConnections
            .Where(connection => WorldRelayCapturePreflightLogic.IsActiveTcpState(connection.State))
            .ToArray();

        return new OriginalFlowCapturePreflightReport(
            fullClientExePath,
            WatchedServicePorts,
            clientProcesses,
            serviceListeners,
            missingServicePorts,
            clientWatchedConnections,
            blockingClientConnections);
    }

    public static string FormatReport(OriginalFlowCapturePreflightReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Original-flow capture preflight:");
        builder.AppendLine($"Client: {report.ClientExePath}");
        builder.AppendLine(
            $"Watched service ports: {string.Join(", ", report.WatchedServicePorts.Select(WorldRelayCapturePreflightLogic.FormatKeyPort))}");
        builder.AppendLine("Required service listeners:");

        foreach (var port in report.WatchedServicePorts)
        {
            var listeners = report.ServiceListeners
                .Where(connection => connection.LocalPort == port)
                .ToArray();
            if (listeners.Length == 0)
            {
                builder.AppendLine($"  {WorldRelayCapturePreflightLogic.FormatKeyPort(port)}: missing");
                continue;
            }

            foreach (var listener in listeners)
            {
                builder.AppendLine(
                    $"  {WorldRelayCapturePreflightLogic.FormatKeyPort(port)}: {FormatConnection(listener)}");
            }
        }

        builder.AppendLine($"Services ready: {report.ServicesReady}");
        builder.AppendLine($"Client running: {report.ClientRunning}");
        builder.AppendLine(
            $"Client PID(s): {WorldRelayCapturePreflightLogic.FormatClientProcessIds(report.ClientProcesses)}");
        builder.AppendLine("Client TCP connections involving watched service ports:");

        if (report.ClientWatchedConnections.Count == 0)
        {
            builder.AppendLine("  none");
        }
        else
        {
            foreach (var connection in report.ClientWatchedConnections)
            {
                builder.AppendLine($"  {FormatConnection(connection)}");
            }
        }

        if (report.BlockingClientConnections.Count > 0)
        {
            builder.AppendLine(
                $"WARNING: client TCP connection(s) already involve watched service ports: {report.BlockingClientConnections.Count}");
            builder.AppendLine("Status: FAIL - restart or fully close the client before opening the timed capture window.");
        }
        else if (!report.ServicesReady)
        {
            builder.AppendLine(
                $"Status: FAIL - required old-stack listener(s) missing: {FormatPorts(report.MissingServicePorts)}.");
        }
        else if (!report.ClientRunning)
        {
            builder.AppendLine("Status: PASS - old stack is listening; start the capture before launching the client.");
        }
        else
        {
            builder.AppendLine("Status: PASS - old stack is listening and the client has no active watched-port TCP socket.");
        }

        builder.AppendLine($"Ready: {report.Ready}");
        return builder.ToString();
    }

    public static int GetExitCode(OriginalFlowCapturePreflightReport report)
    {
        return report.Ready ? 0 : 2;
    }

    private static bool InvolvesWatchedServicePort(WorldRelayCaptureTcpConnection connection)
    {
        return WatchedServicePorts.Contains(connection.LocalPort) ||
            WatchedServicePorts.Contains(connection.RemotePort);
    }

    private static bool IsListeningTcpState(string state)
    {
        var normalized = state.Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        return string.Equals(normalized, "Listen", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Listening", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatConnection(WorldRelayCaptureTcpConnection connection)
    {
        return $"pid={connection.OwningProcessId} state={connection.State} " +
            $"local={connection.LocalAddress}:{connection.LocalPort} " +
            $"remote={connection.RemoteAddress}:{connection.RemotePort} " +
            $"watchedPorts={FormatMatchedWatchedPorts(connection)}";
    }

    private static string FormatMatchedWatchedPorts(WorldRelayCaptureTcpConnection connection)
    {
        var ports = WatchedServicePorts
            .Where(port => port == connection.LocalPort || port == connection.RemotePort)
            .Select(WorldRelayCapturePreflightLogic.FormatKeyPort)
            .ToArray();

        return ports.Length == 0 ? "<none>" : string.Join(",", ports);
    }

    private static string FormatPorts(IReadOnlyList<int> ports)
    {
        return ports.Count == 0
            ? "<none>"
            : string.Join(", ", ports.Select(WorldRelayCapturePreflightLogic.FormatKeyPort));
    }
}

internal static class NativeMethods
{
    public const int ErrorSuccess = 0;
    public const int ErrorInsufficientBuffer = 122;
    public const int AfInet = 2;
    public const int TcpTableOwnerPidAll = 5;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern int GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int tcpTableLength,
        [MarshalAs(UnmanagedType.Bool)] bool sort,
        int ipVersion,
        int tableClass,
        int reserved);

    [StructLayout(LayoutKind.Sequential)]
    public struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }
}

internal sealed record WorldRelayCapturePreflightProcess(int Id, string ExecutablePath);

internal sealed record WorldRelayCaptureTcpConnection(
    int OwningProcessId,
    string LocalAddress,
    int LocalPort,
    string RemoteAddress,
    int RemotePort,
    string State);

internal sealed record WorldRelayCapturePreflightReport(
    string ClientExePath,
    IReadOnlyList<int> KeyPorts,
    IReadOnlyList<WorldRelayCapturePreflightProcess> ClientProcesses,
    IReadOnlyList<WorldRelayCaptureTcpConnection> KeyConnections,
    IReadOnlyList<WorldRelayCaptureTcpConnection> BlockingConnections)
{
    public bool ClientRunning => ClientProcesses.Count > 0;

    public bool Ready => BlockingConnections.Count == 0;
}

internal sealed record OriginalFlowCapturePreflightReport(
    string ClientExePath,
    IReadOnlyList<int> WatchedServicePorts,
    IReadOnlyList<WorldRelayCapturePreflightProcess> ClientProcesses,
    IReadOnlyList<WorldRelayCaptureTcpConnection> ServiceListeners,
    IReadOnlyList<int> MissingServicePorts,
    IReadOnlyList<WorldRelayCaptureTcpConnection> ClientWatchedConnections,
    IReadOnlyList<WorldRelayCaptureTcpConnection> BlockingClientConnections)
{
    public bool ClientRunning => ClientProcesses.Count > 0;

    public bool ServicesReady => MissingServicePorts.Count == 0;

    public bool Ready => ServicesReady && BlockingClientConnections.Count == 0;
}
