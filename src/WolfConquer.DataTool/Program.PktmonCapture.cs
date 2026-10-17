using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

internal static partial class Program
{
    static int PktmonCaptureReadiness(string[] args)
    {
        PktmonCaptureReadinessOptions options;
        try
        {
            options = ResolvePktmonCaptureReadinessOptions(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var report = CreatePktmonCaptureReadinessReport(options.RootPath, options.MisplacedRootPath);

        Console.WriteLine("Pktmon capture readiness:");
        Console.WriteLine($"Root: {report.RootPath}");
        foreach (var item in report.Items)
        {
            Console.WriteLine(
                $"{item.Kind} port={item.Port} scriptExists={item.ScriptExists} " +
                $"script={item.ScriptPath} timedScriptExists={item.TimedScriptExists} " +
                $"timedScript={item.TimedScriptPath} pcapngExists={item.PcapngExists} pcapng={item.PcapngPath} " +
                $"pcapngLength={item.Pcapng.Length} pcapngSha256={item.Pcapng.Sha256} " +
                $"pcapngHeaderValid={item.Pcapng.HeaderValid} pcapngVersion={item.Pcapng.Version} " +
                $"pcapngByteOrder={item.Pcapng.ByteOrder} " +
                $"pcapngSectionHeaderBlocks={item.Pcapng.SectionHeaderBlocks} " +
                $"pcapngInterfaceDescriptionBlocks={item.Pcapng.InterfaceDescriptionBlocks} " +
                $"pcapngEnhancedPacketBlocks={item.Pcapng.EnhancedPacketBlocks} " +
                $"pcapngSimplePacketBlocks={item.Pcapng.SimplePacketBlocks} " +
                $"pcapngTotalBlocks={item.Pcapng.TotalBlocks} " +
                $"pcapngValidation={item.Pcapng.Validation} " +
                $"tcpServicePorts={FormatServicePorts(item.ServicePayloadPorts)}");
        }

        foreach (var item in report.MisplacedItems)
        {
            Console.WriteLine(
                $"misplaced {item.Kind} port={item.Port} root={item.RootPath} " +
                $"pcapng={item.Pcapng.Path} pcapngLength={item.Pcapng.Length} " +
                $"pcapngSha256={item.Pcapng.Sha256} pcapngReady={item.Pcapng.Ready} " +
                $"pcapngValidation={item.Pcapng.Validation}");
        }

        if (report.MisplacedItems.Count > 0)
        {
            Console.WriteLine(
                "MisplacedCaptureHint: Elevated PowerShell wrote capture output outside the workspace. " +
                "Use the rooted scripts or rerun the direct admin command from the repo root.");
        }

        Console.WriteLine($"ReadyMode: {report.ReadyMode}");
        if (!report.Ready)
        {
            Console.WriteLine($"NextAction: {report.NextAction}");
        }

        Console.WriteLine($"Ready: {report.Ready}");
        return report.Ready ? 0 : 2;
    }

    static int PktmonCaptureRunbook(string[] args)
    {
        string rootPath;
        try
        {
            rootPath = ResolvePktmonCaptureRunbookRootPath(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var report = CreatePktmonCaptureReadinessReport(rootPath);
        var dataToolProjectPath = Path.Combine(
            report.RootPath,
            "src",
            "WolfConquer.DataTool",
            "WolfConquer.DataTool.csproj");

        Console.WriteLine("Pktmon capture runbook:");
        Console.WriteLine($"Root: {report.RootPath}");
        Console.WriteLine("Expected files:");
        foreach (var item in report.Items)
        {
            Console.WriteLine(
                $"{item.Kind} port={item.Port} scriptExists={item.ScriptExists} " +
                $"script={item.ScriptPath} pcapng={item.Pcapng.Path}");
        }

        Console.WriteLine("Generate default scripts from normal PowerShell:");
        foreach (var item in report.Items)
        {
            Console.WriteLine(
                $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
                $"write-pktmon-capture-script {item.Kind} {QuotePowerShellSingle(item.ScriptPath)}");
        }

        Console.WriteLine("Generate timed scripts from normal PowerShell for bounded capture windows:");
        foreach (var item in report.Items.Where(item => item.Kind is "login" or "world"))
        {
            var seconds = item.Kind == "login" ? 150 : 180;
            Console.WriteLine(
                $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
                $"write-pktmon-capture-script {item.Kind} {QuotePowerShellSingle(CreateTimedPktmonScriptPath(item.ScriptPath))} " +
                $"--duration-seconds {seconds}");
        }
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"write-pktmon-capture-script original-flow {QuotePowerShellSingle(CreateOriginalFlowTimedPktmonScriptPath(report.RootPath))} " +
            "--duration-seconds 240");

        Console.WriteLine("Launch one elevated capture at a time from normal PowerShell:");
        foreach (var item in report.Items)
        {
            Console.WriteLine($"{item.Kind}: {CreateElevatedPowerShellLaunchCommand(report.RootPath, item.ScriptPath)}");
        }

        Console.WriteLine("Launch timed elevated captures if you generated the bounded scripts:");
        foreach (var item in report.Items.Where(item => item.Kind is "login" or "world"))
        {
            Console.WriteLine($"{item.Kind}: {CreateElevatedPowerShellLaunchCommand(report.RootPath, CreateTimedPktmonScriptPath(item.ScriptPath))}");
        }
        Console.WriteLine($"original-flow: {CreateElevatedPowerShellLaunchCommand(report.RootPath, CreateOriginalFlowTimedPktmonScriptPath(report.RootPath))}");

        Console.WriteLine("Run the combined capture directly inside an already-elevated PowerShell:");
        Console.WriteLine($"Set-Location {QuotePowerShellSingle(report.RootPath)}");
        Console.WriteLine($"powershell.exe -ExecutionPolicy Bypass -File {QuotePowerShellSingle(CreateOriginalFlowTimedPktmonScriptPath(report.RootPath))}");

        Console.WriteLine("Verify after the combined original-flow capture or both separate captures:");
        Console.WriteLine(
            $"dotnet run --project {QuotePowerShellSingle(dataToolProjectPath)} -- " +
            $"pktmon-capture-readiness {QuotePowerShellSingle(report.RootPath)}");
        return 0;
    }

    static int PktmonFixtureSummary(string[] args)
    {
        PktmonFixtureSummaryReport report;
        try
        {
            report = CreatePktmonFixtureSummaryReport(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine("Pktmon pcapng fixture summary:");
        if (report.RootPath is not null)
        {
            Console.WriteLine($"Root: {report.RootPath}");
        }

        foreach (var item in report.Items)
        {
            var prefix = string.IsNullOrEmpty(item.Kind)
                ? string.Empty
                : $"{item.Kind} port={item.Port} ";
            Console.WriteLine(
                $"{prefix}pcapng={item.Pcapng.Path} exists={item.Pcapng.Exists} " +
                $"length={item.Pcapng.Length} sha256={item.Pcapng.Sha256} " +
                $"headerValid={item.Pcapng.HeaderValid} version={item.Pcapng.Version} " +
                $"byteOrder={item.Pcapng.ByteOrder} " +
                $"sectionHeaderBlocks={item.Pcapng.SectionHeaderBlocks} " +
                $"interfaceDescriptionBlocks={item.Pcapng.InterfaceDescriptionBlocks} " +
                $"enhancedPacketBlocks={item.Pcapng.EnhancedPacketBlocks} " +
                $"simplePacketBlocks={item.Pcapng.SimplePacketBlocks} " +
                $"totalBlocks={item.Pcapng.TotalBlocks} " +
                $"validation={item.Pcapng.Validation}");
        }

        Console.WriteLine($"Ready: {report.Ready}");
        return report.Ready ? 0 : 2;
    }

    static int PktmonPayloadSummary(string[] args)
    {
        PktmonPayloadSummaryReport report;
        try
        {
            report = CreatePktmonPayloadSummaryReport(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine("Pktmon pcapng payload summary:");
        Console.WriteLine("Extractor: bounded Ethernet/IPv4/TCP and raw IPv4/TCP packet bytes from Enhanced Packet/Simple Packet blocks; non-TCP, IPv6, VLAN, fragmented, truncated, or non-IP link-layer payloads are counted only as packet blocks.");
        if (report.RootPath is not null)
        {
            Console.WriteLine($"Root: {report.RootPath}");
        }

        foreach (var item in report.Items)
        {
            var prefix = string.IsNullOrEmpty(item.Kind)
                ? string.Empty
                : $"{item.Kind} port={item.Port} ";
            var payload = item.Payload;
            Console.WriteLine(
                $"{prefix}pcapng={payload.Pcapng.Path} exists={payload.Pcapng.Exists} " +
                $"length={payload.Pcapng.Length} validation={payload.Pcapng.Validation} " +
                $"enhancedPacketBlocks={payload.Pcapng.EnhancedPacketBlocks} " +
                $"simplePacketBlocks={payload.Pcapng.SimplePacketBlocks} " +
                $"totalBlocks={payload.Pcapng.TotalBlocks} " +
                $"tcpPayloadCandidates={payload.Candidates.Count} " +
                $"tcpPayloadLengths={FormatTcpPayloadLengths(payload.Candidates)} " +
                $"tcpPorts={FormatTcpPayloadPorts(payload.Candidates)} " +
                $"watchedPorts={FormatWatchedPorts(item.WatchedPorts)} " +
                $"tcpServicePorts={FormatTcpPayloadServicePorts(payload.Candidates, item.WatchedPorts)} " +
                $"samples={FormatTcpPayloadSamples(payload.Candidates)}");
        }

        Console.WriteLine($"Ready: {report.Ready}");
        return report.Ready ? 0 : 2;
    }

    static PktmonCaptureReadinessOptions ResolvePktmonCaptureReadinessOptions(string[] args)
    {
        string? rootPath = null;
        string? misplacedRootPath = GetDefaultPktmonMisplacedRootPath();

        for (var index = 1; index < args.Length; index++)
        {
            var value = args[index];
            if (string.Equals(value, "--misplaced-root", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("Missing value for --misplaced-root.");
                }

                misplacedRootPath = Path.GetFullPath(args[index + 1]);
                index++;
                continue;
            }

            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown pktmon-capture-readiness option: {value}");
            }

            if (rootPath is not null)
            {
                throw new ArgumentException("Pktmon capture readiness accepts at most one root path.");
            }

            rootPath = value;
        }

        return new PktmonCaptureReadinessOptions(
            Path.GetFullPath(rootPath ?? "."),
            misplacedRootPath);
    }

    static string? GetDefaultPktmonMisplacedRootPath()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32")
            : null;
    }

    static string ResolvePktmonCaptureRunbookRootPath(string[] args)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException("Pktmon capture runbook accepts at most one root path.");
        }

        return Path.GetFullPath(args.Length > 1 ? args[1] : ".");
    }

    static PktmonCaptureReadinessReport CreatePktmonCaptureReadinessReport(
        string rootPath,
        string? misplacedRootPath = null)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        var items = new[]
        {
        CreatePktmonCaptureReadinessItem(fullRootPath, CreatePktmonCaptureSpec("login")),
        CreatePktmonCaptureReadinessItem(fullRootPath, CreatePktmonCaptureSpec("world")),
        CreatePktmonCaptureReadinessItem(fullRootPath, CreatePktmonCaptureSpec("original-flow")),
    };
        var misplacedItems = CreateMisplacedPktmonCaptureItems(fullRootPath, misplacedRootPath);

        return new PktmonCaptureReadinessReport(fullRootPath, items, misplacedItems);
    }

    static IReadOnlyList<PktmonMisplacedCaptureItem> CreateMisplacedPktmonCaptureItems(
        string rootPath,
        string? misplacedRootPath)
    {
        if (string.IsNullOrWhiteSpace(misplacedRootPath))
        {
            return [];
        }

        var fullMisplacedRootPath = Path.GetFullPath(misplacedRootPath);
        if (string.Equals(rootPath, fullMisplacedRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return
        [
            .. new[]
        {
            CreatePktmonCaptureSpec("login"),
            CreatePktmonCaptureSpec("world"),
            CreatePktmonCaptureSpec("original-flow"),
        }
        .Select(spec =>
        {
            var pcapngPath = CreateExpectedPktmonPcapngPath(fullMisplacedRootPath, spec);
            return new PktmonMisplacedCaptureItem(
                spec.Kind,
                spec.Port,
                fullMisplacedRootPath,
                SummarizePcapngFile(pcapngPath));
        })
        .Where(item => item.Pcapng.Exists),
    ];
    }

    static PktmonCaptureReadinessItem CreatePktmonCaptureReadinessItem(string rootPath, PktmonCaptureSpec spec)
    {
        var scriptPath = Path.Combine(
            rootPath,
            ".setup_work",
            "capture-scripts",
            $"{spec.Kind}-pktmon-capture.ps1");
        var pcapngPath = CreateExpectedPktmonPcapngPath(rootPath, spec);
        var timedScriptPath = CreateTimedPktmonScriptPath(scriptPath);

        return new PktmonCaptureReadinessItem(
            spec.Kind,
            spec.Port,
            scriptPath,
            File.Exists(scriptPath),
            timedScriptPath,
            File.Exists(timedScriptPath),
            pcapngPath,
            SummarizePcapngFile(pcapngPath),
            GetTcpPayloadServicePorts(
                SummarizePcapngPayloadFile(pcapngPath).Candidates,
                EnumeratePktmonFilters(spec)
                    .Select(filter => filter.Port)
                    .Distinct()
                    .Order()
                    .ToArray()));
    }

    static PktmonFixtureSummaryReport CreatePktmonFixtureSummaryReport(string[] args)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException("Pktmon fixture summary accepts at most one root path or .pcapng path.");
        }

        var targetPath = Path.GetFullPath(args.Length > 1 ? args[1] : ".");
        if (args.Length > 1 && IsPcapngPath(args[1]))
        {
            return new PktmonFixtureSummaryReport(
                RootPath: null,
                Items:
                [
                    new PktmonFixtureSummaryItem(
                    Kind: string.Empty,
                    Port: null,
                    Pcapng: SummarizePcapngFile(targetPath)),
                ]);
        }

        var rootPath = targetPath;
        return new PktmonFixtureSummaryReport(
            rootPath,
            [
                CreatePktmonFixtureSummaryItem(rootPath, CreatePktmonCaptureSpec("login")),
            CreatePktmonFixtureSummaryItem(rootPath, CreatePktmonCaptureSpec("world")),
            CreatePktmonFixtureSummaryItem(rootPath, CreatePktmonCaptureSpec("original-flow")),
            ]);
    }

    static bool IsPcapngPath(string path)
    {
        return string.Equals(Path.GetExtension(path), ".pcapng", StringComparison.OrdinalIgnoreCase);
    }

    static PktmonFixtureSummaryItem CreatePktmonFixtureSummaryItem(string rootPath, PktmonCaptureSpec spec)
    {
        return new PktmonFixtureSummaryItem(
            spec.Kind,
            spec.Port,
            SummarizePcapngFile(CreateExpectedPktmonPcapngPath(rootPath, spec)));
    }

    static PktmonPayloadSummaryReport CreatePktmonPayloadSummaryReport(string[] args)
    {
        if (args.Length > 2)
        {
            throw new ArgumentException("Pktmon payload summary accepts at most one root path or .pcapng path.");
        }

        var targetPath = Path.GetFullPath(args.Length > 1 ? args[1] : ".");
        if (args.Length > 1 && IsPcapngPath(args[1]))
        {
            return new PktmonPayloadSummaryReport(
                RootPath: null,
                Items:
                [
                    new PktmonPayloadSummaryItem(
                    Kind: string.Empty,
                    Port: null,
                    WatchedPorts: [],
                    Payload: SummarizePcapngPayloadFile(targetPath)),
                ]);
        }

        var rootPath = targetPath;
        return new PktmonPayloadSummaryReport(
            rootPath,
            [
                CreatePktmonPayloadSummaryItem(rootPath, CreatePktmonCaptureSpec("login")),
            CreatePktmonPayloadSummaryItem(rootPath, CreatePktmonCaptureSpec("world")),
            CreatePktmonPayloadSummaryItem(rootPath, CreatePktmonCaptureSpec("original-flow")),
            ]);
    }

    static PktmonPayloadSummaryItem CreatePktmonPayloadSummaryItem(string rootPath, PktmonCaptureSpec spec)
    {
        return new PktmonPayloadSummaryItem(
            spec.Kind,
            spec.Port,
            EnumeratePktmonFilters(spec)
                .Select(filter => filter.Port)
                .Distinct()
                .Order()
                .ToArray(),
            SummarizePcapngPayloadFile(CreateExpectedPktmonPcapngPath(rootPath, spec)));
    }

    static string CreateExpectedPktmonPcapngPath(string rootPath, PktmonCaptureSpec spec)
    {
        return Path.Combine(
            rootPath,
            ".setup_work",
            "captures",
            $"{spec.Kind}-pktmon",
            $"{spec.FileStem}.pcapng");
    }

    static PcapngFileSummary SummarizePcapngFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return new PcapngFileSummary(
                Path: fullPath,
                Exists: false,
                Length: 0,
                Sha256: "<missing>",
                HeaderValid: false,
                Version: "<unknown>",
                ByteOrder: "<unknown>",
                SectionHeaderBlocks: 0,
                InterfaceDescriptionBlocks: 0,
                EnhancedPacketBlocks: 0,
                SimplePacketBlocks: 0,
                TotalBlocks: 0,
                Validation: "missing");
        }

        var bytes = File.ReadAllBytes(fullPath);
        var validation = ValidatePcapngBlocks(bytes);
        return new PcapngFileSummary(
            Path: fullPath,
            Exists: true,
            Length: bytes.LongLength,
            Sha256: Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            HeaderValid: validation.HeaderValid,
            Version: validation.Version,
            ByteOrder: validation.ByteOrder,
            SectionHeaderBlocks: validation.SectionHeaderBlocks,
            InterfaceDescriptionBlocks: validation.InterfaceDescriptionBlocks,
            EnhancedPacketBlocks: validation.EnhancedPacketBlocks,
            SimplePacketBlocks: validation.SimplePacketBlocks,
            TotalBlocks: validation.TotalBlocks,
            Validation: validation.Validation);
    }

