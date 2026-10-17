using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WolfConquer.Core.World;

internal static partial class Program
{
    private const string VisibleLoginLineMarkersUsage =
        "Usage: WolfConquer.DataTool visible-login-line-markers --account-log path --gateway-log path --world-log path [--client-log path] [--output-json path]";

    private const string VisibleLoginSeamAuditUsage =
        "Usage: WolfConquer.DataTool visible-login-seam-audit --account-log path --gateway-log path --world-log path " +
        "[--client-log path] [--gateway-since-line n] [--account-since-line n] [--world-since-line n] [--client-since-line n] [--line-markers path]";

    static int VisibleLoginLineMarkers(string[] args)
    {
        if (!TryParseVisibleLoginLineMarkerOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(VisibleLoginLineMarkersUsage);
            return 1;
        }

        VisibleLoginLineMarkerReport report;
        try
        {
            report = VisibleLoginLineMarkerCapturer.Capture(options);
            if (options.OutputJsonPath is not null)
            {
                VisibleLoginLineMarkerCapturer.WriteJson(report, options.OutputJsonPath);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not capture visible login line markers: {ex.Message}");
            return 1;
        }

        Console.WriteLine(VisibleLoginLineMarkerCapturer.FormatReport(report, options.OutputJsonPath));
        return 0;
    }

    static int VisibleLoginSeamAudit(string[] args)
    {
        if (!TryParseVisibleLoginSeamAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(VisibleLoginSeamAuditUsage);
            return 1;
        }

        VisibleLoginSeamAuditReport report;
        try
        {
            report = VisibleLoginSeamLogAuditor.Audit(options);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit visible login seam logs: {ex.Message}");
            return 1;
        }

        Console.WriteLine(VisibleLoginSeamLogAuditor.FormatReport(report));
        return report.Ready ? 0 : 2;
    }

    private static bool TryParseVisibleLoginLineMarkerOptions(
        string[] args,
        out VisibleLoginLineMarkerOptions options,
        out string error)
    {
        options = new VisibleLoginLineMarkerOptions("", "", "", null, null);
        error = "";

        string? accountLogPath = null;
        string? gatewayLogPath = null;
        string? worldLogPath = null;
        string? clientLogPath = null;
        string? outputJsonPath = null;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (TryReadOptionValue(args, ref index, "--account-log", argument, out var accountValue))
            {
                accountLogPath = Path.GetFullPath(accountValue);
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--gateway-log", argument, out var gatewayValue))
            {
                gatewayLogPath = Path.GetFullPath(gatewayValue);
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--world-log", argument, out var worldValue))
            {
                worldLogPath = Path.GetFullPath(worldValue);
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--client-log", argument, out var clientValue))
            {
                clientLogPath = Path.GetFullPath(clientValue);
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--output-json", argument, out var outputJsonValue))
            {
                outputJsonPath = Path.GetFullPath(outputJsonValue);
                continue;
            }

            error = $"Unknown option: {argument}";
            return false;
        }

        if (accountLogPath is null)
        {
            error = "Missing --account-log path.";
            return false;
        }

        if (gatewayLogPath is null)
        {
            error = "Missing --gateway-log path.";
            return false;
        }

        if (worldLogPath is null)
        {
            error = "Missing --world-log path.";
            return false;
        }

        options = new VisibleLoginLineMarkerOptions(
            accountLogPath,
            gatewayLogPath,
            worldLogPath,
            clientLogPath,
            outputJsonPath);
        return true;
    }

    private static bool TryParseVisibleLoginSeamAuditOptions(
        string[] args,
        out VisibleLoginSeamAuditOptions options,
        out string error)
    {
        options = new VisibleLoginSeamAuditOptions("", "", "", null);
        error = "";

        string? accountLogPath = null;
        string? gatewayLogPath = null;
        string? worldLogPath = null;
        string? clientLogPath = null;
        var accountSinceLine = 0;
        var gatewaySinceLine = 0;
        var worldSinceLine = 0;
        var clientSinceLine = 0;
        var accountSinceLineSet = false;
        var gatewaySinceLineSet = false;
        var worldSinceLineSet = false;
        var clientSinceLineSet = false;
        string? lineMarkersPath = null;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (TryReadOptionValue(args, ref index, "--account-log", argument, out var accountValue))
            {
                accountLogPath = Path.GetFullPath(accountValue);
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--gateway-log", argument, out var gatewayValue))
            {
                gatewayLogPath = Path.GetFullPath(gatewayValue);
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--world-log", argument, out var worldValue))
            {
                worldLogPath = Path.GetFullPath(worldValue);
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--client-log", argument, out var clientValue))
            {
                clientLogPath = Path.GetFullPath(clientValue);
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--account-since-line", argument, out var accountSinceValue))
            {
                if (!TryParseVisibleLoginSinceLine("--account-since-line", accountSinceValue, out accountSinceLine, out error))
                {
                    return false;
                }

                accountSinceLineSet = true;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--gateway-since-line", argument, out var gatewaySinceValue))
            {
                if (!TryParseVisibleLoginSinceLine("--gateway-since-line", gatewaySinceValue, out gatewaySinceLine, out error))
                {
                    return false;
                }

                gatewaySinceLineSet = true;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--world-since-line", argument, out var worldSinceValue))
            {
                if (!TryParseVisibleLoginSinceLine("--world-since-line", worldSinceValue, out worldSinceLine, out error))
                {
                    return false;
                }

                worldSinceLineSet = true;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--client-since-line", argument, out var clientSinceValue))
            {
                if (!TryParseVisibleLoginSinceLine("--client-since-line", clientSinceValue, out clientSinceLine, out error))
                {
                    return false;
                }

                clientSinceLineSet = true;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--line-markers", argument, out var lineMarkersValue))
            {
                lineMarkersPath = Path.GetFullPath(lineMarkersValue);
                continue;
            }

            error = $"Unknown option: {argument}";
            return false;
        }

        if (lineMarkersPath is not null)
        {
            VisibleLoginLineMarkerReport markerReport;
            try
            {
                markerReport = VisibleLoginLineMarkerCapturer.ReadJson(lineMarkersPath);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or JsonException or NotSupportedException or UnauthorizedAccessException)
            {
                error = $"Could not read --line-markers path: {ex.Message}";
                return false;
            }

            accountLogPath ??= markerReport.AccountLogPath;
            gatewayLogPath ??= markerReport.GatewayLogPath;
            worldLogPath ??= markerReport.WorldLogPath;
            clientLogPath ??= markerReport.ClientLogPath;
            if (!accountSinceLineSet)
            {
                accountSinceLine = markerReport.AccountLineMarker;
            }

            if (!gatewaySinceLineSet)
            {
                gatewaySinceLine = markerReport.GatewayLineMarker;
            }

            if (!worldSinceLineSet)
            {
                worldSinceLine = markerReport.WorldLineMarker;
            }

            if (!clientSinceLineSet)
            {
                clientSinceLine = markerReport.ClientLineMarker ?? 0;
            }
        }

        if (accountLogPath is null)
        {
            error = "Missing --account-log path.";
            return false;
        }

        if (gatewayLogPath is null)
        {
            error = "Missing --gateway-log path.";
            return false;
        }

        if (worldLogPath is null)
        {
            error = "Missing --world-log path.";
            return false;
        }

        options = new VisibleLoginSeamAuditOptions(
            accountLogPath,
            gatewayLogPath,
            worldLogPath,
            clientLogPath,
            accountSinceLine,
            gatewaySinceLine,
            worldSinceLine,
            clientSinceLine);
        return true;
    }

    private static bool TryParseVisibleLoginSinceLine(
        string optionName,
        string value,
        out int line,
        out string error)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out line) || line < 0)
        {
            error = $"{optionName} must be a non-negative integer.";
            return false;
        }

        error = "";
        return true;
    }
}

internal sealed record VisibleLoginLineMarkerOptions(
    string AccountLogPath,
    string GatewayLogPath,
    string WorldLogPath,
    string? ClientLogPath,
    string? OutputJsonPath);

internal sealed record VisibleLoginLineMarkerReport(
    string AccountLogPath,
    string GatewayLogPath,
    string WorldLogPath,
    string? ClientLogPath,
    int AccountLineMarker,
    int GatewayLineMarker,
    int WorldLineMarker,
    int? ClientLineMarker);

internal sealed record VisibleLoginSeamAuditOptions(
    string AccountLogPath,
    string GatewayLogPath,
    string WorldLogPath,
    string? ClientLogPath,
    int AccountSinceLine = 0,
    int GatewaySinceLine = 0,
    int WorldSinceLine = 0,
    int ClientSinceLine = 0);

internal sealed record VisibleLoginSeamAuditReport(
    string AccountLogPath,
    string GatewayLogPath,
    string WorldLogPath,
    string? ClientLogPath,
    int AccountSinceLine,
    int GatewaySinceLine,
    int WorldSinceLine,
    int ClientSinceLine,
    VisibleLoginGatewaySummary Gateway,
    VisibleLoginAccountSummary Account,
    VisibleLoginWorldSummary World,
    VisibleLoginClientSummary Client)
{
    public bool Ready =>
        Gateway.WelcomeHandled &&
        Account.Account2Ready &&
        World.Success is not null &&
        World.ClosedAfterSuccess;
}

internal sealed record VisibleLoginGatewaySummary(
    int ScannedLines,
    bool WelcomeHandled,
    int? WelcomeHandledLine);

internal sealed record VisibleLoginAccountSummary(
    int ScannedLines,
    bool Account2Ready,
    int? Account2ReadyLine);

internal sealed record VisibleLoginWorldSummary(
    int ScannedLines,
    VisibleLoginWorldSuccess? Success,
    bool ClosedAfterSuccess,
    int? ClosedAfterSuccessLine,
    VisibleLoginOutboundSequence OutboundBeforeSuccess,
    VisibleLoginSkippedOutboundSequence SkippedOutboundBeforeSuccess,
    VisibleLoginOutboundSequence OutboundAfterSuccessBeforeClose,
    VisibleLoginOutboundFrame? LastOutboundBeforeClose);

internal sealed record VisibleLoginWorldSuccess(
    int Line,
    string Key,
    string Uid,
    string Map);

internal sealed record VisibleLoginOutboundFrame(
    int Line,
    int FrameLength,
    ushort Opcode,
    int PayloadLength,
    VisibleLoginGameItemPreview? GameItem,
    VisibleLoginServerConfigPreview? ServerConfig);

internal sealed record VisibleLoginSkippedOutboundFrame(
    int Line,
    string Reason,
    int FrameLength,
    ushort Opcode,
    int PayloadLength);

internal sealed record VisibleLoginServerConfigPreview(
    uint Reserved0,
    uint Reserved1,
    uint Value15,
    uint Value9999,
    uint Value3,
    byte Flag);

internal sealed record VisibleLoginOutboundSequence(
    int TotalCount,
    IReadOnlyList<VisibleLoginOutboundFrame> Frames,
    bool IsTail);

internal sealed record VisibleLoginSkippedOutboundSequence(
    int TotalCount,
    IReadOnlyList<VisibleLoginSkippedOutboundFrame> Frames,
    bool IsTail);

internal sealed record VisibleLoginGameItemPreview(
    uint Uid,
    uint ItemId,
    ushort Durability,
    ushort MaxDurability,
    ushort Mode,
    byte Position,
    uint? SpellId,
    byte? Effect,
    byte? Plus,
    byte? Bless,
    byte? Bound,
    byte? Enchant,
    ushort? StackSize,
    uint? TimeLeftInMinutes,
    uint? RuneExperience);

internal sealed record VisibleLoginClientSummary(
    int ScannedLines,
    IReadOnlyList<VisibleLoginClientWarning> Warnings);

internal sealed record VisibleLoginClientWarning(
    int Line,
    string Kind,
    string Preview);

internal static class VisibleLoginLineMarkerCapturer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static VisibleLoginLineMarkerReport Capture(VisibleLoginLineMarkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        VisibleLoginSeamLogAuditor.RequireFile(options.AccountLogPath, "Account log");
        VisibleLoginSeamLogAuditor.RequireFile(options.GatewayLogPath, "Gateway log");
        VisibleLoginSeamLogAuditor.RequireFile(options.WorldLogPath, "World log");
        if (options.ClientLogPath is not null)
        {
            VisibleLoginSeamLogAuditor.RequireFile(options.ClientLogPath, "Client debug log");
        }

        return new VisibleLoginLineMarkerReport(
            options.AccountLogPath,
            options.GatewayLogPath,
            options.WorldLogPath,
            options.ClientLogPath,
            CountSharedLogLines(options.AccountLogPath),
            CountSharedLogLines(options.GatewayLogPath),
            CountSharedLogLines(options.WorldLogPath),
            options.ClientLogPath is null ? null : CountSharedLogLines(options.ClientLogPath));
    }

    public static void WriteJson(VisibleLoginLineMarkerReport report, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine);
    }

    public static VisibleLoginLineMarkerReport ReadJson(string path)
    {
        var fullPath = Path.GetFullPath(path);
        VisibleLoginSeamLogAuditor.RequireFile(fullPath, "Visible login line marker JSON");
        var report = JsonSerializer.Deserialize<VisibleLoginLineMarkerReport>(
            File.ReadAllText(fullPath),
            JsonOptions);
        if (report is null)
        {
            throw new ArgumentException("Visible login line marker JSON is empty.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(report.AccountLogPath))
        {
            throw new ArgumentException("Visible login line marker JSON is missing AccountLogPath.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(report.GatewayLogPath))
        {
            throw new ArgumentException("Visible login line marker JSON is missing GatewayLogPath.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(report.WorldLogPath))
        {
            throw new ArgumentException("Visible login line marker JSON is missing WorldLogPath.", nameof(path));
        }

        if (report.AccountLineMarker < 0 ||
            report.GatewayLineMarker < 0 ||
            report.WorldLineMarker < 0 ||
            report.ClientLineMarker < 0)
        {
            throw new ArgumentException("Visible login line marker JSON contains a negative line marker.", nameof(path));
        }

        return report with
        {
            AccountLogPath = Path.GetFullPath(report.AccountLogPath),
            GatewayLogPath = Path.GetFullPath(report.GatewayLogPath),
            WorldLogPath = Path.GetFullPath(report.WorldLogPath),
            ClientLogPath = string.IsNullOrWhiteSpace(report.ClientLogPath)
                ? null
                : Path.GetFullPath(report.ClientLogPath),
        };
    }

    public static string FormatReport(VisibleLoginLineMarkerReport report, string? outputJsonPath = null)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Visible login line markers:");
        builder.AppendLine($"Gateway lineMarker={report.GatewayLineMarker.ToString(CultureInfo.InvariantCulture)} path={report.GatewayLogPath}");
        builder.AppendLine($"Account lineMarker={report.AccountLineMarker.ToString(CultureInfo.InvariantCulture)} path={report.AccountLogPath}");
        builder.AppendLine($"World lineMarker={report.WorldLineMarker.ToString(CultureInfo.InvariantCulture)} path={report.WorldLogPath}");
        builder.AppendLine(report.ClientLogPath is null
            ? "Client lineMarker=<none> path=<none>"
            : $"Client lineMarker={report.ClientLineMarker!.Value.ToString(CultureInfo.InvariantCulture)} path={report.ClientLogPath}");
        if (outputJsonPath is not null)
        {
            builder.AppendLine($"Marker JSON: {Path.GetFullPath(outputJsonPath)}");
        }

        builder.AppendLine("After the manual 7899 login, run:");
        builder.AppendLine(FormatAuditCommand(report, outputJsonPath));
        return builder.ToString();
    }

    private static string FormatAuditCommand(VisibleLoginLineMarkerReport report, string? outputJsonPath)
    {
        var command = new System.Text.StringBuilder("WolfConquer.DataTool visible-login-seam-audit");
        if (outputJsonPath is not null)
        {
            AppendCommandOption(command, "--line-markers", Path.GetFullPath(outputJsonPath));
            return command.ToString();
        }

        AppendCommandOption(command, "--gateway-log", report.GatewayLogPath);
        AppendCommandOption(command, "--gateway-since-line", report.GatewayLineMarker.ToString(CultureInfo.InvariantCulture));
        AppendCommandOption(command, "--account-log", report.AccountLogPath);
        AppendCommandOption(command, "--account-since-line", report.AccountLineMarker.ToString(CultureInfo.InvariantCulture));
        AppendCommandOption(command, "--world-log", report.WorldLogPath);
        AppendCommandOption(command, "--world-since-line", report.WorldLineMarker.ToString(CultureInfo.InvariantCulture));
        if (report.ClientLogPath is not null)
        {
            AppendCommandOption(command, "--client-log", report.ClientLogPath);
            AppendCommandOption(command, "--client-since-line", report.ClientLineMarker!.Value.ToString(CultureInfo.InvariantCulture));
        }

        return command.ToString();
    }

    private static void AppendCommandOption(System.Text.StringBuilder command, string name, string value) =>
        command
            .Append(' ')
            .Append(name)
            .Append(' ')
            .Append(QuoteCommandArgument(value));

    private static string QuoteCommandArgument(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static int CountSharedLogLines(string path)
    {
        var count = 0;
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is not null)
        {
            count++;
        }

        return count;
    }
}

internal static class VisibleLoginSeamLogAuditor
{
    private const int OutboundSequenceFrameLimit = 12;

    private static readonly Regex GatewayWelcomeHandledPattern = new(
        @"\bGateway\s+GuardShield\s+command\b.*\bcommand=Welcome\b.*\bstatus=Handled\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex AccountReadyFor2Pattern = new(
        @"\bAccountServer\s+legacy\s+login\s+Ready\b.*\bfor\s+2\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex AccountMalformedFallbackReadyFor2Pattern = new(
        @"\bAccountServer\s+malformed\s+legacy\s+auth\s+fallback\s+Ready\b.*\busername=2\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WorldSuccessPattern = new(
        @"\bWorldServer\s+legacy\s+MsgLoginGame\s+Success\s+key=(?<key>\d+)\s+uid=(?<uid>\d+)\s+map=(?<map>\d+)\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WorldOutboundFramePattern = new(
        @"\bLegacyGameWireTrace\s+outbound-frame\s+frameLength=(?<frameLength>\d+)\s+opcode=(?<opcode>\d+)\s+payloadLength=(?<payloadLength>\d+)\s+plainHex=(?<plainHex>[0-9A-Fa-f]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex WorldSkippedOutboundFramePattern = new(
        @"\bLegacyGameWireTrace\s+outbound-frame-skipped\s+reason=(?<reason>\S+)\s+frameLength=(?<frameLength>\d+)\s+opcode=(?<opcode>\d+)\s+payloadLength=(?<payloadLength>\d+)(?:\s+plainHex=(?<plainHex>[0-9A-Fa-f]+))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static VisibleLoginSeamAuditReport Audit(VisibleLoginSeamAuditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        RequireFile(options.AccountLogPath, "Account log");
        RequireFile(options.GatewayLogPath, "Gateway log");
        RequireFile(options.WorldLogPath, "World log");
        if (options.ClientLogPath is not null)
        {
            RequireFile(options.ClientLogPath, "Client debug log");
        }

        var gateway = ScanGatewayLog(options.GatewayLogPath, options.GatewaySinceLine);
        var account = ScanAccountLog(options.AccountLogPath, options.AccountSinceLine);
        var world = ScanWorldLog(options.WorldLogPath, options.WorldSinceLine);
        var client = options.ClientLogPath is null
            ? new VisibleLoginClientSummary(0, [])
            : ScanClientLog(options.ClientLogPath, options.ClientSinceLine);

        return new VisibleLoginSeamAuditReport(
            options.AccountLogPath,
            options.GatewayLogPath,
            options.WorldLogPath,
            options.ClientLogPath,
            options.AccountSinceLine,
            options.GatewaySinceLine,
            options.WorldSinceLine,
            options.ClientSinceLine,
            gateway,
            account,
            world,
            client);
    }

    public static string FormatReport(VisibleLoginSeamAuditReport report)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Visible login seam audit:");
        builder.AppendLine($"Gateway log: {report.GatewayLogPath}");
        builder.AppendLine($"Account log: {report.AccountLogPath}");
        builder.AppendLine($"World log: {report.WorldLogPath}");
        builder.AppendLine($"Client log: {report.ClientLogPath ?? "<none>"}");
        builder.AppendLine(
            "Line filters: " +
            $"gateway>{report.GatewaySinceLine.ToString(CultureInfo.InvariantCulture)} " +
            $"account>{report.AccountSinceLine.ToString(CultureInfo.InvariantCulture)} " +
            $"world>{report.WorldSinceLine.ToString(CultureInfo.InvariantCulture)} " +
            $"client>{report.ClientSinceLine.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine(
            $"Gateway Welcome handled: {report.Gateway.WelcomeHandled} " +
            $"line={FormatLine(report.Gateway.WelcomeHandledLine)}");
        builder.AppendLine(
            $"Account Ready for account 2: {report.Account.Account2Ready} " +
            $"line={FormatLine(report.Account.Account2ReadyLine)}");
        builder.AppendLine(
            $"World MsgLoginGame Success: {report.World.Success is not null} " +
            FormatWorldSuccess(report.World.Success));
        builder.AppendLine(FormatOutboundSequence(
            "World outbound before latest success",
            report.World.OutboundBeforeSuccess));
        builder.AppendLine(
            $"World startup prefix before latest success: {ClassifyStartupPrefix(report.World.OutboundBeforeSuccess)}");
        builder.AppendLine(FormatOutboundSequence(
            "World outbound after startup prefix before latest success",
            GetOutboundAfterStartupPrefix(report.World.OutboundBeforeSuccess)));
        builder.AppendLine(FormatSkippedOutboundSequence(
            "World skipped outbound before latest success",
            report.World.SkippedOutboundBeforeSuccess));
        builder.AppendLine(FormatSkippedOutboundSequence(
            "World skipped outbound after startup prefix before latest success",
            GetSkippedOutboundAfterStartupPrefix(
                report.World.OutboundBeforeSuccess,
                report.World.SkippedOutboundBeforeSuccess)));
        builder.AppendLine(
            $"World client closed after success: {report.World.ClosedAfterSuccess} " +
            $"line={FormatLine(report.World.ClosedAfterSuccessLine)}");
        builder.AppendLine(FormatOutboundSequence(
            "World outbound after success before close",
            report.World.OutboundAfterSuccessBeforeClose));
        builder.AppendLine(FormatLastOutboundBeforeClose(report.World.LastOutboundBeforeClose));
        builder.AppendLine($"Client ToStringEx/MicroSpotData warnings: {report.Client.Warnings.Count}");
        foreach (var warning in report.Client.Warnings)
        {
            builder.AppendLine(
                $"  line={warning.Line.ToString(CultureInfo.InvariantCulture)} " +
                $"kind={warning.Kind} preview={warning.Preview}");
        }

        builder.AppendLine($"Ready: {report.Ready}");
        return builder.ToString();
    }

    private static VisibleLoginGatewaySummary ScanGatewayLog(string path, int sinceLine)
    {
        var scannedLines = 0;
        int? handledLine = null;
        foreach (var line in ReadSharedLogLines(path, sinceLine))
        {
            scannedLines++;
            if (GatewayWelcomeHandledPattern.IsMatch(line.Text))
            {
                handledLine = line.LineNumber;
            }
        }

        return new VisibleLoginGatewaySummary(scannedLines, handledLine is not null, handledLine);
    }

    private static VisibleLoginAccountSummary ScanAccountLog(string path, int sinceLine)
    {
        var scannedLines = 0;
        int? readyLine = null;
        foreach (var line in ReadSharedLogLines(path, sinceLine))
        {
            scannedLines++;
            if (AccountReadyFor2Pattern.IsMatch(line.Text) ||
                AccountMalformedFallbackReadyFor2Pattern.IsMatch(line.Text))
            {
                readyLine = line.LineNumber;
            }
        }

        return new VisibleLoginAccountSummary(scannedLines, readyLine is not null, readyLine);
    }

    private static VisibleLoginWorldSummary ScanWorldLog(string path, int sinceLine)
    {
        var scannedLines = 0;
        VisibleLoginWorldSuccess? success = null;
        int? closedAfterSuccessLine = null;
        VisibleLoginOutboundFrame? lastOutbound = null;
        VisibleLoginOutboundFrame? lastOutboundBeforeClose = null;
        var recentOutboundBeforeSuccess = new Queue<VisibleLoginOutboundFrame>();
        var recentSkippedOutboundBeforeSuccess = new Queue<VisibleLoginSkippedOutboundFrame>();
        var outboundBeforeSuccessTotal = 0;
        var skippedOutboundBeforeSuccessTotal = 0;
        VisibleLoginOutboundSequence outboundBeforeSuccess = new(0, [], IsTail: true);
        VisibleLoginSkippedOutboundSequence skippedOutboundBeforeSuccess = new(0, [], IsTail: true);
        var outboundAfterSuccess = new List<VisibleLoginOutboundFrame>();
        var outboundAfterSuccessTotal = 0;
        foreach (var line in ReadSharedLogLines(path, sinceLine))
        {
            scannedLines++;
            if (TryParseOutboundFrame(line.LineNumber, line.Text, out var outbound))
            {
                lastOutbound = outbound;
                outboundBeforeSuccessTotal++;
                EnqueueOutboundTail(recentOutboundBeforeSuccess, outbound);
                if (success is not null && closedAfterSuccessLine is null)
                {
                    outboundAfterSuccessTotal++;
                    if (outboundAfterSuccess.Count < OutboundSequenceFrameLimit)
                    {
                        outboundAfterSuccess.Add(outbound);
                    }
                }
            }

            if (TryParseSkippedOutboundFrame(line.LineNumber, line.Text, out var skippedOutbound))
            {
                skippedOutboundBeforeSuccessTotal++;
                EnqueueSkippedOutboundTail(recentSkippedOutboundBeforeSuccess, skippedOutbound);
            }

            var successMatch = WorldSuccessPattern.Match(line.Text);
            if (successMatch.Success)
            {
                outboundBeforeSuccess = new VisibleLoginOutboundSequence(
                    outboundBeforeSuccessTotal,
                    recentOutboundBeforeSuccess.ToArray(),
                    IsTail: true);
                skippedOutboundBeforeSuccess = new VisibleLoginSkippedOutboundSequence(
                    skippedOutboundBeforeSuccessTotal,
                    recentSkippedOutboundBeforeSuccess.ToArray(),
                    IsTail: true);
                success = new VisibleLoginWorldSuccess(
                    line.LineNumber,
                    successMatch.Groups["key"].Value,
                    successMatch.Groups["uid"].Value,
                    successMatch.Groups["map"].Value);
                closedAfterSuccessLine = null;
                lastOutboundBeforeClose = null;
                outboundAfterSuccess.Clear();
                outboundAfterSuccessTotal = 0;
                continue;
            }

            if (success is not null &&
                closedAfterSuccessLine is null &&
                line.Text.Contains("WorldServer closed client", StringComparison.OrdinalIgnoreCase))
            {
                closedAfterSuccessLine = line.LineNumber;
                lastOutboundBeforeClose = lastOutbound;
            }
        }

        return new VisibleLoginWorldSummary(
            scannedLines,
            success,
            closedAfterSuccessLine is not null,
            closedAfterSuccessLine,
            outboundBeforeSuccess,
            skippedOutboundBeforeSuccess,
            new VisibleLoginOutboundSequence(
                outboundAfterSuccessTotal,
                outboundAfterSuccess.ToArray(),
                IsTail: false),
            lastOutboundBeforeClose);
    }

    private static VisibleLoginClientSummary ScanClientLog(string path, int sinceLine)
    {
        var scannedLines = 0;
        var warnings = new List<VisibleLoginClientWarning>();
        foreach (var line in ReadSharedLogLines(path, sinceLine))
        {
            scannedLines++;
            var hasToStringEx = line.Text.Contains("ToStringEx", StringComparison.OrdinalIgnoreCase);
            var hasMicroSpotData = line.Text.Contains("MicroSpotData", StringComparison.OrdinalIgnoreCase);
            if (!hasToStringEx && !hasMicroSpotData)
            {
                continue;
            }

            warnings.Add(new VisibleLoginClientWarning(
                line.LineNumber,
                hasToStringEx && hasMicroSpotData
                    ? "ToStringEx,MicroSpotData"
                    : hasToStringEx ? "ToStringEx" : "MicroSpotData",
                TrimPreview(line.Text.Trim(' ', '\t', '\uFFFD'), 220)));
        }

        return new VisibleLoginClientSummary(scannedLines, warnings);
    }

    private static string FormatWorldSuccess(VisibleLoginWorldSuccess? success)
    {
        return success is null
            ? "line=<none> key=<none> uid=<none> map=<none>"
            : $"line={success.Line.ToString(CultureInfo.InvariantCulture)} key={success.Key} uid={success.Uid} map={success.Map}";
    }

    private static string FormatLastOutboundBeforeClose(VisibleLoginOutboundFrame? frame)
    {
        if (frame is null)
        {
            return "World last outbound before close: line=<none> opcode=<none> payloadLength=<none> item=<none>";
        }

        var item = frame.GameItem is null
            ? "<none>"
            : FormatGameItemPreview(frame.GameItem);
        var config = frame.ServerConfig is null
            ? ""
            : $" config={FormatServerConfigPreview(frame.ServerConfig)}";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"World last outbound before close: line={frame.Line} opcode={frame.Opcode} payloadLength={frame.PayloadLength} item={item}{config}");
    }

    private static string FormatOutboundSequence(string label, VisibleLoginOutboundSequence sequence)
    {
        if (sequence.TotalCount == 0)
        {
            return $"{label}: count=0 frames=<none>";
        }

        var count = sequence.TotalCount.ToString(CultureInfo.InvariantCulture);
        var countLabel = sequence.TotalCount == sequence.Frames.Count
            ? $"count={count}"
            : sequence.IsTail
                ? $"count={count} last={sequence.Frames.Count.ToString(CultureInfo.InvariantCulture)}"
                : $"count={count} shown={sequence.Frames.Count.ToString(CultureInfo.InvariantCulture)}";
        var frames = string.Join(", ", sequence.Frames.Select(FormatOutboundSequenceFrame));
        return $"{label}: {countLabel} frames={frames}";
    }

    private static string FormatSkippedOutboundSequence(string label, VisibleLoginSkippedOutboundSequence sequence)
    {
        if (sequence.TotalCount == 0)
        {
            return $"{label}: count=0 frames=<none>";
        }

        var count = sequence.TotalCount.ToString(CultureInfo.InvariantCulture);
        var countLabel = sequence.TotalCount == sequence.Frames.Count
            ? $"count={count}"
            : sequence.IsTail
                ? $"count={count} last={sequence.Frames.Count.ToString(CultureInfo.InvariantCulture)}"
                : $"count={count} shown={sequence.Frames.Count.ToString(CultureInfo.InvariantCulture)}";
        var frames = string.Join(", ", sequence.Frames.Select(FormatSkippedOutboundSequenceFrame));
        return $"{label}: {countLabel} frames={frames}";
    }

    private static string FormatOutboundSequenceFrame(VisibleLoginOutboundFrame frame)
    {
        var suffix = frame.ServerConfig is null
            ? ""
            : $"[config={FormatServerConfigPreview(frame.ServerConfig)}]";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{frame.Opcode}:{frame.PayloadLength}{suffix}");
    }

    private static string FormatSkippedOutboundSequenceFrame(VisibleLoginSkippedOutboundFrame frame) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{frame.Reason}:{frame.Opcode}:{frame.PayloadLength}");

    private static string ClassifyStartupPrefix(VisibleLoginOutboundSequence sequence)
    {
        return FindLatestStartupPrefix(sequence.Frames)?.Label ?? "unknown";
    }

    private static VisibleLoginOutboundSequence GetOutboundAfterStartupPrefix(VisibleLoginOutboundSequence sequence)
    {
        var prefix = FindLatestStartupPrefix(sequence.Frames);
        if (prefix is null)
        {
            return new VisibleLoginOutboundSequence(0, [], IsTail: false);
        }

        var frames = sequence.Frames
            .Skip(prefix.EndIndex + 1)
            .Take(OutboundSequenceFrameLimit)
            .ToArray();
        return new VisibleLoginOutboundSequence(
            Math.Max(0, sequence.Frames.Count - prefix.EndIndex - 1),
            frames,
            IsTail: false);
    }

    private static VisibleLoginSkippedOutboundSequence GetSkippedOutboundAfterStartupPrefix(
        VisibleLoginOutboundSequence outboundSequence,
        VisibleLoginSkippedOutboundSequence skippedSequence)
    {
        var prefix = FindLatestStartupPrefix(outboundSequence.Frames);
        if (prefix is null)
        {
            return new VisibleLoginSkippedOutboundSequence(0, [], IsTail: false);
        }

        var prefixEndLine = outboundSequence.Frames[prefix.EndIndex].Line;
        var frames = skippedSequence.Frames
            .Where(frame => frame.Line > prefixEndLine)
            .Take(OutboundSequenceFrameLimit)
            .ToArray();
        return new VisibleLoginSkippedOutboundSequence(
            frames.Length,
            frames,
            IsTail: false);
    }

    private static VisibleLoginStartupPrefix? FindLatestStartupPrefix(IReadOnlyList<VisibleLoginOutboundFrame> frames)
    {
        VisibleLoginStartupPrefix? best = null;
        var patterns = new VisibleLoginStartupPattern[]
        {
            new(
                "source-compatible:2161->2224->2047",
                new ushort[]
                {
                    WorldPacketOpcodes.LegacyServerVersion,
                    WorldPacketOpcodes.LegacyServerTimer,
                    WorldPacketOpcodes.LegacyServerConfig,
                }),
            new(
                "config-before-timer:2161->2047->2224",
                new ushort[]
                {
                    WorldPacketOpcodes.LegacyServerVersion,
                    WorldPacketOpcodes.LegacyServerConfig,
                    WorldPacketOpcodes.LegacyServerTimer,
                }),
            new(
                "skip-timer:2161->2047",
                new ushort[]
                {
                    WorldPacketOpcodes.LegacyServerVersion,
                    WorldPacketOpcodes.LegacyServerConfig,
                }),
            new(
                "timer-only:2161->2224",
                new ushort[]
                {
                    WorldPacketOpcodes.LegacyServerVersion,
                    WorldPacketOpcodes.LegacyServerTimer,
                }),
        };

        foreach (var pattern in patterns)
        {
            for (var index = 0; index <= frames.Count - pattern.Opcodes.Count; index++)
            {
                if (!MatchesOpcodesAt(frames, index, pattern.Opcodes))
                {
                    continue;
                }

                var endIndex = index + pattern.Opcodes.Count - 1;
                if (best is null ||
                    endIndex > best.EndIndex ||
                    (endIndex == best.EndIndex && pattern.Opcodes.Count > best.Length))
                {
                    best = new VisibleLoginStartupPrefix(pattern.Label, endIndex, pattern.Opcodes.Count);
                }
            }
        }

        return best;
    }

    private static bool MatchesOpcodesAt(
        IReadOnlyList<VisibleLoginOutboundFrame> frames,
        int startIndex,
        IReadOnlyList<ushort> opcodes)
    {
        if (frames.Count - startIndex < opcodes.Count)
        {
            return false;
        }

        for (var index = 0; index < opcodes.Count; index++)
        {
            if (frames[startIndex + index].Opcode != opcodes[index])
            {
                return false;
            }
        }

        return true;
    }

    private sealed record VisibleLoginStartupPattern(string Label, IReadOnlyList<ushort> Opcodes);

    private sealed record VisibleLoginStartupPrefix(string Label, int EndIndex, int Length);

    private static string FormatServerConfigPreview(VisibleLoginServerConfigPreview config) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{config.Reserved0},{config.Reserved1},{config.Value15},{config.Value9999},{config.Value3},{config.Flag}");

    private static void EnqueueOutboundTail(
        Queue<VisibleLoginOutboundFrame> frames,
        VisibleLoginOutboundFrame frame)
    {
        frames.Enqueue(frame);
        while (frames.Count > OutboundSequenceFrameLimit)
        {
            frames.Dequeue();
        }
    }

    private static void EnqueueSkippedOutboundTail(
        Queue<VisibleLoginSkippedOutboundFrame> frames,
        VisibleLoginSkippedOutboundFrame frame)
    {
        frames.Enqueue(frame);
        while (frames.Count > OutboundSequenceFrameLimit)
        {
            frames.Dequeue();
        }
    }

    private static string FormatGameItemPreview(VisibleLoginGameItemPreview item)
    {
        var builder = new System.Text.StringBuilder();
        AppendGameItemField(builder, "uid", item.Uid);
        AppendGameItemField(builder, "itemId", item.ItemId);
        AppendGameItemField(builder, "durability", item.Durability);
        AppendGameItemField(builder, "maxDurability", item.MaxDurability);
        AppendGameItemField(builder, "mode", item.Mode);
        AppendGameItemField(builder, "position", item.Position);
        AppendGameItemField(builder, "spellId", item.SpellId);
        AppendGameItemField(builder, "effect", item.Effect);
        AppendGameItemField(builder, "plus", item.Plus);
        AppendGameItemField(builder, "bless", item.Bless);
        AppendGameItemField(builder, "bound", item.Bound);
        AppendGameItemField(builder, "enchant", item.Enchant);
        AppendGameItemField(builder, "stackSize", item.StackSize);
        AppendGameItemField(builder, "timeLeftInMinutes", item.TimeLeftInMinutes);
        AppendGameItemField(builder, "runeExperience", item.RuneExperience);
        return builder.ToString();
    }

    private static void AppendGameItemField(
        System.Text.StringBuilder builder,
        string name,
        IFormattable value)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder
            .Append(name)
            .Append('=')
            .Append(value.ToString(null, CultureInfo.InvariantCulture));
    }

    private static void AppendGameItemField(System.Text.StringBuilder builder, string name, uint value) =>
        AppendGameItemField(builder, name, (IFormattable)value);

    private static void AppendGameItemField(System.Text.StringBuilder builder, string name, ushort value) =>
        AppendGameItemField(builder, name, (IFormattable)value);

    private static void AppendGameItemField(System.Text.StringBuilder builder, string name, byte value) =>
        AppendGameItemField(builder, name, (IFormattable)value);

    private static void AppendGameItemField(System.Text.StringBuilder builder, string name, uint? value)
    {
        if (value.HasValue)
        {
            AppendGameItemField(builder, name, value.Value);
        }
    }

    private static void AppendGameItemField(System.Text.StringBuilder builder, string name, ushort? value)
    {
        if (value.HasValue)
        {
            AppendGameItemField(builder, name, value.Value);
        }
    }

    private static void AppendGameItemField(System.Text.StringBuilder builder, string name, byte? value)
    {
        if (value.HasValue)
        {
            AppendGameItemField(builder, name, value.Value);
        }
    }

    private static bool TryParseOutboundFrame(
        int lineNumber,
        string line,
        out VisibleLoginOutboundFrame frame)
    {
        frame = default!;
        var match = WorldOutboundFramePattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["frameLength"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var frameLength) ||
            !ushort.TryParse(match.Groups["opcode"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var opcode) ||
            !int.TryParse(match.Groups["payloadLength"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var payloadLength))
        {
            return false;
        }

        var hex = match.Groups["plainHex"].Value;
        frame = new VisibleLoginOutboundFrame(
            lineNumber,
            frameLength,
            opcode,
            payloadLength,
            opcode == WorldPacketOpcodes.LegacyGameItem && TryParseGameItemPreview(hex, out var gameItem)
                ? gameItem
                : null,
            opcode == WorldPacketOpcodes.LegacyServerConfig && TryParseServerConfigPreview(hex, out var serverConfig)
                ? serverConfig
                : null);
        return true;
    }

    private static bool TryParseSkippedOutboundFrame(
        int lineNumber,
        string line,
        out VisibleLoginSkippedOutboundFrame frame)
    {
        frame = default!;
        var match = WorldSkippedOutboundFramePattern.Match(line);
        if (!match.Success ||
            !int.TryParse(match.Groups["frameLength"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var frameLength) ||
            !ushort.TryParse(match.Groups["opcode"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var opcode) ||
            !int.TryParse(match.Groups["payloadLength"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var payloadLength))
        {
            return false;
        }

        frame = new VisibleLoginSkippedOutboundFrame(
            lineNumber,
            match.Groups["reason"].Value,
            frameLength,
            opcode,
            payloadLength);
        return true;
    }

    private static bool TryParseServerConfigPreview(string frameHex, out VisibleLoginServerConfigPreview config)
    {
        config = default!;
        try
        {
            var bytes = Convert.FromHexString(frameHex);
            const int reserved0Offset = 0;
            const int reserved1Offset = reserved0Offset + sizeof(uint);
            const int value15Offset = reserved1Offset + sizeof(uint);
            const int value9999Offset = value15Offset + sizeof(uint);
            const int value3Offset = value9999Offset + sizeof(uint);
            const int flagOffset = value3Offset + sizeof(uint);
            if (!TryReadUInt32(bytes, reserved0Offset, out var reserved0) ||
                !TryReadUInt32(bytes, reserved1Offset, out var reserved1) ||
                !TryReadUInt32(bytes, value15Offset, out var value15) ||
                !TryReadUInt32(bytes, value9999Offset, out var value9999) ||
                !TryReadUInt32(bytes, value3Offset, out var value3) ||
                !TryReadByte(bytes, flagOffset, out var flag))
            {
                return false;
            }

            config = new VisibleLoginServerConfigPreview(
                reserved0,
                reserved1,
                value15,
                value9999,
                value3,
                flag);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParseGameItemPreview(string frameHex, out VisibleLoginGameItemPreview item)
    {
        item = default!;
        try
        {
            var bytes = Convert.FromHexString(frameHex);
            const int uidOffset = 0;
            const int itemIdOffset = uidOffset + sizeof(uint);
            const int durabilityOffset = itemIdOffset + sizeof(uint);
            const int maxDurabilityOffset = durabilityOffset + sizeof(ushort);
            const int modeOffset = maxDurabilityOffset + sizeof(ushort);
            const int positionOffset = modeOffset + sizeof(ushort);
            const int socketProgressOffset = positionOffset + sizeof(byte);
            const int socketOneOffset = socketProgressOffset + sizeof(uint);
            const int socketTwoOffset = socketOneOffset + sizeof(byte);
            const int spellIdOffset = socketTwoOffset + sizeof(byte);
            const int effectOffset = spellIdOffset + sizeof(uint);
            const int plusOffset = effectOffset + sizeof(byte);
            const int blessOffset = plusOffset + sizeof(byte);
            const int boundOffset = blessOffset + sizeof(byte);
            const int enchantOffset = boundOffset + sizeof(byte);
            const int progressGreenOffset = enchantOffset + sizeof(byte);
            const int suspiciousOffset = progressGreenOffset + sizeof(uint);
            const int reserved35Offset = suspiciousOffset + sizeof(byte);
            const int lockedOffset = reserved35Offset + sizeof(byte);
            const int reserved37Offset = lockedOffset + sizeof(byte);
            const int plusProgressOffset = reserved37Offset + sizeof(byte);
            const int remainingTimeOffset = plusProgressOffset + sizeof(uint);
            const int timeLeftInMinutesOffset = remainingTimeOffset + sizeof(uint);
            const int stackSizeOffset = timeLeftInMinutesOffset + sizeof(uint);
            const int perfectionLevelOffset = stackSizeOffset + sizeof(ushort);
            const int perfectionProgressOffset = perfectionLevelOffset + sizeof(uint);
            const int ownerUidOffset = perfectionProgressOffset + sizeof(uint);
            const int ownerNameOffset = ownerUidOffset + sizeof(uint);
            const int signatureOffset = ownerNameOffset + LegacyGameItemPacket.FixedOwnerNameLength;
            const int reserved160Offset = signatureOffset + LegacyGameItemPacket.FixedSignatureLength;
            const int runeExperienceOffset = reserved160Offset + sizeof(ushort);

            if (!TryReadUInt32(bytes, uidOffset, out var uid) ||
                !TryReadUInt32(bytes, itemIdOffset, out var itemId) ||
                !TryReadUInt16(bytes, durabilityOffset, out var durability) ||
                !TryReadUInt16(bytes, maxDurabilityOffset, out var maxDurability) ||
                !TryReadUInt16(bytes, modeOffset, out var mode) ||
                !TryReadByte(bytes, positionOffset, out var position))
            {
                return false;
            }

            item = new VisibleLoginGameItemPreview(
                uid,
                itemId,
                durability,
                maxDurability,
                mode,
                position,
                TryReadUInt32(bytes, spellIdOffset, out var spellId) ? spellId : null,
                TryReadByte(bytes, effectOffset, out var effect) ? effect : null,
                TryReadByte(bytes, plusOffset, out var plus) ? plus : null,
                TryReadByte(bytes, blessOffset, out var bless) ? bless : null,
                TryReadByte(bytes, boundOffset, out var bound) ? bound : null,
                TryReadByte(bytes, enchantOffset, out var enchant) ? enchant : null,
                TryReadUInt16(bytes, stackSizeOffset, out var stackSize) ? stackSize : null,
                TryReadUInt32(bytes, timeLeftInMinutesOffset, out var timeLeftInMinutes) ? timeLeftInMinutes : null,
                TryReadUInt32(bytes, runeExperienceOffset, out var runeExperience) ? runeExperience : null);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryReadUInt32(byte[] frameBytes, int payloadOffset, out uint value)
    {
        const int frameHeaderLength = 4;
        var offset = frameHeaderLength + payloadOffset;
        if (frameBytes.Length < offset + sizeof(uint))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(frameBytes.AsSpan(offset, sizeof(uint)));
        return true;
    }

    private static bool TryReadUInt16(byte[] frameBytes, int payloadOffset, out ushort value)
    {
        const int frameHeaderLength = 4;
        var offset = frameHeaderLength + payloadOffset;
        if (frameBytes.Length < offset + sizeof(ushort))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(frameBytes.AsSpan(offset, sizeof(ushort)));
        return true;
    }

    private static bool TryReadByte(byte[] frameBytes, int payloadOffset, out byte value)
    {
        const int frameHeaderLength = 4;
        var offset = frameHeaderLength + payloadOffset;
        if (frameBytes.Length <= offset)
        {
            value = 0;
            return false;
        }

        value = frameBytes[offset];
        return true;
    }

    private static string FormatLine(int? line) =>
        line?.ToString(CultureInfo.InvariantCulture) ?? "<none>";

    private static string TrimPreview(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..(maxLength - 3)] + "...";
    }

    internal static void RequireFile(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{label} not found: {path}", path);
        }
    }

    private readonly record struct SharedLogLine(int LineNumber, string Text);

    private static IEnumerable<SharedLogLine> ReadSharedLogLines(string path, int sinceLine)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (lineNumber <= sinceLine)
            {
                continue;
            }

            yield return new SharedLogLine(lineNumber, line);
        }
    }
}
