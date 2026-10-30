using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed class LegacyRune8140CatalogIngestionTests
{
    [Fact]
    public void ActualCatalogIngestionWarnsWithoutSelectingTheExplicitDuplicateOrChangingItsRawRows()
    {
        var rows = MagicEffectParser.Parse("[814000]\r\nName=Vampire\r\nTargetType=2\r\n" +
            "[814000]\r\nName=Caprice\r\nTargetType=8\r\nIsRuneSkill=1\r\n", "declared-legacy-8140.ini");
        var original = rows.ToArray(); var world = Catalog(rows);
        var warning = Assert.Single(world.Diagnostics, value => value.Code == MagicEffectRuneClassificationDecoder.WarningCode);
        Assert.StartsWith("WARNING:", warning.Message, StringComparison.Ordinal); Assert.Contains("814000", warning.Message, StringComparison.Ordinal);
        Assert.False(LegacyRuneSpellClassification.TryCapture(world, 8140, out var proof)); Assert.Null(proof);
        Assert.True(LegacyRuneSpellClassification.HasExplicitEntry(world, 8140));
        Assert.Equal(2, world.MagicEffectsById[814000].Count);
        for (int index = 0; index < original.Length; index++)
        {
            Assert.Same(original[index], world.MagicEffects[index]); Assert.Same(original[index], world.MagicEffectByIdAndOccurrence[(814000, index + 1)]);
        }
        Assert.False(rows[0].Attributes.ContainsKey("IsRuneSkill")); Assert.Equal("1", rows[1].Attributes["IsRuneSkill"]);
    }

    [Fact]
    public void AUniqueExplicitResourceShapeStillDecodesWithoutActivatingAnotherClientProfile()
    {
        var world = Catalog(MagicEffectParser.Parse("[814000]\r\nName=Caprice\r\nIsRuneSkill=1\r\n", "owned-unique-resource.ini"));
        Assert.DoesNotContain(world.Diagnostics, value => value.Code == MagicEffectRuneClassificationDecoder.WarningCode);
        Assert.True(LegacyRuneSpellClassification.TryCapture(world, 8140, out var proof)); Assert.True(proof!.IsRuneSkill); Assert.True(proof.IsCurrent());
        // This proves only the existing metadata API. No Frame/owner/profile is fabricated.
    }

    [Fact]
    public void MissingOrdinaryClassificationRetainsItsExistingAbsenceAndNoWarning()
    {
        var world = Catalog(MagicEffectParser.Parse("[100000]\r\nName=Thunder\r\n"));
        Assert.DoesNotContain(world.Diagnostics, value => value.Code == MagicEffectRuneClassificationDecoder.WarningCode);
        Assert.False(LegacyRuneSpellClassification.TryCapture(world, 1000, out _)); Assert.False(LegacyRuneSpellClassification.HasExplicitEntry(world, 1000));
    }

    [Fact]
    public void OrdinaryDuplicateEffectsWithoutAnExplicitRuneFlagProduceNoNewWarning()
    {
        var world = Catalog(MagicEffectParser.Parse("[100000]\r\nName=Thunder\r\nStatus=5\r\nStatus=145\r\n[100000]\r\nDesc=Other\r\n"));
        Assert.DoesNotContain(world.Diagnostics, value => value.Code == MagicEffectRuneClassificationDecoder.WarningCode);
        Assert.Equal(2, world.MagicEffectsById[100000].Count); Assert.Equal("145", world.MagicEffects[0].Attributes["Status"]);
    }

    [Theory]
    [InlineData("-1")] [InlineData("bad")]
    public void MalformedExplicitFlagsWarnWithoutThrowingOrDowngradingToOrdinary(string value)
    {
        var world = Catalog(MagicEffectParser.Parse("[814000]\r\nIsRuneSkill=" + value + "\r\n"));
        var warning = Assert.Single(world.Diagnostics, row => row.Code == MagicEffectRuneClassificationDecoder.WarningCode);
        Assert.StartsWith("WARNING:", warning.Message, StringComparison.Ordinal);
        Assert.False(LegacyRuneSpellClassification.TryCapture(world, 8140, out _)); Assert.True(LegacyRuneSpellClassification.HasExplicitEntry(world, 8140));
        Assert.Equal(value, world.MagicEffects[0].Attributes["IsRuneSkill"]);
    }

    private static WorldDataCatalog Catalog(IReadOnlyList<MagicEffect> rows) =>
        NativeCallerWorldDataFixture.Create(Array.Empty<ItemType>(), [], magicEffects: rows);
}

// AUTHOR ONLY until Root separately executes after the full gate. This records
// effective OS behavior, not a guessed first/last policy or runtime activation.
[Collection("NativeItemCounterIniAdapter")]
public sealed class LegacyRune8140NativeIniObservationTests(ITestOutputHelper output)
{
    [WindowsNativeIniTheory]
    [InlineData(false)] [InlineData(true)]
    public void OwnedDuplicate8140InputRecordsNativeValueWithUniqueControlsAndPreservedBytes(bool explicitFirst)
    {
        using var files = new LegacyNativeIniCompleteOwnedProfiles();
        const string missing = "[814000]\r\nName=Vampire\r\nTargetType=2\r\n";
        const string flagged = "[814000]\r\nName=Caprice\r\nTargetType=8\r\nIsRuneSkill=1\r\n";
        var absentPath = files.Seed(missing); var presentPath = files.Seed(flagged);
        Assert.Equal((byte)0, files.Adapter(absentPath).ReadByte("814000", "IsRuneSkill", 0));
        Assert.Equal((byte)1, files.Adapter(presentPath).ReadByte("814000", "IsRuneSkill", 0));
        var path = files.Seed(explicitFirst ? flagged + missing : missing + flagged); var before = files.Bytes(path);
        var actual = files.Adapter(path); var raw = actual.ReadString("814000", "IsRuneSkill", "0", 6);
        var value = actual.ReadByte("814000", "IsRuneSkill", 0);
        Assert.Contains(value, new byte[] { 0, 1 }); Assert.Equal(byte.Parse(raw, NumberStyles.None, CultureInfo.InvariantCulture), value);
        Assert.Equal(before, files.Bytes(path));
        var analysis = MagicEffectRuneClassificationDecoder.Parse(explicitFirst ? flagged + missing : missing + flagged, 814000);
        Assert.Equal(MagicEffectRuneClassificationStatus.Ambiguous, analysis.Status); Assert.Null(analysis.UniqueValue);
        output.WriteLine(JsonSerializer.Serialize(new { Kind = "OwnedNative8140Observation", ExplicitFirst = explicitFirst,
            NativeReadString = raw, NativeReadByte = value, DefaultValue = 0, SectionOccurrences = 2,
            UniqueMissingControl = 0, UniqueExplicitControl = 1, FileBytesUnchanged = true,
            Scope = "Existing wrapper; owned absolute temporary INI only; no target/profile activation" }));
    }
}
