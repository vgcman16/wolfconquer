using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task RootManualEarnedNeckFifteenAndRingTwentyExpireAtInclusiveBoundaries()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true);
        await fixture.QueryRootItemEffectsAsync();
        await fixture.CastAsync(1_000);
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: true);
        Assert.Equal(fixture.Now.AddSeconds(15), RootManualLifetimeDeadline(fixture, 8));
        Assert.Equal(fixture.Now.AddSeconds(20), RootManualLifetimeDeadline(fixture, 9));
        await fixture.StartRootItemEffectRuntimeAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(14_980));
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: true);
        Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, start)));
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: false);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.Equal(new ulong[] { 512 }, RootManualLifetimeVectors(fixture.FramesSince(uid, start)));
        var ringStart = fixture.Order.Count;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(4_980));
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: false);
        Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, ringStart)));
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.Equal(new ulong[] { 0 }, RootManualLifetimeVectors(fixture.FramesSince(uid, ringStart)));
    }

    [Fact]
    public async Task RootManualTwoDueFlagsPublishTwoIndividualOwnerPeerVectorsNotOneAtomicClear()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true);
        await fixture.QueryRootItemEffectsAsync();
        await fixture.CastAsync(1_000);
        await fixture.StartRootItemEffectRuntimeAsync();
        var start = fixture.Order.Count;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(20));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        var own = RootManualLifetimeVectors(fixture.FramesSince(PlayerUid, start));
        var peer = RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, start));
        Assert.Equal(2, own.Length);
        Assert.Contains(own[0], new ulong[] { 256, 512 }); // No native dictionary priority is invented.
        Assert.Equal(0UL, own[1]);
        Assert.Equal(own, peer);
        var all = fixture.Order.ToArray().Skip(start)
            .Where(entry => RootManualItemBits(entry.Bytes) is not null).ToArray();
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid, PlayerUid, TargetPlayerUid },
            all.Select(entry => entry.Recipient).ToArray());
    }

    [Fact]
    public async Task RootManualAlreadyActiveProcDoesNotReplaceEarnedFifteenOrTwentySecondRegistration()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true);
        await fixture.QueryRootItemEffectsAsync();
        await fixture.CastAsync(1_000);
        var shieldDeadline = RootManualLifetimeDeadline(fixture, 8);
        var stigmaDeadline = RootManualLifetimeDeadline(fixture, 9);
        await fixture.StartRootItemEffectRuntimeAsync();
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(10));
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000); // A real later public input; not a timer or flag mutation.
        fixture.AssertProducedStamp(fixture.Now);
        Assert.Equal(shieldDeadline, RootManualLifetimeDeadline(fixture, 8));
        Assert.Equal(stigmaDeadline, RootManualLifetimeDeadline(fixture, 9));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(uid, start)),
                packet => packet.SpellId is 1_095 or 1_020);
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(5));
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: false); // Original W15, not W25.
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(5));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false); // Original W20, not W30.
        // Output cannot independently prove a consumed no-output rate draw; source still draws before presence.
    }

    [Fact]
    public async Task RootManualCompletedPrefixRealDeathRetiresBothFlagsWithoutFutureExpiryPublication()
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true,
            nativeDeathCasterQueryControls: true);
        await fixture.PrepareRootNativeDeathCasterAsync();
        await fixture.QueryRootItemEffectsAsync();
        await fixture.CastAsync(1_000);
        await fixture.StartRootItemEffectRuntimeAsync();
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(5_000L, fixture.Owner.Character.MinHitPoints);
        AssertRootManualLifetimeFlags(fixture, stigma: true, shield: true);
        Assert.Equal(fixture.Now.AddSeconds(10), RootManualLifetimeDeadline(fixture, 8)); // Earned at W0; original W15.
        Assert.Equal(fixture.Now.AddSeconds(15), RootManualLifetimeDeadline(fixture, 9)); // Earned at W0; original W20.
        await fixture.KillRootItemEffectOwnerAsync(); // Queried peer MA1 + raw9999, then native player /2 = lethal5000.
        Assert.Equal(0L, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        var start = fixture.Order.Count;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(15));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        Assert.Equal(0L, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 10));
        Assert.Equal(1_003L, fixture.Owner.Character.Body);
        Assert.Equal(0L, fixture.Owner.Character.Face);
        Assert.Equal("98", fixture.Owner.Character.Attributes["TransformationID"]);
        var own = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        // Native Ghost AddFlag vector, then TransformationID setter Mesh, then owner MapStatus.
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid, PlayerUid, TargetPlayerUid, PlayerUid },
            fixture.Order.ToArray().Skip(start).Select(entry => entry.Recipient).ToArray());
        Assert.Equal(new ushort[] { LegacyMsgUpdatePacket.Opcode, LegacyMsgUpdatePacket.Opcode,
                LegacyMapStatusPacket.Opcode },
            own.Select(bytes => ReadFrame(bytes).Header.Opcode).ToArray());
        Assert.Equal(new ushort[] { LegacyMsgUpdatePacket.Opcode, LegacyMsgUpdatePacket.Opcode },
            peer.Select(bytes => ReadFrame(bytes).Header.Opcode).ToArray());
        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(own[2]), out var map));
        Assert.Equal(1_000U, map.Id);
        Assert.Equal(1_000U, map.BaseId);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var updates = ParseMsgUpdates(fixture.FramesSince(uid, start));
            Assert.Equal(2, updates.Count);
            Assert.All(updates, packet => Assert.Equal(PlayerUid, packet.Uid));
            var flag = Assert.Single(updates[0].Updates);
            Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, flag.Type);
            Assert.NotNull(flag.Values);
            Assert.Equal(18, flag.Values.Count);
            Assert.Equal(1_056UL, flag.Values[0]); // FULL first word: Dead5 | Ghost10; no8/9 or unexpected bits.
            Assert.All(flag.Values.Skip(1), value => Assert.Equal(0UL, value));
            Assert.Equal(new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mesh, 980_001_003UL),
                Assert.Single(updates[1].Updates)); //98*10_000_000 + Face0*10_000 + Body1003.
            Assert.Equal(new ulong[] { 0 }, RootManualLifetimeVectors(fixture.FramesSince(uid, start)));
        } // Exact five owed frames: missing Mesh OR extra item-expiry traffic must fail.
        // Only completed-prefix death-before-capture. An already-entered XP1020 tail may still add8 after nested death.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootManualExpiryOriginalOwnerLossStopsPeerAndSecondCapturedFlagAfterEnteredOwnerVector(bool rebind)
    {
        var armed = false;
        var entered = false;
        var boundary = -1;
        WorldSession? replacement = null;
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true,
            onSend: (current, uid, bytes) =>
            {
                if (!armed || entered || uid != PlayerUid || RootManualItemBits(bytes) is not (256 or 512))
                    return ValueTask.CompletedTask;
                entered = true;
                boundary = current.Order.Count - 1;
                replacement = current.RetireRootItemEffectSubject(PlayerUid, rebind);
                return ValueTask.CompletedTask;
            });
        await fixture.QueryRootItemEffectsAsync();
        var initialPeerHitpoints = fixture.Peer.Character.MinHitPoints;
        Assert.Equal(100_000L, initialPeerHitpoints);
        await fixture.CastAsync(1_000);
        var postSetupPeerHitpoints = fixture.Peer.Character.MinHitPoints;
        Assert.True(postSetupPeerHitpoints > 0);
        Assert.True(postSetupPeerHitpoints < initialPeerHitpoints);
        await fixture.StartRootItemEffectRuntimeAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(20));
        armed = false;
        Assert.True(entered && boundary >= start);
        Assert.Empty(fixture.Order.ToArray().Skip(boundary + 1));
        Assert.Empty(RootManualLifetimeVectors(fixture.FramesSince(TargetPlayerUid, start)));
        Assert.Equal(postSetupPeerHitpoints, fixture.Peer.Character.MinHitPoints);
        if (rebind)
        {
            Assert.Same(replacement, fixture.RootItemEffectSlot(PlayerUid));
            Assert.Equal((long)PlayerUid + 9_000, Assert.IsType<WorldSession>(replacement).Character.Uid);
        }
        else Assert.Null(fixture.RootItemEffectSlot(PlayerUid));
    }

    [Fact]
    public async Task RootManualExpiryCallbackRealReinsertionOfProcessedFlagSurvivesOtherCapturedDueEntry()
    {
        var armed = false;
        var entered = false;
        var removedFirst = 0;
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200), itemEffectLifetimeControls: true,
            onSend: async (current, uid, bytes) =>
            {
                if (!armed || entered || uid != PlayerUid || RootManualItemBits(bytes) is not { } bits ||
                    bits is not (256 or 512)) return;
                entered = true;
                removedFirst = bits == 256 ? 9 : 8;
                await current.CastAsync(1_000); // Real prefix re-add of the just-removed key; current other key is still active.
            });
        await fixture.QueryRootItemEffectsAsync();
        await fixture.CastAsync(1_000);
        await fixture.StartRootItemEffectRuntimeAsync();
        armed = true;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromSeconds(20));
        armed = false;
        Assert.True(entered);
        Assert.Contains(removedFirst, new[] { 8, 9 });
        AssertRootManualLifetimeFlags(fixture, stigma: removedFirst == 9, shield: removedFirst == 8);
        var seconds = removedFirst == 9 ? 20 : 15;
        Assert.Equal(fixture.Now.AddSeconds(seconds), RootManualLifetimeDeadline(fixture, removedFirst));
        fixture.AssertProducedStamp(fixture.Now);
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(seconds * 1_000 - 20));
        AssertRootManualLifetimeFlags(fixture, stigma: removedFirst == 9, shield: removedFirst == 8);
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
        AssertRootManualLifetimeFlags(fixture, stigma: false, shield: false);
        // Captured bytes may be delivered after the nested sameUID input; no source generation cancellation is invented.
    }

    private static DateTimeOffset RootManualLifetimeDeadline(RootMagicEntryFixture fixture, int flag) =>
        DateTimeOffset.Parse(fixture.Owner.Character.Attributes[flag == 9
            ? WorldPacketHandlers.RuntimeLegacyStigmaExpiresAtUtcAttribute
            : WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute], CultureInfo.InvariantCulture);

    private static void AssertRootManualLifetimeFlags(RootMagicEntryFixture fixture, bool stigma, bool shield)
    {
        Assert.Equal(stigma, HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.Equal(shield, HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
    }

    private static ulong[] RootManualLifetimeVectors(IEnumerable<byte[]> frames) =>
        ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid).SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { Count: > 0 })
            .Select(entry => entry.Values![0] & 768UL).ToArray();
}
