using System.Globalization;

internal static partial class Program
{
    private const string ClientAssetPresenceAuditUsage =
        "Usage: WolfConquer.DataTool client-asset-presence-audit <clientRoot> <relativeAssetPath> [relativeAssetPath ...] [--reference label=path] [...] [--require-client]";

    static int ClientAssetPresenceAudit(string[] args)
    {
        if (!TryParseClientAssetPresenceAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(ClientAssetPresenceAuditUsage);
            return 1;
        }

        ClientAssetPresenceAuditReport report;
        try
        {
            report = ClientAssetPresenceAuditor.Audit(options);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not audit client asset presence: {ex.Message}");
            Console.Error.WriteLine(ClientAssetPresenceAuditUsage);
            return 1;
        }

        PrintClientAssetPresenceAuditReport(report);
        return options.RequireClient && report.MissingClientAssetCount > 0 ? 2 : 0;
    }

    private static bool TryParseClientAssetPresenceAuditOptions(
        string[] args,
        out ClientAssetPresenceAuditOptions options,
        out string error)
    {
        options = new ClientAssetPresenceAuditOptions("", [], [], false);
        error = "";

        if (args.Length < 3)
        {
            error = "Missing client root or relative asset path.";
            return false;
        }

        string clientRoot;
        try
        {
            clientRoot = Path.GetFullPath(args[1]);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Invalid client root path: {ex.Message}";
            return false;
        }

        if (!Directory.Exists(clientRoot))
        {
            error = $"Client root not found: {clientRoot}";
            return false;
        }

        var relativeAssetPaths = new List<string>();
        var referenceRoots = new List<ClientAssetPresenceReferenceRoot>();
        var requireClient = false;

        for (var index = 2; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--require-client", StringComparison.OrdinalIgnoreCase))
            {
                requireClient = true;
                continue;
            }

            if (string.Equals(argument, "--reference", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    error = "Missing value for --reference.";
                    return false;
                }

                if (!TryParseClientAssetPresenceReferenceRoot(args[++index], out var referenceRoot, out error))
                {
                    return false;
                }

                referenceRoots.Add(referenceRoot);
                continue;
            }

            const string referencePrefix = "--reference=";
            if (argument.StartsWith(referencePrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseClientAssetPresenceReferenceRoot(argument[referencePrefix.Length..], out var referenceRoot, out error))
                {
                    return false;
                }

                referenceRoots.Add(referenceRoot);
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown client-asset-presence-audit option: {argument}";
                return false;
            }

            if (!TryNormalizeClientAssetPresenceRelativePath(argument, out var relativeAssetPath, out error))
            {
                return false;
            }

            relativeAssetPaths.Add(relativeAssetPath);
        }

        if (relativeAssetPaths.Count == 0)
        {
            error = "Missing relative asset path.";
            return false;
        }

        options = new ClientAssetPresenceAuditOptions(
            clientRoot,
            relativeAssetPaths,
            referenceRoots,
            requireClient);
        return true;
    }

    private static bool TryParseClientAssetPresenceReferenceRoot(
        string value,
        out ClientAssetPresenceReferenceRoot referenceRoot,
        out string error)
    {
        referenceRoot = new ClientAssetPresenceReferenceRoot("", "");
        error = "";

        var separator = value.IndexOf('=', StringComparison.Ordinal);
        if (separator < 0)
        {
            error = "--reference requires label=path.";
            return false;
        }

        var label = value[..separator].Trim();
        if (label.Length == 0)
        {
            error = "--reference label must be non-empty.";
            return false;
        }

        var path = value[(separator + 1)..].Trim();
        if (path.Length == 0)
        {
            error = "--reference path must be non-empty.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Invalid --reference path for '{label}': {ex.Message}";
            return false;
        }

        if (!Directory.Exists(fullPath))
        {
            error = $"Reference path not found for '{label}': {fullPath}";
            return false;
        }

        referenceRoot = new ClientAssetPresenceReferenceRoot(label, fullPath);
        return true;
    }

    private static bool TryNormalizeClientAssetPresenceRelativePath(
        string value,
        out string relativeAssetPath,
        out string error)
    {
        relativeAssetPath = "";
        error = "";

        var normalized = value.Trim().Trim('"', '\'').Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (normalized.Length == 0)
        {
            error = "Asset path must be non-empty.";
            return false;
        }

        if (IsRootedClientAssetPresencePath(normalized))
        {
            error = $"Asset path must be relative: {value}";
            return false;
        }

        var parts = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(static part => !string.Equals(part, ".", StringComparison.Ordinal))
            .ToArray();
        if (parts.Length == 0)
        {
            error = "Asset path must be non-empty.";
            return false;
        }

        if (parts.Any(static part => string.Equals(part, "..", StringComparison.Ordinal)))
        {
            error = $"Asset path cannot contain '..': {value}";
            return false;
        }

        relativeAssetPath = string.Join('/', parts);
        return true;
    }

