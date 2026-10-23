using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeSharedPathFrameworkCompatTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void EntireUtf16DomainMatchesIndependentLiteralFrameworkSets(bool path, bool file)
    {
        char[] units = new char[65536];
        for (int i = 0; i < units.Length; i++) units[i] = (char)i;
        string input = new(units);
        string actual = input.RemoveIllegalCharacters(path, file);
        Assert.Equal(65536, actual.Length);
        int firstMismatch = -1, mismatches = 0, changes = 0, inputMutations = 0;
        for (int unit = 0; unit < units.Length; unit++)
        {
            // Independent membership predicates, not modern Path APIs or a copy of production's array.
            bool forbiddenPath = unit is <= 31 or 34 or 60 or 62 or 124;
            bool forbiddenFile = forbiddenPath || unit is 58 or 42 or 63 or 92 or 47;
            char expected = (file && forbiddenFile) || (path && forbiddenPath) ? '_' : (char)unit;
            if (actual[unit] != expected)
            {
                if (firstMismatch < 0) firstMismatch = unit;
                mismatches++;
            }
            if (actual[unit] != (char)unit) changes++;
            if (input[unit] != (char)unit) inputMutations++;
        }
        string diagnostic = firstMismatch < 0 ? "none" :
            "U+" + firstMismatch.ToString("X4") + " actual=U+" + ((int)actual[firstMismatch]).ToString("X4");
        // Never dump the 65,536-unit input/actual strings on assertion failure.
        Assert.True(mismatches == 0, "path=" + path + ", file=" + file + ", mismatches=" + mismatches + ", first=" + diagnostic);
        Assert.Equal(0, inputMutations);
        Assert.Equal(file ? 41 : path ? 36 : 0, changes);
        if (!path && !file) Assert.Same(input, actual);
        else Assert.NotSame(input, actual);
    }

    [Theory]
    [InlineData(34)] [InlineData(60)] [InlineData(62)]
    public void FrameworkQuoteAndAngleCharactersAreReplacedForPathOnly(int value)
    {
        string input = new(new[] { 'a', (char)value, 'b' });
        Assert.Equal("a_b", input.RemoveIllegalCharacters(path: true, file: false));
        Assert.Equal("a_b", input.RemoveIllegalCharacters(path: false, file: true));
        Assert.Equal("a_b", input.RemoveIllegalCharacters(path: true, file: true));
        Assert.Same(input, input.RemoveIllegalCharacters(path: false, file: false));
        Assert.Equal((char)value, input[1]);
    }

    [Theory]
    [InlineData("/")] [InlineData("\\")] [InlineData(":")] [InlineData("*")] [InlineData("?")]
    public void PathOnlyPreservesNativeFileSeparatorsColonAndWildcardsByReference(string legalPathCharacter)
    {
        string input = "left" + legalPathCharacter + "right";
        Assert.Same(input, input.RemoveIllegalCharacters(path: true, file: false));
        Assert.Equal("left_right", input.RemoveIllegalCharacters(path: false, file: true));
        Assert.Equal("left_right", input.RemoveIllegalCharacters(path: true, file: true));
    }

    [Fact]
    public void BothFlagsOnNullKeepTheOriginalInstanceReplaceFaultWithoutValidation()
    {
        string input = null!;
        Assert.Throws<NullReferenceException>(() => input.RemoveIllegalCharacters(path: true, file: true));
        Assert.Null(input.RemoveIllegalCharacters(path: false, file: false));
    }

    [Theory]
    [InlineData("<UpdateScore>b__0", "_UpdateScore_b__0", "_UpdateScore_b__0")]
    [InlineData("<>c.<LoginHandler>b__12_0", "__c._LoginHandler_b__12_0", "__c._LoginHandler_b__12_0")]
    [InlineData("quote\"name|tail", "quote_name_tail", "quote_name_tail")]
    [InlineData("normal/file\\path:?*", "normal/file\\path:?*", "normal_file_path___")]
    public void WholeMethodKeepsFrameworkLambdaStyleNamesAndEachFlagCombination(
        string input, string expectedPath, string expectedFile)
    {
        Assert.Same(input, input.RemoveIllegalCharacters(path: false, file: false));
        Assert.Equal(expectedPath, input.RemoveIllegalCharacters(path: true, file: false));
        Assert.Equal(expectedFile, input.RemoveIllegalCharacters(path: false, file: true));
        Assert.Equal(expectedFile, input.RemoveIllegalCharacters(path: true, file: true));
    }
}
