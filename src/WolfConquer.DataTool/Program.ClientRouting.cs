using System.Text.Json;
using System.Text;
using WolfConquer.Core.Client;
using WolfConquer.Core.Data;

internal static partial class Program
{
    static string ResolveClientPath(string[] args)
    {
        return args.Length > 1
            ? Path.GetFullPath(args[1])
            : Path.GetFullPath("client");
    }

    static string ResolveRoutingDbServerPath(string[] args)
    {
        return args.Length > 2
            ? Path.GetFullPath(args[2])
            : Path.GetFullPath("data/DBServer");
    }

    static string ResolveClientRoutingSnapshotOutputPath(string[] args)
    {
        return args.Length > 2
            ? Path.GetFullPath(args[2])
            : Path.GetFullPath(Path.Combine(
                ".setup_work",
                "client-routing-snapshots",
                $"client-routing-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}",
                "manifest.txt"));
    }

    static int InspectClientRouting(string[] args)
    {
        ClientRoutingInspectOptions options;
        try
        {
            options = ResolveClientRoutingInspectOptions(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(
                "Usage: WolfConquer.DataTool inspect-client-routing [clientPath] [dbServerPath] [--local-address address]");
            return 1;
        }

        var clientPath = options.ClientPath;
        var dbServerPath = options.DbServerPath;

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        if (!Directory.Exists(dbServerPath))
        {
            Console.Error.WriteLine($"DBServer path not found: {dbServerPath}");
            return 1;
        }

        var serverListPath = Path.Combine(clientPath, "server38.dat");
        if (!File.Exists(serverListPath))
        {
            Console.Error.WriteLine($"Client server list not found: {serverListPath}");
            return 1;
        }

        var clientConfigPath = Path.Combine(dbServerPath, "client_config.ini");
        if (!File.Exists(clientConfigPath))
        {
            Console.Error.WriteLine($"DBServer client config not found: {clientConfigPath}");
            return 1;
        }

        IReadOnlyList<ClientServerListEntry> serverListEntries;
        IReadOnlyList<ClientConfigRecord> clientConfigRecords;
        IReadOnlyList<ClientSocketConfigEndpoint> socketConfigEndpoints;
        try
        {
            serverListEntries = ClientServerListParser.Parse(
                File.ReadAllBytes(serverListPath),
                ClientTextEncodingKind.CodePage936,
                Path.GetFileName(serverListPath));
            clientConfigRecords = ClientConfigParser.ParseFile(clientConfigPath);
            socketConfigEndpoints = ReadClientSocketConfigEndpoints(clientPath);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Could not inspect client routing: {ex.Message}");
            return 1;
        }

        var plan = ClientRoutingPlanner.Create(
            serverListEntries,
            clientConfigRecords,
            socketConfigEndpoints,
            options.LocalAddress);

        Console.WriteLine("Client routing inventory:");
        Console.WriteLine($"Client: {clientPath}");
        Console.WriteLine($"DBServer: {dbServerPath}");
        Console.WriteLine($"Local address: {options.LocalAddress}");
        Console.WriteLine("current -> local");

        foreach (var step in plan.Steps)
        {
            var local = step.LocalPort is null
                ? $"{step.LocalAddress}:deferred"
                : $"{step.LocalAddress}:{step.LocalPort.Value}";
            Console.WriteLine(
                $"{step.Order}. {step.Status.ToLowerInvariant()} {step.Role} [{step.Source}] " +
                $"{step.CurrentAddress}:{step.CurrentPort} -> {local}");
            Console.WriteLine($"   action: {step.Action}");
            Console.WriteLine($"   verify: {step.Verification}");
        }

        return 0;
    }

    static ClientRoutingInspectOptions ResolveClientRoutingInspectOptions(string[] args)
    {
        var positionals = new List<string>();
        var localAddress = "127.0.0.1";

        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--local-address", StringComparison.OrdinalIgnoreCase))
            {
                if (index == args.Length - 1)
                {
                    throw new ArgumentException("Missing value for --local-address.");
                }

                localAddress = args[++index];
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown inspect-client-routing option: {arg}");
            }

            positionals.Add(arg);
        }

        if (positionals.Count > 2)
        {
            throw new ArgumentException("Too many positional arguments for inspect-client-routing.");
        }

        if (string.IsNullOrWhiteSpace(localAddress))
        {
            throw new ArgumentException("--local-address cannot be empty.");
        }

        return new ClientRoutingInspectOptions(
            positionals.Count >= 1
                ? Path.GetFullPath(positionals[0])
                : Path.GetFullPath("client"),
            positionals.Count >= 2
                ? Path.GetFullPath(positionals[1])
                : Path.GetFullPath("data/DBServer"),
            localAddress);
    }