    private static bool IsRootedClientAssetPresencePath(string value)
    {
        return value.StartsWith('/') ||
            value.StartsWith('\\') ||
            (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':') ||
            Path.IsPathRooted(value.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void PrintClientAssetPresenceAuditReport(ClientAssetPresenceAuditReport report)
    {
        Console.WriteLine("Client asset presence audit:");
        Console.WriteLine($"Client root: {report.ClientRoot}");
        Console.WriteLine($"References: {FormatClientAssetPresenceReferences(report.ReferenceRoots)}");
        Console.WriteLine($"Require client: {report.RequireClient}");
        Console.WriteLine($"Assets: {report.Assets.Count.ToString(CultureInfo.InvariantCulture)}");

        foreach (var asset in report.Assets)
        {
            Console.WriteLine($"asset {asset.RelativePath}");
            Console.WriteLine($"  client {FormatClientAssetPresenceStatus(asset.Client)}");
            foreach (var reference in asset.References)
            {
                Console.WriteLine($"  reference {reference.Label} {FormatClientAssetPresenceStatus(reference)}");
            }
        }

        Console.WriteLine($"Missing client assets: {report.MissingClientAssetCount.ToString(CultureInfo.InvariantCulture)}");
    }

    private static string FormatClientAssetPresenceReferences(IReadOnlyList<ClientAssetPresenceReferenceRoot> referenceRoots)
    {
        return referenceRoots.Count == 0
            ? "<none>"
            : string.Join(", ", referenceRoots.Select(static root => $"{root.Label}={root.RootPath}"));
    }

    private static string FormatClientAssetPresenceStatus(ClientAssetPresenceFileStatus status)
    {
        return status.Exists
            ? $"exists length={status.Length.ToString(CultureInfo.InvariantCulture)}"
            : "missing";
    }
}

internal sealed record ClientAssetPresenceAuditOptions(
    string ClientRoot,
    IReadOnlyList<string> RelativeAssetPaths,
    IReadOnlyList<ClientAssetPresenceReferenceRoot> ReferenceRoots,
    bool RequireClient);

internal sealed record ClientAssetPresenceReferenceRoot(
    string Label,
    string RootPath);

internal sealed record ClientAssetPresenceAuditReport(
    string ClientRoot,
    IReadOnlyList<ClientAssetPresenceReferenceRoot> ReferenceRoots,
    bool RequireClient,
    IReadOnlyList<ClientAssetPresenceAssetReport> Assets)
{
    public int MissingClientAssetCount { get; } = Assets.Count(static asset => !asset.Client.Exists);
}

internal sealed record ClientAssetPresenceAssetReport(
    string RelativePath,
    ClientAssetPresenceFileStatus Client,
    IReadOnlyList<ClientAssetPresenceFileStatus> References);

internal sealed record ClientAssetPresenceFileStatus(
    string Label,
    bool Exists,
    long Length);

internal static class ClientAssetPresenceAuditor
{
    public static ClientAssetPresenceAuditReport Audit(ClientAssetPresenceAuditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var assets = options.RelativeAssetPaths
            .Select(relativeAssetPath => new ClientAssetPresenceAssetReport(
                relativeAssetPath,
                GetFileStatus("client", options.ClientRoot, relativeAssetPath),
                options.ReferenceRoots
                    .Select(referenceRoot => GetFileStatus(referenceRoot.Label, referenceRoot.RootPath, relativeAssetPath))
                    .ToArray()))
            .ToArray();

        return new ClientAssetPresenceAuditReport(
            options.ClientRoot,
            options.ReferenceRoots,
            options.RequireClient,
            assets);
    }

    private static ClientAssetPresenceFileStatus GetFileStatus(
        string label,
        string rootPath,
        string relativeAssetPath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            rootPath,
            relativeAssetPath.Replace('/', Path.DirectorySeparatorChar)));
        var rootFullPath = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!fullPath.StartsWith(rootFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Asset path escapes root: {relativeAssetPath}");
        }

        var fileInfo = new FileInfo(fullPath);
        return fileInfo.Exists
            ? new ClientAssetPresenceFileStatus(label, true, fileInfo.Length)
            : new ClientAssetPresenceFileStatus(label, false, 0);
    }
}
