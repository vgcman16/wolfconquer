using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSubProfessionMetadataTests
{
    [WindowsNativeIniFact]
    public void RealUtf16IniLoadsAllSeventyRowsAndPerformerIgnoresBothRequirements()
    {
        using var owned = new OwnedIni();
        owned.Seed(CompleteIni());
        var metadata = new LegacyNativeSubProfessionInfo();
        metadata.Load();
        AssertComplete(metadata);
        var ini = new LegacyNativeItemCounterShellIni(owned.FilePath, overridePath: true);
        Assert.Equal("魔攻+6000;物攻+12000", ini.ReadString("6", "intro", ""));
        Assert.Equal((byte)250, ini.ReadByte("6", "req_lv", 0));
        Assert.Equal((byte)255, ini.ReadByte("6", "req_meto", 0));
        Assert.Equal(0u, metadata[LegacyNativeDbLevExp.Sort.Performer][0].NeedLevel);
        Assert.Equal((byte)0, metadata[LegacyNativeDbLevExp.Sort.Performer][0].NeedReborn);
    }

    [WindowsNativeIniFact]
    public void RepeatedLoadThrowsAtFirstDuplicateWithoutClearingRowsOrReplacingOwners()
    {
        using var owned = new OwnedIni();
        owned.Seed(CompleteIni());
        var metadata = new LegacyNativeSubProfessionInfo();
        metadata.Load();
        var first = metadata[LegacyNativeDbLevExp.Sort.MartialArtist];
        var row = first[0];
        uint[] damage = row.Damage;
        var retained = new Dictionary<byte, LegacyNativeSubProfessionInfo.ProfessionInfo>
        {
            [99] = new() { Damage = [123], NeedLevel = 456, NeedReborn = 7 },
        };
        metadata.Add(LegacyNativeDbLevExp.Sort.User, retained);
        Assert.Throws<ArgumentException>(() => metadata.Load());
        Assert.Equal(8, metadata.Count);
        Assert.Equal(71, metadata.Values.Sum(rows => rows.Count));
        Assert.Same(first, metadata[LegacyNativeDbLevExp.Sort.MartialArtist]);
        Assert.Same(row, first[0]);
        Assert.Same(damage, first[0].Damage);
        Assert.Same(retained, metadata[LegacyNativeDbLevExp.Sort.User]);
        AssertRow(row, 1, 0);
        AssertRow(metadata[LegacyNativeDbLevExp.Sort.Wrangler][9], 9, 9);
    }

    [WindowsNativeIniFact]
    public void MissingPlusInLaterIntroPublishesOnlyTheExactCompletedPrefix()
    {
        using var owned = new OwnedIni();
        owned.Seed(CompleteIni(new() { ["23/intro"] = "malformed" }));
        var metadata = new LegacyNativeSubProfessionInfo();
        Assert.Throws<IndexOutOfRangeException>(() => metadata.Load());
        Assert.Equal(2, metadata.Count);
        Assert.Equal(13, metadata.Values.Sum(rows => rows.Count));
        Assert.Equal(10, metadata[LegacyNativeDbLevExp.Sort.MartialArtist].Count);
        var partial = metadata[LegacyNativeDbLevExp.Sort.Warlock];
        Assert.Equal(new byte[] { 0, 1, 2 }, partial.Keys.Order().ToArray());
        AssertRow(partial[2], 2, 2);
        Assert.False(partial.ContainsKey(3));
        Assert.False(metadata.ContainsKey(LegacyNativeDbLevExp.Sort.ChiMaster));
    }

    [WindowsNativeIniFact]
    public void PerformerSecondValueParseFaultDoesNotPublishItsHalfParsedRecord()
    {
        using var owned = new OwnedIni();
        owned.Seed(CompleteIni(new() { ["64/intro"] = "魔攻+123;物攻+not-a-number" }));
        var metadata = new LegacyNativeSubProfessionInfo();
        Assert.Throws<FormatException>(() => metadata.Load());
        Assert.Equal(6, metadata.Count);
        Assert.Equal(54, metadata.Values.Sum(rows => rows.Count));
        var partial = metadata[LegacyNativeDbLevExp.Sort.Performer];
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, partial.Keys.Order().ToArray());
        AssertRow(partial[3], 6, 3);
        Assert.False(partial.ContainsKey(4));
        Assert.False(metadata.ContainsKey(LegacyNativeDbLevExp.Sort.Wrangler));
    }

    [WindowsNativeIniFact]
    public void UInt32DamageOverflowRetainsPriorClassesAndEarlierWranglerPhases()
    {
        using var owned = new OwnedIni();
        owned.Seed(CompleteIni(new() { ["92/intro"] = "特效+4294967296%" }));
        var metadata = new LegacyNativeSubProfessionInfo();
        Assert.Throws<OverflowException>(() => metadata.Load());
        Assert.Equal(7, metadata.Count);
        Assert.Equal(62, metadata.Values.Sum(rows => rows.Count));
        Assert.Equal(new byte[] { 0, 1 }, metadata[LegacyNativeDbLevExp.Sort.Wrangler].Keys.Order().ToArray());
        AssertRow(metadata[LegacyNativeDbLevExp.Sort.Wrangler][1], 9, 1);
        AssertRow(metadata[LegacyNativeDbLevExp.Sort.Performer][9], 6, 9);
    }

    [WindowsNativeIniFact]
    public void MissingIniUsesEmptyNativeIntroThenFaultsWithoutCreatingAFileOrClearingState()
    {
        using var owned = new OwnedIni();
        var metadata = new LegacyNativeSubProfessionInfo();
        var retained = new Dictionary<byte, LegacyNativeSubProfessionInfo.ProfessionInfo>();
        metadata.Add(LegacyNativeDbLevExp.Sort.User, retained);
        Assert.False(File.Exists(owned.FilePath));
        Assert.Throws<IndexOutOfRangeException>(() => metadata.Load());
        Assert.Single(metadata);
        Assert.Same(retained, metadata[LegacyNativeDbLevExp.Sort.User]);
        Assert.False(File.Exists(owned.FilePath));
    }

    [WindowsNativeIniFact]
    public void ActualIniConstructorConcatenatesLiveLocationPrefixWithoutInsertingASeparator()
    {
        using var owned = new OwnedIni();
        owned.Seed(CompleteIni());
        owned.Seed(CompleteIni(offset: 50000), prefix: true);
        owned.UseLiteralPrefix();
        Assert.Equal(owned.PrefixPath, new LegacyNativeItemCounterShellIni("SubProfessionInfo.ini").FileName);
        var metadata = new LegacyNativeSubProfessionInfo();
        metadata.Load();
        AssertComplete(metadata, 50000);
    }

    [WindowsNativeIniFact]
    public void RequirementReadsUseByteBoundsAndZeroDefaultsDespiteUInt32NeedLevelStorage()
    {
        using var owned = new OwnedIni();
        string?[] tokens = ["255", "256", "-1", "bad", "", "65535", null, "+255", "4294967295", "1.5"];
        byte[] expected = [255, 0, 0, 0, 0, 0, 0, 255, 0, 0];
        var overrides = new Dictionary<string, string?>();
        for (byte phase = 0; phase < 10; phase++)
        {
            overrides[Section(1, phase) + "/req_lv"] = tokens[phase];
            overrides[Section(1, phase) + "/req_meto"] = tokens[phase];
        }
        owned.Seed(CompleteIni(overrides));
        var metadata = new LegacyNativeSubProfessionInfo();
        metadata.Load();
        Assert.Equal(70, metadata.Values.Sum(rows => rows.Count));
        Assert.Equal(typeof(uint), typeof(LegacyNativeSubProfessionInfo.ProfessionInfo).GetField("NeedLevel")!.FieldType);
        for (byte phase = 0; phase < 10; phase++)
        {
            var row = metadata[LegacyNativeDbLevExp.Sort.MartialArtist][phase];
            Assert.Equal((uint)expected[phase], row.NeedLevel);
            Assert.Equal(expected[phase], row.NeedReborn);
            Assert.Equal(new[] { Damage(1, phase) }, row.Damage);
        }
    }
}