    static PcapngPayloadFileSummary SummarizePcapngPayloadFile(string path)
    {
        var pcapng = SummarizePcapngFile(path);
        if (!pcapng.Exists || !pcapng.HeaderValid)
        {
            return new PcapngPayloadFileSummary(pcapng, []);
        }

        var bytes = File.ReadAllBytes(pcapng.Path);
        return new PcapngPayloadFileSummary(
            pcapng,
            ExtractTcpPayloadCandidates(bytes));
    }

    static string FormatTcpPayloadLengths(IReadOnlyList<TcpPayloadCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return "<none>";
        }

        return string.Join(
            ",",
            candidates
                .GroupBy(candidate => candidate.PayloadLength)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}x{group.Count()}"));
    }

    static string FormatTcpPayloadPorts(IReadOnlyList<TcpPayloadCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return "<none>";
        }

        return string.Join(
            ",",
            candidates
                .GroupBy(candidate => $"{candidate.SourcePort}->{candidate.DestinationPort}")
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}x{group.Count()}"));
    }

    static string FormatWatchedPorts(IReadOnlyList<int> watchedPorts)
    {
        return watchedPorts.Count == 0
            ? "<none>"
            : string.Join(",", watchedPorts);
    }

    static string FormatTcpPayloadServicePorts(
        IReadOnlyList<TcpPayloadCandidate> candidates,
        IReadOnlyList<int> watchedPorts)
    {
        var counts = GetTcpPayloadServicePortCounts(candidates, watchedPorts);

        return counts.Count == 0
            ? "<none>"
            : string.Join(",", counts.Select(item => $"{item.Port}x{item.Count}"));
    }

    static IReadOnlyList<int> GetTcpPayloadServicePorts(
        IReadOnlyList<TcpPayloadCandidate> candidates,
        IReadOnlyList<int> watchedPorts)
    {
        return GetTcpPayloadServicePortCounts(candidates, watchedPorts)
            .Select(item => item.Port)
            .ToArray();
    }

    static IReadOnlyList<TcpPayloadServicePortCount> GetTcpPayloadServicePortCounts(
        IReadOnlyList<TcpPayloadCandidate> candidates,
        IReadOnlyList<int> watchedPorts)
    {
        if (candidates.Count == 0 || watchedPorts.Count == 0)
        {
            return [];
        }

        return watchedPorts
            .Select(port => new
            {
                Port = port,
                Count = candidates.Count(candidate => candidate.SourcePort == port || candidate.DestinationPort == port),
            })
            .Where(item => item.Count > 0)
            .OrderBy(item => item.Port)
            .Select(item => new TcpPayloadServicePortCount(item.Port, item.Count))
            .ToArray();
    }

    static string FormatServicePorts(IReadOnlyList<int> servicePorts)
    {
        return servicePorts.Count == 0
            ? "<none>"
            : string.Join(",", servicePorts);
    }

    static string FormatTcpPayloadSamples(IReadOnlyList<TcpPayloadCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return "<none>";
        }

        return string.Join(
            ";",
            candidates.Select(candidate =>
                $"{candidate.BlockKind}#{candidate.PacketBlockIndex}:packetLength={candidate.PacketLength},sourcePort={candidate.SourcePort},destinationPort={candidate.DestinationPort},payloadLength={candidate.PayloadLength},firstHex={candidate.FirstHex},firstBase64={candidate.FirstBase64}"));
    }

    static PcapngBlockValidation ValidatePcapngBlocks(ReadOnlySpan<byte> bytes)
    {
        const int MinimumSectionHeaderBlockLength = 28;
        const int MinimumBlockLength = 12;
        const uint InterfaceDescriptionBlockType = 0x00000001;
        const uint SimplePacketBlockType = 0x00000003;
        const uint EnhancedPacketBlockType = 0x00000006;

        if (bytes.Length < MinimumSectionHeaderBlockLength)
        {
            return new PcapngBlockValidation(
                HeaderValid: false,
                Version: "<unknown>",
                ByteOrder: "<unknown>",
                SectionHeaderBlocks: 0,
                InterfaceDescriptionBlocks: 0,
                EnhancedPacketBlocks: 0,
                SimplePacketBlocks: 0,
                TotalBlocks: 0,
                Validation: "too-short");
        }

        if (!MatchesBytes(bytes[..4], 0x0A, 0x0D, 0x0D, 0x0A))
        {
            return new PcapngBlockValidation(
                HeaderValid: false,
                Version: "<unknown>",
                ByteOrder: "<unknown>",
                SectionHeaderBlocks: 0,
                InterfaceDescriptionBlocks: 0,
                EnhancedPacketBlocks: 0,
                SimplePacketBlocks: 0,
                TotalBlocks: 0,
                Validation: "wrong-section-header-block-magic");
        }

        var version = "<unknown>";
        var byteOrder = "<unknown>";
        bool? currentSectionLittleEndian = null;
        var sectionHeaderBlocks = 0;
        var interfaceDescriptionBlocks = 0;
        var enhancedPacketBlocks = 0;
        var simplePacketBlocks = 0;
        var totalBlocks = 0;
        var offset = 0;

        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < MinimumBlockLength)
            {
                return Invalid("truncated-block-header");
            }

            var isSectionHeader = MatchesBytes(bytes.Slice(offset, 4), 0x0A, 0x0D, 0x0D, 0x0A);
            bool littleEndian;
            uint blockLength;

            if (isSectionHeader)
            {
                if (bytes.Length - offset < MinimumSectionHeaderBlockLength)
                {
                    return Invalid("invalid-section-header-block-length");
                }

                var byteOrderMagic = bytes.Slice(offset + 8, 4);
                var sectionLittleEndian = MatchesBytes(byteOrderMagic, 0x4D, 0x3C, 0x2B, 0x1A);
                var sectionBigEndian = MatchesBytes(byteOrderMagic, 0x1A, 0x2B, 0x3C, 0x4D);
                if (!sectionLittleEndian && !sectionBigEndian)
                {
                    return Invalid("wrong-byte-order-magic");
                }

                littleEndian = sectionLittleEndian;
                var sectionByteOrder = littleEndian ? "little" : "big";
                blockLength = ReadUInt32(bytes.Slice(offset + 4, 4), littleEndian);
                var majorVersion = ReadUInt16(bytes.Slice(offset + 12, 2), littleEndian);
                var minorVersion = ReadUInt16(bytes.Slice(offset + 14, 2), littleEndian);
                var sectionVersion = $"{majorVersion}.{minorVersion}";

                if (blockLength < MinimumSectionHeaderBlockLength || blockLength > bytes.Length - offset)
                {
                    return Invalid("invalid-section-header-block-length", sectionVersion, sectionByteOrder);
                }

                var closingBlockLength = ReadUInt32(bytes.Slice(offset + (int)blockLength - 4, 4), littleEndian);
                if (closingBlockLength != blockLength)
                {
                    return Invalid("mismatched-section-header-block-length", sectionVersion, sectionByteOrder);
                }

                if (majorVersion != 1 || minorVersion != 0)
                {
                    return Invalid("unsupported-pcapng-version", sectionVersion, sectionByteOrder);
                }

                if (sectionHeaderBlocks == 0)
                {
                    version = sectionVersion;
                    byteOrder = sectionByteOrder;
                }

                currentSectionLittleEndian = littleEndian;
                sectionHeaderBlocks++;
            }
            else
            {
                if (currentSectionLittleEndian is null)
                {
                    return Invalid("wrong-section-header-block-magic");
                }

                littleEndian = currentSectionLittleEndian.Value;
                var blockType = ReadUInt32(bytes.Slice(offset, 4), littleEndian);
                blockLength = ReadUInt32(bytes.Slice(offset + 4, 4), littleEndian);
                if (blockLength < MinimumBlockLength || blockLength > bytes.Length - offset)
                {
                    return Invalid("invalid-block-length");
                }

                var closingBlockLength = ReadUInt32(bytes.Slice(offset + (int)blockLength - 4, 4), littleEndian);
                if (closingBlockLength != blockLength)
                {
                    return Invalid("mismatched-block-length");
                }

                if (blockType == InterfaceDescriptionBlockType)
                {
                    interfaceDescriptionBlocks++;
                }
                else if (blockType == EnhancedPacketBlockType)
                {
                    enhancedPacketBlocks++;
                }
                else if (blockType == SimplePacketBlockType)
                {
                    simplePacketBlocks++;
                }
            }

            totalBlocks++;
            offset += (int)blockLength;
        }

        var validation = enhancedPacketBlocks + simplePacketBlocks > 0
            ? "ok"
            : "no-packet-blocks";
        return new PcapngBlockValidation(
            HeaderValid: true,
            version,
            byteOrder,
            sectionHeaderBlocks,
            interfaceDescriptionBlocks,
            enhancedPacketBlocks,
            simplePacketBlocks,
            totalBlocks,
            validation);

        PcapngBlockValidation Invalid(
            string validation,
            string? failureVersion = null,
            string? failureByteOrder = null)
        {
            return new PcapngBlockValidation(
                HeaderValid: false,
                Version: failureVersion ?? version,
                ByteOrder: failureByteOrder ?? byteOrder,
                SectionHeaderBlocks: sectionHeaderBlocks,
                InterfaceDescriptionBlocks: interfaceDescriptionBlocks,
                EnhancedPacketBlocks: enhancedPacketBlocks,
                SimplePacketBlocks: simplePacketBlocks,
                TotalBlocks: totalBlocks,
                Validation: validation);
        }
    }

    static ushort ReadUInt16(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        return littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    static uint ReadUInt32(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        return littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    static bool MatchesBytes(ReadOnlySpan<byte> bytes, byte first, byte second, byte third, byte fourth)
    {
        return bytes.Length == 4 &&
            bytes[0] == first &&
            bytes[1] == second &&
            bytes[2] == third &&
            bytes[3] == fourth;
    }

}
