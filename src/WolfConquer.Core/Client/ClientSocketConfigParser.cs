using System.Globalization;

namespace WolfConquer.Core.Client;

public sealed record ClientSocketConfigEndpoint(
    string SectionName,
    int ServerIndex,
    string ServerName,
    string EndpointKind,
    string Address,
    int Port,
    string SourcePath,
    int LineNumber);

public static class ClientSocketConfigParser
{
    public static IReadOnlyList<ClientSocketConfigEndpoint> Parse(
        byte[] bytes,
        ClientTextEncodingKind encodingKind,
        string sourcePath = "AutoPatch/SocketConfig.ini")
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var encoding = ClientTextEncodings.GetEncoding(encodingKind);
        var file = ClientStringFile.Parse(bytes, encoding);
        var entries = new List<ClientSocketConfigEndpoint>();
        var currentSection = string.Empty;

        foreach (var line in file.Lines.Select((line, index) => new { line, index }))
        {
            var text = line.line.Text.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            if (TryParseSection(text, out var section))
            {
                currentSection = section;
                continue;
            }

            var separator = text.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = text[..separator].Trim();
            if (!TryParseServerIndex(key, out var serverIndex))
            {
                continue;
            }

            var fields = text[(separator + 1)..]
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 5 ||
                !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var primaryPort) ||
                !int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fallbackPort))
            {
                continue;
            }

            var serverName = fields[0];
            var lineNumber = line.index + 1;
            entries.Add(new ClientSocketConfigEndpoint(
                currentSection,
                serverIndex,
                serverName,
                "primary",
                fields[1],
                primaryPort,
                sourcePath,
                lineNumber));
            entries.Add(new ClientSocketConfigEndpoint(
                currentSection,
                serverIndex,
                serverName,
                "fallback",
                fields[3],
                fallbackPort,
                sourcePath,
                lineNumber));
        }

        return entries;
    }

    private static bool TryParseServerIndex(string key, out int serverIndex)
    {
        const string Prefix = "Server";
        if (key.Length <= Prefix.Length ||
            !key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            serverIndex = 0;
            return false;
        }

        return int.TryParse(
            key[Prefix.Length..],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out serverIndex);
    }

    private static bool TryParseSection(string text, out string section)
    {
        if (text.Length > 2 && text[0] == '[' && text[^1] == ']')
        {
            section = text[1..^1];
            return true;
        }

        section = string.Empty;
        return false;
    }
}
