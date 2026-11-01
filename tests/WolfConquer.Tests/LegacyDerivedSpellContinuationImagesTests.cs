using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA controls only. These images do not issue awards, query participation,
// journal permission, admission origins or a positive live recovery fixture.
public sealed class LegacyDerivedSpellContinuationImagesTests
{
    private const long Account = 77;
    private const uint Uid = 1_293_600;
    private const string Marker = "1|00112233445566778899aabbccddeeff|77|1293600|10|90|250|240|100|0|11|1|1|-";
    private const string Character = "[Character]\r\nUID=1293600\r\nName=ContinuationData\r\nBody=1003\r\nFace=1\r\nHaire=10\r\n" +
        "Level=11\r\nExperience=240\r\nClass=1000\r\nFirstClass=0\r\nSecoundeClass=0\r\nReborn=0\r\n" +
        "Map=1002\r\nX=410\r\nY=350\r\nPMap=1002\r\nPMapX=410\r\nPMapY=350\r\n" +
        "Strength=20\r\nAgility=10\r\nVitaliti=30\r\nSpirit=40\r\nAtributes=5\r\nMinHitPoints=50\r\nMinMana=60\r\nMaxMana=100\r\n" +
        "Money=0\r\nConquerPoints=0\r\nBoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=1\r\n" +
        "Other=UP\r\nOpaqueNote=low\r\nExperienceContinuation=" + Marker + "\r\n";

    [Fact]
    public void ActualDerivedDataRecipePreservesPendingAwardAndOpaqueFields()
    {
        var before = Bytes(Character);
        var projection = LegacyDerivedSpellReconciliation.Project(before, null,
            new[] { new LegacyDerivedSpellDesiredRow(20_000, LegacyDerivedSpellOrigins.Granted(20_000, 3)) });
        var after = projection.CopyCharacterAfter();
        Assert.Equal(LegacyExperienceContinuationCodec.Decode(Marker),
            LegacyDerivedSpellContinuationImages.RequirePreserved(Account, Uid, before, after));
        string text = Encoding.UTF8.GetString(after);
        Assert.Contains("Other=UP\r\nOpaqueNote=low\r\n", text, StringComparison.Ordinal);
        Assert.Contains("ExperienceContinuation=" + Marker + "\r\n", text, StringComparison.Ordinal);
        Assert.Equal(Bytes(Character), before);
    }

    [Fact]
    public void DerivedDataRecipeRejectsCaseAmbiguousSavedFieldsWithoutChangingInput()
    {
        // The actual shared INI projector rejects case-folded duplicate saved
        // keys, including unrelated keys. This is a declared invalid image,
        // not a positive claim about an admitted ordinary character.
        var before = Bytes(Character.Replace("OpaqueNote=low", "other=low", StringComparison.Ordinal));
        var saved = before.ToArray();
        var error = Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellReconciliation.Project(before, null,
            new[] { new LegacyDerivedSpellDesiredRow(20_000, LegacyDerivedSpellOrigins.Granted(20_000, 3)) }));
        Assert.Equal("Ambiguous admission field.", error.Message);
        Assert.Equal(saved, before);
    }

    [Theory]
    [InlineData("Class=1000", "Class=1001")]
    [InlineData("Strength=20", "Strength=21")]
    [InlineData("Agility=10", "Agility=11")]
    [InlineData("Vitaliti=30", "Vitaliti=31")]
    [InlineData("Spirit=40", "Spirit=41")]
    [InlineData("Atributes=5", "Atributes=4")]
    [InlineData("MinHitPoints=50", "MinHitPoints=51")]
    [InlineData("MinMana=60", "MinMana=61")]
    [InlineData("MaxMana=100", "MaxMana=101")]
    public void DerivedParticipantCannotChangeProtectedProgressionOrVitals(string before, string after) =>
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellContinuationImages.RequirePreserved(Account, Uid,
            Bytes(Character), Bytes(Character.Replace(before, after, StringComparison.Ordinal))));

    [Fact]
    public void MissingMalformedDuplicateOrForeignMarkerRefuses()
    {
        var before = Bytes(Character);
        string unmarked = Character.Replace("ExperienceContinuation=" + Marker + "\r\n", "", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellContinuationImages.RequirePreserved(Account, Uid, before, Bytes(unmarked)));
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellContinuationImages.RequirePreserved(Account, Uid, Bytes(unmarked), before));
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellContinuationImages.RequirePreserved(Account + 1, Uid, before, before));
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellContinuationImages.RequirePreserved(Account, Uid + 1, before, before));
        var malformed = Bytes(Character.Replace(Marker, "", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellContinuationImages.RequirePreserved(Account, Uid, malformed, malformed));
        var duplicate = Bytes(Character + "experiencecontinuation=" + Marker + "\r\n");
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellContinuationImages.RequirePreserved(Account, Uid, duplicate, duplicate));
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
