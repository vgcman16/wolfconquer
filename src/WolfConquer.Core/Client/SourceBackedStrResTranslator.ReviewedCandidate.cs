using System.Globalization;

namespace WolfConquer.Core.Client;

public static partial class SourceBackedStrResTranslator
{
    /// <summary>Builds explicit human-text edits without changing IDs, formatting or protected data.</summary>
    public static ClientPatchCandidate BuildReviewedFileCandidate(
        ReadOnlySpan<byte> originalBytes,
        IReadOnlyList<ClientTextReplacement> reviewedReplacements)
    {
        ArgumentNullException.ThrowIfNull(reviewedReplacements);
        var count = reviewedReplacements.Count;
        if (count is < 1 or > 4096)
        {
            throw new ArgumentException("Supply 1 to 4096 explicitly reviewed resource edits.", nameof(reviewedReplacements));
        }

        var original = originalBytes.ToArray();
        var originalText = StrResEncoding.GetString(original);
        if (!StrResEncoding.GetBytes(originalText).AsSpan().SequenceEqual(original))
        {
            throw new InvalidDataException("Resource input does not have an exact CP936 roundtrip.");
        }

        var entries = ClientStringFile.Parse(original, StrResEncoding, ";").KeyValues();
        var edits = new ClientTextReplacement[count];
        var selected = new HashSet<uint>();
        for (var index = 0; index < edits.Length; index++)
        {
            var edit = reviewedReplacements[index];
            ArgumentNullException.ThrowIfNull(edit);
            ArgumentNullException.ThrowIfNull(edit.Value);
            if (!uint.TryParse(edit.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
                id.ToString(CultureInfo.InvariantCulture) != edit.Key ||
                !selected.Add(id) || edit.ReplaceLine || edit.Occurrence != 1)
            {
                throw new InvalidDataException("Reviewed edits require unique exact numeric IDs and value-only replacements.");
            }

            var matches = entries.Where(entry => uint.TryParse(entry.Key, NumberStyles.None,
                CultureInfo.InvariantCulture, out var entryId) && entryId == id).ToArray();
            if (matches.Length != 1 || !StringComparer.Ordinal.Equals(matches[0].Key, edit.Key))
            {
                throw new InvalidDataException("Each reviewed resource ID must occur exactly once in the input.");
            }

            ValidateReviewedResourceValue(matches[0].Value, edit.Value);
            edits[index] = new ClientTextReplacement(edit.Key, edit.Value);
        }

        var patch = new ClientPatchFile("ini/StrRes.ini", ClientTextEncodingKind.CodePage936, Array.AsReadOnly(edits), [";"]);
        var candidate = EnglishClientPatch.BuildFileCandidate(patch, original);
        if (candidate.Result.Missing != 0)
        {
            throw new InvalidDataException("A reviewed resource edit was not applied.");
        }

        return candidate;
    }

    private static void ValidateReviewedResourceValue(string original, string replacement)
    {
        if (StringComparer.Ordinal.Equals(original, replacement))
        {
            return;
        }

        if (!NeedsTranslation(original) || string.IsNullOrWhiteSpace(replacement) ||
            ContainsControlCharacter(replacement) || !CanEncodeStrRes(replacement))
        {
            throw new InvalidDataException("Reviewed resource edits require untranslated human text and representable single-line output.");
        }

        ValidateProtectedResourceFragments(original, replacement);
        if (!IsReplacementCompatibleWithTarget(original, replacement) ||
            original.Count(value => value is '%' or '\uFF05') != replacement.Count(value => value is '%' or '\uFF05') ||
            original.Count(value => value == '<') != replacement.Count(value => value == '<') ||
            original.Count(value => value == '>') != replacement.Count(value => value == '>'))
        {
            throw new InvalidDataException("Reviewed resource edit changed formatting tokens or percent markers.");
        }
    }
}
