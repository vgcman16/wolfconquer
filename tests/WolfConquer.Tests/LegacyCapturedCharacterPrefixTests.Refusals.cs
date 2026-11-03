using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class LegacyCapturedCharacterPrefixTests
{
    [Theory]
    [InlineData("RelicSpirit", "bad")]
    [InlineData("RelicSpirit", "1")]
    [InlineData("SpawinEffect", "")]
    [InlineData("SpawinEffectSkills", "False extra")]
    [InlineData("DeityLandLuckyPoints", "4294967296")]
    [InlineData("HeroPoints", "-1")]
    [InlineData("ClassExperience", "not-a-number")]
    [InlineData("SavePromote", "4294967296")]
    [InlineData("SkinUnlock", "256")]
    [InlineData("DoomosaurSkin", "-1")]
    [InlineData("PrayingElfSkin", "256")]
    [InlineData("PolarDragonSkin", "256")]
    [InlineData("ChargeChi", "256")]
    [InlineData("ChargeGuildSkill", "256")]
    [InlineData("TailedBeastActivated", "yes")]
    [InlineData("TailedBeastTotalPoints", "4294967296")]
    [InlineData("TailedBeastFlag", "-1")]
    public void MalformedPresentOptionalScalarsNeverDefault(string key, string value) =>
        Refuses(CharacterBytes(key + "=" + value + "\r\n"), "capturedPrefixOptionalValueInvalid");

    [Theory]
    [InlineData("Body=1003", "Body=65536")]
    [InlineData("Face=0", "Face=-1")]
    [InlineData("Online=1", "Online=4294967296")]
    public void RequiredNativeWidthsRefuseOtherwiseParseableCompleteCore(string oldValue, string newValue) =>
        Refuses(Encoding.UTF8.GetBytes(CompleteText().Replace(oldValue, newValue, StringComparison.Ordinal)),
            "capturedPrefixRequiredValueInvalid");

    [Theory]
    [InlineData("RelicSpirit=\"True\"\r\n")]
    [InlineData("Spouse='Partner'\r\n")]
    [InlineData("Spouse=Part\tner\r\n")]
    [InlineData("HairfaceStorage=0/\0hidden\r\n")]
    public void CapturedImportExplicitlyRefusesQuotedOrEmbeddedControlValues(string fields) =>
        Refuses(CharacterBytes(fields), "capturedPrefixQuotedOrControlValue");

    [Fact]
    public void DuplicateOptionalFieldsCannotChooseOneInterpretation() =>
        Refuses(CharacterBytes("RelicSpirit=True\r\nrelicspirit=True\r\n"), "capturedPrefixCharacterFieldsAmbiguous");

    [Fact]
    public void DuplicateCharacterSectionAlreadyRefusesAtTheGenuineCoreImageBoundary()
    {
        var bytes = CharacterBytes("[Character]\r\nRelicSpirit=True\r\n");
        var before = bytes.ToArray();
        Assert.False(SqlWorldStateImage.TryCreate(CapturedAccount, CapturedCharacter, bytes,
            PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty), out var image, out var failure));
        Assert.Null(image); Assert.Equal(SqlWorldStateFailure.InvalidFunds, failure);
        Assert.Equal(before, bytes);
        // No private constructor bypass can turn the rejected section layout into captured authority.
    }

    [Fact]
    public void NameAndUntrimmedHairfaceBoundsAreCheckedWithoutNativeOrShadowFiles()
    {
        Refuses(Encoding.UTF8.GetBytes(CompleteText().Replace("Name=SqlStateSynthetic",
            "Name=" + new string('n', 255), StringComparison.Ordinal)), "capturedPrefixNameOrSpouseTooLong");
        Refuses(CharacterBytes("Spouse=" + new string('s', 255) + "\r\n"), "capturedPrefixNameOrSpouseTooLong");
        // Full saved bytes count: trimming cannot hide an oversized optional value.
        Refuses(CharacterBytes("HairfaceStorage=" + new string(' ', 256 * 1024) + "0/\r\n"),
            "capturedPrefixHairfaceTooLong");
        var accepted = Capture(CharacterBytes("HairfaceStorage=" + new string(' ', 256 * 1024 - 2) + "0/\r\n"));
        Assert.Equal("0/", accepted.HairfaceLine);
    }
}
