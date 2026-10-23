using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ActiveTitleStorageSafetyTests
{
    [Theory]
    [InlineData("3,2001,1,4001,2,4002,0,")]
    [InlineData("3,2001,1,4001,4294967295,4002,0,")]
    [InlineData("3,2001,1,4001,-1,4002,0,")]
    [InlineData("3,2001,1,4001,active,4002,0,")]
    [InlineData("3,2001,1,4001,0,4002,2,")]
    [InlineData("3,2001,2,4001,0,4002,0,")]
    [InlineData("3,4001,0,2001,1,4001,0,")]
    [InlineData("4,2001,1,4001,0,4002,0,4002,1,")]
    [InlineData("3,2001,1,4001,0,-4002,0,")]
    [InlineData("3,2001,1,4001,0,other,0,")]
    [InlineData("2,2001,1,4001,0,4002,0,")]
    [InlineData("4,2001,1,4001,0,4002,0,")]
    [InlineData("three,2001,1,4001,0,4002,0,")]
    [InlineData("-3,2001,1,4001,0,4002,0,")]
    [InlineData("4294967296,2001,1,4001,0,4002,0,")]
    [InlineData("0,2001,1,4001,0,4002,0,")]
    [InlineData("3,2001,1,,4001,0,4002,0,")]
    [InlineData("3,2001,1,4001,,0,4002,0,")]
    [InlineData("3,2001,1,4001,0,4002,0,,")]
    [InlineData(",3,2001,1,4001,0,4002,0,")]
    [InlineData("2001,1,4001,0,,4002,0,")]
    [InlineData("2001,1,4001,0,4002,2,")]
    [InlineData("2001,1,4001,0,2001,0,")]
    [InlineData("2001,1,4001,2,4002,0,")]
    [InlineData("2001,1,4001,0,4002,")]
    public async Task EntireOwnershipListMustValidateBeforeAnyEquipEffect(string storedTitles)
    {
        using var fixture = new Fixture();
        var source = ConfigureAdditionalTitleState(fixture, storedTitles);
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        var written = File.GetLastWriteTimeUtc(fixture.SnapshotPath);

        await fixture.DispatchAsync(context: fixture.Context with { Session = source });

        fixture.AssertRejected(source, before);
        Assert.Equal(written, File.GetLastWriteTimeUtc(fixture.SnapshotPath));
        Assert.False(fixture.Recovery.IsBlocked);
        Assert.DoesNotContain("persisted LegacyMsgTitleStorage", fixture.Log.ToString());
    }

    [Theory]
    [InlineData("3,2001,1,4001,2,4002,0,")]
    [InlineData("4,2001,1,4001,1,4002,0,4002,1,")]
    [InlineData("3,2001,1,4001,1,,4002,0,")]
    [InlineData("2001,1,4001,1,4002,invalid,")]
    public async Task InvalidWholeOwnershipCannotClearAnEquippedWing(string storedTitles)
    {
        using var fixture = new Fixture(equipped: true);
        var source = ConfigureAdditionalTitleState(fixture, storedTitles);
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        var written = File.GetLastWriteTimeUtc(fixture.SnapshotPath);

        await fixture.DispatchAsync(LegacyTitleStorageAction.UnEquip,
            context: fixture.Context with { Session = source });

        fixture.AssertRejected(source, before);
        Assert.Equal(written, File.GetLastWriteTimeUtc(fixture.SnapshotPath));
        Assert.False(fixture.Recovery.IsBlocked);
        Assert.Equal(PackedWing, fixture.Current().Character.Attributes["SpecialWingID"]);
    }

    [Theory]
    [InlineData("3,2001,1,4001,0,4002,0,", ActiveWingTitles)]
    [InlineData("3,2001,1,4001,0,4002,0", ActiveWingTitles)]
    [InlineData("2001,1,4001,0,4002,0,", "2001,1,4001,1,4002,0,")]
    [InlineData("2001,1,4001,0,4002,0", "2001,1,4001,1,4002,0,")]
    public async Task CountedAndBarePairsRemainValidWithOptionalSingleFinalSeparator(
        string storedTitles, string expectedActiveTitles)
    {
        using var fixture = new Fixture { VerifyPersistenceBeforePublication = true };
        var source = ConfigureAdditionalTitleState(fixture, storedTitles);

        await fixture.DispatchAsync(context: fixture.Context with { Session = source });

        Assert.NotSame(source, fixture.Current());
        Assert.Equal(expectedActiveTitles, fixture.Current().Character.Attributes["SpecialTitles"]);
        Assert.Equal(PackedWing, fixture.Current().Character.Attributes["SpecialWingID"]);
        AssertTitleAttributesEqual(fixture.Current().Character, fixture.ReadPersisted());
        Assert.Equal(new[] { LegacyTitleStorageAction.Equip, LegacyTitleStorageAction.UseTitle },
            fixture.Sent.Select(frame => ReadTitlePacket(frame).Action));
        AssertPeerSpawn(Assert.Single(fixture.PeerSent), 40_010_017UL);
        Assert.False(fixture.Recovery.IsBlocked);
    }

    private static WorldSession ConfigureAdditionalTitleState(Fixture fixture, string? storedTitles = null,
        int absentNumericMask = 0)
    {
        var numericKeys = new[] { "SpecialTitleID", "SpecialWingID", "SpecialTitleScore" };
        var lines = new List<string>();
        foreach (var line in File.ReadAllLines(fixture.SnapshotPath))
        {
            if (storedTitles is not null && line.StartsWith("SpecialTitles=", StringComparison.Ordinal))
            {
                lines.Add("SpecialTitles=" + storedTitles);
                continue;
            }
            var omit = false;
            for (var index = 0; index < numericKeys.Length; index++)
                omit |= (absentNumericMask & (1 << index)) != 0 &&
                    line.StartsWith(numericKeys[index] + "=", StringComparison.Ordinal);
            if (!omit) lines.Add(line);
        }
        var text = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        File.WriteAllText(fixture.SnapshotPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var source = fixture.Original with
        {
            Character = CharacterSnapshotParser.ParseText(text, fixture.SnapshotPath),
        };
        fixture.Sessions.Set(fixture.Connection, source);
        return source;
    }
}
