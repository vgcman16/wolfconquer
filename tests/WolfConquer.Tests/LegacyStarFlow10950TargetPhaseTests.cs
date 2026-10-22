using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task StarFlow10950PlayerGateAcquiresFlashingBeforeItsLiteralReceiveAndRetainsIncomingHit()
    {
        var armed = false;
        var entered = 0;
        var before = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                current.EnterStarFlowPlayerGateView();
                before = current.Peer.Character.MinHitPoints;
            }
            if (armed && recipient == PlayerUid && StarGateFlashVector(bytes))
            {
                entered++;
                Assert.Equal(before, current.Peer.Character.MinHitPoints);
                Assert.Equal("true", current.Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(0)]);
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, entered);
        var owner = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        var flash = Array.FindIndex(owner, StarGateFlashVector);
        var proc = Array.FindIndex(owner, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 10_950));
        Assert.True(flash >= 0 && proc > flash);
        var flashOwner = Assert.Single(owner, StarGateFlashVector);
        Assert.Equal(flashOwner, Assert.Single(peer, StarGateFlashVector));
        Assert.Equal(300U, Assert.Single(Assert.Single(ParseSpellAnimations(owner),
            packet => packet.SpellId == 10_950).Targets).Damage);
        Assert.Equal(before - 300 - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Fact]
    public async Task StarFlow10950RealImmortalForceDenialPublishesOwnerForbiddenPirateBeforeEmptyProc()
    {
        var armed = false;
        var before = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: async (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                current.EnterStarFlowPlayerGateView();
                before = current.Peer.Character.MinHitPoints;
                await current.CastPeerImmortalForceAsync();
                Assert.True(HasLegacyStatusFlag(current.Peer.Character.Attributes, 0xDC));
            }
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        var owner = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        Assert.Single(owner, StarGateForbidden);
        Assert.DoesNotContain(peer, StarGateForbidden);
        var denied = Array.FindIndex(owner, StarGateForbidden);
        var proc = Array.FindIndex(owner, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 10_950));
        Assert.True(denied >= 0 && proc > denied);
        Assert.Empty(Assert.Single(ParseSpellAnimations(owner), packet => packet.SpellId == 10_950).Targets);
        Assert.Single(ParseSpellAnimations(owner), packet => packet.SpellId == 14_710);
        Assert.Equal(before - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Fact]
    public async Task StarFlow10950EquippedLearnedHellVortexActuallyCountersBeforeOuterDenial()
    {
        var armed = false;
        var before = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                current.EnterStarFlowPlayerGateView();
                current.EquipPeerHellVortexForGate();
                before = current.Peer.Character.MinHitPoints;
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        var frames = fixture.FramesSince(PlayerUid, start);
        var counter = Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == 16_370);
        Assert.Equal(TargetPlayerUid, counter.AttackerUid);
        Assert.Equal((ushort)0, counter.X);
        Assert.Equal((ushort)0, counter.Y);
        var hit = Assert.Single(counter.Targets, target => target.Uid == PlayerUid);
        Assert.True(hit.Damage > 0);
        Assert.Equal(5_000L - hit.Damage, fixture.Owner.Character.MinHitPoints);
        Assert.Empty(Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == 10_950).Targets);
        Assert.True(Array.FindIndex(frames, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 16_370)) <
            Array.FindIndex(frames, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 10_950)));
        Assert.Equal(before - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.DoesNotContain(frames, StarGateForbidden);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Fact]
    public async Task StarFlow10950CapturedRadiusExcludesImmortalForceBeforeAnyGateSideEffect()
    {
        var armed = false;
        var before = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: async (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                current.EnterStarFlowPlayerGateView();
                await current.CastPeerImmortalForceAsync();
                current.EquipPeerHellVortexForGate();
                current.ChangePeerDuringArrowSend(value => value with { Character = value.Character with { X = 835, Y = 584 } });
                before = current.Peer.Character.MinHitPoints;
            }
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(5L, Math.Abs(fixture.Peer.Character.X - 830));
        var frames = fixture.FramesSince(PlayerUid, start);
        Assert.DoesNotContain(frames, StarGateForbidden);
        Assert.DoesNotContain(ParseSpellAnimations(frames), packet => packet.SpellId == 16_370);
        Assert.DoesNotContain(frames, StarGateFlashVector);
        Assert.Empty(Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == 10_950).Targets);
        Assert.Equal(before - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Fact]
    public async Task StarFlow10950OrdinaryFinalMapBlockStillOwesEarlierFlashingVector()
    {
        var armed = false;
        var before = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                current.EnterStarFlowPlayerGateView();
                current.ChangeOwnerDuringArrowSend(value => value with { Character = value.Character with { MapId = 1_008 } });
                current.ChangePeerDuringArrowSend(value => value with { Character = value.Character with { MapId = 1_008 } });
                before = current.Peer.Character.MinHitPoints;
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        var frames = fixture.FramesSince(PlayerUid, start);
        Assert.Single(frames, StarGateFlashVector);
        Assert.Empty(Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == 10_950).Targets);
        Assert.True(Array.FindIndex(frames, StarGateFlashVector) <
            Array.FindIndex(frames, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 10_950)));
        Assert.Equal(before - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1008)]
    public async Task StarFlow10950JiangEarlyAdmissionSkipsFlashingAndFinalDynamicBlock(int dynamicId)
    {
        var armed = false;
        var before = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                current.EnterStarFlowPlayerGateView();
                current.ChangeOwnerDuringArrowSend(value => value with { Character = value.Character with
                { Attributes = new Dictionary<string, string>(value.Character.Attributes)
                    { ["DynamicID"] = dynamicId.ToString(System.Globalization.CultureInfo.InvariantCulture), ["PkMode"] = "6" } } });
                current.ChangePeerDuringArrowSend(value => value with { Character = value.Character with
                { Attributes = new Dictionary<string, string>(value.Character.Attributes)
                    { ["DynamicID"] = dynamicId.ToString(System.Globalization.CultureInfo.InvariantCulture), ["JiangHuActive"] = "true" } } });
                before = current.Peer.Character.MinHitPoints;
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        var frames = fixture.FramesSince(PlayerUid, start);
        Assert.DoesNotContain(frames, StarGateFlashVector);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0));
        Assert.Equal(300U, Assert.Single(Assert.Single(ParseSpellAnimations(frames),
            packet => packet.SpellId == 10_950).Targets).Damage);
        Assert.Equal(before - 300 - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Fact]
    public async Task StarFlow10950FlashCallbackCarriesSameUidHpManaAndPositionWithoutRecenteringOrRegating()
    {
        var armed = false;
        var entered = 0;
        var before = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                current.EnterStarFlowPlayerGateView();
                before = current.Peer.Character.MinHitPoints;
            }
            if (armed && recipient == PlayerUid && StarGateFlashVector(bytes))
            {
                entered++;
                current.ChangeOwnerDuringArrowSend(value => value with
                { Character = value.Character with { MinHitPoints = 0, MinMana = 77, X = 850, Y = 600 } });
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, entered);
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal(850, fixture.Owner.Character.X);
        Assert.Equal(600, fixture.Owner.Character.Y);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        var animation = Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)),
            packet => packet.SpellId == 10_950);
        Assert.Equal((ushort)830, animation.X);
        Assert.Equal((ushort)584, animation.Y);
        Assert.Equal(300U, Assert.Single(animation.Targets).Damage);
        Assert.Equal(before - 300 - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StarFlow10950FlashingOwnerLossStopsPeerVectorAndAllNewOuterWork(bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        var boundary = -1;
        var before = 0L;
        WorldSession? replacement = null;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                current.EnterStarFlowPlayerGateView();
                before = current.Peer.Character.MinHitPoints;
            }
            if (armed && recipient == PlayerUid && StarGateFlashVector(bytes))
            {
                callbacks++;
                if (rebind) replacement = current.RebindOwner(); else current.RemoveOwner();
                boundary = current.Order.Count;
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, callbacks);
        Assert.True(boundary > start);
        Assert.Equal(before, fixture.Peer.Character.MinHitPoints);
        Assert.DoesNotContain(fixture.FramesSince(TargetPlayerUid, boundary), StarGateFlashVector);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(uid, boundary);
            Assert.DoesNotContain(ParseSpellAnimations(frames), packet => packet.SpellId is 10_950 or 1_000);
            Assert.DoesNotContain(ParseMsgUpdates(frames), packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        }
        if (rebind)
        {
            Assert.Same(replacement, fixture.OwnerSlot);
            Assert.Equal(77_777, fixture.OwnerSlot!.Character.MinHitPoints);
            Assert.Equal(77, fixture.OwnerSlot.Character.MinMana);
            Assert.False(HasLegacyStatusFlag(fixture.OwnerSlot.Character.Attributes, 0));
        }
        else Assert.Null(fixture.OwnerSlot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StarFlow10950FlashingTargetLossSkipsLostVictimWithoutTouchingReplacement(bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        var boundary = -1;
        WorldSession? replacement = null;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
                current.EnterStarFlowPlayerGateView();
            if (armed && recipient == PlayerUid && StarGateFlashVector(bytes))
            {
                callbacks++;
                if (rebind) replacement = current.RebindPeer(); else current.RemovePeer();
                boundary = current.Order.Count;
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, callbacks);
        Assert.True(boundary >= 0);
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        if (rebind)
        {
            Assert.Same(replacement, fixture.PeerSlot);
            Assert.Equal(88_888, fixture.PeerSlot!.Character.MinHitPoints);
            Assert.Equal(88, fixture.PeerSlot.Character.MinMana);
            Assert.False(HasLegacyStatusFlag(fixture.PeerSlot.Character.Attributes, 0));
        }
        else Assert.Null(fixture.PeerSlot);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(uid, boundary);
            Assert.DoesNotContain(ParseMsgUpdates(frames), packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
            Assert.DoesNotContain(ParseSpellAnimations(frames), packet => packet.Targets.Any(target =>
                target.Uid == TargetPlayerUid || target.Uid == TargetPlayerUid + 9_000));
        }
        // Captured owner0 vector may legitimately reach a freshly visible replacement peer.
    }

    private static bool StarGateFlashVector(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && IsLegacyStatusFlagSet(values, 0));
    private static bool StarGateForbidden(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ForbiddenPirate && entry.Value == 1);
}
