using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ActiveTitleStorageSafetyTests
{
    [Fact]
    public async Task Unequip4001ClearsOnlyWingAndPreservesNormalTitleScoreAndClassicTitle()
    {
        using var fixture = new Fixture(equipped: true) { VerifyPersistenceBeforePublication = true };
        await fixture.DispatchAsync(LegacyTitleStorageAction.UnEquip);

        var current = fixture.Current();
        Assert.Equal("0", current.Character.Attributes["SpecialWingID"]);
        Assert.Equal(NormalTitle, current.Character.Attributes["SpecialTitleID"]);
        Assert.Equal(NormalScore, current.Character.Attributes["SpecialTitleScore"]);
        Assert.Equal("9", current.Character.Attributes["MyTitle"]);
        Assert.Equal(InactiveWingTitles, current.Character.Attributes["SpecialTitles"]);
        AssertTitleAttributesEqual(current.Character, fixture.ReadPersisted());
        var acknowledgement = ReadTitlePacket(Assert.Single(fixture.Sent));
        Assert.Equal(LegacyTitleStorageAction.UnEquip, acknowledgement.Action);
        Assert.Equal(WingId, acknowledgement.DwParam2);
        Assert.Equal(WingSubId, acknowledgement.DwParam3);
        AssertPeerSpawn(Assert.Single(fixture.PeerSent), wing: 0);
    }

    [Theory]
    [InlineData(LegacyTitleStorageAction.Equip)]
    [InlineData(LegacyTitleStorageAction.UnEquip)]
    public async Task ForgedSubtypeCannotReachAcknowledgementOrUseTitle(LegacyTitleStorageAction action)
    {
        using var fixture = new Fixture(equipped: action == LegacyTitleStorageAction.UnEquip)
        { VerifyPersistenceBeforePublication = true };
        await fixture.DispatchAsync(action, subtype: uint.MaxValue);

        Assert.Equal(action == LegacyTitleStorageAction.Equip ? 2 : 1, fixture.Sent.Count);
        var acknowledgement = ReadTitlePacket(fixture.Sent[0]);
        Assert.Equal(action, acknowledgement.Action);
        Assert.Equal(WingId, acknowledgement.DwParam2);
        Assert.Equal(WingSubId, acknowledgement.DwParam3);
        if (action == LegacyTitleStorageAction.Equip)
        {
            var useTitle = ReadTitlePacket(fixture.Sent[1]);
            Assert.Equal(LegacyTitleStorageAction.UseTitle, useTitle.Action);
            Assert.Equal(WingId, useTitle.DwParam2);
            Assert.Equal(WingSubId, useTitle.DwParam3);
            Assert.Equal(new LegacyTitleStorageTitle(WingId, WingSubId, DwParam1: 1), useTitle.Title);
            Assert.Equal(PackedWing, fixture.Current().Character.Attributes["SpecialWingID"]);
        }
        AssertTitleAttributesEqual(fixture.Current().Character, fixture.ReadPersisted());
        AssertPeerSpawn(Assert.Single(fixture.PeerSent),
            action == LegacyTitleStorageAction.Equip ? 40_010_017UL : 0UL);
    }

    [Fact]
    public async Task EquipThenUnequipCommitsAndResolvesRealSaveBeforePublishingEachResponse()
    {
        using var fixture = new Fixture { VerifyPersistenceBeforePublication = true };
        await fixture.DispatchAsync();
        var equipped = fixture.Current();
        Assert.NotSame(fixture.Original, equipped);
        Assert.Equal(ActiveWingTitles, equipped.Character.Attributes["SpecialTitles"]);
        var active = LegacyTitleStoragePacket.ResolveActiveSpecialTitleState(fixture.ReadPersisted(), fixture.UsersRoot);
        Assert.Equal(40_010_017UL, active.SpecialWingId);
        Assert.Equal(20_010_023UL, active.SpecialTitleId);
        Assert.Equal(73UL, active.SpecialTitleScore);

        await fixture.DispatchAsync(LegacyTitleStorageAction.UnEquip,
            context: fixture.Context with { Session = equipped });

        var unequipped = fixture.Current();
        Assert.NotSame(equipped, unequipped);
        Assert.Equal(InactiveWingTitles, unequipped.Character.Attributes["SpecialTitles"]);
        active = LegacyTitleStoragePacket.ResolveActiveSpecialTitleState(fixture.ReadPersisted(), fixture.UsersRoot);
        Assert.Equal(0UL, active.SpecialWingId);
        Assert.Equal(20_010_023UL, active.SpecialTitleId);
        Assert.Equal(73UL, active.SpecialTitleScore);
        Assert.Equal(new[] { LegacyTitleStorageAction.Equip, LegacyTitleStorageAction.UseTitle, LegacyTitleStorageAction.UnEquip },
            fixture.Sent.Select(bytes => ReadTitlePacket(bytes).Action));
        Assert.Equal(2, fixture.PeerSent.Count);
        AssertPeerSpawn(fixture.PeerSent[0], 40_010_017UL);
        AssertPeerSpawn(fixture.PeerSent[1], 0UL);
        AssertTitleAttributesEqual(unequipped.Character, fixture.ReadPersisted());
        Assert.Contains("[UnrelatedSection]", File.ReadAllText(fixture.SnapshotPath));
        Assert.Contains("Marker=retained", File.ReadAllText(fixture.SnapshotPath));
    }

    [Fact]
    public async Task MismatchedAccountAndCharacterIdsFailClosedEvenWithBothFilesPresent()
    {
        using var fixture = new Fixture(differentAccountId: true);
        var accountBytes = fixture.WriteAccountDecoy();
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        Assert.NotEqual(fixture.Original.AccountId, fixture.Original.Character.Uid);

        await fixture.DispatchAsync();

        fixture.AssertRejected(fixture.Original, before);
        Assert.Equal(accountBytes, fixture.ReadAccountDecoy());
    }
}
