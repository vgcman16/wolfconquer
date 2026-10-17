using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.World;

internal static partial class Program
{
    private const string CombatCloseEvidenceUsage =
        "Usage: WolfConquer.DataTool combat-close-evidence --world-log path [--client-log path] [--world-since-line N] [--client-since-line N]";

    static int CombatCloseEvidence(string[] args)
    {
        if (!TryParseCombatCloseEvidenceOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(CombatCloseEvidenceUsage);
            return 1;
        }

        CombatCloseEvidenceReport report;
        try
        {
            report = CombatCloseEvidenceAuditor.Audit(options);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not extract combat close evidence: {ex.Message}");
            Console.Error.WriteLine(CombatCloseEvidenceUsage);
            return 1;
        }

        Console.WriteLine(CombatCloseEvidenceAuditor.FormatReport(report));
        return report.HasExactWireEvidence ? 0 : 2;
    }

    private static bool TryParseCombatCloseEvidenceOptions(
        string[] args,
        out CombatCloseEvidenceOptions options,
        out string error)
    {
        options = new CombatCloseEvidenceOptions("", null, 0, 0);
        error = "";

        string? worldLogPath = null;
        string? clientLogPath = null;
        var worldSinceLine = 0;
        var clientSinceLine = 0;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
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

            if (TryReadOptionValue(args, ref index, "--world-since-line", argument, out var worldSinceValue))
            {
                if (!TryParsePositiveOrZeroLine("--world-since-line", worldSinceValue, out worldSinceLine, out error))
                {
                    return false;
                }

                continue;
            }

            if (TryReadOptionValue(args, ref index, "--client-since-line", argument, out var clientSinceValue))
            {
                if (!TryParsePositiveOrZeroLine("--client-since-line", clientSinceValue, out clientSinceLine, out error))
                {
                    return false;
                }

                continue;
            }

            error = $"Unknown option: {argument}";
            return false;
        }

        if (worldLogPath is null)
        {
            error = "Missing --world-log path.";
            return false;
        }

        options = new CombatCloseEvidenceOptions(worldLogPath, clientLogPath, worldSinceLine, clientSinceLine);
        return true;
    }

    private static bool TryParsePositiveOrZeroLine(string optionName, string value, out int line, out string error)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out line) || line < 0)
        {
            error = $"Invalid {optionName} value: {value}";
            return false;
        }

        error = "";
        return true;
    }
}

internal sealed record CombatCloseEvidenceOptions(
    string WorldLogPath,
    string? ClientLogPath,
    int WorldSinceLine,
    int ClientSinceLine);

internal sealed record CombatCloseEvidenceReport(
    string WorldLogPath,
    string? ClientLogPath,
    int WorldSinceLine,
    int ClientSinceLine,
    CombatCloseLine? LatestClose,
    CombatCloseLine? LatestLoginSuccessBeforeClose,
    CombatCloseWireFrame? LatestInboundAttackBeforeClose,
    IReadOnlyList<CombatCloseWireFrame> LastInboundFramesBeforeClose,
    IReadOnlyList<CombatCloseWireFrame> LastOutboundFramesBeforeClose,
    IReadOnlyList<CombatCloseLine> SemanticCombatBeforeClose,
    IReadOnlyList<CombatCloseLine> SourceSilentBeforeClose,
    IReadOnlyList<CombatCloseLine> ClientSymptoms,
    IReadOnlyList<CombatCloseLine> CombatCompatibilityFencesBeforeClose,
    IReadOnlyList<CombatCloseLine> CombatGearMonsterHitPassiveOutcomesBeforeClose,
    IReadOnlyList<CombatCloseLine> LegacyWeaponProcMonsterAttackOutcomesBeforeClose,
    IReadOnlyList<CombatCloseLine> ProactiveMonsterAttacksBeforeClose,
    IReadOnlyList<CombatCloseLine> GuardMonsterProtectionAttacksBeforeClose,
    IReadOnlyList<CombatCloseLine> MonsterLifecycleBeforeClose,
    IReadOnlyList<CombatCloseLine> ClientMissingAnimationErrors)
{
    public bool HasExactWireEvidence => LatestInboundAttackBeforeClose is not null && LastOutboundFramesBeforeClose.Count > 0;
}

internal sealed record CombatCloseLine(int LineNumber, string Text);

internal sealed record CombatCloseWireFrame(
    int LineNumber,
    string Direction,
    int FrameLength,
    ushort Opcode,
    int PayloadLength,
    string Hex);

internal static class CombatCloseEvidenceAuditor
{
    private const int WindowLimit = 12;

