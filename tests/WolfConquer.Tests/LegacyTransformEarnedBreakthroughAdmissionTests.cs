using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransformRetainsActuallyEarnedBreakthroughUntilFinishQueryAdmissionOwnsItsReset(bool admitFinish)
    {
        var armed = false;
        var incoming = 0;
        var finishes = 0;
        using var fixture = new MonsterPostInteractionFixture(attack: 2_000, mutableBreakthrough: true,
            onSend: async (current, recipient, bytes) =>
            {
                if (!armed) return;
                if (MonsterPostIncoming(bytes))
                {
                    incoming++;
                    Assert.Equal(3_000, current.Owner.Character.MinHitPoints);
                    Assert.Equal("207", current.Owner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
                    MonsterPostAssertNoDeath(current.Owner);
                }
                if (recipient != TargetPlayerUid ||
                    !MsgActionPacket.TryParse(ReadFrame(bytes), out var action) ||
                    action.Type != MsgActionType.FinishTransform) return;

                finishes++;
                Assert.Equal(TargetPlayerUid, action.ObjId);
                Assert.Equal("0", current.Owner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
                Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(current.Owner.Character, out _));
                Assert.Equal(admitFinish ? 501 : 3_000, current.Owner.Character.MinHitPoints);
                Assert.True(HasLegacyStatusFlag(current.Owner.Character.Attributes, 212));
                Assert.True(HasLegacyStatusFlag(current.Owner.Character.Attributes, 213));
                Assert.True(HasLegacyStatusFlag(current.Owner.Character.Attributes, 313)); // Query resets BT, not this live flag.
                MonsterPostAssertNoDeath(current.Owner); // Finish118 precedes GhostStamp/talk/normal Dead.

                // A read-only public status request INSIDE the real118 send
                // observes this Finish result before normal death's separate reset.
                var atFinish = await current.StatusAsync();
                Assert.Equal(50U, atFinish.Counteraction); // Raw CA control, NOT an earned-CA claim.
                Assert.Equal(admitFinish ? 60U : 66U, atFinish.Breakthrough);
                Assert.Equal(admitFinish ? 501U : 3_000U, atFinish.MaxHP);
                Assert.Equal(admitFinish ? 16_000 : 10_000,
                    LegacyFineRainEquipmentAdmission.ForOwner(current.Owner).StampMilliseconds);
            });
        await fixture.PrimeAsync(500);
        await fixture.CastSelfAsync(14_500); // Real producer creates flags, power1 and admission stamp10000.
        Assert.Equal(501, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        await fixture.EarnBreakthroughAsync(); // Real equipped201/learned17490/FrontBreak9: BT60->66 and313.
        var earned = await fixture.StatusAsync();
        Assert.Equal(50U, earned.Counteraction);
        Assert.Equal(66U, earned.Breakthrough);
        Assert.Equal(501U, earned.MaxHP);
        Assert.Equal(501, fixture.Owner.Character.MinHitPoints); // Outgoing real17490 did not hurt its caster.

        var entryStart = fixture.Order.Count;
        await fixture.CastSelfAsync(1_350);
        var entryFrames = fixture.Order.ToArray().Skip(entryStart).ToArray();
        var entryOwner = fixture.OwnerFramesSince(entryStart);
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.Owner.Character, out var transformed));
        Assert.Equal(3_000U, transformed);
        Assert.Equal(3_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal("207", fixture.Owner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
        var entered = await fixture.StatusAsync();
        Assert.Equal(50U, entered.Counteraction);
        Assert.Equal(66U, entered.Breakthrough); // CreateTransform has NO QueryEquipment/reset.
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 313));
        Assert.Equal(3_000U, entered.MaxHP);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.DoesNotContain(entryOwner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(entryOwner, DirectOverlayLifecycleIsWait);
        DirectNormalDeathAssertPairs(entryFrames, [(501U, 3_000U), (3_000U, 3_000U)]);
        var entrySpawn = Assert.Single(entryFrames, frame => frame.Recipient == PlayerUid &&
            TransformEarnedBtIsOwnerSpawn(frame.Bytes));
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(entrySpawn.Bytes), out var transformedSpawn));
        Assert.Equal(501UL, transformedSpawn.GetNumericField(21)); // Peer spawn precedes UpdateStatus.
        Assert.Equal(501UL, transformedSpawn.GetNumericField(23));
        var entryAnimation = Assert.Single(ParseSpellAnimations(entryOwner));
        Assert.Equal(1_350, entryAnimation.SpellId);
        Assert.Equal(60U, Assert.Single(entryAnimation.Targets).Damage);
        var entryHp = entryFrames.Select((frame, index) => (frame, index)).Where(item =>
            item.frame.Recipient == TargetPlayerUid && DirectNormalDeathIsHp(item.frame.Bytes)).ToArray();
        Assert.Equal(4, entryHp.Length);
        var entrySpawnIndex = Array.FindIndex(entryFrames, frame => frame.Recipient == PlayerUid &&
            TransformEarnedBtIsOwnerSpawn(frame.Bytes));
        var entryAnimationIndex = Array.FindIndex(entryFrames, frame => frame.Recipient == TargetPlayerUid &&
            LegacySpellAnimationPacket.TryParse(ReadFrame(frame.Bytes), out var animation) && animation.SpellId == 1_350);
        Assert.True(entrySpawnIndex >= 0 && entrySpawnIndex < entryHp[0].index);
        Assert.True(entryAnimationIndex > entryHp[^1].index);

        // Move only the real fixture clocks; no flag/stamp/cache or desired BT seed.
        // No runtime loop runs, so this is exactly the Finish admission control,
        // not a claim that real periodic212 invocations have happened.
        if (admitFinish) fixture.Advance(TimeSpan.FromSeconds(6));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync(); // Real4000 monster hit chooses lethal, then target Finish, then normal Dead.
        armed = false;
        Assert.Equal(2, incoming);
        Assert.Equal(1, finishes);
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        var owner = fixture.OwnerFramesSince(start);
        Assert.Equal(4_000, AssertSinglePhysicalAttack(owner, TargetPlayerUid).Query.Damage);
        var finish = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            MsgActionPacket.TryParse(ReadFrame(frame.Bytes), out var action) &&
            action.ObjId == TargetPlayerUid && action.Type == MsgActionType.FinishTransform);
        Assert.True(finish >= 0);
        var talk = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            DirectNormalDeathTalk(frame.Bytes));
        Assert.True(talk > finish);
        var beforeFinish = frames.Take(finish).ToArray();
        var ownerBeforeFinish = beforeFinish.Where(frame => frame.Recipient == TargetPlayerUid)
            .Select(frame => frame.Bytes).ToArray();
        Assert.Equal(admitFinish ? 1 : 0, ownerBeforeFinish.Count(bytes =>
            LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _)));
        Assert.Equal(admitFinish ? 0 : 1, ownerBeforeFinish.Count(DirectOverlayLifecycleIsWait));
        Assert.Equal(admitFinish ? 2 : 1, owner.Count(bytes =>
            LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _)));
        Assert.Equal(admitFinish ? 0 : 1, owner.Count(DirectOverlayLifecycleIsWait));
        var beforeFinishSpawns = beforeFinish.Where(frame => frame.Recipient == PlayerUid &&
            TransformEarnedBtIsOwnerSpawn(frame.Bytes)).ToArray();
        Assert.Equal(admitFinish ? 2 : 1, beforeFinishSpawns.Length); // Query appearance plus Finish's extra peer spawn.
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(beforeFinishSpawns[^1].Bytes), out var finalFinishSpawn));
        Assert.Equal(admitFinish ? 501UL : 3_000UL, finalFinishSpawn.GetNumericField(21));
        Assert.Equal(admitFinish ? 501UL : 3_000UL, finalFinishSpawn.GetNumericField(23));
        if (admitFinish)
        {
            DirectNormalDeathAssertPairs(beforeFinish, [(500U, 3_000U), (501U, 501U), (501U, 501U)]);
            DirectNormalDeathAssertPairs(frames,
                [(500U, 3_000U), (501U, 501U), (501U, 501U),
                 (500U, 501U), (500U, 500U), (500U, 500U), (500U, 0U)]);
        }
        else
        {
            Assert.DoesNotContain(ownerBeforeFinish, DirectNormalDeathIsHp);
            DirectNormalDeathAssertPairs(frames,
                [(500U, 3_000U), (500U, 500U), (500U, 500U), (500U, 0U)]);
        }

        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 212));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 213));
        var dead = await fixture.StatusAsync();
        Assert.Equal(50U, dead.Counteraction);
        Assert.Equal(60U, dead.Breakthrough); // Separate ClearFlags/query reset, even after rejected Finish.
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 313));
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).FrontBreakExpiresAtUtc);
        Assert.Equal(500U, dead.MaxHP);
        Assert.Equal(admitFinish ? 16_000 : 10_000,
            LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Single(ParseDeathAttacks(owner));
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    private static bool TransformEarnedBtIsOwnerSpawn(byte[] bytes) =>
        LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
        spawn.GetNumericField(2) == TargetPlayerUid;
}
