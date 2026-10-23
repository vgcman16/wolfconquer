using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

internal static partial class Program
{
    private const string ClientNativeLaunchProbeUsage =
        "Usage: WolfConquer.DataTool client-native-launch-probe <exePath> [--workdir path] [--arg value] [...] [--timeout-ms N] [--kill-after-ms N] [--kill-on-timeout|--no-kill-on-timeout] [--debug-log path] [...] [--top-threads N] [--ready-file path] [--args arg ...]";

    static int ClientNativeLaunchProbe(string[] args)
    {
        ClientNativeLaunchProbeOptions options;
        try
        {
            options = ResolveClientNativeLaunchProbeOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ClientNativeLaunchProbeUsage);
            return 1;
        }

        var report = RunClientNativeLaunchProbe(options);
        PrintClientNativeLaunchProbeReport(report);

        if (!report.StartSucceeded)
        {
            return 1;
        }

        if (report.Readiness.Requested &&
            (!report.Readiness.Observed || report.Readiness.Error.Length != 0 || report.NativeError.Length != 0) &&
            !report.TimedOut && !report.KillAttempted)
        {
            return 1;
        }

        return report.TimedOut || report.KillAttempted ? 2 : 0;
    }

    internal static ClientNativeLaunchProbeOptions ResolveClientNativeLaunchProbeOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("client-native-launch-probe requires an executable path.");
        }

        var executablePath = Path.GetFullPath(args[1]);
        var workingDirectory = Path.GetDirectoryName(executablePath) ?? ".";
        var launchArguments = new List<string>();
        var timeoutMs = 5000;
        int? killAfterMs = null;
        var killOnTimeout = true;
        var debugLogPaths = new List<string>();
        var topThreadCount = 5;
        string? readyFilePath = null;

        for (var index = 2; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--workdir", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "--working-directory", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"{argument} requires a path.");
                }

                workingDirectory = Path.GetFullPath(args[++index]);
                continue;
            }

            const string workDirPrefix = "--workdir=";
            if (argument.StartsWith(workDirPrefix, StringComparison.OrdinalIgnoreCase))
            {
                workingDirectory = Path.GetFullPath(argument[workDirPrefix.Length..]);
                continue;
            }

            if (string.Equals(argument, "--arg", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-native-launch-probe --arg requires a value.");
                }

                launchArguments.Add(args[++index]);
                continue;
            }

            const string argPrefix = "--arg=";
            if (argument.StartsWith(argPrefix, StringComparison.OrdinalIgnoreCase))
            {
                launchArguments.Add(argument[argPrefix.Length..]);
                continue;
            }

            if (string.Equals(argument, "--debug-log", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-native-launch-probe --debug-log requires a path.");
                }

                debugLogPaths.Add(Path.GetFullPath(args[++index]));
                continue;
            }

            const string debugLogPrefix = "--debug-log=";
            if (argument.StartsWith(debugLogPrefix, StringComparison.OrdinalIgnoreCase))
            {
                debugLogPaths.Add(Path.GetFullPath(argument[debugLogPrefix.Length..]));
                continue;
            }

            if (string.Equals(argument, "--top-threads", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-native-launch-probe --top-threads requires a count.");
                }

                topThreadCount = ParseClientLaunchEvidenceInteger(
                    args[++index],
                    "--top-threads",
                    minimum: 0,
                    maximum: 64);
                continue;
            }

            const string topThreadsPrefix = "--top-threads=";
            if (argument.StartsWith(topThreadsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                topThreadCount = ParseClientLaunchEvidenceInteger(
                    argument[topThreadsPrefix.Length..],
                    "--top-threads",
                    minimum: 0,
                    maximum: 64);
                continue;
            }

            if (string.Equals(argument, "--ready-file", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-native-launch-probe --ready-file requires a path.");
                }

                readyFilePath = Path.GetFullPath(args[++index]);
                continue;
            }

            const string readyFilePrefix = "--ready-file=";
            if (argument.StartsWith(readyFilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                readyFilePath = Path.GetFullPath(argument[readyFilePrefix.Length..]);
                continue;
            }

            if (string.Equals(argument, "--args", StringComparison.OrdinalIgnoreCase))
            {
                for (var argumentIndex = index + 1; argumentIndex < args.Length; argumentIndex++)
                {
                    launchArguments.Add(args[argumentIndex]);
                }

                break;
            }

            if (string.Equals(argument, "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-native-launch-probe --timeout-ms requires a count.");
                }

                timeoutMs = ParseClientLaunchEvidenceInteger(
                    args[++index],
                    "--timeout-ms",
                    minimum: 1,
                    maximum: 120000);
                continue;
            }

            const string timeoutPrefix = "--timeout-ms=";
            if (argument.StartsWith(timeoutPrefix, StringComparison.OrdinalIgnoreCase))
            {
                timeoutMs = ParseClientLaunchEvidenceInteger(
                    argument[timeoutPrefix.Length..],
                    "--timeout-ms",
                    minimum: 1,
                    maximum: 120000);
                continue;
            }

            if (string.Equals(argument, "--kill-after-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("client-native-launch-probe --kill-after-ms requires a count.");
                }

                killAfterMs = ParseClientLaunchEvidenceInteger(
                    args[++index],
                    "--kill-after-ms",
                    minimum: 1,
                    maximum: 120000);
                continue;
            }

            const string killAfterPrefix = "--kill-after-ms=";
            if (argument.StartsWith(killAfterPrefix, StringComparison.OrdinalIgnoreCase))
            {
                killAfterMs = ParseClientLaunchEvidenceInteger(
                    argument[killAfterPrefix.Length..],
                    "--kill-after-ms",
                    minimum: 1,
                    maximum: 120000);
                continue;
            }

            if (string.Equals(argument, "--kill-on-timeout", StringComparison.OrdinalIgnoreCase))
            {
                killOnTimeout = true;
                continue;
            }

            if (string.Equals(argument, "--no-kill-on-timeout", StringComparison.OrdinalIgnoreCase))
            {
                killOnTimeout = false;
                continue;
            }

            throw new ArgumentException($"Unknown client-native-launch-probe option: {argument}");
        }

        if (readyFilePath is not null && killAfterMs is int boundedKillAfter && boundedKillAfter >= timeoutMs)
        {
            throw new ArgumentException("--ready-file requires --kill-after-ms to be less than --timeout-ms.");
        }

        return new ClientNativeLaunchProbeOptions(
            executablePath,
            Path.GetFullPath(workingDirectory),
            launchArguments,
            timeoutMs,
            killAfterMs,
            killOnTimeout,
            debugLogPaths.Distinct(ClientNativeRuntimePathComparer).ToArray(),
            topThreadCount,
            readyFilePath);
    }

    private static ClientNativeLaunchProbeReport RunClientNativeLaunchProbe(ClientNativeLaunchProbeOptions options)
    {
        var runtimeReport = RunClientNativeRuntimeSnapshot(options);
        return new ClientNativeLaunchProbeReport(
            options,
            runtimeReport.StartSucceeded,
            runtimeReport.ProcessId,
            runtimeReport.NativeError,
            runtimeReport.Exited,
            runtimeReport.ExitCode,
            runtimeReport.TimedOut,
            runtimeReport.KillAttempted,
            runtimeReport.Killed,
            runtimeReport.KillError,
            runtimeReport.ElapsedMilliseconds,
            runtimeReport.Snapshots,
            runtimeReport.Readiness);
    }

    private static string FormatClientNativeLaunchProbeException(Exception ex)
    {
        if (ex is Win32Exception win32Exception)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"type=Win32Exception nativeErrorCode={win32Exception.NativeErrorCode} message={win32Exception.Message}");
        }

        return string.Create(CultureInfo.InvariantCulture, $"type={ex.GetType().Name} message={ex.Message}");
    }

    private static void PrintClientNativeLaunchProbeReport(ClientNativeLaunchProbeReport report)
    {
        Console.WriteLine("Client native launch probe:");
        Console.WriteLine("Mode: native-process-start no-frida no-mutation");
        Console.WriteLine($"Executable: {report.Options.ExecutablePath}");
        Console.WriteLine($"Working directory: {report.Options.WorkingDirectory}");
        Console.WriteLine($"Arguments: {report.Options.Arguments.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Timeout ms: {report.Options.TimeoutMs.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Kill after ms: {(report.Options.KillAfterMs is int killAfterMs ? killAfterMs.ToString(CultureInfo.InvariantCulture) : "<none>")}");
        Console.WriteLine($"Kill on timeout: {FormatClientLaunchEvidenceBool(report.Options.KillOnTimeout)}");
        Console.WriteLine($"Debug logs: {FormatClientNativeRuntimeDebugLogPaths(report.Options.DebugLogPaths)}");
        Console.WriteLine($"Top threads: {report.Options.TopThreadCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Ready file: {report.Options.ReadyFilePath ?? "<none>"}");
        Console.WriteLine($"ReadinessRequested: {FormatClientLaunchEvidenceBool(report.Readiness.Requested)}");
        Console.WriteLine($"ReadinessObserved: {FormatClientLaunchEvidenceBool(report.Readiness.Observed)}");
        Console.WriteLine($"ReadinessError: {(report.Readiness.Error.Length == 0 ? "<none>" : report.Readiness.Error)}");
        Console.WriteLine($"StartSucceeded: {FormatClientLaunchEvidenceBool(report.StartSucceeded)}");
        Console.WriteLine($"ProcessId: {(report.ProcessId is int processId ? processId.ToString(CultureInfo.InvariantCulture) : "<none>")}");
        Console.WriteLine($"Exited: {FormatClientLaunchEvidenceBool(report.Exited)}");
        Console.WriteLine($"ExitCode: {(report.ExitCode is int exitCode ? exitCode.ToString(CultureInfo.InvariantCulture) : "<none>")}");
        Console.WriteLine($"TimedOut: {FormatClientLaunchEvidenceBool(report.TimedOut)}");
        Console.WriteLine($"KillAttempted: {FormatClientLaunchEvidenceBool(report.KillAttempted)}");
        Console.WriteLine($"Killed: {FormatClientLaunchEvidenceBool(report.Killed)}");
        Console.WriteLine($"ElapsedMs: {report.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"NativeError: {(report.NativeError.Length == 0 ? "<none>" : report.NativeError)}");
        Console.WriteLine($"KillError: {(report.KillError.Length == 0 ? "<none>" : report.KillError)}");
        PrintClientNativeRuntimeSnapshots(report.RuntimeSnapshots);
    }
}

internal sealed record ClientNativeLaunchProbeOptions(
    string ExecutablePath,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    int TimeoutMs,
    int? KillAfterMs,
    bool KillOnTimeout,
    IReadOnlyList<string> DebugLogPaths,
    int TopThreadCount,
    string? ReadyFilePath = null)
{
    public int TimeoutMilliseconds => TimeoutMs;

    public int? KillAfterMilliseconds => KillAfterMs;
}

internal sealed record ClientNativeLaunchProbeReport(
    ClientNativeLaunchProbeOptions Options,
    bool StartSucceeded,
    int? ProcessId,
    string NativeError,
    bool Exited,
    int? ExitCode,
    bool TimedOut,
    bool KillAttempted,
    bool Killed,
    string KillError,
    long ElapsedMilliseconds,
    IReadOnlyList<ClientNativeRuntimeSnapshot> RuntimeSnapshots,
    ClientNativeLaunchReadiness Readiness);
