namespace WolfConquer.Core.Client;

public static partial class EnglishClientPatch
{
    /// <summary>Builds one patch in memory, retaining the existing validation and replacement rules.</summary>
    public static ClientPatchCandidate BuildFileCandidate(ClientPatchFile patchFile, ReadOnlySpan<byte> originalBytes)
    {
        ArgumentNullException.ThrowIfNull(patchFile);
        ValidateFiles([patchFile], requireReplacements: true);
        var original = originalBytes.ToArray();
        var encoding = ClientTextEncodings.GetEncoding(patchFile.EncodingKind);
        patchFile.ValidateOriginal?.Invoke(patchFile.RelativePath, original, encoding);
        if (patchFile.FullTextPatch is not null)
        {
            if (patchFile.Replacements.Count != 0 || patchFile.CommentPrefixes is { Count: > 0 })
            {
                throw new InvalidOperationException($"Full-text client patch cannot combine replacements or comment prefixes: {patchFile.RelativePath}");
            }

            var bytes = AddPreamble(BuildFullTextBytes(patchFile.FullTextPatch, encoding), encoding);
            return new ClientPatchCandidate(bytes, new ClientPatchFileResult(
                patchFile.RelativePath, 1, 0, !original.AsSpan().SequenceEqual(bytes)));
        }

        var originalContent = StripPreamble(original, encoding);
        var textFile = ClientStringFile.Parse(originalContent, encoding, (patchFile.CommentPrefixes ?? []).ToArray());
        var applied = 0;
        var missing = 0;
        var deferredMissing = new List<ClientTextReplacement>();
        foreach (var replacement in patchFile.Replacements)
        {
            var normalized = NormalizeReplacementForClientFile(patchFile, replacement);
            if (SetReplacement(textFile, normalized))
            {
                applied++;
            }
            else
            {
                deferredMissing.Add(normalized);
            }
        }

        foreach (var replacement in deferredMissing)
        {
            if (SetReplacement(textFile, replacement))
            {
                applied++;
            }
            else
            {
                missing++;
            }
        }

        var updated = AddPreamble(textFile.ToBytes(), encoding);
        return new ClientPatchCandidate(updated, new ClientPatchFileResult(
            patchFile.RelativePath, applied, missing, !original.AsSpan().SequenceEqual(updated)));
    }
}
