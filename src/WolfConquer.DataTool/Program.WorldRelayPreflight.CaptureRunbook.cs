using System.IO;

internal static partial class Program
{
    static int WorldRelayCaptureRunbook(string[] args)
    {
        string rootPath;
        try
        {
            rootPath = ResolveWorldRelayCaptureRunbookRootPath(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var fullRootPath = Path.GetFullPath(rootPath);
        var dataToolProjectPath = Path.Combine(
            fullRootPath,
            "src",
            "WolfConquer.DataTool",
            "WolfConquer.DataTool.csproj");
        var accountServerProjectPath = Path.Combine(
            fullRootPath,
            "src",
            "WolfConquer.AccountServer",
            "WolfConquer.AccountServer.csproj");
        var outputPath = Path.Combine(
            fullRootPath,
            ".setup_work",
            "runtime-smoke",
            "real-client-world-relay-capture",
            "world-entry-relay.json");

        const string liveAddress = "127.0.0.1";
        const int accountPort = 9960;
        const int originalWorldPort = 5818;
        const int relayWorldPort = 15819;

        Console.WriteLine("Real-client world relay capture runbook:");
        Console.WriteLine($"Root: {fullRootPath}");
        Console.WriteLine($"Relay listen: {liveAddress}:{relayWorldPort}");
        Console.WriteLine("Relay listen choice: use the live address clients and AccountServer can reach; use 0.0.0.0 only if the host cannot bind the live address directly.");
        Console.WriteLine($"Relay target: {liveAddress}:{originalWorldPort}");
        Console.WriteLine($"Output JSON: {outputPath}");
        Console.WriteLine("Guardrails: runbook only; these commands print the staged flow and do not stop or start processes by themselves.");
        Console.WriteLine("Set live AccountServer and smoke-test inputs:");
        Console.WriteLine("Optional account DB configuration (not bundled): config/GuardShield.ini.");
        Console.WriteLine(FormatAccountMySqlEnvironmentCommand());
        Console.WriteLine("$smokeUsername = '<live account username>'");
        Console.WriteLine("$smokePassword = '<live account password>'");
        Console.WriteLine("$serverName = 'WolfConquer'");
        Console.WriteLine("$stopWolfAccountServer = \"Get-CimInstance Win32_Process | Where-Object { `$_.Name -eq 'dotnet.exe' -and `$_.CommandLine -like '*WolfConquer.AccountServer.dll*--listen*--port 9960*' } | ForEach-Object { Stop-Process -Id `$_.ProcessId -Force }\"");
        Console.WriteLine("1. Run the preflight and do not continue until it reports Ready: True:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            "world-relay-capture-preflight");
        Console.WriteLine("If preflight reports an active 5818 or 15819 client socket, log out or restart the client before opening the relay window.");
        Console.WriteLine("2. Start the relay before restarting AccountServer:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"tcp-relay-capture {liveAddress} {relayWorldPort} {liveAddress} {originalWorldPort} " +
            $"{QuotePowerShellSingle(outputPath)} --max-bytes 65536 --accept-seconds 180 --idle-seconds 45");
        Console.WriteLine("3. Stop the current WolfConquer AccountServer, then restart it so successful auth forwards to the relay:");
        Console.WriteLine("powershell -NoProfile -ExecutionPolicy Bypass -Command $stopWolfAccountServer");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(accountServerProjectPath)} -- " +
            $"--listen --address 0.0.0.0 --port {accountPort} --legacy-account-wire " +
            $"--world-endpoint {liveAddress}:{relayWorldPort}");
        Console.WriteLine("4. Verify AccountServer returns the relay endpoint before live-client login:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"legacy-account-smoke {liveAddress} {accountPort} $smokeUsername $smokePassword $serverName " +
            $"--expect-ip {liveAddress} --expect-port {relayWorldPort}");
        Console.WriteLine("5. Log in once with the live client only while the relay and forwarded AccountServer are active, then stop the relay and restore AccountServer immediately.");
        Console.WriteLine("6. Stop the relay-mode AccountServer, then restore forwarding to the original world endpoint:");
        Console.WriteLine("powershell -NoProfile -ExecutionPolicy Bypass -Command $stopWolfAccountServer");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(accountServerProjectPath)} -- " +
            $"--listen --address 0.0.0.0 --port {accountPort} --legacy-account-wire " +
            $"--world-endpoint {liveAddress}:{originalWorldPort}");
        Console.WriteLine("7. Verify AccountServer returns the original endpoint after restore:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"legacy-account-smoke {liveAddress} {accountPort} $smokeUsername $smokePassword $serverName " +
            $"--expect-ip {liveAddress} --expect-port {originalWorldPort}");
        return 0;
    }

    static string ResolveWorldRelayCaptureRunbookRootPath(string[] args)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException("World relay capture runbook accepts at most one root path.");
        }

        return Path.GetFullPath(args.Length > 1 ? args[1] : ".");
    }
}
