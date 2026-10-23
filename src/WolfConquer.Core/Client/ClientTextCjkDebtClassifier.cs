namespace WolfConquer.Core.Client;

internal static class ClientTextCjkDebtClassifier
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly string[] InlineCommentMarkers = ["//", "--", ";"];

    public static ClientTextCjkDebtCategory Classify(string relativePath, string line)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(line);

        if (IsSupportDataPath(relativePath))
        {
            return ClientTextCjkDebtCategory.SupportData;
        }

        if (IsPlayerStatePath(relativePath))
        {
            return ClientTextCjkDebtCategory.PlayerState;
        }

        return IsCommentOnlyLine(line) ||
            IsGuiLayoutCommentAssignment(relativePath, line)
            ? ClientTextCjkDebtCategory.Comment
            : ClientTextCjkDebtCategory.VisibleText;
    }

    public static bool IsActionable(ClientTextCjkDebtCategory category)
    {
        return category == ClientTextCjkDebtCategory.VisibleText;
    }

    private static bool IsSupportDataPath(string relativePath)
    {
        return PathComparer.Equals(Path.GetFileName(relativePath), "pinyindata.ini") ||
            PathComparer.Equals(Path.GetFileName(relativePath), "tymgcjy.dat");
    }

    private static bool IsPlayerStatePath(string relativePath)
    {
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (PathComparer.Equals(Path.GetFileName(relativePath), "Associate.txt"))
        {
            return true;
        }

        if (segments.Length == 1 &&
            (PathComparer.Equals(segments[0], "Arena.ini") ||
                PathComparer.Equals(segments[0], "InnerPower.txt")))
        {
            return true;
        }

        if (IsRankStateTextFile(relativePath))
        {
            return true;
        }

        return segments[0] switch
        {
            var segment when PathComparer.Equals(segment, "Users") => true,
            var segment when PathComparer.Equals(segment, "Guilds") => true,
            var segment when PathComparer.Equals(segment, "Unions") => true,
            var segment when PathComparer.Equals(segment, "Associate") => true,
            var segment when PathComparer.Equals(segment, "rank") => true,
            var segment when PathComparer.Equals(segment, "ranks") => true,
            _ => false,
        };
    }

    private static bool IsRankStateTextFile(string relativePath)
    {
        if (!PathComparer.Equals(Path.GetExtension(relativePath), ".txt"))
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        return fileName.Contains("Rank", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("Ranking", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCommentOnlyLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith("--", StringComparison.Ordinal) ||
            trimmed.StartsWith(';') ||
            trimmed.StartsWith('#') ||
            trimmed.StartsWith("/*", StringComparison.Ordinal) ||
            trimmed.StartsWith('*') ||
            IsIniSectionCommentOnlyLine(line) ||
            HasOnlyInlineCommentCjk(line);
    }

    private static bool IsGuiLayoutCommentAssignment(string relativePath, string line)
    {
        if (!IsGuiLayoutPath(relativePath))
        {
            return false;
        }

        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith("comment", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex != "comment".Length)
        {
            return false;
        }

        return ContainsCjk(trimmed.AsSpan(equalsIndex + 1));
    }

    private static bool IsGuiLayoutPath(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return PathComparer.Equals(fileName, "GUI.ini") ||
            PathComparer.Equals(fileName, "GUI800X600.ini");
    }

    private static bool IsIniSectionCommentOnlyLine(string line)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('['))
        {
            return false;
        }

        var closeIndex = trimmed.IndexOf(']');
        if (closeIndex <= 1)
        {
            return false;
        }

        var sectionName = trimmed.AsSpan(1, closeIndex - 1);
        if (ContainsCjk(sectionName) || !IsAsciiSectionName(sectionName))
        {
            return false;
        }

        var rest = trimmed.AsSpan(closeIndex + 1).TrimStart();
        if (!rest.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsCjk(rest[2..]);
    }

    private static bool IsAsciiSectionName(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) &&
                character is not '-' and not '_' and not '.')
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasOnlyInlineCommentCjk(string line)
    {
        foreach (var marker in InlineCommentMarkers)
        {
            var markerIndex = IndexOfInlineCommentMarker(line, marker);
            while (markerIndex >= 0)
            {
                if (!ContainsCjk(line.AsSpan(0, markerIndex)) &&
                    ContainsCjk(line.AsSpan(markerIndex + marker.Length)))
                {
                    return true;
                }

                markerIndex = IndexOfInlineCommentMarker(line, marker, markerIndex + marker.Length);
            }
        }

        return false;
    }

    private static int IndexOfInlineCommentMarker(string line, string marker, int startIndex = 0)
    {
        var inSingleQuote = false;
        var inDoubleQuote = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '\\' && (inSingleQuote || inDoubleQuote))
            {
                index++;
                continue;
            }

            if (!inDoubleQuote && character == '\'')
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (!inSingleQuote && character == '"')
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (index < startIndex ||
                inSingleQuote ||
                inDoubleQuote ||
                index == 0 ||
                !line.AsSpan(index).StartsWith(marker, StringComparison.Ordinal) ||
                !IsInlineCommentBoundary(line, index, marker.Length))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private static bool IsInlineCommentBoundary(string line, int markerIndex, int markerLength)
    {
        var previous = line[markerIndex - 1];
        var nextIndex = markerIndex + markerLength;
        return char.IsWhiteSpace(previous) ||
            nextIndex >= line.Length ||
            char.IsWhiteSpace(line[nextIndex]);
    }

    private static bool ContainsCjk(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (IsCjkCharacter(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCjkCharacter(char value)
    {
        return value is >= '\u3400' and <= '\u9FFF';
    }
}
