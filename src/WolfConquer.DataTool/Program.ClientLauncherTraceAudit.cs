using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Runtime;

internal static partial class Program
{
    private static readonly Regex LauncherTraceTcpRowRegex = new(
        @"^\s*(?<first>\d{1,2}:\d{2}:\d{2}\.\d{3})\s+(?<last>\d{1,2}:\d{2}:\d{2}\.\d{3})\s+(?<samples>\d+)\s+(?<pid>\d+)\s+(?<local>\S+)\s+(?<remote>\S+)\s+(?<state>\S+)",
        RegexOptions.CultureInvariant);

    private static readonly Regex LauncherTraceProcessRowRegex = new(
        @"^\s*(?<first>\d{1,2}:\d{2}:\d{2}\.\d{3})\s+(?<last>\d{1,2}:\d{2}:\d{2}\.\d{3})\s+(?<samples>\d+)\s+(?<pid>\d+)\s+(?<parent>\d*)\s+(?<name>\S+)\s+(?<path>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex LauncherTraceGameInfoLineRegex = new(
        @"^\s*(?<time>\d{1,2}:\d{2}:\d{2}\.\d{3})\s+(?<pid>\d+)\s+(?<path>\S*GameInfo\.ini)\s+(?<detail>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LauncherTraceSidecarLogRegex = new(
        @"^mode=(?<mode>\S+)\s+process=(?<process>\S+)\s+.*?\blog=(?<log>.+?)\s+stdout=(?<stdout>.+?)\s+stderr=(?<stderr>.+)$",
        RegexOptions.CultureInvariant);

    private static readonly string[] ClientLauncherTraceFridaEvidenceSections =
    [
        "Frida spawn evidence",
        "Frida HTTP redirect attachments",
    ];

    static int ClientLauncherTraceAudit(string[] args)
    {
        ClientLauncherTraceAuditOptions options;
        try
        {
            options = ResolveClientLauncherTraceAuditOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (!File.Exists(options.ReportPath))
        {
            Console.Error.WriteLine($"Launcher trace report not found: {options.ReportPath}");
            return 1;
        }

        var report = ClientLauncherTraceAuditLogic.Analyze(
            File.ReadAllLines(options.ReportPath),
            options.AllowedExternalRoutes,
            Path.GetDirectoryName(options.ReportPath));
        Console.Write(ClientLauncherTraceAuditLogic.FormatReport(options.ReportPath, report));
        return report.Ready ? 0 : 2;
    }

    static ClientLauncherTraceAuditOptions ResolveClientLauncherTraceAuditOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("Usage: WolfConquer.DataTool client-launcher-trace-audit <launcherTraceReportPath> [--allow-external address:port] [...]");
        }

