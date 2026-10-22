using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeSharedPathTests
{
    [Fact]
    public void NoFlagsReturnsTheSameStringIncludingIllegalCharacters()
    {
        var value = new string("name/with\\chars:\0\r\n".ToCharArray());
        Assert.Same(value, value.RemoveIllegalCharacters(path: false, file: false));
    }

    [Fact]
    public void NoFlagsReturnsNullWithoutInventedValidation()
    {
        string value = null!;
        Assert.Null(value.RemoveIllegalCharacters(path: false, file: false));
    }

    [Fact]
    public void FileFlagOnNullKeepsTheNativeInstanceReplaceFault()
    {
        string value = null!;
        Assert.Throws<NullReferenceException>(() =>
            value.RemoveIllegalCharacters(path: false, file: true));
    }

    [Fact]
    public void PathFlagOnNullKeepsTheNativeInstanceReplaceFault()
    {
        string value = null!;
        Assert.Throws<NullReferenceException>(() =>
            value.RemoveIllegalCharacters(path: true, file: false));
    }

    [Fact]
    public void FileFlagReplacesEveryActualRuntimeInvalidFileNameCharacter()
    {
        var chars = Path.GetInvalidFileNameChars();
        Assert.NotEmpty(chars);
        var value = "prefix" + new string(chars) + "suffix";
        Assert.Equal("prefix" + new string('_', chars.Length) + "suffix",
            value.RemoveIllegalCharacters(path: false, file: true));
    }

    [Fact]
    public void PathFlagReplacesEveryActualRuntimeInvalidPathCharacter()
    {
        var chars = Path.GetInvalidPathChars();
        Assert.NotEmpty(chars);
        var value = "prefix" + new string(chars) + "suffix";
        Assert.Equal("prefix" + new string('_', chars.Length) + "suffix",
            value.RemoveIllegalCharacters(path: true, file: false));
    }

    [Fact]
    public void BothFlagsKeepFileThenPathReplacementWithoutDroppingCharacters()
    {
        var fileChars = Path.GetInvalidFileNameChars();
        var pathChars = Path.GetInvalidPathChars();
        var value = "left" + new string(fileChars) + "middle" + new string(pathChars) + "right";
        var expected = "left" + new string('_', fileChars.Length) +
            "middle" + new string('_', pathChars.Length) + "right";
        var actual = value.RemoveIllegalCharacters(path: true, file: true);
        Assert.Equal(expected, actual);
        Assert.Equal(value.Length, actual.Length);
    }

    [Fact]
    public void EmptyInputRemainsEmptyAcrossAllNativeFlagCombinations()
    {
        Assert.Same(string.Empty, string.Empty.RemoveIllegalCharacters(path: false, file: false));
        Assert.Same(string.Empty, string.Empty.RemoveIllegalCharacters(path: false, file: true));
        Assert.Same(string.Empty, string.Empty.RemoveIllegalCharacters(path: true, file: false));
        Assert.Same(string.Empty, string.Empty.RemoveIllegalCharacters(path: true, file: true));
    }

    [Fact]
    public void MatchingNoInvalidCharactersKeepsTheOriginalReference()
    {
        var value = new string("Safe_Name-123.txt".ToCharArray());
        Assert.Same(value, value.RemoveIllegalCharacters(path: false, file: true));
        Assert.Same(value, value.RemoveIllegalCharacters(path: true, file: false));
        Assert.Same(value, value.RemoveIllegalCharacters(path: true, file: true));
    }

    [Fact]
    public void NonAsciiCharactersAreNotNarrowedNormalizedOrEscaped()
    {
        var value = new string(new[] { '\u03A9', '\u4E2D', '\u0100', '\uD800', '\uDC00' });
        var actual = value.RemoveIllegalCharacters(path: true, file: true);
        Assert.Equal(value, actual);
        Assert.Same(value, actual);
    }

    [Fact]
    public void RepeatedInvalidCharactersAreReplacedWithoutMutatingTheInput()
    {
        var invalid = Path.GetInvalidFileNameChars()[0];
        var value = new string(new[] { invalid, 'A', invalid, '_', invalid });
        Assert.Equal("_A___", value.RemoveIllegalCharacters(path: false, file: true));
        Assert.Equal(new[] { invalid, 'A', invalid, '_', invalid }, value.ToCharArray());
    }

    [Fact]
    public void FullStringLengthBeyondUInt16IsRetainedWithoutTruncation()
    {
        var invalid = Path.GetInvalidFileNameChars()[0];
        var value = new string(invalid, 65537);
        var actual = value.RemoveIllegalCharacters(path: false, file: true);
        Assert.Equal(65537, actual.Length);
        Assert.Equal(new string('_', 65537), actual);
        Assert.Equal(new string(invalid, 65537), value);
    }
}
