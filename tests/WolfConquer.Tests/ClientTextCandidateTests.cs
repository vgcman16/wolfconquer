using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientTextCandidateTests
{
    [Theory]
    [InlineData(ClientTextEncodingKind.Utf8)]
    [InlineData(ClientTextEncodingKind.Utf8Bom)]
    [InlineData(ClientTextEncodingKind.CodePage936)]
    [InlineData(ClientTextEncodingKind.Utf16LeBom)]
    public void ExplicitSyntheticCandidateOwnsSourceValidatorAndOutputBuffers(ClientTextEncodingKind kind)
    {
        var encoding = ClientTextEncodings.GetEncoding(kind);
        var original = Encode(encoding, ";keep \u4E2D\r\nName=\u65E7\u503C\nName=other\rTail=keep");
        var preserved = original.ToArray();
        var expected = Encode(encoding, ";keep \u4E2D\r\nName=Synthetic output\nName=other\rTail=keep");
        byte[]? validatorInput = null;
        var patch = new ClientPatchFile("ini/in-memory-only.ini", kind,
            [new("Name", "Synthetic output")], ValidateOriginal: (_, bytes, _) => validatorInput = bytes);

        var candidate = EnglishClientPatch.BuildFileCandidate(patch, original);

        Assert.Equal(new ClientPatchFileResult(patch.RelativePath, 1, 0, true), candidate.Result);
        Assert.Equal(preserved, original);
        Assert.NotNull(validatorInput);
        Assert.NotSame(original, validatorInput);
        Assert.Equal(expected, candidate.ToArray());
        Array.Fill(original, (byte)'X');
        Array.Fill(validatorInput!, (byte)'Y');
        var exported = candidate.ToArray();
        Array.Fill(exported, (byte)'Z');
        Assert.Equal(expected, candidate.ToArray());
        var repeat = EnglishClientPatch.BuildFileCandidate(patch, expected);
        Assert.False(repeat.Result.Changed);
        Assert.Equal(expected, repeat.ToArray());
    }

    [Fact]
    public void ExplicitFullTextCandidateNormalizesLineEndingsAndRejectsConflictingModes()
    {
        var patch = new ClientPatchFile("in-memory-full-text.ini", ClientTextEncodingKind.Utf8Bom, [],
            FullTextPatch: new("First\nSecond\rThird\r\n", EnsureFinalNewline: true));
        byte[] original = [0x6F, 0x6C, 0x64];
        var candidate = EnglishClientPatch.BuildFileCandidate(patch, original);
        var expected = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes("First\r\nSecond\r\nThird\r\n")).ToArray();

        Assert.Equal(new ClientPatchFileResult(patch.RelativePath, 1, 0, true), candidate.Result);
        Assert.Equal(new byte[] { 0x6F, 0x6C, 0x64 }, original);
        Assert.Equal(expected, candidate.ToArray());
        Assert.False(EnglishClientPatch.BuildFileCandidate(patch, expected).Result.Changed);
        Assert.Throws<ArgumentException>(() => EnglishClientPatch.BuildFileCandidate(
            patch with { Replacements = [new("Name", "Synthetic output")] }, original));
        Assert.Throws<ArgumentException>(() => EnglishClientPatch.BuildFileCandidate(
            patch with { CommentPrefixes = [";"] }, original));
        Assert.Equal(new byte[] { 0x6F, 0x6C, 0x64 }, original);
    }

    [Fact]
    public void MissingCatalogOrOperationsCannotProduceASuccessfulEmptyCandidate()
    {
        Assert.Throws<ArgumentNullException>(() => EnglishClientPatch.BuildFileCandidate(null!, []));
        Assert.Throws<ArgumentException>(() => EnglishClientPatch.BuildFileCandidate(
            new("synthetic.ini", ClientTextEncodingKind.Utf8, []), []));
        Assert.Throws<ArgumentException>(() => EnglishClientPatch.BuildFileCandidate(
            new("../outside.ini", ClientTextEncodingKind.Utf8, [new("Name", "Synthetic output")]), []));
    }

    private static byte[] Encode(Encoding encoding, string text) =>
        [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
}
