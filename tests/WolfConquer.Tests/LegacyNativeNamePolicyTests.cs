using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeNamePolicyTests
{
    private static bool IsAllowedCodeUnit(char value) =>
        (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z') ||
        (value >= '0' && value <= '9') || (value >= '\u0621' && value <= '\u064A') ||
        (value >= '\u4E00' && value <= '\u9FA5') || "*~.&$".Contains(value);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EntireUtf16DomainUsesExactlyTheExistingRepertoireAtEachPosition(int shape)
    {
        for (int unit = char.MinValue; unit <= char.MaxValue; unit++)
        {
            char value = (char)unit;
            string name = shape switch
            {
                0 => value.ToString(),
                1 => value + "Wf",
                2 => "W" + value + "f",
                _ => "Wf" + value,
            };
            bool expected = IsAllowedCodeUnit(value);
            bool actual = LegacyNativeBaseFunc.NameStrCheck(name);
            Assert.True(actual == expected, $"U+{unit:X4}, shape={shape}: expected {expected}, observed {actual}.");
        }
    }

    [Theory]
    [InlineData("A B")]
    [InlineData("A@B")]
    [InlineData("A\tB")]
    [InlineData("A\rB")]
    [InlineData("A\nB")]
    [InlineData("A\0B")]
    [InlineData("A\u0019B")]
    [InlineData("A\u0301B")]
    [InlineData("A🐺B")]
    [InlineData("AéB")]
    public void InvalidWholeNamesRefuseEvenWhenTheSizeOptionIsDisabled(string name)
    {
        Assert.False(LegacyNativeBaseFunc.NameStrCheck(name));
        Assert.False(LegacyNativeBaseFunc.NameStrCheck(name, ExceptedSize: false));
    }

    [Theory]
    [InlineData("Wolf42")]
    [InlineData("狼王")]
    [InlineData("ءي")]
    [InlineData("العربية5")]
    [InlineData("A*~.&$B")]
    [InlineData("一龥")]
    public void IntendedAsciiArabicCjkAndPunctuationNamesRemainValid(string name)
    {
        Assert.True(LegacyNativeBaseFunc.NameStrCheck(name));
        Assert.True(LegacyNativeBaseFunc.NameStrCheck(name, ExceptedSize: false));
    }

    [Fact]
    public void ExistingReservedTokenManifestStillDeniesWithoutGrantingAnyAuthority()
    {
        string[] tokens = ["GM", "PM", "MrRayzo", "Rayzo", "SYSTEM", "None", "Clicker", "SpeedHack",
            "Aimbot", "CheatEngine", "Hack", "Engine", "Cheat", "clicker", "speed", "GMPM", "Moksha",
            "TQ", "TQGMPM", "TQPM", "TQGM", "guard1", "Guard", "Guard1", "Kosamk", "kosamk"];
        foreach (var token in tokens)
        {
            Assert.False(LegacyNativeBaseFunc.NameStrCheck("W" + token + "f"));
            Assert.False(LegacyNativeBaseFunc.NameStrCheck("W" + token + "f", ExceptedSize: false));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(64)]
    public void Utf16LengthLimitAndSizeOnlyOptOutStayExplicit(int length)
    {
        string name = new('狼', length);
        Assert.Equal(length is > 0 and <= 16, LegacyNativeBaseFunc.NameStrCheck(name));
        Assert.Equal(length > 0, LegacyNativeBaseFunc.NameStrCheck(name, ExceptedSize: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("zh-CN")]
    public void RepertoireDoesNotExpandWithCultureOrUnicodeNormalization(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.True(LegacyNativeBaseFunc.NameStrCheck("Wolf42"));
            Assert.True(LegacyNativeBaseFunc.NameStrCheck("狼王"));
            Assert.True(LegacyNativeBaseFunc.NameStrCheck("ءي"));
            Assert.False(LegacyNativeBaseFunc.NameStrCheck("Wİf"));
            Assert.False(LegacyNativeBaseFunc.NameStrCheck("W١f"));
            Assert.False(LegacyNativeBaseFunc.NameStrCheck("W\u0301f"));
            Assert.False(LegacyNativeBaseFunc.NameStrCheck("W\u0620f"));
            Assert.False(LegacyNativeBaseFunc.NameStrCheck("W\u064Bf"));
            Assert.False(LegacyNativeBaseFunc.NameStrCheck("W\u9FA6f"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
