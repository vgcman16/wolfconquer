using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task RootGhostReadsBodyFaceAfterStatusFanoutCapturesMeshAndReadsMapAfterMeshFanout()
    {
        var armed = false;
        var phases = new ConcurrentQueue<(uint Recipient, string Phase)>();
        await using var fixture = new RootMagicEntryFixture(
            itemEffects: new(RightEffect: 0), itemEffectLifetimeControls: true,
            nativeDeathCasterQueryControls: true,
            onSend: (current, uid, bytes) =>
            {
                if (!armed || RootGhostPhaseKind(bytes) is not { } phase)
                    return ValueTask.CompletedTask;
                phases.Enqueue((uid, phase));
                if (phase == "Status" && uid == PlayerUid)
                {
                    Assert.True(HasLegacyStatusFlag(current.Owner.Character.Attributes, 10));
                    current.ChangeRootGhostRawInput(PlayerUid, value => value with
                        { Character = value.Character with { Body = 1_007, Face = 154 } });
                }
                else if (phase == "Status" && uid == TargetPlayerUid)
                {
                    Assert.Equal(1_007L, current.Owner.Character.Body);
                    current.ChangeRootGhostRawInput(PlayerUid, value => value with
                        { Character = value.Character with { Body = 1_003, Face = 23 } });
                }
                else if (phase == "Mesh" && uid == PlayerUid)
                {
                    Assert.Equal("98", current.Owner.Character.Attributes["TransformationID"]);
                    Assert.Equal(980_231_003UL, Assert.Single(
                        Assert.Single(ParseMsgUpdates([bytes])).Updates).Value);
                    current.ChangeRootGhostRawInput(PlayerUid, value => value with
                        { Character = value.Character with { Body = 1_007, Face = 154 } });
                }
                else if (phase == "Mesh" && uid == TargetPlayerUid)
                {
                    Assert.Equal(1_007L, current.Owner.Character.Body);
                    Assert.Equal(154L, current.Owner.Character.Face);
                    Assert.Equal(980_231_003UL, Assert.Single(
                        Assert.Single(ParseMsgUpdates([bytes])).Updates).Value);
                    current.ChangeRootGhostRawInput(PlayerUid, value => value with
                        { Character = value.Character with { MapId = 1_002 } });
                }
                return ValueTask.CompletedTask;
            });
        await PrepareRootGhostPhaseAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
        await WaitUntilAsync(() => phases.Count == 5, TimeSpan.FromSeconds(2));
        armed = false;

        Assert.Equal(new[]
        {
            (PlayerUid, "Status"), (TargetPlayerUid, "Status"),
            (PlayerUid, "Mesh"), (TargetPlayerUid, "Mesh"), (PlayerUid, "Map"),
        }, phases.ToArray());
        var events = RootGhostPhaseEvents(fixture, start);
        Assert.Equal(5, events.Length);
        Assert.Equal(events[0].Bytes, events[1].Bytes);
        Assert.Equal(events[2].Bytes, events[3].Bytes); // Immutable captured Mesh through peer callback mutations.
        foreach (var entry in events.Take(2))
        {
            var flag = Assert.Single(Assert.Single(ParseMsgUpdates([entry.Bytes])).Updates);
            Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, flag.Type);
            Assert.Equal(18, Assert.IsAssignableFrom<IReadOnlyList<ulong>>(flag.Values).Count);
            Assert.Equal(1_056UL, flag.Values![0]);
            Assert.All(flag.Values.Skip(1), word => Assert.Equal(0UL, word));
        }
        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(events[4].Bytes), out var map));
        Assert.Equal(new LegacyMapStatusPacket(1_002, 1_002, 0), map);
        Assert.Equal(0L, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 10));
        Assert.Equal(1_007L, fixture.Owner.Character.Body);
        Assert.Equal(154L, fixture.Owner.Character.Face);
        Assert.Equal(1_002L, fixture.Owner.Character.MapId);
        Assert.Equal("98", fixture.Owner.Character.Attributes["TransformationID"]); // Assigned before later raw Body input.
    }

    [Theory]
    [InlineData("Status", false)]
    [InlineData("Status", true)]
    [InlineData("Mesh", false)]
    [InlineData("Mesh", true)]
    public async Task RootGhostOriginalOwnerLossAtEnteredStatusOrMeshStopsEveryOwedTail(
        string lossPhase, bool rebind)
    {
        var armed = false;
        var entered = false;
        var boundary = -1;
        WorldSession? replacement = null;
        await using var fixture = new RootMagicEntryFixture(
            itemEffects: new(RightEffect: 0), itemEffectLifetimeControls: true,
            nativeDeathCasterQueryControls: true,
            onSend: (current, uid, bytes) =>
            {
                if (!armed || entered || uid != PlayerUid || RootGhostPhaseKind(bytes) != lossPhase)
                    return ValueTask.CompletedTask;
                entered = true;
                boundary = current.Order.Count - 1;
                replacement = current.RetireRootGhostSubject(PlayerUid, rebind);
                return ValueTask.CompletedTask;
            });
        await PrepareRootGhostPhaseAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
        await WaitUntilAsync(() => entered, TimeSpan.FromSeconds(2));
        armed = false;

        Assert.True(boundary >= start);
        Assert.Empty(fixture.Order.ToArray().Skip(boundary + 1));
        var events = RootGhostPhaseEvents(fixture, start);
        Assert.Equal(lossPhase == "Status"
            ? new[] { (PlayerUid, "Status") }
            : new[] { (PlayerUid, "Status"), (TargetPlayerUid, "Status"), (PlayerUid, "Mesh") },
            events.Select(entry => (entry.Recipient, RootGhostPhaseKind(entry.Bytes)!)).ToArray());
        Assert.Equal(100_000L, fixture.Peer.Character.MinHitPoints);
        if (rebind)
        {
            Assert.Same(replacement, fixture.RootItemEffectSlot(PlayerUid));
            Assert.Equal((long)PlayerUid + 9_000,
                Assert.IsType<WorldSession>(replacement).Character.Uid);
        }
        else Assert.Null(fixture.RootItemEffectSlot(PlayerUid));
    }

    [Theory]
    [InlineData("Status", false)]
    [InlineData("Status", true)]
    [InlineData("Mesh", false)]
    [InlineData("Mesh", true)]
    public async Task RootGhostCapturedViewerLossSkipsOldIdentityWithoutCancellingOwnerTail(
        string lossPhase, bool rebind)
    {
        var armed = false;
        var entered = false;
        uint firstPeer = 0;
        uint retiredPeer = 0;
        WorldSession? replacement = null;
        ValueTask OnSend(RootMagicEntryFixture current, uint uid, byte[] bytes)
        {
            if (!armed || entered || uid == PlayerUid || RootGhostPhaseKind(bytes) != lossPhase)
                return ValueTask.CompletedTask;
            Assert.Contains(uid, new[] { TargetPlayerUid, RootGhostExtraViewerUid });
            entered = true;
            firstPeer = uid;
            retiredPeer = uid == TargetPlayerUid ? RootGhostExtraViewerUid : TargetPlayerUid;
            replacement = current.RetireRootGhostSubject(retiredPeer, rebind);
            return ValueTask.CompletedTask;
        }
        await using var fixture = new RootMagicEntryFixture(
            itemEffects: new(RightEffect: 0), itemEffectLifetimeControls: true,
            nativeDeathCasterQueryControls: true, onSend: OnSend);
        fixture.AddRootGhostViewer(RootGhostExtraViewerUid, OnSend);
        await PrepareRootGhostPhaseAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceRootItemEffectRuntimeAsync(TimeSpan.FromMilliseconds(20));
        await WaitUntilAsync(() => fixture.FramesSince(PlayerUid, start)
            .Any(bytes => RootGhostPhaseKind(bytes) == "Map"), TimeSpan.FromSeconds(2));
        armed = false;

        Assert.True(entered);
        Assert.NotEqual(firstPeer, retiredPeer);
        var events = RootGhostPhaseEvents(fixture, start);
        Assert.Equal(new[] { PlayerUid, firstPeer },
            events.Where(entry => RootGhostPhaseKind(entry.Bytes) == lossPhase)
                .Select(entry => entry.Recipient).ToArray());
        Assert.Equal(new[] { "Status", "Mesh", "Map" },
            events.Where(entry => entry.Recipient == PlayerUid)
                .Select(entry => RootGhostPhaseKind(entry.Bytes)).ToArray());
        if (lossPhase == "Status")
        {
            var expectedMeshRecipients = rebind
                ? new[] { PlayerUid, firstPeer, retiredPeer } : new[] { PlayerUid, firstPeer };
            Assert.Equal(expectedMeshRecipients.OrderBy(uid => uid),
                events.Where(entry => RootGhostPhaseKind(entry.Bytes) == "Mesh")
                    .Select(entry => entry.Recipient).OrderBy(uid => uid));
            Assert.Equal(rebind ? 6 : 5, events.Length);
            // A replacement visible at the NEXT fresh Mesh snapshot is a legitimate new viewer, not the old captured identity.
        }
        else
        {
            Assert.Equal(new[] { PlayerUid, TargetPlayerUid, RootGhostExtraViewerUid }.OrderBy(uid => uid),
                events.Where(entry => RootGhostPhaseKind(entry.Bytes) == "Status")
                    .Select(entry => entry.Recipient).OrderBy(uid => uid));
            Assert.Equal(6, events.Length);
        }
        var meshFrames = events.Where(entry => RootGhostPhaseKind(entry.Bytes) == "Mesh").ToArray();
        Assert.All(meshFrames, entry => Assert.Equal(meshFrames[0].Bytes, entry.Bytes));
        Assert.Equal(0L, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 10));
        Assert.Equal("98", fixture.Owner.Character.Attributes["TransformationID"]);
        Assert.Equal(1_000L, fixture.Owner.Character.MapId);
        if (rebind)
        {
            Assert.Same(replacement, fixture.RootItemEffectSlot(retiredPeer));
            Assert.Equal((long)retiredPeer + 9_000,
                Assert.IsType<WorldSession>(replacement).Character.Uid);
            Assert.Equal(100_000L, replacement!.Character.MinHitPoints);
        }
        else Assert.Null(fixture.RootItemEffectSlot(retiredPeer));
    }
}

