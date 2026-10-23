using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeNameCharacterSearchTests
{
    [Theory]
    [InlineData('\\')]
    [InlineData('\'')]
    [InlineData('{')]
    [InlineData('}')]
    [InlineData('[')]
    [InlineData(']')]
    [InlineData('/')]
    public void ForbiddenAsciiCharactersRejectAtEveryPositionAndWithSizeExemption(char forbidden)
    {
        foreach (string name in new[] { forbidden.ToString(), forbidden + "Wolf", "Wo" + forbidden + "lf", "Wolf" + forbidden })
        {
            Assert.False(LegacyNativeBaseFunc.NameStrCheck(name));
            Assert.False(LegacyNativeBaseFunc.NameStrCheck(name, ExceptedSize: false));
        }
    }

    [Theory]
    [InlineData('\\')]
    [InlineData('\'')]
    [InlineData('{')]
    [InlineData('}')]
    [InlineData('[')]
    [InlineData(']')]
    public void PublishedRealClanAdmissionRejectsTheSameForbiddenInteriorCharacter(char forbidden)
    {
        string name = "Wo" + forbidden + "lf";
        Assert.False(LegacyNativeClanRuntime.AllowCreateClan(name));
    }

    [Theory]
    [InlineData("Wolf42")]
    [InlineData("0123456789")]
    [InlineData("一龥")]
    [InlineData("ءي")]
    [InlineData("Wolf~42")]
    [InlineData("Wolf.42")]
    [InlineData("Wolf&42")]
    public void ExistingValidNameSamplesRemainAccepted(string name)
    {
        Assert.True(LegacyNativeBaseFunc.NameStrCheck(name));
        Assert.True(LegacyNativeBaseFunc.NameStrCheck(name, ExceptedSize: false));
    }

    [Theory]
    [InlineData("GM")]
    [InlineData("PM")]
    [InlineData("SYSTEM")]
    [InlineData("None")]
    [InlineData("Guard")]
    [InlineData("TQ")]
    public void MultiCharacterReservedTokenRejectionIsNotWeakenedByTheCharCleanup(string token)
    {
        Assert.False(LegacyNativeBaseFunc.NameStrCheck("A" + token + "B"));
        Assert.False(LegacyNativeBaseFunc.NameStrCheck("A" + token + "B", ExceptedSize: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingNameRefusesWithoutThrowing(string? name) =>
        Assert.False(LegacyNativeBaseFunc.NameStrCheck(name));

    [Fact]
    public void NameLengthLimitAndExplicitSizeExemptionAreUnchanged()
    {
        Assert.True(LegacyNativeBaseFunc.NameStrCheck(new string('A', 16)));
        Assert.False(LegacyNativeBaseFunc.NameStrCheck(new string('A', 17)));
        Assert.True(LegacyNativeBaseFunc.NameStrCheck(new string('A', 17), ExceptedSize: false));
        Assert.False(LegacyNativeBaseFunc.NameStrCheck("Wolf\u0019X"));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void AsciiDelimiterRejectionDoesNotDependOnCurrentCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (char forbidden in "\\'{}[]")
                Assert.False(LegacyNativeBaseFunc.NameStrCheck("Wo" + forbidden + "lf"));
            Assert.True(LegacyNativeBaseFunc.NameStrCheck("Wolf42"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
