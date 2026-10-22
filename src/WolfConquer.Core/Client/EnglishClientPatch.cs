using System.Text;

namespace WolfConquer.Core.Client;

/// <summary>Applies a caller-supplied text catalog. No client text is bundled.</summary>
public static partial class EnglishClientPatch
{
    private const int LegacyNpcDisplayNameMaxLength = 28;

    public static ClientPatchResult Apply(string clientRoot, IReadOnlyList<ClientPatchFile> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientRoot);
        ValidateFiles(files, requireReplacements: true);
        // Validate every destination before changing any file.
        foreach (var file in files)
        {
            var target = ResolveTargetPath(clientRoot, file.RelativePath);
            if (!File.Exists(target))
            {
                throw new FileNotFoundException("Catalog target was not found.", target);
            }
        }

        return new ClientPatchResult(files.Select(file => ApplyFile(clientRoot, file)).ToArray());
    }

    public static string ResolveTargetPath(string clientRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientRoot);
        ValidateRelativePath(relativePath);
        var root = Path.GetFullPath(clientRoot);
        var target = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, target);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Catalog paths must stay inside the supplied client root.", nameof(relativePath));
        }

        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("Catalog targets cannot traverse symbolic links or junctions.", nameof(relativePath));
            }
        }

        return target;
    }

    internal static void ValidateRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = relativePath.Replace('\\', '/');
        if (Path.IsPathRooted(relativePath) || normalized.StartsWith('/') || normalized.Contains(':') ||
            normalized.Split('/').Any(part => part is ".." or "." or ""))
        {
            throw new ArgumentException("Catalog paths must be nonempty relative file paths without traversal.", nameof(relativePath));
        }
    }

    internal static void ValidateFiles(IReadOnlyList<ClientPatchFile> files, bool requireReplacements)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            throw new ArgumentException("An explicit, nonempty patch catalog is required.", nameof(files));
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            ArgumentNullException.ThrowIfNull(file);
            ValidateRelativePath(file.RelativePath);
            if (!paths.Add(file.RelativePath.Replace('\\', '/')))
            {
                throw new ArgumentException("Each catalog target must be unique.", nameof(files));
            }
            if (!Enum.IsDefined(file.EncodingKind))
            {
                throw new ArgumentException("The catalog contains an unsupported encoding.", nameof(files));
            }
            ArgumentNullException.ThrowIfNull(file.Replacements);
            foreach (var prefix in file.CommentPrefixes ?? [])
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
            }
            if (file.FullTextPatch is not null &&
                (file.Replacements.Count > 0 || file.CommentPrefixes is { Count: > 0 }))
            {
                throw new ArgumentException("Full-text patches cannot also contain line replacements or comment prefixes.", nameof(files));
            }
            if (requireReplacements && file.ExpectedApplied == 0)
            {
                throw new ArgumentException("Every patch target must supply replacements or full text.", nameof(files));
            }
            if (file.FullTextPatch is not null)
            {
                ArgumentNullException.ThrowIfNull(file.FullTextPatch.Text);
            }
            foreach (var replacement in file.Replacements)
            {
                ArgumentNullException.ThrowIfNull(replacement);
                ArgumentException.ThrowIfNullOrWhiteSpace(replacement.Key);
                ArgumentNullException.ThrowIfNull(replacement.Value);
                ArgumentOutOfRangeException.ThrowIfLessThan(replacement.Occurrence, 1);
            }
        }
    }

    private static ClientPatchFileResult ApplyFile(string clientRoot, ClientPatchFile patchFile)
    {
        var path = ResolveTargetPath(clientRoot, patchFile.RelativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Client patch target not found: {path}", path);
        }

        var candidate = BuildFileCandidate(patchFile, File.ReadAllBytes(path));
        if (candidate.Result.Changed)
        {
            File.WriteAllBytes(path, candidate.ToArray());
        }

        return candidate.Result;
    }

    private static ClientTextReplacement NormalizeReplacementForClientFile(
        ClientPatchFile patchFile,
        ClientTextReplacement replacement)
    {
        if (!replacement.ReplaceLine &&
            IsLegacyNpcDisplayNameReplacement(patchFile.RelativePath, replacement.Key))
        {
            if (string.IsNullOrWhiteSpace(replacement.Value))
            {
                return replacement with { Value = BuildUnnamedLegacyNpcDisplayName(replacement.Key) };
            }

            if (replacement.Value.Length > LegacyNpcDisplayNameMaxLength)
            {
                return replacement with { Value = ShortenLegacyNpcDisplayName(replacement.Value) };
            }
        }

        return replacement;
    }

    private static bool IsLegacyNpcDisplayNameReplacement(string relativePath, string key)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        return (StringComparer.OrdinalIgnoreCase.Equals(normalizedPath, "ini/npc.ini") ||
                StringComparer.OrdinalIgnoreCase.Equals(normalizedPath, "ini/NpcX.ini")) &&
            key.EndsWith(".Name", StringComparison.OrdinalIgnoreCase);
    }

    private static string ShortenLegacyNpcDisplayName(string value)
    {
        if (value.Length <= LegacyNpcDisplayNameMaxLength)
        {
            return value;
        }

        var cutIndex = value.LastIndexOf(' ', LegacyNpcDisplayNameMaxLength - 1);
        var length = cutIndex > 0 ? cutIndex : LegacyNpcDisplayNameMaxLength;
        var shortened = value[..length].TrimEnd(' ', '-', ':', '[', '(', '/');
        return shortened.Length == 0
            ? value[..LegacyNpcDisplayNameMaxLength].TrimEnd()
            : shortened;
    }

    private static string BuildUnnamedLegacyNpcDisplayName(string key)
    {
        var separator = key.IndexOf('.');
        var id = separator > 0 ? key[..separator] : key;
        if (id.StartsWith("NpcType", StringComparison.OrdinalIgnoreCase))
        {
            id = id["NpcType".Length..];
        }

        return string.IsNullOrWhiteSpace(id) ? "Unnamed NPC" : "Unnamed " + id;
    }

    private static byte[] BuildFullTextBytes(ClientFullTextPatch patch, Encoding encoding)
    {
        var normalized = patch.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal)
            .TrimEnd('\r', '\n');

        if (patch.EnsureFinalNewline)
        {
            normalized += "\r\n";
        }

        return encoding.GetBytes(normalized);
    }

    private static byte[] StripPreamble(byte[] bytes, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        if (preamble.Length == 0 || !bytes.AsSpan().StartsWith(preamble))
        {
            return bytes;
        }

        return bytes[preamble.Length..];
    }

    private static byte[] AddPreamble(byte[] bytes, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        if (preamble.Length == 0 || bytes.AsSpan().StartsWith(preamble))
        {
            return bytes;
        }

        var output = new byte[preamble.Length + bytes.Length];
        preamble.CopyTo(output, 0);
        bytes.CopyTo(output, preamble.Length);
        return output;
    }

    private static bool SetReplacement(ClientStringFile textFile, ClientTextReplacement replacement)
    {
        if (replacement.ReplaceLine && IsWholeLineCommentReplacement(replacement.Key))
        {
            return textFile.SetLine(replacement.Key, replacement.Value, replacement.Occurrence);
        }

        var separator = replacement.Key.IndexOf('.');
        if (separator > 0 &&
            separator < replacement.Key.Length - 1 &&
            (!replacement.ReplaceLine || LooksLikeSectionScopedKey(replacement.Key, separator)))
        {
            if (replacement.ReplaceLine)
            {
                if (textFile.SetSectionLine(
                    replacement.Key[..separator],
                    replacement.Key[(separator + 1)..],
                    replacement.Value,
                    replacement.Occurrence))
                {
                    return true;
                }

                return textFile.SetLine(replacement.Key, replacement.Value, replacement.Occurrence);
            }

            return textFile.SetSectionValue(
                replacement.Key[..separator],
                replacement.Key[(separator + 1)..],
                replacement.Value,
                replacement.Occurrence);
        }

        if (replacement.ReplaceLine)
        {
            return textFile.SetLine(replacement.Key, replacement.Value, replacement.Occurrence);
        }

        return textFile.SetValue(replacement.Key, replacement.Value, replacement.Occurrence);
    }

    private static bool IsWholeLineCommentReplacement(string key)
    {
        var trimmed = key.TrimStart();
        return trimmed.StartsWith(';') ||
            trimmed.StartsWith('#') ||
            trimmed.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool LooksLikeSectionScopedKey(string key, int separator)
    {
        for (var index = 0; index < separator; index++)
        {
            var value = key[index];
            if (!char.IsAsciiLetterOrDigit(value) && value != '_')
            {
                return false;
            }
        }

        return true;
    }

}
