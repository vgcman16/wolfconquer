using System.Text;

namespace WolfConquer.Core.Client;

public sealed record ClientFullTextPatch(string Text, bool EnsureFinalNewline);

public sealed record ClientPatchFile(
    string RelativePath,
    ClientTextEncodingKind EncodingKind,
    IReadOnlyList<ClientTextReplacement> Replacements,
    IReadOnlyList<string>? CommentPrefixes = null,
    ClientFullTextPatch? FullTextPatch = null,
    Action<string, byte[], Encoding>? ValidateOriginal = null)
{
    public int ExpectedApplied => FullTextPatch is null ? Replacements.Count : 1;
}
