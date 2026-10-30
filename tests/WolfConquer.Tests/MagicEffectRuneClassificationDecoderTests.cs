using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class MagicEffectRuneClassificationDecoderTests
{
    [Theory]
    [InlineData("0", 0)] [InlineData("1", 1)] [InlineData("2", 2)] [InlineData("255", 255)]
    public void AUniqueExplicitByteRetainsItsValueWithoutAnUnknownBoolDefault(string text, byte expected)
    {
        var result = MagicEffectRuneClassificationDecoder.Parse("[814000]\r\nName=Caprice\r\nIsRuneSkill=" + text + "\r\n", 814000, "owned-decoder.ini");
        Assert.Equal(MagicEffectRuneClassificationStatus.UniqueExplicit, result.Status); Assert.Equal(expected, result.UniqueValue);
        Assert.Equal(1, result.SectionCount); var declaration = Assert.Single(result.Declarations);
        Assert.Equal(1, declaration.Occurrence); Assert.Equal(3, declaration.SourceLine); Assert.Equal("owned-decoder.ini", declaration.SourceFile);
        Assert.Equal("IsRuneSkill", declaration.Key); Assert.Equal(text, declaration.Value); Assert.Equal("IsRuneSkill=" + text, declaration.RawLine);
        Assert.Equal(expected, declaration.ParsedValue);
    }

    [Theory]
    [InlineData("-1")] [InlineData("256")] [InlineData("true")]
    public void AnExplicitInvalidByteRemainsMalformedAndCannotBecomeFalse(string text)
    {
        var result = MagicEffectRuneClassificationDecoder.Parse("[814000]\r\nIsRuneSkill=" + text + "\r\n", 814000);
        Assert.Equal(MagicEffectRuneClassificationStatus.Malformed, result.Status); Assert.Null(result.UniqueValue);
        Assert.Equal(text, Assert.Single(result.Declarations).Value); Assert.Null(result.Declarations[0].ParsedValue);
    }

    [Fact]
    public void AFieldOrSectionAbsenceIsMissingRatherThanExplicitZero()
    {
        foreach (var text in new[] { "[814000]\r\nName=Vampire\r\n", "[100000]\r\nName=Other\r\n" })
        {
            var result = MagicEffectRuneClassificationDecoder.Parse(text, 814000);
            Assert.Equal(MagicEffectRuneClassificationStatus.Missing, result.Status); Assert.Null(result.UniqueValue); Assert.Empty(result.Declarations);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TheActualMissingVampireAndExplicitCapriceShapeIsAmbiguousInBothOrders(bool reverse)
    {
        const string missing = "[814000]\r\nName=Vampire\r\nTargetType=2\r\n";
        const string explicitRune = "[814000]\r\nName=Caprice\r\nTargetType=8\r\nIsRuneSkill=1\r\n";
        var rows = MagicEffectParser.Parse(reverse ? explicitRune + missing : missing + explicitRune);
        var result = MagicEffectRuneClassificationDecoder.Analyze(rows, 814000);
        Assert.Equal(MagicEffectRuneClassificationStatus.Ambiguous, result.Status); Assert.Null(result.UniqueValue); Assert.Equal(2, result.SectionCount);
        Assert.Equal((byte)1, Assert.Single(result.Declarations).ParsedValue); Assert.Equal(reverse ? 1 : 2, result.Declarations[0].Occurrence);
        Assert.Equal(reverse ? "Caprice" : "Vampire", rows[0].Name); Assert.Equal(reverse ? "Vampire" : "Caprice", rows[1].Name);
        Assert.Equal(new[] { 1, 2 }, rows.Select(row => row.Occurrence));
    }

    [Theory]
    [InlineData("IsRuneSkill=1\r\nIsRuneSkill=1\r\n")]
    [InlineData("IsRuneSkill=1\r\nIsRuneSkill=0\r\n")]
    [InlineData("IsRuneSkill=1\r\nISRUNESKILL=1\r\n")]
    public void RepeatedExplicitKeysCannotBeHiddenByTheParsersLastAttributeProjection(string entries)
    {
        var rows = MagicEffectParser.Parse("[814000]\r\n" + entries);
        var result = MagicEffectRuneClassificationDecoder.Analyze(rows, 814000);
        Assert.Equal(MagicEffectRuneClassificationStatus.Ambiguous, result.Status); Assert.Null(result.UniqueValue);
        Assert.Equal(2, result.Declarations.Count); Assert.Equal(2, rows[0].Entries.Count); Assert.Single(rows[0].Attributes);
    }

    [Fact]
    public void ADetachedAttributeFlagCannotReplaceItsRawDeclaration()
    {
        var row = Assert.Single(MagicEffectParser.Parse("[814000]\r\nIsRuneSkill=1\r\n"));
        var altered = row with { Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["IsRuneSkill"] = "0" } };
        var result = MagicEffectRuneClassificationDecoder.Analyze(new[] { altered }, 814000);
        Assert.Equal(MagicEffectRuneClassificationStatus.Malformed, result.Status); Assert.Null(result.UniqueValue);
        Assert.Equal((byte)1, Assert.Single(result.Declarations).ParsedValue);
    }

    [Fact]
    public void AChangedRawLineCannotAgreeOnlyThroughItsParsedFields()
    {
        var row = Assert.Single(MagicEffectParser.Parse("[814000]\r\nIsRuneSkill=1\r\n"));
        var altered = row with { Entries = row.Entries.Select(entry => entry with { RawLine = "IsRuneSkill=0" }).ToArray() };
        var result = MagicEffectRuneClassificationDecoder.Analyze(new[] { altered }, 814000);
        Assert.Equal(MagicEffectRuneClassificationStatus.Malformed, result.Status); Assert.Null(result.UniqueValue);
        Assert.Null(Assert.Single(result.Declarations).ParsedValue);
    }

    [Fact]
    public void AParsedNoncanonicalSectionKeepsItsDeclarationButCannotBecomeUniqueEvidence()
    {
        var result = MagicEffectRuneClassificationDecoder.Parse("[0814000]\r\nIsRuneSkill=1\r\n", 814000);
        Assert.Equal(MagicEffectRuneClassificationStatus.Malformed, result.Status); Assert.Null(result.UniqueValue);
        Assert.Equal((byte)1, Assert.Single(result.Declarations).ParsedValue);
    }
}
