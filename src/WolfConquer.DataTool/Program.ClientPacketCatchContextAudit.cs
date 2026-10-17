using System.Globalization;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private const string ClientPacketCatchContextAuditUsage =
        "Usage: WolfConquer.DataTool client-packet-catch-context-audit <clientDebugLogPath> [--since-line N] [--opcode opcode] [...]";

    static int ClientPacketCatchContextAudit(string[] args)
    {
        if (!TryParseClientPacketCatchContextAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(ClientPacketCatchContextAuditUsage);
            return 1;
        }

        ClientPacketCatchContextAuditReport report;
        try
        {
            report = ClientPacketCatchContextAuditor.Audit(options);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit client packet catch context: {ex.Message}");
            Console.Error.WriteLine(ClientPacketCatchContextAuditUsage);
            return 1;
        }

        Console.WriteLine("Client packet catch context audit:");
        Console.WriteLine($"Client log: {report.ClientLogPath}");
        Console.WriteLine($"Since line: {report.SinceLine?.ToString(CultureInfo.InvariantCulture) ?? "<none>"}");
        Console.WriteLine($"Opcode filter: {FormatClientPacketCatchOpcodeFilter(report.OpcodeFilter)}");
        Console.WriteLine($"Context radius: {ClientPacketCatchContextAuditor.ContextRadius}");
        Console.WriteLine($"Windows: reported={report.Windows.Count} totalMatches={report.TotalMatches} limit={ClientPacketCatchContextAuditor.WindowLimit}");

        if (report.Windows.Count == 0)
        {
            Console.WriteLine("Packet catch contexts: <none>");
            return 0;
        }

        foreach (var window in report.Windows)
        {
            Console.WriteLine($"Window {window.Index}: line={window.LineNumber} opcode={window.Opcode}");
            foreach (var contextLine in window.Lines)
            {
                var marker = contextLine.IsMatch ? ">" : " ";
                Console.WriteLine($"{marker} {contextLine.LineNumber.ToString(CultureInfo.InvariantCulture).PadLeft(6)} | {contextLine.Text}");
            }
        }

        if (report.TotalMatches > report.Windows.Count)
        {
            Console.WriteLine($"Additional matches omitted: {report.TotalMatches - report.Windows.Count}");
        }

        return 0;
    }

    private static bool TryParseClientPacketCatchContextAuditOptions(
        string[] args,
        out ClientPacketCatchContextAuditOptions options,
        out string error)
    {
        options = new ClientPacketCatchContextAuditOptions("", null, []);
        error = "";

        if (args.Length < 2)
        {
            error = "Missing client debug log path.";
            return false;
        }

        var clientLogPath = Path.GetFullPath(args[1]);
        int? sinceLine = null;
        var opcodes = new SortedSet<ushort>();

        for (var index = 2; index < args.Length; index++)
        {
            var argument = args[index];
            if (TryReadOptionValue(args, ref index, "--since-line", argument, out var sinceLineValue))
            {
                if (!int.TryParse(sinceLineValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSinceLine) ||
                    parsedSinceLine < 1)
                {
                    error = $"Invalid --since-line value: {sinceLineValue}";
                    return false;
                }

                sinceLine = parsedSinceLine;
                continue;
            }

            if (TryReadOptionValue(args, ref index, "--opcode", argument, out var opcodeValue))
            {
                if (!ushort.TryParse(opcodeValue, NumberStyles.None, CultureInfo.InvariantCulture, out var opcode))
                {
                    error = $"Invalid --opcode value: {opcodeValue}";
                    return false;
                }

                opcodes.Add(opcode);
                continue;
            }

            error = $"Unknown option: {argument}";
            return false;
        }

        options = new ClientPacketCatchContextAuditOptions(clientLogPath, sinceLine, opcodes.ToArray());
        return true;
    }

    private static string FormatClientPacketCatchOpcodeFilter(IReadOnlyCollection<ushort> opcodes)
    {
        return opcodes.Count == 0
            ? "<all>"
            : string.Join(", ", opcodes.Select(static opcode => opcode.ToString(CultureInfo.InvariantCulture)));
    }
}

internal sealed record ClientPacketCatchContextAuditOptions(
    string ClientLogPath,
    int? SinceLine,
    IReadOnlyCollection<ushort> Opcodes);

internal sealed record ClientPacketCatchContextAuditReport(
    string ClientLogPath,
    int? SinceLine,
    IReadOnlyCollection<ushort> OpcodeFilter,
    int TotalMatches,
    IReadOnlyList<ClientPacketCatchContextWindow> Windows);

internal sealed record ClientPacketCatchContextWindow(
    int Index,
    int LineNumber,
    ushort Opcode,
    IReadOnlyList<ClientPacketCatchContextLine> Lines);

internal sealed record ClientPacketCatchContextLine(
    int LineNumber,
    string Text,
    bool IsMatch);

internal static class ClientPacketCatchContextAuditor
{
    public const int ContextRadius = 2;
    public const int WindowLimit = 50;

    private static readonly Regex ClientCatchPattern = new(
        @"catch\s+error\s+in\s+process\s+msg\s*:?\s*(?<opcode>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static ClientPacketCatchContextAuditReport Audit(ClientPacketCatchContextAuditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.ClientLogPath))
        {
            throw new FileNotFoundException($"Client debug log not found: {options.ClientLogPath}", options.ClientLogPath);
        }

        var fullLogPath = Path.GetFullPath(options.ClientLogPath);
        var lines = File.ReadAllLines(fullLogPath);
        var opcodeFilter = new HashSet<ushort>(options.Opcodes);
        var windows = new List<ClientPacketCatchContextWindow>();
        var totalMatches = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            if (options.SinceLine is not null && lineNumber < options.SinceLine.Value)
            {
                continue;
            }

            var match = ClientCatchPattern.Match(lines[index]);
            if (!match.Success ||
                !ushort.TryParse(match.Groups["opcode"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var opcode) ||
                opcodeFilter.Count > 0 && !opcodeFilter.Contains(opcode))
            {
                continue;
            }

            totalMatches++;
            if (windows.Count >= WindowLimit)
            {
                continue;
            }

            windows.Add(CreateWindow(windows.Count + 1, lines, index, opcode));
        }

        return new ClientPacketCatchContextAuditReport(
            fullLogPath,
            options.SinceLine,
            options.Opcodes.OrderBy(static opcode => opcode).ToArray(),
            totalMatches,
            windows);
    }

    private static ClientPacketCatchContextWindow CreateWindow(
        int windowIndex,
        IReadOnlyList<string> lines,
        int matchIndex,
        ushort opcode)
    {
        var firstIndex = Math.Max(0, matchIndex - ContextRadius);
        var lastIndex = Math.Min(lines.Count - 1, matchIndex + ContextRadius);
        var contextLines = new List<ClientPacketCatchContextLine>();

        for (var index = firstIndex; index <= lastIndex; index++)
        {
            contextLines.Add(new ClientPacketCatchContextLine(index + 1, lines[index], index == matchIndex));
        }

        return new ClientPacketCatchContextWindow(windowIndex, matchIndex + 1, opcode, contextLines);
    }
}
