using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class TitleStoragePersistenceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RealCoordinatorCommitsAllTitleFieldsAndPreservesOtherStateAndEncoding(int encodingKind)
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed(encodingKind);
        var updated = TitlePersistenceFileFixture.Updated(original);
        Assert.True(LegacyTitleStoragePersistence.TryPersist(original, updated,
            TitlePersistenceFileFixture.Values(updated), out var reason), reason);
        var text = CharacterSnapshotFileEncoding.ReadAllText(fixture.PathName, out var encoding);
        Assert.Equal(TitlePersistenceFileFixture.EncodingFor(encodingKind).CodePage, encoding.CodePage);
        Assert.Equal(TitlePersistenceFileFixture.EncodingFor(encodingKind).GetPreamble(), encoding.GetPreamble());
        var actual = CharacterSnapshotParser.ParseText(text);
        foreach (var (key, value) in TitlePersistenceFileFixture.Values(updated))
            Assert.Equal(value, actual.Attributes[key]);
        Assert.Equal("0", original.Character.Attributes["SpecialTitleID"]);
        Assert.Equal(original.Character.Money, actual.Money);
        Assert.Equal("Crème€", actual.Name);
        Assert.Contains("SecurityPass=91357,0,0,", text);
        Assert.Contains("ManagedTradeEscrowMoney=17", text);
        Assert.Contains("[Unrelated]", text);
        Assert.Contains("Sentinel=café€", text);
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.character-file-*"));
    }

    [Theory]
    [InlineData("SpecialTitles", "2,2149,0,4001,1,")]
    [InlineData("SpecialTitleID", "9999")]
    [InlineData("SpecialWingID", "40011000")]
    [InlineData("SpecialTitleScore", "0150")]
    public void AnyStaleTitleFieldRejectsBeforeWriting(string key, string changed)
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        var lines = File.ReadAllLines(fixture.PathName);
        File.WriteAllLines(fixture.PathName, lines.Select(line =>
            line.StartsWith(key + "=", StringComparison.Ordinal) ? key + "=" + changed : line));
        fixture.AssertRejected(original, updated, TitlePersistenceFileFixture.Values(updated), "titleStorageStateStale");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void InvalidBindingOrRelativeRootCannotSelectAFile(int kind)
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        original = kind switch
        {
            0 => original with { AccountId = 0 },
            1 => original with { AccountId = -1 },
            2 => original with { AccountId = TitlePersistenceFileFixture.Uid + 1 },
            3 => original with { UsersRoot = "." },
            _ => original,
        };
        if (kind == 4) updated = updated with { UsersRoot = "." };
        fixture.AssertRejected(original, updated, TitlePersistenceFileFixture.Values(updated), "titleStorageBindingInvalid");
    }

    [Fact]
    public void StoredUidMustMatchTheBoundAccountBeforeAnyWrite()
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        File.WriteAllText(fixture.PathName, File.ReadAllText(fixture.PathName).Replace(
            "UID=" + TitlePersistenceFileFixture.Uid.ToString(CultureInfo.InvariantCulture),
            "UID=" + (TitlePersistenceFileFixture.Uid + 1).ToString(CultureInfo.InvariantCulture)));
        fixture.AssertRejected(original, updated, TitlePersistenceFileFixture.Values(updated), "titleStorageUidMismatch");
    }

    [Theory]
    [InlineData("Money", "999999")]
    [InlineData("UID", "1")]
    [InlineData("SpecialTitleID", "1")]
    [InlineData("SpecialTitles", "owned\r\nMoney=999999")]
    public void CandidateCannotSmuggleUnrelatedOrDifferentState(string key, string value)
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        var values = TitlePersistenceFileFixture.Values(updated);
        values[key] = value;
        fixture.AssertRejected(original, updated, values, "titleStorageCandidateFieldsInvalid");
    }

    [Fact]
    public void CaseDuplicateCandidateCannotSelectOneValue()
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        var values = new Dictionary<string, string>(TitlePersistenceFileFixture.Values(updated), StringComparer.Ordinal)
        { ["specialtitleid"] = "21492149" };
        fixture.AssertRejected(original, updated, values, "titleStorageCandidateFieldsInvalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnrelatedScalarOrAttributeChangeCannotBypassOrdinaryAutosave(bool attribute)
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        updated = attribute
            ? updated with { Character = updated.Character with { Attributes =
                new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Money"] = "999" } } }
            : updated with { Character = updated.Character with { Money = 999 } };
        fixture.AssertRejected(original, updated, TitlePersistenceFileFixture.Values(updated), "titleStorageUnrelatedStateChanged");
    }

    [Theory]
    [InlineData("SpecialTitleID")]
    [InlineData("SpecialWingID")]
    [InlineData("SpecialTitleScore")]
    [InlineData("SpecialTitles")]
    public void MissingAndPresentZeroOrListAreNotInterchangeableBaselines(string key)
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        File.WriteAllLines(fixture.PathName, File.ReadAllLines(fixture.PathName)
            .Where(line => !line.StartsWith(key + "=", StringComparison.Ordinal)));
        fixture.AssertRejected(original, updated, TitlePersistenceFileFixture.Values(updated), "titleStorageStateStale");
    }

    [WindowsSecondaryPasswordPersistenceFact]
    public void RealReplacementFailureIsOrdinaryOnlyWithExactPreimageThenRetryWorks()
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed(2);
        var updated = TitlePersistenceFileFixture.Updated(original);
        var before = File.ReadAllBytes(fixture.PathName);
        using (new FileStream(fixture.PathName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(LegacyTitleStoragePersistence.TryPersist(original, updated,
                TitlePersistenceFileFixture.Values(updated), out var reason));
            Assert.StartsWith("titleStorageWriteFailed:", reason);
            Assert.Equal(before, File.ReadAllBytes(fixture.PathName));
        }
        Assert.True(LegacyTitleStoragePersistence.TryPersist(original, updated,
            TitlePersistenceFileFixture.Values(updated), out var retry), retry);
    }

    [Fact]
    public void MissingUpdatedNumericFieldCannotBeWrittenAsAnInvisibleZero()
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        var attrs = new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        attrs.Remove("SpecialWingID");
        updated = updated with { Character = updated.Character with { Attributes = attrs } };
        fixture.AssertRejected(original, updated, TitlePersistenceFileFixture.Values(updated), "titleStorageCandidateFieldsInvalid");
    }

    [Fact]
    public void ExplicitCanonicalZerosInCommittedSessionPermitFollowingRequests()
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var attrs = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        attrs.Remove("SpecialWingID");
        original = original with { Character = original.Character with { Attributes = attrs } };
        File.WriteAllLines(fixture.PathName, File.ReadAllLines(fixture.PathName)
            .Where(line => !line.StartsWith("SpecialWingID=", StringComparison.Ordinal)));
        var updated = TitlePersistenceFileFixture.Updated(original);
        updated = updated with { Character = updated.Character with { Attributes =
            new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["SpecialWingID"] = "0" } } };
        Assert.True(LegacyTitleStoragePersistence.TryPersist(original, updated,
            TitlePersistenceFileFixture.Values(updated), out var reason), reason);
        Assert.True(LegacyTitleStoragePersistence.TryPersist(updated, updated,
            TitlePersistenceFileFixture.Values(updated), out reason), reason);
    }

    [Fact]
    public void UnrepresentableCandidateIsRejectedBeforeAWindows1252FileChanges()
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed(2);
        var updated = TitlePersistenceFileFixture.Updated(original);
        updated = updated with { Character = updated.Character with { Attributes =
            new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["SpecialTitles"] = "2,2149,1,4001,0,雪" } } };
        fixture.AssertRejected(original, updated, TitlePersistenceFileFixture.Values(updated), "titleStorageCandidateEncodingLoss");
    }

    [Fact]
    public void OversizeProjectedSnapshotRejectsBeforeTheRealCoordinatorWrites()
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        var updated = TitlePersistenceFileFixture.Updated(original);
        updated = updated with { Character = updated.Character with { Attributes =
            new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["SpecialTitles"] = new string('1', CharacterSnapshotFileBaseline.MaximumBytes) } } };
        fixture.AssertRejected(original, updated, TitlePersistenceFileFixture.Values(updated), "titleStorageCandidateSnapshotTooLarge");
    }
}
