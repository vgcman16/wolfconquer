using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ActiveTitleStorageSafetyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task MissingNumericTitleAttributesBecomeExplicitAndSecondRealRequestDoesNotGoStale(int absentMask)
    {
        using var fixture = new Fixture { VerifyPersistenceBeforePublication = true };
        var source = ConfigureAdditionalTitleState(fixture, absentNumericMask: absentMask);
        var expectedTitle = (absentMask & 1) != 0 ? "0" : NormalTitle;
        var expectedScore = (absentMask & 4) != 0 ? "0" : NormalScore;

        await fixture.DispatchAsync(context: fixture.Context with { Session = source });

        var equipped = fixture.Current();
        Assert.NotSame(source, equipped);
        Assert.Equal(expectedTitle, equipped.Character.Attributes["SpecialTitleID"]);
        Assert.Equal(expectedScore, equipped.Character.Attributes["SpecialTitleScore"]);
        Assert.Equal(PackedWing, equipped.Character.Attributes["SpecialWingID"]);
        AssertTitleAttributesEqual(equipped.Character, fixture.ReadPersisted());
        Assert.Equal(2, fixture.Sent.Count);
        AssertAdditionalCanonicalSpawn(Assert.Single(fixture.PeerSent),
            expectedTitle, expectedScore, PackedWing);

        await fixture.DispatchAsync(LegacyTitleStorageAction.UnEquip,
            context: fixture.Context with { Session = equipped });

        var unequipped = fixture.Current();
        Assert.NotSame(equipped, unequipped);
        Assert.Equal(expectedTitle, unequipped.Character.Attributes["SpecialTitleID"]);
        Assert.Equal(expectedScore, unequipped.Character.Attributes["SpecialTitleScore"]);
        Assert.Equal("0", unequipped.Character.Attributes["SpecialWingID"]);
        Assert.Equal(InactiveWingTitles, unequipped.Character.Attributes["SpecialTitles"]);
        AssertTitleAttributesEqual(unequipped.Character, fixture.ReadPersisted());
        Assert.Equal(new[] { LegacyTitleStorageAction.Equip, LegacyTitleStorageAction.UseTitle, LegacyTitleStorageAction.UnEquip },
            fixture.Sent.Select(frame => ReadTitlePacket(frame).Action));
        Assert.Equal(2, fixture.PeerSent.Count);
        AssertAdditionalCanonicalSpawn(fixture.PeerSent[1], expectedTitle, expectedScore, "0");
        Assert.DoesNotContain("titleStorageStateStale", fixture.Log.ToString());
        Assert.False(fixture.Recovery.IsBlocked);
    }

    [Theory]
    [InlineData(LegacyTitleStorageAction.Equip)]
    [InlineData(LegacyTitleStorageAction.UnEquip)]
    public async Task AbsentRecoveryOwnerRejectsWithoutStateFileOrPublicationEffects(LegacyTitleStorageAction action)
    {
        using var fixture = new Fixture(equipped: action == LegacyTitleStorageAction.UnEquip);
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        var written = File.GetLastWriteTimeUtc(fixture.SnapshotPath);

        await fixture.DispatchAsync(action, context: fixture.Context with { TradeRecovery = null });

        fixture.AssertRejected(fixture.Original, before);
        Assert.Equal(written, File.GetLastWriteTimeUtc(fixture.SnapshotPath));
        Assert.False(fixture.Recovery.IsBlocked);
        Assert.DoesNotContain("persisted LegacyMsgTitleStorage", fixture.Log.ToString());
    }

    private static void AssertAdditionalCanonicalSpawn(byte[] frame,
        string expectedTitle, string expectedScore, string expectedWing)
    {
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame), out var spawn));
        Assert.Equal((ulong)Fixture.CharacterUid, spawn.GetNumericField(2));
        Assert.Equal(ulong.Parse(expectedTitle, CultureInfo.InvariantCulture), spawn.GetNumericField(89));
        Assert.Equal(ulong.Parse(expectedScore, CultureInfo.InvariantCulture), spawn.GetNumericField(90));
        Assert.Equal(ulong.Parse(expectedWing, CultureInfo.InvariantCulture), spawn.GetNumericField(91));
    }
}