    static IReadOnlyList<ClientSocketConfigEndpoint> ReadClientSocketConfigEndpoints(string clientPath)
    {
        var socketConfigPath = Path.Combine(clientPath, "AutoPatch", "SocketConfig.ini");
        if (!File.Exists(socketConfigPath))
        {
            return [];
        }

        return ClientSocketConfigParser.Parse(
            File.ReadAllBytes(socketConfigPath),
            ClientTextEncodingKind.CodePage936,
            NormalizeClientRelativePath(Path.GetRelativePath(clientPath, socketConfigPath)));
    }

    static int SnapshotClientRouting(string[] args)
    {
        var clientPath = ResolveClientPath(args);
        var outputPath = ResolveClientRoutingSnapshotOutputPath(args);

        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }

        var manifest = ClientRoutingSnapshotter.Create(clientPath, DateTimeOffset.UtcNow);
        WriteClientRoutingSnapshotManifest(outputPath, manifest);

        Console.WriteLine("Client routing snapshot:");
        Console.WriteLine($"Client: {manifest.ClientRoot}");
        Console.WriteLine($"Output: {outputPath}");

        foreach (var file in manifest.Files)
        {
            Console.WriteLine(FormatClientRoutingSnapshotFile(file));
        }

        return 0;
    }

    static int ClientStructuralDrift(string[] args)
    {
        if (args.Length != 4)
        {
            Console.Error.WriteLine("Usage: WolfConquer.DataTool client-structural-drift <originalClientPath> <patchedClientPath> <catalogJsonPath>");
            return 1;
        }

        var originalClientPath = Path.GetFullPath(args[1]);
        var patchedClientPath = Path.GetFullPath(args[2]);

        if (!Directory.Exists(originalClientPath))
        {
            Console.Error.WriteLine($"Original client path not found: {originalClientPath}");
            return 1;
        }

        if (!Directory.Exists(patchedClientPath))
        {
            Console.Error.WriteLine($"Patched client path not found: {patchedClientPath}");
            return 1;
        }

        IReadOnlyList<ClientPatchFile> catalog;
        try
        {
            catalog = ClientPatchCatalog.Load(args[3]);
            foreach (var file in catalog)
            {
                EnglishClientPatch.ResolveTargetPath(originalClientPath, file.RelativePath);
                EnglishClientPatch.ResolveTargetPath(patchedClientPath, file.RelativePath);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Invalid comparison catalog: {ex.Message}");
            return 1;
        }

        var comparedFiles = 0;
        var reports = new List<EnglishClientStructuralDriftReport>();
        var missingFiles = new List<(string RelativePath, string Side, string FullPath)>();

        foreach (var patchFile in catalog)
        {
            var originalPath = EnglishClientPatch.ResolveTargetPath(originalClientPath, patchFile.RelativePath);
            var patchedPath = EnglishClientPatch.ResolveTargetPath(patchedClientPath, patchFile.RelativePath);
            var originalExists = File.Exists(originalPath);
            var patchedExists = File.Exists(patchedPath);

            if (!originalExists && !patchedExists)
            {
                continue;
            }

            var relativePath = NormalizeClientRelativePath(patchFile.RelativePath);
            if (!originalExists || !patchedExists)
            {
                missingFiles.Add(originalExists
                    ? (relativePath, "patched", patchedPath)
                    : (relativePath, "original", originalPath));
                continue;
            }

            var encoding = ClientTextEncodings.GetEncoding(patchFile.EncodingKind);
            var options = new EnglishClientStructuralDriftOptions
            {
                IsIniLike = IsClientStructuralIniLikePath(patchFile.RelativePath),
                ExpectInPlaceLineReplacement = patchFile.FullTextPatch is null,
                TextEncoding = encoding,
            };

            EnglishClientStructuralDriftReport report;
            try
            {
                report = EnglishClientStructuralDriftGuard.CompareBytes(
                    relativePath,
                    File.ReadAllBytes(originalPath),
                    File.ReadAllBytes(patchedPath),
                    encoding,
                    options);
            }
            catch (Exception ex) when (ex is ArgumentException or DecoderFallbackException or EncoderFallbackException)
            {
                Console.Error.WriteLine($"Could not compare {relativePath}: {ex.Message}");
                return 1;
            }

            comparedFiles++;
            if (report.HasHighRiskIssues)
            {
                reports.Add(report);
            }
        }

        if (comparedFiles == 0 && missingFiles.Count == 0)
        {
            Console.Error.WriteLine("No supplied catalog files were found under either client path.");
            return 1;
        }

        var issueCount = reports.Sum(report => report.Issues.Count) + missingFiles.Count;

        Console.WriteLine("Client structural drift:");
        Console.WriteLine($"Original: {originalClientPath}");
        Console.WriteLine($"Patched: {patchedClientPath}");
        Console.WriteLine($"Compared files: {comparedFiles}");
        Console.WriteLine($"High-risk issues: {issueCount}");

        foreach (var report in reports)
        {
            Console.WriteLine(report.RelativePath);
            foreach (var issue in report.Issues)
            {
                var location = issue.LineNumber is { } lineNumber
                    ? $"line {lineNumber} "
                    : string.Empty;
                Console.WriteLine($"  {location}{issue.Kind}: {issue.Message}");
            }
        }

        foreach (var missingFile in missingFiles)
        {
            Console.WriteLine(missingFile.RelativePath);
            Console.WriteLine($"  MissingFile: {missingFile.Side} file not found: {missingFile.FullPath}");
        }

        if (issueCount == 0)
        {
            Console.WriteLine("No high-risk structural drift found.");
            return 0;
        }

        return 2;
    }

    static int ClientFileDrift(string[] args)
    {
        if (args.Length is < 3 or > 6)
        {
            Console.Error.WriteLine("Usage: WolfConquer.DataTool client-file-drift <originalClientPath> <patchedClientPath> [--include-logs] [--patch-files-only <catalogJsonPath>]");
            return 1;
        }

        var includeLogs = false;
        var patchFilesOnly = false;
        string? catalogPath = null;
        for (var index = 3; index < args.Length; index++)
        {
            if (string.Equals(args[index], "--include-logs", StringComparison.OrdinalIgnoreCase))
            {
                includeLogs = true;
                continue;
            }

            if (string.Equals(args[index], "--patch-files-only", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine("--patch-files-only requires an explicit catalog JSON path.");
                    return 1;
                }
                patchFilesOnly = true;
                catalogPath = args[++index];
                continue;
            }

            Console.Error.WriteLine($"Unknown option: {args[index]}");
            Console.Error.WriteLine("Usage: WolfConquer.DataTool client-file-drift <originalClientPath> <patchedClientPath> [--include-logs] [--patch-files-only <catalogJsonPath>]");
            return 1;
        }

        var originalClientPath = Path.GetFullPath(args[1]);
        var patchedClientPath = Path.GetFullPath(args[2]);

        if (!Directory.Exists(originalClientPath))
        {
            Console.Error.WriteLine($"Original client path not found: {originalClientPath}");
            return 1;
        }

        if (!Directory.Exists(patchedClientPath))
        {
            Console.Error.WriteLine($"Patched client path not found: {patchedClientPath}");
            return 1;
        }

        ClientFileDriftReport report;
        try
        {
            var catalog = patchFilesOnly ? ClientPatchCatalog.Load(catalogPath!) : null;
            if (catalog is not null)
            {
                foreach (var file in catalog)
                {
                    EnglishClientPatch.ResolveTargetPath(originalClientPath, file.RelativePath);
                    EnglishClientPatch.ResolveTargetPath(patchedClientPath, file.RelativePath);
                }
                if (!catalog.Any(file =>
                    File.Exists(EnglishClientPatch.ResolveTargetPath(originalClientPath, file.RelativePath)) ||
                    File.Exists(EnglishClientPatch.ResolveTargetPath(patchedClientPath, file.RelativePath))))
                {
                    throw new InvalidDataException("No supplied catalog files were found under either client path.");
                }
            }
            report = ClientFileDriftReporter.Compare(
                originalClientPath,
                patchedClientPath,
                new ClientFileDriftOptions
                {
                    ExcludeLogAndDebugFiles = !includeLogs,
                    IncludeRelativePaths = patchFilesOnly
                        ? catalog!.Select(file => file.RelativePath).ToArray()
                        : null,
                });
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Could not compare client files: {ex.Message}");
            return 1;
        }

        Console.WriteLine("Client file drift:");
        Console.WriteLine($"Original: {report.OriginalClientRoot}");
        Console.WriteLine($"Patched: {report.PatchedClientRoot}");
        Console.WriteLine($"Scope: {(patchFilesOnly ? "Supplied catalog files only" : "All client files")}");
        Console.WriteLine($"Include logs/debug/temp: {includeLogs}");
        Console.WriteLine($"Changed files: {report.ChangedFiles.Count}");
        Console.WriteLine($"Missing files: {report.MissingFiles.Count}");
        Console.WriteLine($"Extra files: {report.ExtraFiles.Count}");

        foreach (var changedFile in report.ChangedFiles)
        {
            Console.WriteLine(
                $"changed {changedFile.RelativePath} " +
                $"original {FormatClientFileDriftFile(changedFile.OriginalFile)} " +
                $"patched {FormatClientFileDriftFile(changedFile.PatchedFile)}");
        }

        foreach (var missingFile in report.MissingFiles)
        {
            Console.WriteLine($"missing {missingFile.RelativePath} {FormatClientFileDriftFile(missingFile)}");
        }

        foreach (var extraFile in report.ExtraFiles)
        {
            Console.WriteLine($"extra {extraFile.RelativePath} {FormatClientFileDriftFile(extraFile)}");
        }

        if (!report.HasDrift)
        {
            Console.WriteLine("No client file drift found.");
            return 0;
        }

        return 2;
    }

    static string FormatClientFileDriftFile(ClientFileDriftFile file)
    {
        return $"length={file.Length} sha256={CompactSha256(file.Sha256)}";
    }

    static string CompactSha256(string sha256)
    {
        return sha256.Length <= 12 ? sha256 : sha256[..12];
    }

    static bool IsClientStructuralIniLikePath(string relativePath)
    {
        return relativePath.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(relativePath), "server38.dat", StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeClientRelativePath(string relativePath)
    {
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    static void WriteClientRoutingSnapshotManifest(string outputPath, ClientRoutingSnapshotManifest manifest)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("Client routing snapshot:");
        builder.Append("CapturedAtUtc: ");
        builder.AppendLine(manifest.CapturedAtUtc.ToString("O"));
        builder.Append("Client: ");
        builder.AppendLine(manifest.ClientRoot);

        foreach (var file in manifest.Files)
        {
            builder.AppendLine(FormatClientRoutingSnapshotFile(file));
        }

        File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    static string FormatClientRoutingSnapshotFile(ClientRoutingSnapshotFile file)
    {
        return $"{file.RelativePath} length={file.Length} sha256={file.Sha256} md5={file.Md5}";
    }

    private sealed record ClientRoutingInspectOptions(
        string ClientPath,
        string DbServerPath,
        string LocalAddress);
}
