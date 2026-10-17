using System.Globalization;

internal static partial class Program
{
    static int WritePktmonCaptureScript(string[] args)
    {
        PktmonCaptureSpec spec;
        try
        {
            spec = ResolvePktmonCaptureSpec(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var outputDirectory = Path.GetDirectoryName(spec.OutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(spec.OutputPath, CreatePktmonCaptureScript(spec));

        Console.WriteLine("Pktmon capture script:");
        Console.WriteLine($"Kind: {spec.Kind}");
        Console.WriteLine(spec.AdditionalFilters.Count == 0
            ? $"Port: {spec.Port}"
            : $"Ports: {FormatPktmonFilterPorts(spec)}");
        Console.WriteLine(spec.DurationSeconds is { } seconds
            ? $"Mode: timed {seconds} seconds"
            : "Mode: manual stop");
        Console.WriteLine($"Output: {spec.OutputPath}");
        Console.WriteLine("Run from normal PowerShell; the script relaunches itself elevated if needed.");
        return 0;
    }

    static PktmonCaptureSpec ResolvePktmonCaptureSpec(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("Missing capture kind. Use 'login' or 'world'.");
        }

        var kind = args[1].ToLowerInvariant();
        var spec = CreatePktmonCaptureSpec(kind);

        string? outputPath = null;
        int? durationSeconds = null;
        for (var index = 2; index < args.Length; index++)
        {
            var value = args[index];
            if (string.Equals(value, "--duration-seconds", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("Missing value for --duration-seconds.");
                }

                if (!int.TryParse(args[index + 1], out var seconds) || seconds <= 0)
                {
                    throw new ArgumentException("--duration-seconds must be a positive whole number.");
                }

                durationSeconds = seconds;
                index++;
                continue;
            }

            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown write-pktmon-capture-script option: {value}");
            }

            if (outputPath is not null)
            {
                throw new ArgumentException("write-pktmon-capture-script accepts at most one output path.");
            }

            outputPath = Path.GetFullPath(value);
        }

        outputPath ??= Path.GetFullPath(Path.Combine(
                ".setup_work",
                "capture-scripts",
                $"{kind}-pktmon-capture.ps1"));
        return spec with { OutputPath = outputPath, DurationSeconds = durationSeconds };
    }

    static PktmonCaptureSpec CreatePktmonCaptureSpec(string kind)
    {
        return kind switch
        {
            "login" => new PktmonCaptureSpec(
                "login",
                "CO_LOGIN_9960",
                9960,
                128,
                32,
                "co_login_9960_pktmon",
                "Run one normal client login, then return here and press Enter."),
            "world" => new PktmonCaptureSpec(
                "world",
                "CO_WORLD_5818",
                5818,
                256,
                64,
                "co_world_5818_pktmon",
                "Select the server and enter the world, then return here and press Enter."),
            "original-flow" => new PktmonCaptureSpec(
                "original-flow",
                "CO_LOGINWEB_80",
                80,
                256,
                96,
                "co_original_flow_pktmon",
                "Run one normal client loader, login, server-select, and world-entry flow during this capture window.")
            {
                AdditionalFilters =
                [
                    new PktmonCaptureFilter("CO_GUARDSHIELD_2021", 2021),
                new PktmonCaptureFilter("CO_ACCOUNT_9960", 9960),
                new PktmonCaptureFilter("CO_WORLD_5818", 5818),
            ],
            },
            _ => throw new ArgumentException("Unknown capture kind. Use 'login', 'world', or 'original-flow'."),
        };
    }

    static string CreatePktmonCaptureScript(PktmonCaptureSpec spec)
    {
        if (spec.DurationSeconds is { } durationSeconds)
        {
            return CreateTimedPktmonCaptureScript(spec, durationSeconds);
        }

        var escapedRootPath = ResolvePktmonCaptureScriptRootPath(spec.OutputPath).Replace("'", "''", StringComparison.Ordinal);
        var escapedPrompt = spec.Prompt.Replace("'", "''", StringComparison.Ordinal);
        return $$"""
        $ErrorActionPreference = 'Stop'

        $Root = '{{escapedRootPath}}'
        function Test-IsAdministrator {
            $Identity = [Security.Principal.WindowsIdentity]::GetCurrent()
            $Principal = [Security.Principal.WindowsPrincipal]::new($Identity)
            return $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        }

        function Move-ExistingCaptureFile {
            param([string] $Path)

            if (-not (Test-Path -LiteralPath $Path)) {
                return
            }

            $Stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
            $Backup = "$Path.previous-$Stamp"
            $Index = 1
            while (Test-Path -LiteralPath $Backup) {
                $Backup = "$Path.previous-$Stamp-$Index"
                $Index++
            }

            Move-Item -LiteralPath $Path -Destination $Backup -Force
            Write-Host "Existing capture file moved to $Backup"
        }

        if (-not (Test-IsAdministrator)) {
            Write-Host 'Not elevated; opening an administrator PowerShell for this capture.'
            $ArgumentList = "-NoExit -ExecutionPolicy Bypass -File `"$PSCommandPath`""
            Start-Process -FilePath powershell.exe -Verb RunAs -WorkingDirectory $Root -ArgumentList $ArgumentList
            exit 0
        }

        Set-Location -LiteralPath $Root

        $OutDir = Join-Path $Root '.setup_work\captures\{{spec.Kind}}-pktmon'
        New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

        $Etl = Join-Path $OutDir '{{spec.FileStem}}.etl'
        $Pcap = Join-Path $OutDir '{{spec.FileStem}}.pcapng'

        Write-Host 'WolfConquer {{spec.Kind}} capture for TCP port {{spec.Port}}'
        Write-Host 'This script opens an administrator PowerShell if launched normally.'
        Write-Host "Workspace root: $Root"
        Write-Host "Capture output will be converted to $Pcap"
        Move-ExistingCaptureFile $Etl
        Move-ExistingCaptureFile $Pcap
        pktmon status
        pktmon filter remove
        {{CreatePktmonFilterAddCommands(spec)}}
        pktmon start --capture --comp all --pkt-size {{spec.PacketSize}} --file-name "$Etl" --file-size {{spec.FileSizeMb}} --log-mode circular

        try {
            Write-Host '{{escapedPrompt}}'
            Write-Host 'Return to this elevated window and press Enter to stop pktmon.'
            [Console]::ReadLine() | Out-Null
        }
        finally {
            pktmon stop
            pktmon etl2pcap "$Etl" --out "$Pcap"
            pktmon filter remove
            Write-Host "Capture written to $Pcap"
        }
        """;
    }

    static string CreateTimedPktmonCaptureScript(PktmonCaptureSpec spec, int durationSeconds)
    {
        var escapedRootPath = ResolvePktmonCaptureScriptRootPath(spec.OutputPath).Replace("'", "''", StringComparison.Ordinal);
        var escapedPrompt = spec.Prompt.Replace("'", "''", StringComparison.Ordinal);
        return $$"""
        $ErrorActionPreference = 'Stop'

        $Seconds = {{durationSeconds}}
        $Root = '{{escapedRootPath}}'
        function Test-IsAdministrator {
            $Identity = [Security.Principal.WindowsIdentity]::GetCurrent()
            $Principal = [Security.Principal.WindowsPrincipal]::new($Identity)
            return $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        }

        function Move-ExistingCaptureFile {
            param([string] $Path)

            if (-not (Test-Path -LiteralPath $Path)) {
                return
            }

            $Stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
            $Backup = "$Path.previous-$Stamp"
            $Index = 1
            while (Test-Path -LiteralPath $Backup) {
                $Backup = "$Path.previous-$Stamp-$Index"
                $Index++
            }

            Move-Item -LiteralPath $Path -Destination $Backup -Force
            Write-Host "Existing capture file moved to $Backup"
        }

        if (-not (Test-IsAdministrator)) {
            Write-Host 'Not elevated; opening an administrator PowerShell for this capture.'
            $ArgumentList = "-NoExit -ExecutionPolicy Bypass -File `"$PSCommandPath`""
            Start-Process -FilePath powershell.exe -Verb RunAs -WorkingDirectory $Root -ArgumentList $ArgumentList
            exit 0
        }

        Set-Location -LiteralPath $Root

        $OutDir = Join-Path $Root '.setup_work\captures\{{spec.Kind}}-pktmon'
        New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

        $Etl = Join-Path $OutDir '{{spec.FileStem}}.etl'
        $Pcap = Join-Path $OutDir '{{spec.FileStem}}.pcapng'
        $Log = Join-Path $OutDir '{{spec.FileStem}}.log'

        Start-Transcript -LiteralPath $Log -Force | Out-Null

        Write-Host 'WolfConquer timed {{spec.Kind}} capture for TCP port {{spec.Port}}'
        Write-Host 'This script opens an administrator PowerShell if launched normally.'
        Write-Host "Capture window: $Seconds seconds"
        Write-Host '{{escapedPrompt}}'
        Write-Host "Workspace root: $Root"
        Write-Host "ETL target: $Etl"
        Write-Host "PCAP target: $Pcap"

        try { pktmon stop | Out-Host } catch { }
        try { pktmon filter remove | Out-Host } catch { }

        Move-ExistingCaptureFile $Etl
        Move-ExistingCaptureFile $Pcap

        {{CreatePktmonFilterAddCommands(spec)}}
        pktmon start --capture --comp all --pkt-size {{spec.PacketSize}} --file-name "$Etl" --file-size {{spec.FileSizeMb}} --log-mode circular

        try {
            Start-Sleep -Seconds $Seconds
        }
        finally {
            pktmon stop
            pktmon etl2pcap "$Etl" --out "$Pcap"
            pktmon filter remove
            Write-Host "Capture written to $Pcap"
            Write-Host 'You can close this elevated window.'
            Stop-Transcript | Out-Null
        }
        """;
    }

    static string ResolvePktmonCaptureScriptRootPath(string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (string.IsNullOrEmpty(outputDirectory))
        {
            return Directory.GetCurrentDirectory();
        }

        var directory = new DirectoryInfo(outputDirectory);
        if (string.Equals(directory.Name, "capture-scripts", StringComparison.OrdinalIgnoreCase) &&
            directory.Parent is { } setupDirectory &&
            string.Equals(setupDirectory.Name, ".setup_work", StringComparison.OrdinalIgnoreCase) &&
            setupDirectory.Parent is { } rootDirectory)
        {
            return rootDirectory.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    static string CreateTimedPktmonScriptPath(string scriptPath)
    {
        return Path.Combine(
            Path.GetDirectoryName(scriptPath) ?? "",
            Path.GetFileNameWithoutExtension(scriptPath) + "-timed" + Path.GetExtension(scriptPath));
    }

    static string CreateOriginalFlowTimedPktmonScriptPath(string rootPath)
    {
        return Path.Combine(
            rootPath,
            ".setup_work",
            "capture-scripts",
            "original-flow-pktmon-capture-timed.ps1");
    }

    static string FormatPktmonFilterPorts(PktmonCaptureSpec spec)
    {
        return string.Join(",", EnumeratePktmonFilters(spec).Select(filter => filter.Port.ToString(CultureInfo.InvariantCulture)));
    }

    static string CreatePktmonFilterAddCommands(PktmonCaptureSpec spec)
    {
        return string.Join(
            Environment.NewLine,
            EnumeratePktmonFilters(spec).Select(filter => $"pktmon filter add {filter.FilterName} -t TCP -p {filter.Port}"));
    }

    static IEnumerable<PktmonCaptureFilter> EnumeratePktmonFilters(PktmonCaptureSpec spec)
    {
        yield return new PktmonCaptureFilter(spec.FilterName, spec.Port);
        foreach (var filter in spec.AdditionalFilters)
        {
            yield return filter;
        }
    }

    static string CreateElevatedPowerShellLaunchCommand(string workingDirectory, string scriptPath)
    {
        var argumentList = $"-NoExit -ExecutionPolicy Bypass -File \"{scriptPath}\"";
        return
            $"Start-Process powershell.exe -Verb RunAs -WorkingDirectory {QuotePowerShellSingle(workingDirectory)} " +
            $"-ArgumentList {QuotePowerShellSingle(argumentList)}";
    }
}