    private static readonly Regex WorldClosePattern = new(
        @"\bWorldServer\s+closed\s+client\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WorldLoginSuccessPattern = new(
        @"\bWorldServer\s+legacy\s+MsgLoginGame\s+Success\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InboundFramePattern = new(
        @"\bLegacyGameWireTrace\s+inbound-frame\s+frameLength=(?<frameLength>\d+)\s+opcode=(?<opcode>\d+)\s+payloadLength=(?<payloadLength>\d+)\b.*?\bframeHex=(?<hex>[0-9A-Fa-f]+(?:\.\.\.\(total \d+ bytes\))?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OutboundFramePattern = new(
        @"\bLegacyGameWireTrace\s+outbound-frame\s+frameLength=(?<frameLength>\d+)\s+opcode=(?<opcode>\d+)\s+payloadLength=(?<payloadLength>\d+)\b.*?\bplainHex=(?<hex>[0-9A-Fa-f]+(?:\.\.\.\(total \d+ bytes\))?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ClientSymptomPattern = new(
        @"CreateNewAni|Ani\.cpp|Read Error in ItemType\.dat|invalid NewType|Lua error|Got a player with hero id|\[R,\d+|Close Socket|Begin Close Socket",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ClientMissingAnimationPattern = new(
        @"\bCreateNewAni\s*\([^\r\n]*\)\s*failed\b|\bAni\s+Index\s*\[[^\]\r\n]+\]\s+Not\s+Found\s+In\s*\[[^\]\r\n]+\]|\bAni\.cpp\b[^\r\n]*(?:error|fail|not\s+found|missing)|(?:error|fail|not\s+found|missing)[^\r\n]*\bAni\.cpp\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static CombatCloseEvidenceReport Audit(CombatCloseEvidenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.WorldLogPath))
        {
            throw new FileNotFoundException($"World log not found: {options.WorldLogPath}", options.WorldLogPath);
        }

        if (options.ClientLogPath is not null && !File.Exists(options.ClientLogPath))
        {
            throw new FileNotFoundException($"Client log not found: {options.ClientLogPath}", options.ClientLogPath);
        }

        var fullWorldLogPath = Path.GetFullPath(options.WorldLogPath);
        var worldLines = ReadLogLinesShared(fullWorldLogPath);
        var latestClose = FindLatestLine(worldLines, options.WorldSinceLine, static line => WorldClosePattern.IsMatch(line));
        var closeLineNumber = latestClose?.LineNumber ?? worldLines.Length + 1;
        var latestLoginSuccess = latestClose is null
            ? null
            : FindLatestLine(
                worldLines,
                options.WorldSinceLine,
                line => WorldLoginSuccessPattern.IsMatch(line),
                closeLineNumber);

        var windowStart = latestLoginSuccess?.LineNumber ?? options.WorldSinceLine;
        var inboundFrames = ReadWireFrames(worldLines, options.WorldSinceLine, closeLineNumber, inbound: true);
        var outboundFrames = ReadWireFrames(worldLines, options.WorldSinceLine, closeLineNumber, inbound: false);
        var latestInboundAttack = inboundFrames.LastOrDefault(static frame => frame.Opcode == MsgAttackPacket.Opcode);
        var semanticCombat = FindWindowLines(
            worldLines,
            windowStart,
            closeLineNumber,
            IsSemanticCombatLine);
        var sourceSilent = FindWindowLines(
            worldLines,
            windowStart,
            closeLineNumber,
            static line => line.Contains("source-silent legacy packet", StringComparison.OrdinalIgnoreCase));
        var combatCompatibilityFences = FindWindowLines(
            worldLines,
            options.WorldSinceLine,
            closeLineNumber,
            IsCombatCompatibilityFenceLine);
        var combatGearMonsterHitPassiveOutcomes = FindWindowLines(
            worldLines,
            windowStart,
            closeLineNumber,
            IsCombatGearMonsterHitPassiveOutcomeLine);
        var legacyWeaponProcMonsterAttackOutcomes = FindWindowLines(
            worldLines,
            windowStart,
            closeLineNumber,
            IsLegacyWeaponProcMonsterAttackOutcomeLine);
        var proactiveMonsterAttacks = FindWindowLines(
            worldLines,
            windowStart,
            closeLineNumber,
            IsProactiveMonsterAttackLine);
        var guardMonsterProtectionAttacks = FindWindowLines(
            worldLines,
            windowStart,
            closeLineNumber,
            IsGuardMonsterProtectionAttackLine);
        var monsterLifecycle = FindWindowLines(
            worldLines,
            windowStart,
            closeLineNumber,
            IsMonsterLifecycleLine);
        var clientLines = options.ClientLogPath is null
            ? null
            : ReadLogLinesShared(options.ClientLogPath);
        var clientSymptoms = clientLines is null
            ? []
            : FindWindowLines(
                clientLines,
                options.ClientSinceLine,
                int.MaxValue,
                static line => ClientSymptomPattern.IsMatch(line));
        var clientMissingAnimationErrors = clientLines is null
            ? []
            : FindWindowLines(
                clientLines,
                options.ClientSinceLine,
                int.MaxValue,
                static line => ClientMissingAnimationPattern.IsMatch(line));

        return new CombatCloseEvidenceReport(
            fullWorldLogPath,
            options.ClientLogPath is null ? null : Path.GetFullPath(options.ClientLogPath),
            options.WorldSinceLine,
            options.ClientSinceLine,
            latestClose,
            latestLoginSuccess,
            latestInboundAttack,
            inboundFrames.TakeLast(WindowLimit).ToArray(),
            outboundFrames.TakeLast(WindowLimit).ToArray(),
            semanticCombat,
            sourceSilent,
            clientSymptoms,
            combatCompatibilityFences,
            combatGearMonsterHitPassiveOutcomes,
            legacyWeaponProcMonsterAttackOutcomes,
            proactiveMonsterAttacks,
            guardMonsterProtectionAttacks,
            monsterLifecycle,
            clientMissingAnimationErrors);
    }

    public static string FormatReport(CombatCloseEvidenceReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Combat close evidence:");
        builder.AppendLine($"World log: {report.WorldLogPath}");
        builder.AppendLine($"Client log: {report.ClientLogPath ?? "<none>"}");
        builder.AppendLine($"Line filters: world>{report.WorldSinceLine} client>{report.ClientSinceLine}");
        builder.AppendLine($"World latest close: {FormatLineSummary(report.LatestClose)}");
        builder.AppendLine($"World latest login success before close: {FormatLineSummary(report.LatestLoginSuccessBeforeClose)}");
        builder.AppendLine(
            "World latest inbound MsgAttack before close: " +
            FormatWireFrame(report.LatestInboundAttackBeforeClose));
        builder.AppendLine(FormatWireFrameList(
            "World last inbound frames before close",
            report.LastInboundFramesBeforeClose));
        builder.AppendLine(FormatWireFrameList(
            "World last outbound frames before close",
            report.LastOutboundFramesBeforeClose));
        AppendLineWindow(builder, "World semantic combat before close", report.SemanticCombatBeforeClose);
        AppendLineWindow(builder, "World source-silent before close", report.SourceSilentBeforeClose);
        AppendLineWindow(builder, "Client close/error symptoms", report.ClientSymptoms);
        AppendLineWindow(
            builder,
            "World combat compatibility fences before close",
            report.CombatCompatibilityFencesBeforeClose);
        AppendLineWindow(
            builder,
            "World CombatGear monster-hit passive outcomes before close",
            report.CombatGearMonsterHitPassiveOutcomesBeforeClose);
        AppendLineWindow(
            builder,
            "World legacy weapon-proc monster attack outcomes before close",
            report.LegacyWeaponProcMonsterAttackOutcomesBeforeClose);
        AppendLineWindow(
            builder,
            "World proactive monster attacks before close",
            report.ProactiveMonsterAttacksBeforeClose);
        AppendLineWindow(
            builder,
            "World guard monster-protection attacks before close",
            report.GuardMonsterProtectionAttacksBeforeClose);
        AppendLineWindow(
            builder,
            "World monster death/drop/remove/respawn before close",
            report.MonsterLifecycleBeforeClose);
        AppendLineWindow(
            builder,
            "Client missing-animation errors",
            report.ClientMissingAnimationErrors);
        builder.AppendLine($"Exact wire evidence ready: {report.HasExactWireEvidence}");
        if (!report.HasExactWireEvidence)
        {
            builder.AppendLine("Next capture: enable WOLFCONQUER_TRACE_LEGACY_GAME_WIRE=1 for the world process or route login through legacy-world-active-bridge-capture.");
        }

        return builder.ToString();
    }

    private static CombatCloseLine? FindLatestLine(
        IReadOnlyList<string> lines,
        int sinceLine,
        Func<string, bool> predicate,
        int beforeLine = int.MaxValue)
    {
        for (var index = Math.Min(lines.Count, beforeLine - 1) - 1; index >= 0; index--)
        {
            var lineNumber = index + 1;
            if (lineNumber <= sinceLine)
            {
                break;
            }

            if (predicate(lines[index]))
            {
                return new CombatCloseLine(lineNumber, lines[index]);
            }
        }

        return null;
    }

    private static string[] ReadLogLinesShared(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines.ToArray();
    }

    private static IReadOnlyList<CombatCloseWireFrame> ReadWireFrames(
        IReadOnlyList<string> lines,
        int sinceLine,
        int beforeLine,
        bool inbound)
    {
        var frames = new List<CombatCloseWireFrame>();
        var pattern = inbound ? InboundFramePattern : OutboundFramePattern;
        var direction = inbound ? "inbound" : "outbound";
        for (var index = 0; index < lines.Count; index++)
        {
            var lineNumber = index + 1;
            if (lineNumber <= sinceLine || lineNumber >= beforeLine)
            {
                continue;
            }

            var match = pattern.Match(lines[index]);
            if (!match.Success ||
                !int.TryParse(match.Groups["frameLength"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var frameLength) ||
                !ushort.TryParse(match.Groups["opcode"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var opcode) ||
                !int.TryParse(match.Groups["payloadLength"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var payloadLength))
            {
                continue;
            }

            frames.Add(new CombatCloseWireFrame(
                lineNumber,
                direction,
                frameLength,
                opcode,
                payloadLength,
                match.Groups["hex"].Value));
        }

        return frames;
    }

    private static IReadOnlyList<CombatCloseLine> FindWindowLines(
        IReadOnlyList<string> lines,
        int sinceLine,
        int beforeLine,
        Func<string, bool> predicate)
    {
        var matched = new List<CombatCloseLine>();
        for (var index = 0; index < lines.Count; index++)
        {
            var lineNumber = index + 1;
            if (lineNumber <= sinceLine || lineNumber >= beforeLine)
            {
                continue;
            }

            if (predicate(lines[index]))
            {
                matched.Add(new CombatCloseLine(lineNumber, lines[index]));
            }
        }

        return matched.TakeLast(WindowLimit).ToArray();
    }

    private static bool IsSemanticCombatLine(string line)
    {
        return line.Contains("MsgAttack", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("CombatGear", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("monster", StringComparison.OrdinalIgnoreCase) && (
                line.Contains("attack", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("death", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("respawn", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("movement", StringComparison.OrdinalIgnoreCase)) ||
            line.Contains("floor item expire", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCombatCompatibilityFenceLine(string line)
    {
        return line.Contains(
                "Legacy CombatGear monster-hit passive spell blocklist enabled for client compatibility",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "Newer-client CombatGear floor-passive compatibility enabled",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "Legacy proactive monster attacks disabled for client compatibility",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "Legacy CombatGear monster-hit passives disabled for client compatibility",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "Legacy weapon-proc monster attacks disabled for client compatibility",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains("compatibility fence", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCombatGearMonsterHitPassiveOutcomeLine(string line)
    {
        return line.Contains(
                "skipped legacy CombatGear monster-hit passive replacement",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "replaced legacy monster physical attack with CombatGear spell",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyWeaponProcMonsterAttackOutcomeLine(string line)
    {
        return line.Contains(
                "WorldServer skipped legacy weapon proc monster attack",
                StringComparison.OrdinalIgnoreCase) ||
            line.Contains(
                "WorldServer handled legacy weapon proc monster attack",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProactiveMonsterAttackLine(string line)
    {
        return line.Contains("sent proactive monster attacks", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGuardMonsterProtectionAttackLine(string line)
    {
        return line.Contains("guard monster-protection attack", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMonsterLifecycleLine(string line)
    {
        return line.Contains("monster death", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("monster drop", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("monster remove", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("monster respawn", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatLineSummary(CombatCloseLine? line)
    {
        return line is null
            ? "False line=<none>"
            : $"True line={line.LineNumber} preview={TrimForDisplay(line.Text)}";
    }

    private static string FormatWireFrame(CombatCloseWireFrame? frame)
    {
        return frame is null
            ? "line=<none> opcode=<none> frameLength=<none> payloadLength=<none> hex=<none>"
            : $"line={frame.LineNumber} opcode={frame.Opcode} frameLength={frame.FrameLength} payloadLength={frame.PayloadLength} hex={frame.Hex}";
    }

    private static string FormatWireFrameList(string label, IReadOnlyList<CombatCloseWireFrame> frames)
    {
        if (frames.Count == 0)
        {
            return $"{label}: count=0 frames=<none>";
        }

        var formatted = string.Join(
            ", ",
            frames.Select(static frame => $"{frame.LineNumber}:{frame.Opcode}:{frame.PayloadLength}"));
        return $"{label}: count={frames.Count} frames={formatted}";
    }

    private static void AppendLineWindow(
        System.Text.StringBuilder builder,
        string label,
        IReadOnlyList<CombatCloseLine> lines)
    {
        builder.AppendLine($"{label}: count={lines.Count}");
        foreach (var line in lines)
        {
            builder.AppendLine($"  line={line.LineNumber} preview={TrimForDisplay(line.Text)}");
        }
    }

    private static string TrimForDisplay(string value)
    {
        const int MaxLength = 260;
        var normalized = value.Replace('\t', ' ');
        return normalized.Length <= MaxLength ? normalized : normalized[..MaxLength] + "...";
    }
}
