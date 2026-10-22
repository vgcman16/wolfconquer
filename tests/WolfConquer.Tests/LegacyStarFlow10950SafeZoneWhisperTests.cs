using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(10137, 144, 584, true)]
    [InlineData(10137, 145, 584, false)]
    [InlineData(10250, 1099, 1168, true)]
    [InlineData(10250, 1099, 1167, false)]
    [InlineData(10250, 1100, 1167, false)]
    public async Task StarFlow10950SafeZoneUsesBothNativeMapBoundariesAndOwnerRedWhisper(
        int mapId, int targetX, int targetY, bool denied)
    {
        var armed = false;
        var before = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                StarSafePlace(current, mapId, targetX, targetY);
                before = current.Peer.Character.MinHitPoints;
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        var owner = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        var proc = Assert.Single(ParseSpellAnimations(owner), packet => packet.SpellId == 10_950);
        Assert.Equal((ushort)(targetX - 2), proc.X);
        Assert.Equal((ushort)targetY, proc.Y);
        Assert.DoesNotContain(peer, StarSafeWhisper);
        if (denied)
        {
            StarSafeAssertExactWhisper(Assert.Single(owner, StarSafeWhisper));
            Assert.Empty(proc.Targets);
            Assert.DoesNotContain(owner, StarGateFlashVector);
            Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0));
            Assert.True(Array.FindIndex(owner, StarSafeWhisper) <
                Array.FindIndex(owner, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 10_950)));
        }
        else
        {
            Assert.DoesNotContain(owner, StarSafeWhisper);
            Assert.Equal(300U, Assert.Single(proc.Targets).Damage);
        }
        Assert.Equal(before - (denied ? 0 : 300) - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StarFlow10950ForbiddenPirateAndHellVortexPrecedeSafeZoneWhisper(bool immortalForce)
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
                if (immortalForce)
                {
                    await current.CastPeerImmortalForceAsync();
                    Assert.True(HasLegacyStatusFlag(current.Peer.Character.Attributes, 0xDC));
                }
                current.EquipPeerHellVortexForGate();
                StarSafePlace(current, 10_137, 144, 584);
                if (!immortalForce)
                    current.ChangeOwnerDuringArrowSend(value => value with
                    { Character = value.Character with { X = 146 } }); // Reverse16370 victim is OUTSIDE X<145.
                before = current.Peer.Character.MinHitPoints;
            }
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        var owner = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        Assert.DoesNotContain(owner, StarSafeWhisper);
        Assert.DoesNotContain(peer, StarSafeWhisper);
        Assert.DoesNotContain(owner, StarGateFlashVector);
        Assert.Empty(Assert.Single(ParseSpellAnimations(owner), packet => packet.SpellId == 10_950).Targets);
        if (immortalForce)
        {
            Assert.Single(owner, StarGateForbidden);
            Assert.DoesNotContain(peer, StarGateForbidden);
            Assert.DoesNotContain(ParseSpellAnimations(owner), packet => packet.SpellId == 16_370);
            Assert.True(Array.FindIndex(owner, StarGateForbidden) <
                Array.FindIndex(owner, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 10_950)));
        }
        else
        {
            Assert.DoesNotContain(owner, StarGateForbidden);
            var counter = Assert.Single(ParseSpellAnimations(owner), packet => packet.SpellId == 16_370);
            Assert.Equal(TargetPlayerUid, counter.AttackerUid);
            var counterHit = Assert.Single(counter.Targets, target => target.Uid == PlayerUid);
            Assert.True(counterHit.Damage > 0);
            Assert.Equal(5_000L - counterHit.Damage, fixture.Owner.Character.MinHitPoints);
            Assert.True(Array.FindIndex(owner, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 16_370)) <
                Array.FindIndex(owner, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 10_950)));
        }
        Assert.Equal(before - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task StarFlow10950SafeWhisperRefreshesOriginalOwnerAndTargetAfterItsAwait(
        bool ownerLoss, bool rebind)
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
                StarSafePlace(current, 10_250, 1_099, 1_168);
                before = current.Peer.Character.MinHitPoints;
            }
            if (armed && recipient == PlayerUid && StarSafeWhisper(bytes))
            {
                callbacks++;
                StarSafeAssertExactWhisper(bytes);
                if (ownerLoss)
                {
                    if (rebind) replacement = current.RebindOwner(); else current.RemoveOwner();
                }
                else
                {
                    if (rebind) replacement = current.RebindPeer(); else current.RemovePeer();
                }
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
        Assert.Single(fixture.FramesSince(PlayerUid, start), StarSafeWhisper);
        Assert.DoesNotContain(fixture.FramesSince(TargetPlayerUid, start), StarSafeWhisper);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(uid, boundary);
            Assert.DoesNotContain(ParseMsgUpdates(frames), packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
            Assert.DoesNotContain(ParseSpellAnimations(frames), packet => packet.Targets.Any(target =>
                target.Uid == TargetPlayerUid || target.Uid == TargetPlayerUid + 9_000));
            if (ownerLoss)
                Assert.DoesNotContain(ParseSpellAnimations(frames), packet => packet.SpellId is 10_950 or 1_000);
        }
        if (ownerLoss)
        {
            Assert.Equal(before, fixture.Peer.Character.MinHitPoints);
            if (rebind)
            {
                Assert.Same(replacement, fixture.OwnerSlot);
                Assert.Equal(77_777, fixture.OwnerSlot!.Character.MinHitPoints);
                Assert.Equal(77, fixture.OwnerSlot.Character.MinMana);
            }
            else Assert.Null(fixture.OwnerSlot);
        }
        else
        {
            Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
            Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
            if (rebind)
            {
                Assert.Same(replacement, fixture.PeerSlot);
                Assert.Equal(88_888, fixture.PeerSlot!.Character.MinHitPoints);
                Assert.Equal(88, fixture.PeerSlot.Character.MinMana);
            }
            else Assert.Null(fixture.PeerSlot);
        }
    }

    [Fact]
    public async Task StarFlow10950SafeWhisperCarriesSameUidChangesWithoutRetryingDeniedTarget()
    {
        var armed = false;
        var callbacks = 0;
        var before = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
            {
                StarSafePlace(current, 10_137, 144, 584);
                before = current.Peer.Character.MinHitPoints;
            }
            if (armed && recipient == PlayerUid && StarSafeWhisper(bytes))
            {
                callbacks++;
                current.ChangeOwnerDuringArrowSend(value => value with
                { Character = value.Character with { MinHitPoints = 0, MinMana = 77, X = 150, Y = 600 } });
                current.ChangePeerDuringArrowSend(value => value with
                { Character = value.Character with { X = 152, Y = 600 } });
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, callbacks);
        var owner = fixture.FramesSince(PlayerUid, start);
        StarSafeAssertExactWhisper(Assert.Single(owner, StarSafeWhisper));
        Assert.DoesNotContain(fixture.FramesSince(TargetPlayerUid, start), StarSafeWhisper);
        Assert.Empty(Assert.Single(ParseSpellAnimations(owner), packet => packet.SpellId == 10_950).Targets);
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal(150, fixture.Owner.Character.X);
        Assert.Equal(600, fixture.Owner.Character.Y);
        Assert.Equal(152, fixture.Peer.Character.X);
        Assert.Equal(600, fixture.Peer.Character.Y);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0));
        Assert.Equal(before - 9_001, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    private static void StarSafePlace(ArrowLifecycleFixture fixture, int mapId, int targetX, int targetY)
    {
        fixture.EnterStarFlowPlayerGateView();
        fixture.ChangeOwnerDuringArrowSend(value => value with
        { Character = value.Character with { MapId = mapId, X = targetX - 2, Y = targetY } });
        fixture.ChangePeerDuringArrowSend(value => value with
        { Character = value.Character with { MapId = mapId, X = targetX, Y = targetY } });
    }

    private static bool StarSafeWhisper(byte[] bytes) =>
        LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.ChatType == 2_001 && packet.Content[3] == "You can't PK in the safe zone.";

    private static void StarSafeAssertExactWhisper(byte[] bytes)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(16_711_680U, packet.Color);
        Assert.Equal(2_001U, packet.ChatType);
        Assert.Equal(0U, packet.MessageUid1);
        Assert.Equal(0U, packet.MessageUid2);
        Assert.Equal(0U, packet.ServerId);
        Assert.Equal(0U, packet.Mesh);
        Assert.Equal(0U, packet.UnionType);
        Assert.Equal(1U, packet.Count);
        Assert.Equal(0U, packet.New1);
        Assert.Equal(0U, packet.New2);
        Assert.Equal(new[] { "SYSTEM", "ALL", "", "You can't PK in the safe zone.", "", "", "" }, packet.Content);
        var expected = new LegacyMsgMessagePacket(16_711_680, 2_001, 0, 0, 0, 0, 0, 1, 0, 0,
            ["SYSTEM", "ALL", "", "You can't PK in the safe zone.", "", "", ""]);
        Assert.Equal(expected.ToFrameBytes(), bytes);
        Assert.Equal((ushort)2_179, ReadFrame(bytes).Header.Opcode);
    }
}
