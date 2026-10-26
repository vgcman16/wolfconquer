using System.Reflection;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(10L, false)]
    [InlineData(5051L, false)]
    [InlineData(7051L, false)]
    [InlineData(7051L, true)]
    public Task ActualD13StandaloneMailboxPackageRequiresRealRewardCommitBeforeAnyEffect(
        long classCode, bool savedGear) => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(mixed: true, flag: 1,
            packageClass: classCode, savedPackageGear: savedGear);
        var before = fixture.CaptureTransferFrame();
        var owner = fixture.ReadEnteredMailboxPackageSession();
        var json = JsonSerializer.Serialize(owner);
        Assert.Equal(classCode, owner.Character.Class);
        // Current login exposes Empty when no gear loader/file is configured;
        // neither that placeholder nor a real saved snapshot grants package authority.
        Assert.NotNull(owner.CombatGear);
        Assert.Equal(savedGear, Directory.Exists(Path.Combine(fixture.Root, "PlayersCombatGear")));
        if (savedGear)
        {
            Assert.Equal(17U, owner.CombatGear!.PirateExp);
            Assert.Equal(19U, owner.CombatGear.AirPowers.Points);
        }
        else Assert.Same(CombatGearSnapshot.Empty, owner.CombatGear);

        await fixture.FrameAsync(MailboxPackageRequest());
        AssertMailboxPackageRefused(fixture, before, owner, json, "mailboxClaimPackageOwnersUnresolved");
        await fixture.FrameAsync(MailboxPackageRequest());
        AssertMailboxPackageRefused(fixture, before, owner, json, "mailboxClaimPackageOwnersUnresolved");

        // A genuine supported claim on this same entered owner catches a dirty
        // package/cache rejection without introducing a replacement session or save.
        await fixture.FrameAsync(MailboxMoneyRequest());
        AssertMailboxMoneyCommitted(fixture, before);
        AssertMailboxMoneyFrames(fixture, before, before.FrameCount);
        var afterMoney = fixture.CaptureTransferFrame();
        var current = fixture.ReadEnteredMailboxPackageSession();
        Assert.Equal(owner.PlayerItems, current.PlayerItems);
        Assert.Same(owner.CombatGear, current.CombatGear);
        Assert.Equal(owner.Character.Attributes.GetValueOrDefault("ClassExperience"),
            current.Character.Attributes.GetValueOrDefault("ClassExperience"));
        Assert.Equal(owner.Character.Attributes.GetValueOrDefault("NinjaPoints"),
            current.Character.Attributes.GetValueOrDefault("NinjaPoints"));
        var currentJson = JsonSerializer.Serialize(current);
        await fixture.FrameAsync(MailboxPackageRequest());
        AssertMailboxPackageRefused(fixture, afterMoney, current, currentJson,
            "mailboxClaimPackageOwnersUnresolved");
    });

    [Fact]
    public Task ActualD13StandaloneMailboxPackageFullBagStillRefusesWholeUnsupportedReward() => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(inventoryCount: 40, mixed: true, flag: 1, packageClass: 7051);
        var before = fixture.CaptureTransferFrame();
        var owner = fixture.ReadEnteredMailboxPackageSession();
        var json = JsonSerializer.Serialize(owner);

        await fixture.FrameAsync(MailboxPackageRequest());

        AssertMailboxPackageRefused(fixture, before, owner, json,
            "mailboxClaimPackageOwnersUnresolved");
        await fixture.FrameAsync(MailboxMoneyRequest());
        AssertMailboxMoneyCommitted(fixture, before);
        AssertMailboxMoneyFrames(fixture, before, before.FrameCount);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualD13StandaloneMailboxPackageMissingOrZeroFlagPublishesNoAcknowledgement(bool missing) => RunAsync(async fixture =>
    {
        await fixture.EnterAtomicMailFrameAsync(mixed: true, flag: missing ? 1U : 0U);
        var before = fixture.CaptureTransferFrame();
        var owner = fixture.ReadEnteredMailboxPackageSession();
        var json = JsonSerializer.Serialize(owner);

        await fixture.FrameAsync(MailboxPackageRequest(missing ? 999U : AtomicMailId));

        AssertMailboxPackageRefused(fixture, before, owner, json,
            missing ? "mailboxClaimMailUnavailable" : "mailboxClaimPackageEmpty");
        await fixture.FrameAsync(MailboxMoneyRequest());
        AssertMailboxMoneyCommitted(fixture, before);
        AssertMailboxMoneyFrames(fixture, before, before.FrameCount);
    });

    private static byte[] MailboxPackageRequest(uint id = AtomicMailId) =>
        new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, id, []).ToFrameBytes();

    private static void AssertMailboxPackageRefused(Fixture fixture, Snapshot before,
        WorldSession owner, string json, string reason)
    {
        var current = fixture.ReadEnteredMailboxPackageSession();
        Assert.Same(owner, current);
        Assert.Equal(json, JsonSerializer.Serialize(current));
        fixture.AssertFiles(before.Files);
        Assert.Equal(before.FrameCount, fixture.Sent.Count);
        Assert.Equal(fixture.AtomicMailSeed.Flag,
            Assert.Single(new LegacyMailboxStore(fixture.Root).Load(Fixture.Uid), row => row.Id == AtomicMailId).Flag);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        Assert.Contains(reason, fixture.Log.ToString());
    }

    private sealed partial class Fixture
    {
        internal WorldSession ReadEnteredMailboxPackageSession()
        {
            // Observe only the Store constructed by the actual Frame handler.
            // No reflection writes, owner injection, Store.Set or fabricated readiness.
            Assert.NotNull(frameOwner);
            var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var store = Assert.IsType<WorldSessionStore>(field!.GetValue(frameOwner));
            Assert.True(store.TryGet(Connection, out var current));
            return Assert.IsType<WorldSession>(current);
        }
    }
}
