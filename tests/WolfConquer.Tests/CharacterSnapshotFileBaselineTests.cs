using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class CharacterSnapshotFileBaselineTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CapturedBytesDecodeThroughExistingEncodingAndRetainExactCharacterValues(int encodingKind)
    {
        using var fixture = new TitlePersistenceFileFixture();
        fixture.Seed(encodingKind);
        var text = File.ReadAllText(fixture.PathName, TitlePersistenceFileFixture.EncodingFor(encodingKind))
            .Replace("SecurityPass=91357,0,0,", "SecurityPass= 91357,0,0, ");
        File.WriteAllText(fixture.PathName, text, TitlePersistenceFileFixture.EncodingFor(encodingKind));
        var bytes = File.ReadAllBytes(fixture.PathName);
        Assert.True(CharacterSnapshotFileBaseline.TryRead(fixture.PathName, "secondaryPassword", out var snapshot, out var reason), reason);
        Assert.Equal(bytes, snapshot!.Bytes);
        Assert.Equal(" 91357,0,0, ", snapshot.Fields["SecurityPass"]);
        Assert.Equal("Crème€", snapshot.Fields["Name"]);
        Assert.Equal(TitlePersistenceFileFixture.EncodingFor(encodingKind).CodePage, snapshot.Encoding.CodePage);
        Assert.DoesNotContain("Sentinel", snapshot.Fields.Keys);
    }

    [Theory]
    [InlineData("[Character]\r\nUID=1\r\nuid=2", "CharacterFieldsAmbiguous")]
    [InlineData("[Character]\r\nName=one\r\nNAME=two", "CharacterFieldsAmbiguous")]
    [InlineData("[Character]\r\nSecurityPass=1\r\nsecuritypass=2", "CharacterFieldsAmbiguous")]
    [InlineData("[Character]\r\nUID=1\r\n[character]\r\nOther=two", "CharacterSectionAmbiguous")]
    [InlineData("[Character]\r\nUID=1\r\nnot-a-key", "CharacterLineInvalid")]
    [InlineData("[Character]\r\n =value", "CharacterLineInvalid")]
    [InlineData("[Other]\r\nUID=1", "CharacterSectionMissing")]
    public void InvalidCharacterRowsPublishNoPartialSnapshotWithUnchangedPinReasonSuffix(string text, string suffix)
    {
        using var fixture = new TitlePersistenceFileFixture();
        File.WriteAllText(fixture.PathName, text);
        Assert.False(CharacterSnapshotFileBaseline.TryRead(fixture.PathName, "secondaryPassword", out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.Equal("secondaryPassword" + suffix, reason);
        Assert.False(CharacterSnapshotFileBaseline.TryRead(fixture.PathName, "titleStorage", out snapshot, out reason));
        Assert.Null(snapshot);
        Assert.Equal("titleStorage" + suffix, reason);
    }

    [Fact]
    public void CommentsAndOtherSectionsDoNotAcquireCharacterKeys()
    {
        using var fixture = new TitlePersistenceFileFixture();
        File.WriteAllText(fixture.PathName,
            "# comment\r\n[Character]\r\n ; hidden=1\r\nUID=1\r\nA= first \r\n[Other]\r\nUID=99\r\nA=other\r\nA=duplicate-other");
        Assert.True(CharacterSnapshotFileBaseline.TryRead(fixture.PathName, "secondaryPassword", out var snapshot, out var reason), reason);
        Assert.Equal(2, snapshot!.Fields.Count);
        Assert.Equal("1", snapshot.Fields["UID"]);
        Assert.Equal(" first ", snapshot.Fields["A"]);
    }

    [Fact]
    public void OversizeFileIsRejectedBeforeCapturedByteAllocationOrPublication()
    {
        using var fixture = new TitlePersistenceFileFixture();
        using (var file = new FileStream(fixture.PathName, FileMode.Open, FileAccess.Write, FileShare.None))
            file.SetLength(CharacterSnapshotFileBaseline.MaximumBytes + 1L);
        Assert.False(CharacterSnapshotFileBaseline.TryRead(fixture.PathName, "secondaryPassword", out var snapshot, out var reason));
        Assert.Null(snapshot);
        Assert.Equal("secondaryPasswordSnapshotTooLarge", reason);
        Assert.False(CharacterSnapshotFileBaseline.TryReadBytes(fixture.PathName, "titleStorage", out var bytes, out reason));
        Assert.Empty(bytes);
        Assert.Equal("titleStorageSnapshotTooLarge", reason);
    }

    [Fact]
    public void ExistingPinWrapperStillRejectsDuplicateCharacterFieldsWithoutChangingFile()
    {
        using var fixture = new TitlePersistenceFileFixture();
        var original = fixture.Seed();
        File.AppendAllText(fixture.PathName, "\r\n[character]\r\nSecurityPass=24681,0,0,");
        var bytes = File.ReadAllBytes(fixture.PathName);
        Assert.False(LegacySecondaryPasswordPersistence.TryValidateBaseline(original, out var reason));
        Assert.Equal("secondaryPasswordCharacterSectionAmbiguous", reason);
        Assert.False(LegacySecondaryPasswordPersistence.TryPersist(original, new(24681, 0, default), out reason));
        Assert.Equal("secondaryPasswordCharacterSectionAmbiguous", reason);
        Assert.Equal(bytes, File.ReadAllBytes(fixture.PathName));
    }
}
