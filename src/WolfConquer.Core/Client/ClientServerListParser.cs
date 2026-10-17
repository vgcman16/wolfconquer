using System.Globalization;

namespace WolfConquer.Core.Client;

public static class ClientServerListParser
{
    public static IReadOnlyList<ClientServerListEntry> Parse(
        byte[] bytes,
        ClientTextEncodingKind encodingKind,
        string sourcePath = "server38.dat")
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var encoding = ClientTextEncodings.GetEncoding(encodingKind);
        var file = ClientStringFile.Parse(bytes, encoding);
        var entries = new List<ClientServerListEntry>();
        string? currentSection = null;
        var sectionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in file.Lines)
        {
            var text = line.Text.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            if (IsSection(text, out var section))
            {
                FlushGroup(currentSection, sectionValues, sourcePath, entries);
                currentSection = section;
                sectionValues.Clear();
                continue;
            }

            var separator = text.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            sectionValues[text[..separator].Trim()] = text[(separator + 1)..].Trim();
        }

        FlushGroup(currentSection, sectionValues, sourcePath, entries);
        return entries;
    }

    private static void FlushGroup(
        string? section,
        IReadOnlyDictionary<string, string> values,
        string sourcePath,
        List<ClientServerListEntry> entries)
    {
        if (section is null ||
            !section.StartsWith("Group", StringComparison.OrdinalIgnoreCase) ||
            !values.TryGetValue("ServerAmount", out var serverAmountText) ||
            !int.TryParse(serverAmountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var serverAmount))
        {
            return;
        }

        for (var index = 1; index <= serverAmount; index++)
        {
            if (!values.TryGetValue("Ip" + index.ToString(CultureInfo.InvariantCulture), out var address) ||
                !values.TryGetValue("Port" + index.ToString(CultureInfo.InvariantCulture), out var portText) ||
                !int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            {
                continue;
            }

            var serverNameKey = "ServerName" + index.ToString(CultureInfo.InvariantCulture);
            if (!values.TryGetValue(serverNameKey, out var serverName) || string.IsNullOrWhiteSpace(serverName))
            {
                values.TryGetValue("Server" + index.ToString(CultureInfo.InvariantCulture), out serverName);
            }

            entries.Add(new ClientServerListEntry(
                section,
                index,
                serverName ?? string.Empty,
                address,
                port,
                sourcePath));
        }
    }

    private static bool IsSection(string text, out string section)
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
