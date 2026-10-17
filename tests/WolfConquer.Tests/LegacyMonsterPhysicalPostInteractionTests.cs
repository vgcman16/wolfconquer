using System.Globalization;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task MonsterPhysical2135PrecedesNormalDeadQueryHpZeroAndDeadVector()
    {
        var armed = false;
        var incoming = 0;
        var talks = 0;
        var queries = 0;
        using var fixture = new MonsterPostInteractionFixture(onSend: (current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid) return ValueTask.CompletedTask;
            if (MonsterPostIncoming(bytes))
            {
                incoming++;
                Assert.Equal(300, current.Owner.Character.MinHitPoints);
                MonsterPostAssertNoDeath(current.Owner);
            }
            if (DirectNormalDeathTalk(bytes))
            {
                talks++;
                Assert.Equal(current.Origin.AddSeconds(1),
                    DirectNormalDeathTimestamp(current.Owner, LegacyPlayerDeathState.GhostStampAttribute));
                Assert.False(current.Owner.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
                current.Advance(TimeSpan.FromSeconds(2));
            }
            if (LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _))
            {
                queries++;
                Assert.Equal(300, current.Owner.Character.MinHitPoints);
                Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, 5));
                Assert.Equal(current.Origin.AddSeconds(2),
                    DirectNormalDeathTimestamp(current.Owner, LegacyPlayerDeathState.DeadStampAttribute));
                current.Advance(TimeSpan.FromSeconds(5));
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync(500);
        await fixture.JoinAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(1, incoming);
        Assert.Equal(1, talks);
        Assert.Equal(1, queries);
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        var owner = fixture.OwnerFramesSince(start);
        var physical = AssertSinglePhysicalAttack(owner, TargetPlayerUid).Query;
        Assert.Equal(400, physical.Damage);
        Assert.Equal((ushort)830, physical.X);
        Assert.Equal((ushort)584, physical.Y);
        var clear = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            DirectNormalDeathVector(frame.Bytes, false));
        var query = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            LegacyShowEquipmentPacket.TryParse(ReadFrame(frame.Bytes), out _));
        var dead = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            DirectNormalDeathVector(frame.Bytes, true));
        Assert.True(clear >= 0 && query > clear && dead > query);
        DirectNormalDeathAssertPairs(frames, [(500U, 300U), (500U, 300U), (500U, 300U), (500U, 0U)]);
        var hp = frames.Select((frame, index) => (frame, index)).Where(item =>
            item.frame.Recipient == TargetPlayerUid && DirectNormalDeathIsHp(item.frame.Bytes)).ToArray();
        Assert.Equal(8, hp.Length);
        Assert.True(hp[0].index > query && hp[^1].index < dead);
        var zeroTeam = frames.Select((frame, index) => (frame, index)).Where(item =>
            LegacyTeamMemberInfoPacket.TryParse(ReadFrame(item.frame.Bytes), out var team) &&
            team.Count == 1 && team.Entries.Count == 1 && team.Entries[0].Uid == TargetPlayerUid &&
            team.Entries[0].MaxHitpoints == 500 && team.Entries[0].MinHitpoints == 0).ToArray();
        Assert.Equal(2, zeroTeam.Length);
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), zeroTeam.Select(item => item.frame.Recipient).Order());
        Assert.All(zeroTeam, item => Assert.True(item.index > hp[5].index && item.index < hp[6].index));
        var death = Assert.Single(ParseDeathAttacks(owner)).Query;
        Assert.Equal(400_001U, death.Uid);
        Assert.Equal(TargetPlayerUid, death.OpponentUid);
        Assert.Equal((ushort)830, death.X);
        Assert.Equal((ushort)584, death.Y);
        Assert.True(Array.FindIndex(owner, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Death) >
            Array.FindIndex(owner, bytes => DirectNormalDeathVector(bytes, true)));
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.Equal("400001", fixture.Owner.Character.Attributes[LegacyPlayerDeathState.DeadKillerUidAttribute]);
        Assert.Equal(fixture.Origin.AddSeconds(2),
            DirectNormalDeathTimestamp(fixture.Owner, LegacyPlayerDeathState.DeadStampAttribute));
    }

    [Theory]
    [InlineData(300L, 1_000L, false)]
    [InlineData(1_000L, 300L, true)]
    public async Task MonsterPhysical2135CallbackChoosesFreshHpAndDeathCoordinatesAfterInteraction(
        long initialHp, long callbackHp, bool lethal)
    {
        var armed = false;
        var callbacks = 0;
        using var fixture = new MonsterPostInteractionFixture(initialHp, maximum: 5_000,
            onSend: (current, recipient, bytes) =>
            {
                if (armed && recipient == TargetPlayerUid && MonsterPostIncoming(bytes))
                {
                    Assert.Equal(initialHp, current.Owner.Character.MinHitPoints);
                    MonsterPostAssertNoDeath(current.Owner);
                    callbacks++;
                    current.ChangeVitals(callbackHp);
                }
                return ValueTask.CompletedTask;
            });
        await fixture.PrimeAsync(5_000);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(1, callbacks);
        var frames = fixture.OwnerFramesSince(start);
        var incoming = AssertSinglePhysicalAttack(frames, TargetPlayerUid).Query;
        Assert.Equal(400, incoming.Damage);
        Assert.Equal((ushort)830, incoming.X); // Frame was captured before callback movement.
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal(840, fixture.Owner.Character.X); // No range/PK/admission replay after the await.
        Assert.Equal(585, fixture.Owner.Character.Y);
        if (lethal)
        {
            Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
            var death = Assert.Single(ParseDeathAttacks(frames)).Query;
            Assert.Equal((ushort)840, death.X);
            Assert.Equal((ushort)585, death.Y);
            Assert.Equal("840", fixture.Owner.Character.Attributes["Dead_X"]);
            Assert.Equal("585", fixture.Owner.Character.Attributes["Dead_Y"]);
        }
        else
        {
            Assert.Equal(600, fixture.Owner.Character.MinHitPoints);
            Assert.Empty(ParseDeathAttacks(frames));
            MonsterPostAssertNoDeath(fixture.Owner);
            DirectNormalDeathAssertPairs(fixture.Order.ToArray().Skip(start), [(5_000U, 600U)]);
            Assert.DoesNotContain(frames, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        }
    }

    [Fact]
    public async Task MonsterPhysical2135CallbackCurrentHpZeroStopsSourcePostInteractionAliveGuard()
    {
        var armed = false;
        var callbackEnd = -1;
        using var fixture = new MonsterPostInteractionFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == TargetPlayerUid && MonsterPostIncoming(bytes))
            {
                Assert.Equal(300, current.Owner.Character.MinHitPoints);
                MonsterPostAssertNoDeath(current.Owner);
                current.ChangeVitals(0);
                callbackEnd = current.Order.Count;
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync(500);
        await fixture.SitAsync();
        fixture.SetActivePickBranchInput(); // ActivePick acquisition is unmodeled; explicit snapshot branch input.
        armed = true;
        await fixture.HitAsync();
        Assert.True(callbackEnd >= 0);
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.Equal("250", fixture.Owner.Character.Attributes["Action"]);
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal("1", fixture.Owner.Character.Attributes["ActivePick"]);
        MonsterPostAssertNoDeath(fixture.Owner);
        Assert.DoesNotContain(fixture.OwnerFramesSince(callbackEnd),
            bytes => DirectNormalDeathIsHp(bytes) || DirectNormalDeathTalk(bytes) ||
                LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _) ||
                MsgActionPacket.TryParse(ReadFrame(bytes), out var action) && action.Type == MsgActionType.ActionPick);
        Assert.Empty(ParseDeathAttacks(fixture.OwnerFramesSince(callbackEnd)));
        var all = fixture.Order.ToArray();
        Assert.Equal(TargetPlayerUid, all[callbackEnd - 1].Recipient);
        Assert.True(MonsterPostIncoming(all[callbackEnd - 1].Bytes));
        var owedPeer = Assert.Single(all.Skip(callbackEnd));
        Assert.Equal(PlayerUid, owedPeer.Recipient);
        Assert.True(MonsterPostIncoming(owedPeer.Bytes));
        Assert.Equal(all[callbackEnd - 1].Bytes, owedPeer.Bytes); // Captured original2135 still fans out before Alive is checked.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MonsterPhysical2135OriginalOwnerLossStopsPeerFanoutAndPostHitTail(bool rebind)
    {
        var armed = false;
        var callbackEnd = -1;
        WorldSession? replacement = null;
        using var fixture = new MonsterPostInteractionFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == TargetPlayerUid && MonsterPostIncoming(bytes))
            {
                Assert.Equal(300, current.Owner.Character.MinHitPoints);
                MonsterPostAssertNoDeath(current.Owner);
                if (rebind) replacement = current.RebindOwner();
                else current.RemoveOwner();
                callbackEnd = current.Order.Count;
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync(500);
        armed = true;
        await fixture.HitAsync();
        Assert.True(callbackEnd >= 0);
        Assert.Empty(fixture.Order.ToArray().Skip(callbackEnd)); // No stale peer2135/query/HP/death to either identity.
        if (rebind) Assert.Same(replacement, fixture.Owner);
        else Assert.False(fixture.TryOwner(out _));
    }

    [Fact]
    public async Task MonsterPhysicalSitStaminaSendPrecedesActionNoneAndFreshCurrentHpSetter()
    {
        var armed = false;
        var callbacks = 0;
        using var fixture = new MonsterPostInteractionFixture(1_000, maximum: 5_000,
            onSend: (current, recipient, bytes) =>
            {
                if (armed && recipient == TargetPlayerUid && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
                    packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina))
                {
                    callbacks++;
                    Assert.Equal("80", current.Owner.Character.Attributes["Stamina"]);
                    Assert.Equal("250", current.Owner.Character.Attributes["Action"]); // Source changes Action AFTER SendUpdate.
                    Assert.Equal(1_000, current.Owner.Character.MinHitPoints);
                    current.ChangeVitals(900);
                }
                return ValueTask.CompletedTask;
            });
        await fixture.PrimeAsync(5_000);
        await fixture.SitAsync(); // REAL public ChangeStance, not an Action attribute seed.
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(1, callbacks);
        Assert.Equal("0", fixture.Owner.Character.Attributes["Action"]);
        Assert.Equal("80", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(500, fixture.Owner.Character.MinHitPoints); // CURRENT900-400, not captured1000-400.
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        var frames = fixture.OwnerFramesSince(start);
        var stamina = Array.FindIndex(frames, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
        Assert.True(stamina > Array.FindIndex(frames, MonsterPostIncoming));
        Assert.True(Array.FindIndex(frames, DirectNormalDeathIsHp) > stamina);
        DirectNormalDeathAssertPairs(fixture.Order.ToArray().Skip(start), [(5_000U, 500U)]);
        Assert.Empty(ParseDeathAttacks(frames));
    }

    [Theory]
    [InlineData(1_000L)]
    [InlineData(0L)]
    public async Task MonsterPhysicalActivePickBranchRemovesPickBeforeFreshLethalChoice(long callbackHp)
    {
        var armed = false;
        var callbacks = 0;
        using var fixture = new MonsterPostInteractionFixture(maximum: 5_000, onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == TargetPlayerUid && MsgActionPacket.TryParse(ReadFrame(bytes), out var action) &&
                action.Type == MsgActionType.ActionPick)
            {
                callbacks++;
                Assert.Equal((ushort)2, action.Facing);
                Assert.Equal(0, action.RemainTime);
                Assert.Equal(new[] { "Interrupted" }, action.Strings);
                Assert.False(current.Owner.Character.Attributes.ContainsKey("ActivePick"));
                Assert.Equal(300, current.Owner.Character.MinHitPoints);
                MonsterPostAssertNoDeath(current.Owner);
                current.ChangeVitals(callbackHp);
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync(5_000);
        fixture.SetActivePickBranchInput(); // Explicit source-state input; no local public AddPick producer exists.
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(1, callbacks);
        if (callbackHp == 0)
        {
            // Source Alive was entered ONCE before RemovePick; do not add another alive gate here.
            Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
            Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
            Assert.Single(ParseDeathAttacks(fixture.OwnerFramesSince(start)));
            DirectNormalDeathAssertPairs(fixture.Order.ToArray().Skip(start),
                [(5_000U, 0U), (5_000U, 0U), (5_000U, 0U), (5_000U, 0U)]);
        }
        else
        {
            Assert.Equal(600, fixture.Owner.Character.MinHitPoints);
            MonsterPostAssertNoDeath(fixture.Owner);
            Assert.Empty(ParseDeathAttacks(fixture.OwnerFramesSince(start)));
            DirectNormalDeathAssertPairs(fixture.Order.ToArray().Skip(start), [(5_000U, 600U)]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MonsterPhysicalNormalDeadQueryOriginalOwnerLossStopsHpZeroAndDeadTail(bool rebind)
    {
        var armed = false;
        var callbackEnd = -1;
        WorldSession? replacement = null;
        using var fixture = new MonsterPostInteractionFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == TargetPlayerUid && LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _))
            {
                Assert.Equal(300, current.Owner.Character.MinHitPoints);
                Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, 5));
                Assert.True(current.Owner.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
                if (rebind) replacement = current.RebindOwner();
                else current.RemoveOwner();
                callbackEnd = current.Order.Count;
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync(500);
        armed = true;
        await fixture.HitAsync();
        Assert.True(callbackEnd >= 0);
        Assert.Empty(fixture.Order.ToArray().Skip(callbackEnd));
        if (rebind) Assert.Same(replacement, fixture.Owner);
        else Assert.False(fixture.TryOwner(out _));
    }

    [Fact]
    public async Task MonsterPhysicalLethalTransformedTargetFinishesAndQueriesBeforeGhostAndNormalDead()
    {
        var armed = false;
        var finishes = 0;
        using var fixture = new MonsterPostInteractionFixture(attack: 2_000, onSend: (current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid) return ValueTask.CompletedTask;
            if (MonsterPostIncoming(bytes))
            {
                Assert.Equal(3_000, current.Owner.Character.MinHitPoints);
                Assert.Equal("207", current.Owner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
                MonsterPostAssertNoDeath(current.Owner);
            }
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var action) && action.Type == MsgActionType.FinishTransform)
            {
                finishes++;
                Assert.Equal(500, current.Owner.Character.MinHitPoints);
                Assert.Equal("0", current.Owner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
                MonsterPostAssertNoDeath(current.Owner);
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync(500);
        await fixture.CastSelfAsync(1_350);
        Assert.Equal(3_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(3_000U, (await fixture.StatusAsync()).MaxHP);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(1, finishes);
        var frames = fixture.OwnerFramesSince(start);
        Assert.Equal(4_000, AssertSinglePhysicalAttack(frames, TargetPlayerUid).Query.Damage);
        Assert.Equal(2, frames.Count(bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _)));
        var finish = Array.FindIndex(frames, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out var action) &&
            action.Type == MsgActionType.FinishTransform);
        Assert.True(finish > Array.FindIndex(frames, MonsterPostIncoming));
        Assert.True(Array.FindIndex(frames, DirectNormalDeathTalk) > finish);
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.Equal(500U, (await fixture.StatusAsync()).MaxHP);
        Assert.Single(ParseDeathAttacks(frames));
    }

    [Fact]
    public async Task MonsterPhysicalNormalDeathClearsRealFineRainAndResetsActuallyEarnedCounteraction()
    {
        using var fixture = new MonsterPostInteractionFixture(mutableCounteraction: true);
        await fixture.PrimeAsync(500);
        await fixture.CastSelfAsync(14_500);
        Assert.Equal(501, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(501U, (await fixture.StatusAsync()).MaxHP);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 212));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 213));
        await fixture.EarnCounteractionAsync(); // Actual17490+explicitly synthetic eligible Imp9 rune, not a cached CA seed.
        Assert.Equal(75U, (await fixture.StatusAsync()).Counteraction);
        var start = fixture.Order.Count;
        await fixture.HitAsync();
        var frames = fixture.OwnerFramesSince(start);
        Assert.Equal(400, AssertSinglePhysicalAttack(frames, TargetPlayerUid).Query.Damage);
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 212));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 213));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyFineRain1ExpiresAtUtc"));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyFineRain2ExpiresAtUtc"));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(212)));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(213)));
        Assert.Equal("1", fixture.Owner.Character.Attributes["FineRainPower"]);
        Assert.Equal("1", fixture.Owner.Character.Attributes["defFineRainPower"]);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        var status = await fixture.StatusAsync();
        Assert.Equal(500U, status.MaxHP);
        Assert.Equal(60U, status.Breakthrough);
        Assert.Equal(50U, status.Counteraction);
        Assert.Single(frames, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        DirectNormalDeathAssertPairs(fixture.Order.ToArray().Skip(start),
            [(500U, 210U), (500U, 210U), (500U, 210U), (500U, 0U)]);
        Assert.Equal(0, fixture.ScheduledTimers); // Same-tick damage/query; no periodic212 scope claim.
    }

    private static bool MonsterPostIncoming(byte[] bytes) =>
        MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Query.Uid == 400_001 &&
        packet.Query.OpponentUid == TargetPlayerUid && packet.Query.AttackType == MsgAttackType.Physical;

    private static void MonsterPostAssertNoDeath(WorldSession owner)
    {
        Assert.False(HasLegacyStatusFlag(owner.Character.Attributes, 5));
        Assert.False(owner.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
        Assert.False(owner.Character.Attributes.ContainsKey(LegacyPlayerDeathState.GhostStampAttribute));
    }
}
