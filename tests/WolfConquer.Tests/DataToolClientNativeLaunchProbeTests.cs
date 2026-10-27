using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace WolfConquer.Tests;

public sealed partial class DataToolClientNativeLaunchProbeTests
{
    private const string Usage =
        "WolfConquer.DataTool client-native-launch-probe <exePath> [--workdir path] [--arg value] [...] [--timeout-ms N] [--kill-after-ms N] [--kill-on-timeout|--no-kill-on-timeout] [--debug-log path] [...] [--top-threads N] [--ready-file path] [--args arg ...]";

    [Fact]
    public async Task HelpMentionsClientNativeLaunchProbeUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(Usage, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task ProbeReportShowsExecutableDirectoryShortTimeoutAndKillOnTimeoutDefaults()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            var clientDirectory = Path.Combine(tempRoot, "client");
            Directory.CreateDirectory(clientDirectory);
            var missingPath = Path.Combine(clientDirectory, "play.exe");

            var result = await RunDataToolAsync("client-native-launch-probe", missingPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains($"Executable: {missingPath}", result.StandardOutput);
            Assert.Contains($"Working directory: {Path.GetFullPath(clientDirectory)}", result.StandardOutput);
            Assert.Contains("Arguments: 0", result.StandardOutput);
            Assert.Contains("Timeout ms: 5000", result.StandardOutput);
            Assert.Contains("Kill on timeout: True", result.StandardOutput);
            Assert.Contains("Kill after ms: <none>", result.StandardOutput);
            Assert.Contains("StartSucceeded: False", result.StandardOutput);
            Assert.Contains("NativeError: type=Win32Exception", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task ProbeLaunchesExecutableWithWorkingDirectoryArgumentsAndExitCode()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            var powershellPath = GetWindowsPowerShellPath();
            var proofPath = Path.Combine(tempRoot, "probe-proof.txt");
            var scriptPath = Path.Combine(tempRoot, "probe-target.ps1");
            await File.WriteAllTextAsync(
                scriptPath,
                "Set-Content -LiteralPath $args[0] -Value ((Get-Location).Path + '|' + $args[1])" +
                Environment.NewLine +
                "exit 7" +
                Environment.NewLine);

            var result = await RunDataToolAsync(
                "client-native-launch-probe",
                powershellPath,
                "--workdir",
                tempRoot,
                "--timeout-ms",
                "10000",
                "--args",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                scriptPath,
                proofPath,
                "alpha beta");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Client native launch probe:", result.StandardOutput);
            Assert.Contains("Mode: native-process-start no-frida no-mutation", result.StandardOutput);
            Assert.Contains($"Executable: {powershellPath}", result.StandardOutput);
            Assert.Contains($"Working directory: {Path.GetFullPath(tempRoot)}", result.StandardOutput);
            Assert.Contains("Arguments: 8", result.StandardOutput);
            Assert.Contains("StartSucceeded: True", result.StandardOutput);
            Assert.Contains("ProcessId: ", result.StandardOutput);
            Assert.Contains("ExitCode: 7", result.StandardOutput);
            Assert.Contains("TimedOut: False", result.StandardOutput);
            Assert.Contains("KillAttempted: False", result.StandardOutput);
            Assert.Contains("Killed: False", result.StandardOutput);
            Assert.Contains("NativeError: <none>", result.StandardOutput);
            Assert.Contains("KillError: <none>", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
            var proof = await File.ReadAllTextAsync(proofPath);
            Assert.Equal(Path.GetFullPath(tempRoot) + "|alpha beta", proof.TrimEnd('\r', '\n'));
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task MissingExecutableCapturesWin32CreationFailure()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            var missingPath = Path.Combine(tempRoot, "missing-play.exe");

            var result = await RunDataToolAsync(
                "client-native-launch-probe",
                missingPath,
                "--timeout-ms",
                "1000");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Client native launch probe:", result.StandardOutput);
            Assert.Contains($"Executable: {missingPath}", result.StandardOutput);
            Assert.Contains("StartSucceeded: False", result.StandardOutput);
            Assert.Contains("ProcessId: <none>", result.StandardOutput);
            Assert.Contains("ExitCode: <none>", result.StandardOutput);
            Assert.Contains("TimedOut: False", result.StandardOutput);
            Assert.Contains("Killed: False", result.StandardOutput);
            Assert.Contains("NativeError: type=Win32Exception nativeErrorCode=2", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task KillAfterBoundsLongRunningProbe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            var powershellPath = GetWindowsPowerShellPath();

            var result = await RunDataToolAsync(
                "client-native-launch-probe",
                powershellPath,
                "--workdir",
                tempRoot,
                "--timeout-ms",
                "3000",
                "--kill-after-ms",
                "500",
                "--args",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                "Start-Sleep -Seconds 30");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("StartSucceeded: True", result.StandardOutput);
            Assert.Contains("ExitCode: ", result.StandardOutput);
            Assert.Contains("TimedOut: False", result.StandardOutput);
            Assert.Contains("KillAttempted: True", result.StandardOutput);
            Assert.Contains("Killed: True", result.StandardOutput);
            Assert.Contains("NativeError: <none>", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task RuntimeSnapshotThreadLinesIncludeInstructionPointerModuleRvaAndByteSample()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            var powershellPath = GetWindowsWow64PowerShellPath();

            var result = await RunDataToolAsync(
                "client-native-launch-probe",
                powershellPath,
                "--workdir",
                tempRoot,
                "--timeout-ms",
                "4000",
                "--kill-after-ms",
                "1000",
                "--top-threads",
                "6",
                "--args",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                "Start-Sleep -Seconds 30");

            Assert.Equal(2, result.ExitCode);
            var threadLines = Regex.Matches(result.StandardOutput, @"^  Thread: .*$", RegexOptions.Multiline);
            Assert.True(threadLines.Count > 0, "Expected at least one captured thread line.");

            var sawInstructionPointer = false;
            var sawInstructionModuleRva = false;
            var sawInstructionBytes = false;
            foreach (Match threadLine in threadLines)
            {
                Assert.Contains("instructionPointer=", threadLine.Value);
                Assert.Contains("instructionModule=", threadLine.Value);
                Assert.Contains("instructionRva=", threadLine.Value);
                Assert.Contains("instructionBytes=", threadLine.Value);
                Assert.Contains("instructionError=", threadLine.Value);
                sawInstructionPointer |= Regex.IsMatch(threadLine.Value, @"instructionPointer=0x[0-9A-F]+");
                sawInstructionModuleRva |= Regex.IsMatch(
                    threadLine.Value,
                    @"instructionModule=(?!<unknown>|<none>)[^ ]+ instructionRva=0x[0-9A-F]+");
                sawInstructionBytes |= Regex.IsMatch(
                    threadLine.Value,
                    @"instructionBytes=[0-9A-F]{2}(?:-[0-9A-F]{2})+");
            }

            Assert.True(sawInstructionPointer, "Expected at least one live instruction pointer.");
            Assert.True(sawInstructionModuleRva, "Expected at least one instruction module and RVA.");
            Assert.True(sawInstructionBytes, "Expected at least one live instruction byte sample.");
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task KillAfterRuntimeSnapshotCapturesModulesThreadsTcpAndDebugLogDelta()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        TcpClient? acceptedClient = null;
        try
        {
            listener.Start();
            var acceptTask = listener.AcceptTcpClientAsync();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var powershellPath = GetWindowsPowerShellPath();
            var debugLogPath = Path.Combine(tempRoot, "client-debug.log");
            var scriptPath = Path.Combine(tempRoot, "probe-runtime-target.ps1");
            var readyPath = Path.Combine(tempRoot, "probe-ready.txt");
            await File.WriteAllTextAsync(debugLogPath, "seed");
            await File.WriteAllTextAsync(
                scriptPath,
                """
                $ErrorActionPreference = 'Stop'
                $port = [int]$args[0]
                $logPath = $args[1]
                $readyPath = $args[2]
                $client = New-Object Net.Sockets.TcpClient
                try {
                    $client.Connect('127.0.0.1', $port)
                    [IO.File]::AppendAllText($logPath, "connected" + [Environment]::NewLine)
                    [IO.File]::WriteAllText($readyPath, "pid=$PID`nready`n", [Text.UTF8Encoding]::new($false))
                    $null = $client.GetStream().ReadByte()
                } finally {
                    $client.Dispose()
                }
                """);

            var result = await RunDataToolAsync(
                "client-native-launch-probe",
                powershellPath,
                "--workdir",
                tempRoot,
                "--timeout-ms",
                "5000",
                "--kill-after-ms",
                "1500",
                "--debug-log",
                debugLogPath,
                "--top-threads",
                "8",
                "--ready-file",
                readyPath,
                "--args",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                scriptPath,
                port.ToString(CultureInfo.InvariantCulture),
                debugLogPath,
                readyPath);

            try
            {
                acceptedClient = await acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                throw new Xunit.Sdk.XunitException(
                    "The real fixture never connected before the probe completed. " +
                    $"stdout: {result.StandardOutput} stderr: {result.StandardError}");
            }

            Assert.Contains("ReadinessRequested: True", result.StandardOutput);
            Assert.Contains("ReadinessObserved: True", result.StandardOutput);
            Assert.Contains("ReadinessError: <none>", result.StandardOutput);
            Assert.Contains("TimedOut: False", result.StandardOutput);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("KillAttempted: True", result.StandardOutput);
            Assert.Contains("Killed: True", result.StandardOutput);
            Assert.Contains($"Debug logs: {debugLogPath}", result.StandardOutput);
            Assert.Contains("Top threads: 8", result.StandardOutput);
            Assert.Contains("RuntimeSnapshots:", result.StandardOutput);
            Assert.Contains("RuntimeSnapshot: reason=before-kill-after-bound", result.StandardOutput);
            Assert.Contains("RuntimeSnapshot: reason=after-kill", result.StandardOutput);
            Assert.Contains("rootExists=True", result.StandardOutput);
            Assert.Contains("Process: pid=", result.StandardOutput);
            Assert.Contains("responding=", result.StandardOutput);
            Assert.Contains("mainWindowHandle=", result.StandardOutput);
            Assert.Contains("mainWindowTitle=", result.StandardOutput);
            Assert.Matches(@"Modules: pid=\d+ count=[1-9]\d* names=", result.StandardOutput);
            Assert.Contains("Thread: pid=", result.StandardOutput);
            Assert.Contains("cpuMs=", result.StandardOutput);
            Assert.Contains("waitReason=", result.StandardOutput);
            var threadLines = Regex.Matches(result.StandardOutput, @"^  Thread: .*$", RegexOptions.Multiline);
            Assert.True(threadLines.Count > 0, "Expected at least one captured thread line.");
            var sawStartAddress = false;
            var sawResolvedStartModule = false;
            foreach (Match threadLine in threadLines)
            {
                Assert.Contains("startAddress=", threadLine.Value);
                Assert.Contains("startModule=", threadLine.Value);
                Assert.Contains("startRva=", threadLine.Value);
                Assert.Contains("startError=", threadLine.Value);
                Assert.Contains("instructionPointer=", threadLine.Value);
                Assert.Contains("instructionModule=", threadLine.Value);
                Assert.Contains("instructionRva=", threadLine.Value);
                Assert.Contains("instructionBytes=", threadLine.Value);
                Assert.Contains("instructionError=", threadLine.Value);
                sawStartAddress |= Regex.IsMatch(threadLine.Value, @"startAddress=0x[0-9A-F]+");
                sawResolvedStartModule |= Regex.IsMatch(
                    threadLine.Value,
                    @"startModule=(?!<unknown>|<none>)[^ ]+ startRva=0x[0-9A-F]+");
            }

            Assert.True(sawStartAddress, "Expected at least one thread start address.");
            Assert.True(sawResolvedStartModule, "Expected at least one thread start module and RVA.");
            Assert.True(
                result.StandardOutput.Contains("Tcp: pid=", StringComparison.Ordinal),
                $"Expected a captured TCP row. stdout: {result.StandardOutput} stderr: {result.StandardError}");
            Assert.Contains($"remote=127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}", result.StandardOutput);
            Assert.Matches(
                new Regex(@"DebugLog: path=" + Regex.Escape(debugLogPath) + @".*delta=[1-9]\d*"),
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            acceptedClient?.Dispose();
            listener.Stop();
            DeleteTempRoot(tempRoot);
        }
    }

    [Fact]
    public async Task KillAfterCleansObservedChildWhenLauncherExits()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            var powershellPath = GetWindowsPowerShellPath();
            var childPidPath = Path.Combine(tempRoot, "child-pid.txt");
            var scriptPath = Path.Combine(tempRoot, "spawn-child-and-exit.ps1");
            await File.WriteAllTextAsync(
                scriptPath,
                """
                $childPidPath = $args[0]
                $child = Start-Process -FilePath $PSHOME\powershell.exe -ArgumentList @(
                    '-NoProfile',
                    '-NonInteractive',
                    '-ExecutionPolicy',
                    'Bypass',
                    '-Command',
                    'Start-Sleep -Seconds 30'
                ) -PassThru -WindowStyle Hidden
                Set-Content -LiteralPath $childPidPath -Value $child.Id
                exit 0
                """);

            var result = await RunDataToolAsync(
                "client-native-launch-probe",
                powershellPath,
                "--workdir",
                tempRoot,
                "--timeout-ms",
                "5000",
                "--kill-after-ms",
                "2000",
                "--top-threads",
                "8",
                "--args",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                scriptPath,
                childPidPath);

            Assert.True(File.Exists(childPidPath),
                $"The launcher did not publish its child PID. DataToolExitCode: {result.ExitCode}" +
                $"{Environment.NewLine}stdout:{Environment.NewLine}{result.StandardOutput}" +
                $"{Environment.NewLine}stderr:{Environment.NewLine}{result.StandardError}");
            var childPidText = await File.ReadAllTextAsync(childPidPath);
            var childPid = int.Parse(childPidText.Trim(), CultureInfo.InvariantCulture);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Exited: True", result.StandardOutput);
            Assert.Contains("KillAttempted: True", result.StandardOutput);
            Assert.Contains("Killed: True", result.StandardOutput);
            Assert.Contains("RuntimeSnapshot: reason=after-exit", result.StandardOutput);
            Assert.Contains("RuntimeSnapshot: reason=after-kill", result.StandardOutput);
            Assert.Contains($"Process: pid={childPid.ToString(CultureInfo.InvariantCulture)} exists=True", result.StandardOutput);
            Assert.False(IsProcessRunning(childPid));
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static string GetWindowsPowerShellPath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "SysWOW64",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new FileNotFoundException("Could not locate Windows PowerShell for client-native-launch-probe tests.");
    }

    private static string GetWindowsWow64PowerShellPath()
    {
        var powershellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SysWOW64",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (File.Exists(powershellPath))
        {
            return Path.GetFullPath(powershellPath);
        }

        throw new FileNotFoundException("Could not locate 32-bit Windows PowerShell for WOW64 instruction evidence.");
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-client-native-launch-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        DeleteOwnedNativeLaunchTempRoot(tempRoot);
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = CreateDataToolProcess(repositoryRoot);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static Process CreateDataToolProcess(string repositoryRoot)
    {
        var copiedDataTool = Path.Combine(AppContext.BaseDirectory, "WolfConquer.DataTool.dll");
        if (!File.Exists(copiedDataTool))
        {
            return TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(copiedDataTool);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return process;
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var fromOutputDirectory = TryFindRepositoryRoot(AppContext.BaseDirectory);
        if (fromOutputDirectory is not null)
        {
            return fromOutputDirectory;
        }

        var fromWorkingDirectory = TryFindRepositoryRoot(Environment.CurrentDirectory);
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (sourceDirectory is not null)
        {
            var fromSourceDirectory = TryFindRepositoryRoot(sourceDirectory);
            if (fromSourceDirectory is not null)
            {
                return fromSourceDirectory;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output or working directory.");
    }

    private static string? TryFindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