        var reportPath = Path.GetFullPath(args[1]);
        var allowedExternalRoutes = new List<string>();
        for (var index = 2; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--allow-external", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --allow-external.");
                }

                allowedExternalRoutes.Add(NormalizeEndpointText(args[index]));
                continue;
            }

            throw new ArgumentException($"Unknown client-launcher-trace-audit option: {args[index]}");
        }

        return new ClientLauncherTraceAuditOptions(reportPath, allowedExternalRoutes);
    }

    private static string NormalizeEndpointText(string value)
    {
        if (!TryParseEndpointText(value, out var address, out var port))
        {
            throw new FormatException($"Endpoint must use address:port format: {value}");
        }

        return $"{address}:{port}";
    }

    private static bool TryParseEndpointText(string value, out string address, out int port)
    {
        address = string.Empty;
        port = 0;

        var lastColon = value.LastIndexOf(':');
        if (lastColon <= 0 || lastColon == value.Length - 1)
        {
            return false;
        }

        var portText = value[(lastColon + 1)..];
        if (!int.TryParse(portText, out port) || port is < 0 or > ushort.MaxValue)
        {
            return false;
        }

        address = value[..lastColon];
        return !string.IsNullOrWhiteSpace(address);
    }

    internal sealed record ClientLauncherTraceAuditOptions(
        string ReportPath,
        IReadOnlyList<string> AllowedExternalRoutes);

    internal sealed record ClientLauncherTraceAuditReport(
        IReadOnlyList<ClientLauncherTraceConnection> Connections,
        IReadOnlyList<ClientLauncherTraceInstrumentationEvidence> InstrumentationEvidence,
        IReadOnlyList<ClientLauncherTraceObservedProcess> ObservedProcesses,
        IReadOnlyList<ClientLauncherTraceGameInfoLine> GameInfoTraceLines,
        IReadOnlyList<ClientLauncherTraceSidecarLogEvidence> SidecarLogEvidence)
    {
        public int ExternalRouteCount => Connections.Count(connection => connection.Classification == ClientLauncherTraceRouteClassification.ExternalRoute);

        public int ObservedGameProcessCount => ObservedProcesses.Count(process => IsClientLauncherTraceGameProcessName(process.Name));

        public int GameProcessSpawnCallCount => SidecarLogEvidence.Sum(evidence => evidence.GameProcessSpawnCallCount);

        public int FridaRouteEvidenceLineCount => InstrumentationEvidence.Sum(evidence => evidence.RouteLines);

        public int FridaNetworkEvidenceLineCount => InstrumentationEvidence.Sum(evidence => evidence.NetworkEvidenceLines);

        public int UpdateWebRouteEvidenceCount => FridaRouteEvidenceLineCount;

        public int GameInfoProbeCount => SidecarLogEvidence.Sum(evidence => evidence.GameInfoProbeCount);

        public int RouteUrlLineCount => SidecarLogEvidence.Sum(evidence => evidence.RouteUrlLineCount);

        public int RouteSocketLineCount => SidecarLogEvidence.Sum(evidence => evidence.RouteSocketLineCount);

        public int WinInetRequestLineCount => SidecarLogEvidence.Sum(evidence => evidence.WinInetRequestLineCount);

        public IReadOnlyList<ClientLauncherTraceRouteFenceEvidence> RouteFenceEvidence { get; } =
            ClientLauncherTraceRouteFenceEvidence.Create(Connections);

        public IReadOnlyList<ClientLauncherTraceEvidenceGap> EvidenceGaps { get; } =
            ClientLauncherTraceEvidenceGap.Create(InstrumentationEvidence, SidecarLogEvidence);

        public IReadOnlyList<ClientLauncherTraceLaunchGap> LaunchGaps { get; } =
            ClientLauncherTraceLaunchGap.Create(ObservedProcesses);

        public bool Ready => ExternalRouteCount == 0 && EvidenceGaps.Count == 0 && LaunchGaps.Count == 0;
    }

    internal sealed record ClientLauncherTraceObservedProcess(
        string FirstSeen,
        string LastSeen,
        int Samples,
        int ProcessId,
        int ParentProcessId,
        string Name,
        string Path);

    internal sealed record ClientLauncherTraceGameInfoLine(
        string Time,
        int ProcessId,
        string Path,
        string Detail);

    internal sealed record ClientLauncherTraceSidecarLogReference(
        string Mode,
        string ProcessName,
        string LogPath,
        string StdOutPath,
        string StdErrPath);

    internal sealed record ClientLauncherTraceSidecarLogEvidence(
        string Mode,
        string ProcessName,
        string LogPath,
        bool Exists,
        int IniProbeCount,
        int FileProbeCount,
        int GameInfoProbeCount,
        int ProcessCallCount,
        int GameProcessSpawnCallCount,
        int RouteUrlLineCount,
        int RouteSocketLineCount,
        int WinInetRequestLineCount,
        int FridaHelperFailureCount,
        string FirstFridaHelperFailure);

    internal sealed record ClientLauncherTraceConnection(
        string FirstSeen,
        string LastSeen,
        int Samples,
        int ProcessId,
        string LocalEndpoint,
        string RemoteEndpoint,
        string State,
        ClientLauncherTraceRouteClassification Classification)
    {
        public bool IsExternal => Classification == ClientLauncherTraceRouteClassification.ExternalRoute;
    }

    private static bool IsClientLauncherTraceGameProcessName(string name)
    {
        return string.Equals(name, "Conquer.exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "._cache_Conquer.exe", StringComparison.OrdinalIgnoreCase);
    }

    internal enum ClientLauncherTraceRouteClassification
    {
        LocalBind,
        LocalRoute,
        ExpectedGameRoute,
        AllowedExternalRoute,
        ExternalRoute,
    }

    internal sealed record ClientLauncherTraceInstrumentationEvidence(
        string Mode,
        string ProcessName,
        string TargetPath,
        int HelperProcessId,
        bool Exited,
        int? ExitCode,
        long LogLength,
        int RouteLines,
        int NetworkEvidenceLines);

    internal sealed record ClientLauncherTraceEvidenceGap(
        string Mode,
        string ProcessName,
        string Decision,
        string Reason,
        long LogLength,
        int RouteLines,
        int NetworkEvidenceLines)
    {
        public static IReadOnlyList<ClientLauncherTraceEvidenceGap> Create(
            IReadOnlyList<ClientLauncherTraceInstrumentationEvidence> instrumentationEvidence,
            IReadOnlyList<ClientLauncherTraceSidecarLogEvidence> sidecarLogEvidence)
        {
            var gaps = new List<ClientLauncherTraceEvidenceGap>();
            foreach (var evidence in instrumentationEvidence)
            {
                if (evidence.LogLength > 0)
                {
                    continue;
                }

                gaps.Add(new ClientLauncherTraceEvidenceGap(
                    evidence.Mode,
                    evidence.ProcessName,
                    "trace-evidence-gap",
                    "frida-helper-produced-no-log",
                    evidence.LogLength,
                    evidence.RouteLines,
                    evidence.NetworkEvidenceLines));
            }

            foreach (var evidence in sidecarLogEvidence)
            {
                if (evidence.FridaHelperFailureCount == 0)
                {
                    continue;
                }

                gaps.Add(new ClientLauncherTraceEvidenceGap(
                    evidence.Mode,
                    evidence.ProcessName,
                    "trace-evidence-gap",
                    "frida-helper-failed-to-attach",
                    0,
                    0,
                    0));
            }

            return gaps;
        }
    }

    internal sealed record ClientLauncherTraceLaunchGap(
        string Decision,
        string Expected,
        string Observed,
        string Reason)
    {
        public static IReadOnlyList<ClientLauncherTraceLaunchGap> Create(
            IReadOnlyList<ClientLauncherTraceObservedProcess> observedProcesses)
        {
            if (observedProcesses.Count == 0 ||
                observedProcesses.Any(process => IsClientLauncherTraceGameProcessName(process.Name)))
            {
                return [];
            }

            var observed = string.Join(
                ',',
                observedProcesses
                    .Select(process => process.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            return
            [
                new ClientLauncherTraceLaunchGap(
                    "no-game-process-spawn",
                    "Conquer.exe",
                    observed,
                    "no Conquer.exe child observed in launcher process trace"),
            ];
        }
    }

    internal sealed record ClientLauncherTraceRouteFenceEvidence(
        string RouteName,
        string RemoteEndpoint,
        string Decision,
        string Reason,
        int ProcessId,
        string LocalEndpoint,
        string State,
        int Samples)
    {
        private static readonly string CachedDx9LoginRouteEndpoint =
            Environment.GetEnvironmentVariable("WOLFCONQUER_CACHED_LOGIN_ROUTE_ENDPOINT") ?? "192.0.2.10:9865";

        public static IReadOnlyList<ClientLauncherTraceRouteFenceEvidence> Create(
            IReadOnlyList<ClientLauncherTraceConnection> connections)
        {
            var evidence = new List<ClientLauncherTraceRouteFenceEvidence>();
            foreach (var connection in connections)
            {
                if (!string.Equals(connection.RemoteEndpoint, CachedDx9LoginRouteEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var allowed = connection.Classification == ClientLauncherTraceRouteClassification.AllowedExternalRoute;
                evidence.Add(new ClientLauncherTraceRouteFenceEvidence(
                    "cached-dx9-login-route",
                    connection.RemoteEndpoint,
                    allowed ? "allowed-external-research" : "needs-local-fence",
                    allowed
                        ? "explicitly allowed by --allow-external; still not locally fenced"
                        : "unexpected external cached DX9 login route",
                    connection.ProcessId,
                    connection.LocalEndpoint,
                    connection.State,
                    connection.Samples));
            }

            return evidence;
        }
    }

    internal static class ClientLauncherTraceAuditLogic
    {
        public static ClientLauncherTraceAuditReport Analyze(
            IReadOnlyList<string> lines,
            IReadOnlyList<string> allowedExternalRoutes,
            string? reportDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(lines);
            ArgumentNullException.ThrowIfNull(allowedExternalRoutes);

            var allowed = new HashSet<string>(
                allowedExternalRoutes.Select(NormalizeEndpointText),
                StringComparer.OrdinalIgnoreCase);
            var connections = new List<ClientLauncherTraceConnection>();
            var instrumentationEvidence = new List<ClientLauncherTraceInstrumentationEvidence>();
            var observedProcesses = new List<ClientLauncherTraceObservedProcess>();
            var gameInfoTraceLines = new List<ClientLauncherTraceGameInfoLine>();
            var sidecarLogReferences = new List<ClientLauncherTraceSidecarLogReference>();
            var instrumentationEvidenceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inTcpSection = false;
            var inObservedProcessSection = false;
            var inLauncherFileEvidenceSection = false;
            var inFridaEvidenceSection = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (line.Length == 0)
                {
                    if (inTcpSection && connections.Count > 0)
                    {
                        inTcpSection = false;
                    }

                    if (inFridaEvidenceSection)
                    {
                        inFridaEvidenceSection = false;
                    }

                    if (inObservedProcessSection)
                    {
                        inObservedProcessSection = false;
                    }

                    if (inLauncherFileEvidenceSection)
                    {
                        inLauncherFileEvidenceSection = false;
                    }

                    continue;
                }

                if (string.Equals(line.Trim(), "Observed client processes", StringComparison.OrdinalIgnoreCase))
                {
                    inTcpSection = false;
                    inFridaEvidenceSection = false;
                    inLauncherFileEvidenceSection = false;
                    inObservedProcessSection = true;
                    continue;
                }

                if (string.Equals(line.Trim(), "Observed launcher file evidence", StringComparison.OrdinalIgnoreCase))
                {
                    inTcpSection = false;
                    inObservedProcessSection = false;
                    inFridaEvidenceSection = false;
                    inLauncherFileEvidenceSection = true;
                    continue;
                }

                if (string.Equals(line.Trim(), "Observed TCP connections", StringComparison.OrdinalIgnoreCase))
                {
                    inTcpSection = true;
                    inObservedProcessSection = false;
                    inLauncherFileEvidenceSection = false;
                    continue;
                }

                if (IsClientLauncherTraceFridaEvidenceSection(line.Trim()))
                {
                    inTcpSection = false;
                    inObservedProcessSection = false;
                    inLauncherFileEvidenceSection = false;
                    inFridaEvidenceSection = true;
                    continue;
                }

                if (inObservedProcessSection)
                {
                    if (TryParseClientLauncherTraceObservedProcess(line, out var process))
                    {
                        observedProcesses.Add(process);
                    }

                    continue;
                }

                if (inLauncherFileEvidenceSection)
                {
                    if (TryParseClientLauncherTraceGameInfoLine(line, out var gameInfoLine))
                    {
                        gameInfoTraceLines.Add(gameInfoLine);
                    }

                    continue;
                }

                if (inTcpSection)
                {
                    var match = LauncherTraceTcpRowRegex.Match(line);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var remoteEndpoint = NormalizeEndpointText(match.Groups["remote"].Value);
                    var classification = ClassifyRoute(remoteEndpoint, match.Groups["state"].Value, allowed);
                    connections.Add(new ClientLauncherTraceConnection(
                        match.Groups["first"].Value,
                        match.Groups["last"].Value,
                        int.Parse(match.Groups["samples"].Value),
                        int.Parse(match.Groups["pid"].Value),
                        NormalizeEndpointText(match.Groups["local"].Value),
                        remoteEndpoint,
                        match.Groups["state"].Value,
                        classification));
                    continue;
                }

                if (TryParseClientLauncherTraceSidecarLogReference(line, reportDirectory, out var sidecarLogReference))
                {
                    sidecarLogReferences.Add(sidecarLogReference);
                    continue;
                }

                if (!inFridaEvidenceSection || !TryParseClientLauncherTraceInstrumentationEvidence(line, out var evidence))
                {
                    continue;
                }

                var evidenceKey = $"{evidence.Mode}|{evidence.ProcessName}|{evidence.TargetPath}|{evidence.HelperProcessId}|{evidence.LogLength}|{evidence.RouteLines}|{evidence.NetworkEvidenceLines}";
                if (instrumentationEvidenceKeys.Add(evidenceKey))
                {
                    instrumentationEvidence.Add(evidence);
                }
            }

            var sidecarLogEvidence = ReadClientLauncherTraceSidecarLogEvidence(sidecarLogReferences);
            return new ClientLauncherTraceAuditReport(
                connections,
                instrumentationEvidence,
                observedProcesses,
                gameInfoTraceLines,
                sidecarLogEvidence);
        }

        public static string FormatReport(string path, ClientLauncherTraceAuditReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Client launcher trace audit: {path}");
            builder.AppendLine($"Connections: {report.Connections.Count:N0}");
            builder.AppendLine($"External routes: {report.ExternalRouteCount:N0}");
            builder.AppendLine($"Frida sidecar evidence: {report.InstrumentationEvidence.Count:N0}");
            builder.AppendLine($"Frida route evidence lines: {report.FridaRouteEvidenceLineCount:N0}");
            builder.AppendLine($"Frida network evidence lines: {report.FridaNetworkEvidenceLineCount:N0}");
            builder.AppendLine($"Update/web route evidence: {report.UpdateWebRouteEvidenceCount:N0}");
            foreach (var evidence in report.InstrumentationEvidence.Where(evidence => evidence.RouteLines > 0 || evidence.NetworkEvidenceLines > 0))
            {
                builder.AppendLine(
                    $"route-evidence mode={evidence.Mode} process={evidence.ProcessName} " +
                    $"logLength={evidence.LogLength} routeLines={evidence.RouteLines} " +
                    $"networkEvidenceLines={evidence.NetworkEvidenceLines}");
            }

            builder.AppendLine($"Observed game processes: {report.ObservedGameProcessCount:N0}");
            foreach (var process in report.ObservedProcesses.Where(process => IsClientLauncherTraceGameProcessName(process.Name)))
            {
                builder.AppendLine(
                    $"observed-game-process pid={process.ProcessId} parentPid={process.ParentProcessId} " +
                    $"name={process.Name} samples={process.Samples} path={process.Path}");
            }

            builder.AppendLine($"Game process spawns: {report.ObservedGameProcessCount:N0}");
            builder.AppendLine($"Game process spawn calls: {report.GameProcessSpawnCallCount:N0}");
            foreach (var gap in report.LaunchGaps)
            {
                builder.AppendLine(
                    $"{gap.Decision} expected={gap.Expected} observed={gap.Observed} reason={gap.Reason}");
            }

            builder.AppendLine($"GameInfo trace lines: {report.GameInfoTraceLines.Count:N0}");
            foreach (var gameInfoLine in report.GameInfoTraceLines)
            {
                builder.AppendLine(
                    $"gameinfo-line path={gameInfoLine.Path} pid={gameInfoLine.ProcessId} detail={gameInfoLine.Detail}");
            }

            builder.AppendLine($"GameInfo probes: {report.GameInfoProbeCount:N0}");
            builder.AppendLine($"Launcher route URL lines: {report.RouteUrlLineCount:N0}");
            builder.AppendLine($"Launcher route socket lines: {report.RouteSocketLineCount:N0}");
            builder.AppendLine($"WinINet request lines: {report.WinInetRequestLineCount:N0}");
            foreach (var evidence in report.SidecarLogEvidence)
            {
                builder.AppendLine(
                    $"sidecar-log mode={evidence.Mode} process={evidence.ProcessName} exists={evidence.Exists} " +
                    $"ini={evidence.IniProbeCount} file={evidence.FileProbeCount} gameInfo={evidence.GameInfoProbeCount} " +
                    $"processCalls={evidence.ProcessCallCount} gameSpawns={evidence.GameProcessSpawnCallCount} " +
                    $"routeUrl={evidence.RouteUrlLineCount} routeSocket={evidence.RouteSocketLineCount} " +
                    $"wininet={evidence.WinInetRequestLineCount} fridaFailures={evidence.FridaHelperFailureCount} " +
                    $"path={evidence.LogPath}");
                if (evidence.FirstFridaHelperFailure.Length > 0)
                {
                    builder.AppendLine($"frida-helper-failure process={evidence.ProcessName} message={evidence.FirstFridaHelperFailure}");
                }
            }

            builder.AppendLine($"Trace evidence gaps: {report.EvidenceGaps.Count:N0}");
            foreach (var gap in report.EvidenceGaps)
            {
                builder.AppendLine(
                    $"{gap.Decision} mode={gap.Mode} process={gap.ProcessName} " +
                    $"logLength={gap.LogLength} routeLines={gap.RouteLines} networkEvidenceLines={gap.NetworkEvidenceLines} " +
                    $"reason={gap.Reason}");
            }

            builder.AppendLine($"Route fences: {report.RouteFenceEvidence.Count:N0}");
            foreach (var evidence in report.RouteFenceEvidence)
            {
                builder.AppendLine(
                    $"{evidence.Decision} route={evidence.RouteName} " +
                    $"remote={evidence.RemoteEndpoint} local={evidence.LocalEndpoint} " +
                    $"pid={evidence.ProcessId} state={evidence.State} samples={evidence.Samples} " +
                    $"reason={evidence.Reason}");
            }

            foreach (var connection in report.Connections)
            {
                builder.AppendLine(
                    $"{FormatClassification(connection.Classification)} pid={connection.ProcessId} " +
                    $"local={connection.LocalEndpoint} remote={connection.RemoteEndpoint} " +
                    $"state={connection.State} samples={connection.Samples}");
            }

            builder.AppendLine($"Ready: {report.Ready}");
            return builder.ToString();
        }

        private static bool TryParseClientLauncherTraceObservedProcess(
            string line,
            out ClientLauncherTraceObservedProcess process)
        {
            process = default!;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 ||
                string.Equals(trimmed, "(none)", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("FirstSeen", StringComparison.OrdinalIgnoreCase) ||
                trimmed.All(c => c == '-' || char.IsWhiteSpace(c)))
            {
                return false;
            }

            var match = LauncherTraceProcessRowRegex.Match(line);
            if (!match.Success)
            {
                return false;
            }

            process = new ClientLauncherTraceObservedProcess(
                match.Groups["first"].Value,
                match.Groups["last"].Value,
                int.Parse(match.Groups["samples"].Value),
                int.Parse(match.Groups["pid"].Value),
                int.TryParse(match.Groups["parent"].Value, out var parentProcessId) ? parentProcessId : 0,
                match.Groups["name"].Value,
                match.Groups["path"].Value.Trim());
            return true;
        }

        private static bool TryParseClientLauncherTraceGameInfoLine(
            string line,
            out ClientLauncherTraceGameInfoLine gameInfoLine)
        {
            gameInfoLine = default!;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 ||
                string.Equals(trimmed, "(none)", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Time", StringComparison.OrdinalIgnoreCase) ||
                trimmed.All(c => c == '-' || char.IsWhiteSpace(c)))
            {
                return false;
            }

            var match = LauncherTraceGameInfoLineRegex.Match(line);
            if (!match.Success)
            {
                return false;
            }

            gameInfoLine = new ClientLauncherTraceGameInfoLine(
                match.Groups["time"].Value,
                int.Parse(match.Groups["pid"].Value),
                NormalizeClientLauncherTraceGameInfoPath(match.Groups["path"].Value),
                match.Groups["detail"].Value.Trim());
            return true;
        }

        private static string NormalizeClientLauncherTraceGameInfoPath(string path)
        {
            var normalized = path.Replace('\\', '/');
            return normalized.EndsWith("/ini/GameInfo.ini", StringComparison.OrdinalIgnoreCase)
                ? "ini/GameInfo.ini"
                : "GameInfo.ini";
        }

        private static bool TryParseClientLauncherTraceSidecarLogReference(
            string line,
            string? reportDirectory,
            out ClientLauncherTraceSidecarLogReference reference)
        {
            reference = default!;
            var match = LauncherTraceSidecarLogRegex.Match(line.Trim());
            if (!match.Success)
            {
                return false;
            }

            var logPath = ResolveClientLauncherTraceSidecarPath(match.Groups["log"].Value.Trim(), reportDirectory);
            var stdoutPath = ResolveClientLauncherTraceSidecarPath(match.Groups["stdout"].Value.Trim(), reportDirectory);
            var stderrPath = ResolveClientLauncherTraceSidecarPath(match.Groups["stderr"].Value.Trim(), reportDirectory);

            reference = new ClientLauncherTraceSidecarLogReference(
                match.Groups["mode"].Value,
                match.Groups["process"].Value,
                logPath,
                stdoutPath,
                stderrPath);
            return true;
        }

        private static string ResolveClientLauncherTraceSidecarPath(string path, string? reportDirectory)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathFullyQualified(path) || string.IsNullOrWhiteSpace(reportDirectory))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(reportDirectory, path));
        }

        private static IReadOnlyList<ClientLauncherTraceSidecarLogEvidence> ReadClientLauncherTraceSidecarLogEvidence(
            IReadOnlyList<ClientLauncherTraceSidecarLogReference> references)
        {
            var evidence = new List<ClientLauncherTraceSidecarLogEvidence>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in references)
            {
                var key = $"{reference.Mode}|{reference.ProcessName}|{reference.LogPath}";
                if (!seen.Add(key))
                {
                    continue;
                }

                evidence.Add(ReadClientLauncherTraceSidecarLogEvidence(reference));
            }

            return evidence;
        }

        private static ClientLauncherTraceSidecarLogEvidence ReadClientLauncherTraceSidecarLogEvidence(
            ClientLauncherTraceSidecarLogReference reference)
        {
            if (!File.Exists(reference.LogPath))
            {
                return new ClientLauncherTraceSidecarLogEvidence(
                    reference.Mode,
                    reference.ProcessName,
                    reference.LogPath,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    string.Empty);
            }

            var iniProbeCount = 0;
            var fileProbeCount = 0;
            var gameInfoProbeCount = 0;
            var processCallCount = 0;
            var gameProcessSpawnCallCount = 0;
            var routeUrlLineCount = 0;
            var routeSocketLineCount = 0;
            var winInetRequestLineCount = 0;
            var helperFailures = ReadClientLauncherTraceFridaHelperFailures(reference.StdOutPath)
                .Concat(ReadClientLauncherTraceFridaHelperFailures(reference.StdErrPath))
                .ToArray();

            foreach (var line in File.ReadLines(reference.LogPath))
            {
                if (line.StartsWith("[ini] ", StringComparison.Ordinal))
                {
                    iniProbeCount++;
                }

                if (line.StartsWith("[file] ", StringComparison.Ordinal))
                {
                    fileProbeCount++;
                }

                if (line.Contains("GameInfo.ini", StringComparison.OrdinalIgnoreCase))
                {
                    gameInfoProbeCount++;
                }

                if (line.StartsWith("[process] ", StringComparison.Ordinal))
                {
                    processCallCount++;
                    if (line.Contains("Conquer.exe", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("._cache_Conquer.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        gameProcessSpawnCallCount++;
                    }
                }

                if (line.StartsWith("[route-url] ", StringComparison.Ordinal))
                {
                    routeUrlLineCount++;
                }

                if (line.StartsWith("[route-socket] ", StringComparison.Ordinal))
                {
                    routeSocketLineCount++;
                }

                if (line.StartsWith("[wininet] ", StringComparison.Ordinal))
                {
                    winInetRequestLineCount++;
                }
            }

            return new ClientLauncherTraceSidecarLogEvidence(
                reference.Mode,
                reference.ProcessName,
                reference.LogPath,
                true,
                iniProbeCount,
                fileProbeCount,
                gameInfoProbeCount,
                processCallCount,
                gameProcessSpawnCallCount,
                routeUrlLineCount,
                routeSocketLineCount,
                winInetRequestLineCount,
                helperFailures.Length,
                helperFailures.FirstOrDefault() ?? string.Empty);
        }

        private static IReadOnlyList<string> ReadClientLauncherTraceFridaHelperFailures(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return [];
            }

            var failures = new List<string>();
            foreach (var line in File.ReadLines(path))
            {
                if (line.Contains("Failed to attach", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Failed to spawn", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("refused to load frida-agent", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("terminated during injection", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(line.Trim());
                }
            }

            return failures;
        }

        private static bool IsClientLauncherTraceFridaEvidenceSection(string line)
        {
            return ClientLauncherTraceFridaEvidenceSections.Any(section => string.Equals(section, line, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryParseClientLauncherTraceInstrumentationEvidence(
            string line,
            out ClientLauncherTraceInstrumentationEvidence evidence)
        {
            evidence = default!;
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 8 ||
                string.Equals(tokens[0], "Mode", StringComparison.OrdinalIgnoreCase) ||
                tokens[0].All(c => c == '-'))
            {
                return false;
            }

            var targetPathIndex = Array.FindIndex(tokens, token => token.Contains(@":\", StringComparison.Ordinal));
            if (targetPathIndex < 2 || tokens.Length - targetPathIndex < 6)
            {
                return false;
            }

            var mode = tokens[0];
            var processName = tokens[1];
            var targetPath = tokens[targetPathIndex];
            if (!int.TryParse(tokens[targetPathIndex + 1], out var helperProcessId) ||
                !bool.TryParse(tokens[targetPathIndex + 2], out var exited))
            {
                return false;
            }

            var trailing = tokens[(targetPathIndex + 3)..];
            int? exitCode = null;
            var numericStartIndex = 0;
            if (trailing.Length is 4 or 6)
            {
                if (!int.TryParse(trailing[0], out var parsedExitCode))
                {
                    return false;
                }

                exitCode = parsedExitCode;
                numericStartIndex = 1;
            }

            if (trailing.Length - numericStartIndex < 3 ||
                !long.TryParse(trailing[numericStartIndex], out var logLength) ||
                !int.TryParse(trailing[numericStartIndex + 1], out var routeLines) ||
                !int.TryParse(trailing[numericStartIndex + 2], out var networkEvidenceLines))
            {
                return false;
            }

            evidence = new ClientLauncherTraceInstrumentationEvidence(
                mode,
                processName,
                targetPath,
                helperProcessId,
                exited,
                exitCode,
                logLength,
                routeLines,
                networkEvidenceLines);
            return true;
        }

        private static ClientLauncherTraceRouteClassification ClassifyRoute(
            string remoteEndpoint,
            string state,
            HashSet<string> allowedExternalRoutes)
        {
            if (IsLocalBind(remoteEndpoint, state))
            {
                return ClientLauncherTraceRouteClassification.LocalBind;
            }

            if (IsLoopbackRoute(remoteEndpoint))
            {
                return ClientLauncherTraceRouteClassification.LocalRoute;
            }

            if (IsExpectedGameRoute(remoteEndpoint))
            {
                return ClientLauncherTraceRouteClassification.ExpectedGameRoute;
            }

            if (allowedExternalRoutes.Contains(remoteEndpoint))
            {
                return ClientLauncherTraceRouteClassification.AllowedExternalRoute;
            }

            return ClientLauncherTraceRouteClassification.ExternalRoute;
        }

        private static bool IsLocalBind(string remoteEndpoint, string state)
        {
            return string.Equals(state, "Bound", StringComparison.OrdinalIgnoreCase)
                && string.Equals(remoteEndpoint, "0.0.0.0:0", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLoopbackRoute(string remoteEndpoint)
        {
            return TrySplitEndpoint(remoteEndpoint, out var address, out _)
                && IPAddress.TryParse(address, out var ipAddress)
                && IPAddress.IsLoopback(ipAddress);
        }

        private static bool IsExpectedGameRoute(string remoteEndpoint)
        {
            if (!TrySplitEndpoint(remoteEndpoint, out var address, out var port))
            {
                return false;
            }

            return string.Equals(address, ConquerRuntimeDefaults.ServerAddress, StringComparison.OrdinalIgnoreCase)
                && port is > 0 and <= ushort.MaxValue;
        }

        private static bool TrySplitEndpoint(string endpoint, out string address, out int port)
        {
            return TryParseEndpointText(endpoint, out address, out port);
        }

        private static string FormatClassification(ClientLauncherTraceRouteClassification classification)
        {
            return classification switch
            {
                ClientLauncherTraceRouteClassification.LocalBind => "local-bind",
                ClientLauncherTraceRouteClassification.LocalRoute => "local-route",
                ClientLauncherTraceRouteClassification.ExpectedGameRoute => "expected-game-route",
                ClientLauncherTraceRouteClassification.AllowedExternalRoute => "allowed-external-route",
                _ => "external-route",
            };
        }
    }
}
