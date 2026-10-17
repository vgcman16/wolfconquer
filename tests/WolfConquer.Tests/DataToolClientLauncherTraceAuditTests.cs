namespace WolfConquer.Tests;

public sealed class DataToolClientLauncherTraceAuditTests
{
    [Fact]
    public async Task TraceAuditFlagsUnexpectedExternalTcpRoutes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                reportPath,
                """
                Launcher trace report: 2026-07-06 10:38:46

                Observed TCP connections
                FirstSeen    LastSeen     Samples   Pid Local            Remote              State
                ---------    --------     -------   --- -----            ------              -----
                10:38:09.189 10:38:09.189       1 35056 0.0.0.0:64205    0.0.0.0:0           Bound
                10:38:09.189 10:38:09.189       1 35056 10.0.0.116:64205 192.0.2.10:9865 Established

                Frida HTTP redirect attachments
                  (none)
                """);

            var result = await RunDataToolAsync("client-launcher-trace-audit", reportPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains($"Client launcher trace audit: {reportPath}", result.StandardOutput);
            Assert.Contains("Connections: 2", result.StandardOutput);
            Assert.Contains("External routes: 1", result.StandardOutput);
            Assert.Contains("Trace evidence gaps: 0", result.StandardOutput);
            Assert.Contains("Route fences: 1", result.StandardOutput);
            Assert.Contains(
                "needs-local-fence route=cached-dx9-login-route remote=192.0.2.10:9865 local=10.0.0.116:64205 pid=35056 state=Established samples=1 reason=unexpected external cached DX9 login route",
                result.StandardOutput);
            Assert.Contains("local-bind pid=35056 local=0.0.0.0:64205 remote=0.0.0.0:0 state=Bound samples=1", result.StandardOutput);
            Assert.Contains("external-route pid=35056 local=10.0.0.116:64205 remote=192.0.2.10:9865 state=Established samples=1", result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TraceAuditAllowsExpectedLocalGameRoutes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                reportPath,
                """
                Observed TCP connections
                FirstSeen    LastSeen     Samples   Pid Local            Remote              State
                ---------    --------     -------   --- -----            ------              -----
                10:38:09.189 10:38:09.189       2 35056 127.0.0.1:64205  127.0.0.1:80        Established
                10:38:09.189 10:38:09.189       3 35056 10.0.0.116:64206 127.0.0.1:5818 Established
                """);

            var result = await RunDataToolAsync("client-launcher-trace-audit", reportPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Connections: 2", result.StandardOutput);
            Assert.Contains("External routes: 0", result.StandardOutput);
            Assert.Contains("Trace evidence gaps: 0", result.StandardOutput);
            Assert.Contains("Route fences: 0", result.StandardOutput);
            Assert.Contains("local-route pid=35056 local=127.0.0.1:64205 remote=127.0.0.1:80 state=Established samples=2", result.StandardOutput);
            Assert.Contains("local-route pid=35056 local=10.0.0.116:64206 remote=127.0.0.1:5818 state=Established samples=3", result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TraceAuditCanAllowKnownExternalResearchRoute()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                reportPath,
                """
                Observed TCP connections
                FirstSeen    LastSeen     Samples   Pid Local            Remote              State
                ---------    --------     -------   --- -----            ------              -----
                10:38:09.189 10:38:09.189       1 35056 10.0.0.116:64205 192.0.2.10:9865 Established
                """);

            var result = await RunDataToolAsync(
                "client-launcher-trace-audit",
                reportPath,
                "--allow-external",
                "192.0.2.10:9865");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("External routes: 0", result.StandardOutput);
            Assert.Contains("Trace evidence gaps: 0", result.StandardOutput);
            Assert.Contains("Route fences: 1", result.StandardOutput);
            Assert.Contains(
                "allowed-external-research route=cached-dx9-login-route remote=192.0.2.10:9865 local=10.0.0.116:64205 pid=35056 state=Established samples=1 reason=explicitly allowed by --allow-external; still not locally fenced",
                result.StandardOutput);
            Assert.Contains("allowed-external-route pid=35056 local=10.0.0.116:64205 remote=192.0.2.10:9865 state=Established samples=1", result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TraceAuditFlagsFridaSpawnWithNoInstrumentationLogAsEvidenceGap()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-gap-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                reportPath,
                """
                Launcher trace report: 2026-07-06 12:45:25

                Observed client processes
                FirstSeen    LastSeen     Samples   Pid ParentPid Name                Path
                ---------    --------     -------   --- --------- ----                ----
                12:44:58.801 12:45:23.032      17 37640     46928 ._cache_Conquer.exe C:\fixtures\client\Env_DX9\._cache_Conquer.exe

                Observed TCP connections
                  (none)

                Frida spawn evidence
                Mode  ProcessName         TargetPid TargetPath                              HelperPid Exited ExitCode LogLength RouteLines NetworkEvidenceLines StdOutBytes StdErrBytes
                ----  -----------         --------- ----------                              --------- ------ -------- --------- ---------- -------------------- ----------- -----------
                spawn ._cache_Conquer.exe           C:\fixtures\client\Env_DX9\._cache_Conquer.exe     48864   True                  0          0                    0         129           0

                Frida HTTP redirect attachments
                Mode  ProcessName         TargetPid TargetPath                              HelperPid Exited ExitCode LogLength RouteLines NetworkEvidenceLines StdOutBytes StdErrBytes
                ----  -----------         --------- ----------                              --------- ------ -------- --------- ---------- -------------------- ----------- -----------
                spawn ._cache_Conquer.exe           C:\fixtures\client\Env_DX9\._cache_Conquer.exe     48864   True                  0          0                    0         129           0
                """);

            var result = await RunDataToolAsync("client-launcher-trace-audit", reportPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Connections: 0", result.StandardOutput);
            Assert.Contains("External routes: 0", result.StandardOutput);
            Assert.Contains("Trace evidence gaps: 1", result.StandardOutput);
            Assert.Contains(
                "trace-evidence-gap mode=spawn process=._cache_Conquer.exe logLength=0 routeLines=0 networkEvidenceLines=0 reason=frida-helper-produced-no-log",
                result.StandardOutput);
            Assert.Contains("Route fences: 0", result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TraceAuditFlagsFridaHelperAttachFailureFromStdout()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-frida-fail-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");
        var sidecarLogPath = Path.Combine(tempRoot, "frida.log");
        var stdoutPath = Path.Combine(tempRoot, "frida.stdout.log");
        var stderrPath = Path.Combine(tempRoot, "frida.stderr.log");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(sidecarLogPath, "[loader] placeholder\n");
            await File.WriteAllTextAsync(
                stdoutPath,
                "Failed to attach: process with pid 61920 either refused to load frida-agent, or terminated during injection\n");
            await File.WriteAllTextAsync(stderrPath, string.Empty);
            await File.WriteAllTextAsync(
                reportPath,
                $$"""
                Launcher trace report: 2026-07-08 02:42:41

                Observed TCP connections
                  (none)

                Frida spawn evidence
                Mode  ProcessName         TargetPid TargetPath                              HelperPid Exited ExitCode LogLength RouteLines NetworkEvidenceLines StdOutBytes StdErrBytes
                ----  -----------         --------- ----------                              --------- ------ -------- --------- ---------- -------------------- ----------- -----------
                spawn ._cache_Conquer.exe           client\._cache_Conquer.exe             60596   True        0        123          0                    0         129           0

                Frida helper sidecar logs
                mode=spawn process=._cache_Conquer.exe helperPid=60596 stdoutBytes=129 stderrBytes=0 log={{sidecarLogPath}} stdout={{stdoutPath}} stderr={{stderrPath}}
                """);

            var result = await RunDataToolAsync("client-launcher-trace-audit", reportPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("sidecar-log mode=spawn process=._cache_Conquer.exe exists=True", result.StandardOutput);
            Assert.Contains("fridaFailures=1", result.StandardOutput);
            Assert.Contains("frida-helper-failure process=._cache_Conquer.exe message=Failed to attach:", result.StandardOutput);
            Assert.Contains("Trace evidence gaps: 1", result.StandardOutput);
            Assert.Contains(
                "trace-evidence-gap mode=spawn process=._cache_Conquer.exe logLength=0 routeLines=0 networkEvidenceLines=0 reason=frida-helper-failed-to-attach",
                result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TraceAuditReportsNoGameProcessSpawnWhenLauncherNeverCreatesConquerChild()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-no-game-spawn-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");
        var sidecarLogPath = Path.Combine(tempRoot, "frida-no-conquer.log");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                sidecarLogPath,
                """
                [process] CreateProcessW path=client\._cache_play.exe commandLine=play.exe
                [route-url] http://127.0.0.1/ndlogin/zfplay/
                """);
            await File.WriteAllTextAsync(
                reportPath,
                """
                Launcher trace report: 2026-07-07 09:14:12

                Observed client processes
                FirstSeen    LastSeen     Samples   Pid ParentPid Name              Path
                ---------    --------     -------   --- --------- ----              ----
                09:13:58.012 09:14:10.221       9 41800         0 play.exe          client\play.exe
                09:13:59.102 09:14:10.221       8 42128     41800 ._cache_play.exe  client\._cache_play.exe

                Observed TCP connections
                  (none)

                Frida spawn evidence
                Mode  ProcessName       TargetPid TargetPath                  HelperPid Exited ExitCode LogLength RouteLines NetworkEvidenceLines StdOutBytes StdErrBytes
                ----  -----------       --------- ----------                  --------- ------ -------- --------- ---------- -------------------- ----------- -----------
                spawn ._cache_play.exe            client\._cache_play.exe    43320   True        0      1542          3                    2        2048           0

                Frida HTTP redirect attachments
                Mode  ProcessName       TargetPid TargetPath                  HelperPid Exited ExitCode LogLength RouteLines NetworkEvidenceLines StdOutBytes StdErrBytes
                ----  -----------       --------- ----------                  --------- ------ -------- --------- ---------- -------------------- ----------- -----------
                spawn ._cache_play.exe            client\._cache_play.exe    43320   True        0      1542          3                    2        2048           0

                Frida sidecar logs
                mode=spawn process=._cache_play.exe pid=42128 log=frida-no-conquer.log stdout=frida-no-conquer.out stderr=frida-no-conquer.err
                """);

            var result = await RunDataToolAsync("client-launcher-trace-audit", reportPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Connections: 0", result.StandardOutput);
            Assert.Contains("Observed game processes: 0", result.StandardOutput);
            Assert.Contains("Game process spawn calls: 0", result.StandardOutput);
            Assert.Contains(
                "no-game-process-spawn expected=Conquer.exe observed=play.exe,._cache_play.exe reason=no Conquer.exe child observed in launcher process trace",
                result.StandardOutput);
            Assert.Contains(
                "sidecar-log mode=spawn process=._cache_play.exe exists=True ini=0 file=0 gameInfo=0 processCalls=1 gameSpawns=0",
                result.StandardOutput);
            Assert.DoesNotContain("observed-game-process", result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TraceAuditReportsGameInfoTraceLinesWhenLauncherReadsRootOrIniGameInfo()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-gameinfo-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");
        var sidecarLogPath = Path.Combine(tempRoot, "frida-gameinfo.log");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                sidecarLogPath,
                """
                [ini] client\GameInfo.ini [1] GamePath=client GameProcess=._cache_Conquer.exe
                [file] client\ini\GameInfo.ini [1] GamePath=client GameProcess=Conquer.exe
                """);
            await File.WriteAllTextAsync(
                reportPath,
                """
                Launcher trace report: 2026-07-07 09:32:44

                Observed client processes
                FirstSeen    LastSeen     Samples   Pid ParentPid Name                Path
                ---------    --------     -------   --- --------- ----                ----
                09:32:18.011 09:32:43.902      12 43100     42912 ._cache_Conquer.exe C:\fixtures\client\Env_DX9\._cache_Conquer.exe

                Observed launcher file evidence
                Time         Pid   Path                       Detail
                ----         ---   ----                       ------
                09:32:19.118 43100 client\GameInfo.ini   [1] GamePath=client GameProcess=._cache_Conquer.exe
                09:32:19.124 43100 client\ini\GameInfo.ini [1] GamePath=client GameProcess=Conquer.exe

                Observed TCP connections
                  (none)

                Frida HTTP redirect attachments
                  (none)

                Frida sidecar logs
                mode=attach process=._cache_Conquer.exe pid=43100 log=frida-gameinfo.log stdout=frida-gameinfo.out stderr=frida-gameinfo.err
                """);

            var result = await RunDataToolAsync("client-launcher-trace-audit", reportPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Observed game processes: 1", result.StandardOutput);
            Assert.Contains(
                "observed-game-process pid=43100 parentPid=42912 name=._cache_Conquer.exe samples=12 path=C:\\fixtures\\client\\Env_DX9\\._cache_Conquer.exe",
                result.StandardOutput);
            Assert.Contains("GameInfo probes: 2", result.StandardOutput);
            Assert.Contains(
                "sidecar-log mode=attach process=._cache_Conquer.exe exists=True ini=1 file=1 gameInfo=2",
                result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TraceAuditReportsUpdateAndWebRouteEvidenceCountsFromFridaSidecar()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-sidecar-routes-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");
        var sidecarLogPath = Path.Combine(tempRoot, "frida-routes.log");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                sidecarLogPath,
                """
                [route-url] http://127.0.0.1/ndlogin/zfplay/
                [route-socket] tcp://127.0.0.1:5818
                [wininet] GET http://127.0.0.1/ndlogin/zfplay/
                """);
            await File.WriteAllTextAsync(
                reportPath,
                """
                Launcher trace report: 2026-07-07 09:51:03

                Observed TCP connections
                FirstSeen    LastSeen     Samples   Pid Local            Remote             State
                ---------    --------     -------   --- -----            ------             -----
                09:50:41.220 09:50:41.382       2 44732 127.0.0.1:64001 127.0.0.1:80       Established
                09:50:42.112 09:50:43.010       4 44732 10.0.0.116:64002 127.0.0.1:5818 Established
                09:50:44.310 09:50:44.511       1 44732 10.0.0.116:64003 127.0.0.1:9960 Established

                Frida HTTP redirect attachments
                Mode   ProcessName         TargetPid TargetPath                              HelperPid Exited ExitCode LogLength RouteLines NetworkEvidenceLines StdOutBytes StdErrBytes
                ----   -----------         --------- ----------                              --------- ------ -------- --------- ---------- -------------------- ----------- -----------
                attach ._cache_Conquer.exe     44732 C:\fixtures\client\Env_DX9\._cache_Conquer.exe     45280   True        0      4096          3                    3        8192           0

                Frida sidecar logs
                mode=attach process=._cache_Conquer.exe pid=44732 log=frida-routes.log stdout=frida-routes.out stderr=frida-routes.err
                """);

            var result = await RunDataToolAsync("client-launcher-trace-audit", reportPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Connections: 3", result.StandardOutput);
            Assert.Contains("External routes: 0", result.StandardOutput);
            Assert.Contains("Launcher route URL lines: 1", result.StandardOutput);
            Assert.Contains("Launcher route socket lines: 1", result.StandardOutput);
            Assert.Contains("WinINet request lines: 1", result.StandardOutput);
            Assert.Contains(
                "sidecar-log mode=attach process=._cache_Conquer.exe exists=True ini=0 file=0 gameInfo=0 processCalls=0 gameSpawns=0 routeUrl=1 routeSocket=1 wininet=1",
                result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TraceAuditSummarizesSidecarLogsAndMissingGameLaunch()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-sidecar-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");
        var sidecarLogPath = Path.Combine(tempRoot, "frida-cache-play.log");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                sidecarLogPath,
                """
                [ini] ._cache_play.exe GetPrivateProfileStringW result=18 file=".\ini\GameInfo.ini" section="1" key="GameProcess" value="._cache_Conquer.exe"
                [file] ._cache_play.exe CreateFileW result=0xca8 path="client\ini\GameInfo.ini"
                [route-url] ._cache_play.exe InternetOpenUrlW https://loader.example.invalid/ndlogin/skin/zf.dat
                [route-socket] ._cache_play.exe api=connect 198.51.100.13:80 -> 127.0.0.1:80
                [wininet] ._cache_play.exe HttpOpenRequestW GET /gamer/91safe/index20221212.shtml
                """);
            await File.WriteAllTextAsync(
                reportPath,
                $"""
                Launcher trace report: 2026-07-08 01:01:10

                Observed client processes
                FirstSeen    LastSeen     Samples   Pid ParentPid Name               Path
                ---------    --------     -------   --- --------- ----               ----
                01:00:58.768 01:01:07.347       5 59140     63628 ._cache_play.exe   client\._cache_play.exe
                01:01:02.134 01:01:07.347       4 63476     21412 ._cache_Update.exe client\._cache_Update.exe

                Observed TCP connections
                  (none)

                Frida HTTP redirect attachments
                Mode   ProcessName        TargetPid TargetPath                     HelperPid Exited ExitCode LogLength RouteLines NetworkEvidenceLines
                ----   -----------        --------- ----------                     --------- ------ -------- --------- ---------- --------------------
                attach ._cache_play.exe       59140 client\._cache_play.exe       66968   True        0     12345          2                   10

                Frida helper sidecar logs
                mode=attach process=._cache_play.exe helperPid=66968 stdoutBytes=64874 stderrBytes=0 log={sidecarLogPath} stdout={Path.Combine(tempRoot, "stdout.log")} stderr={Path.Combine(tempRoot, "stderr.log")}
                """);

            var result = await RunDataToolAsync("client-launcher-trace-audit", reportPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Observed game processes: 0", result.StandardOutput);
            Assert.Contains("Game process spawn calls: 0", result.StandardOutput);
            Assert.Contains(
                "no-game-process-spawn expected=Conquer.exe observed=._cache_play.exe,._cache_Update.exe reason=no Conquer.exe child observed in launcher process trace",
                result.StandardOutput);
            Assert.Contains("GameInfo probes: 2", result.StandardOutput);
            Assert.Contains("Launcher route URL lines: 1", result.StandardOutput);
            Assert.Contains("Launcher route socket lines: 1", result.StandardOutput);
            Assert.Contains("WinINet request lines: 1", result.StandardOutput);
            Assert.Contains("sidecar-log mode=attach process=._cache_play.exe exists=True", result.StandardOutput);
            Assert.Contains("ini=1 file=1 gameInfo=2 processCalls=0 gameSpawns=0 routeUrl=1 routeSocket=1 wininet=1", result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TraceAuditCountsObservedAndSpawnedGameProcessEvidence()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-launcher-trace-audit-game-spawn-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempRoot, "launcher-trace.txt");
        var sidecarLogPath = Path.Combine(tempRoot, "frida-cache-play.log");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(
                sidecarLogPath,
                """
                [process] ._cache_play.exe ShellExecuteW result=33 file="._cache_Conquer.exe" params="blacknull" dir="client"
                """);
            await File.WriteAllTextAsync(
                reportPath,
                $"""
                Observed client processes
                FirstSeen    LastSeen     Samples   Pid ParentPid Name                Path
                ---------    --------     -------   --- --------- ----                ----
                01:00:58.768 01:01:07.347       5 59140     63628 ._cache_play.exe    client\._cache_play.exe
                01:01:02.134 01:01:07.347       4 63476     59140 ._cache_Conquer.exe client\._cache_Conquer.exe

                Observed TCP connections
                  (none)

                Frida HTTP redirect attachments
                  (none)

                Frida helper sidecar logs
                mode=attach process=._cache_play.exe helperPid=66968 stdoutBytes=100 stderrBytes=0 log={sidecarLogPath} stdout={Path.Combine(tempRoot, "stdout.log")} stderr={Path.Combine(tempRoot, "stderr.log")}
                """);

            var result = await RunDataToolAsync("client-launcher-trace-audit", reportPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Observed game processes: 1", result.StandardOutput);
            Assert.Contains("observed-game-process pid=63476 parentPid=59140 name=._cache_Conquer.exe samples=4 path=client\\._cache_Conquer.exe", result.StandardOutput);
            Assert.Contains("Game process spawn calls: 1", result.StandardOutput);
            Assert.Contains("sidecar-log mode=attach process=._cache_play.exe exists=True ini=0 file=0 gameInfo=0 processCalls=1 gameSpawns=1", result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HelpIncludesClientLauncherTraceAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool client-launcher-trace-audit <launcherTraceReportPath> [--allow-external address:port] [...]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
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

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
