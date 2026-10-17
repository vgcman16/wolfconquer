using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task MonsterPhysicalAcceptedShieldZeroDamageStillPublishesOneTeamAndSelfSetter()
    {
        var armed = false;
        var incoming = 0;
        using var fixture = new MonsterPostInteractionFixture(1_000, maximum: 5_000, lightningShield: true,
            onSend: (current, _, bytes) =>
            {
                if (armed && MonsterPostIncoming(bytes))
                {
                    incoming++;
                    Assert.Equal(1_000, current.Owner.Character.MinHitPoints);
                    MonsterPostAssertNoDeath(current.Owner);
                }
                return ValueTask.CompletedTask;
            });
        await fixture.PrimeAsync(5_000);
        await fixture.JoinAsync(); // Actual public MsgTeam membership, not a store-only cache.
        await fixture.CastSelfAsync(15_750); // Earn base240 through the real status handler.
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 240));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 237));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LightningShieldLeft"));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(2, incoming); // Captured source response reaches owner and the visible peer.
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        var owner = fixture.OwnerFramesSince(start);
        var attack = AssertSinglePhysicalAttack(owner, TargetPlayerUid).Query;
        Assert.Equal(400_001U, attack.Uid);
        Assert.Equal(TargetPlayerUid, attack.OpponentUid);
        Assert.Equal((ushort)830, attack.X);
        Assert.Equal((ushort)584, attack.Y);
        Assert.Equal(0, attack.Damage); // Literal400 was entirely absorbed by newly earned20,000.
        var azure = ParseMsgUpdates(owner).Where(packet => packet.Uid == TargetPlayerUid)
            .SelectMany(packet => packet.Updates).Where(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield &&
                entry.Flag == 237).ToArray();
        Assert.Equal(new uint[] { 20_000, 19_600 }, azure.Select(entry => entry.Damage));
        Assert.Equal(new uint[] { 10, 10 }, azure.Select(entry => entry.Time));
        Assert.Equal("19600", fixture.Owner.Character.Attributes["LightningShieldLeft"]);
        MonsterPostResponseAssertOneSetter(frames, maximum: 5_000, hitpoints: 1_000);
        Assert.Equal(1_000, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 240));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 237));
        MonsterPostAssertNoDeath(fixture.Owner);
        Assert.Empty(ParseDeathAttacks(owner));
        Assert.Empty(ParseSpellAnimations(owner));
        Assert.DoesNotContain(owner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public async Task MonsterPhysicalLearnedCounterPunchFollowsCompletedTeamAndSelfHpSetter()
    {
        var armed = false;
        var incoming = 0;
        var counters = 0;
        var start = -1;
        using var fixture = new MonsterPostInteractionFixture(1_000, maximum: 5_000, counterPunch: true,
            onSend: (current, recipient, bytes) =>
            {
                if (!armed) return ValueTask.CompletedTask;
                if (MonsterPostIncoming(bytes))
                {
                    incoming++;
                    Assert.Equal(1_000, current.Owner.Character.MinHitPoints);
                    MonsterPostAssertNoDeath(current.Owner);
                }
                if (recipient == TargetPlayerUid &&
                    LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var spell) && spell.SpellId == 14_470)
                {
                    counters++;
                    Assert.Equal(600, current.Owner.Character.MinHitPoints);
                    Assert.Equal(4_993U, current.MonsterHitpoints);
                    Assert.Equal(2, current.OwnerFramesSince(start).Count(DirectNormalDeathIsHp));
                }
                return ValueTask.CompletedTask;
            });
        await fixture.PrimeAsync(5_000);
        var status = await fixture.StatusAsync();
        Assert.Equal(120, fixture.Owner.Character.Level);
        Assert.True(status.MaxAttack <= 25 && status.MinAttack <= 25);
        Assert.Equal(0U, status.CriticalStrike);
        Assert.Equal(0U, status.DashRate);
        Assert.Equal(0U, status.PhysicalDamageIncrease);
        Assert.Contains(fixture.Owner.PlayerSpells.Spells, spell => spell.Id == 14_470 && spell.Level == 0);
        Assert.Equal("0", fixture.Owner.Character.Attributes["LegacyMonsterCounterPunchRoll"]);
        await fixture.JoinAsync();
        start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(2, incoming);
        Assert.Equal(1, counters);
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        var owner = fixture.OwnerFramesSince(start);
        Assert.Equal(400, AssertSinglePhysicalAttack(owner, TargetPlayerUid).Query.Damage);
        MonsterPostResponseAssertOneSetter(frames, maximum: 5_000, hitpoints: 600);
        var counter = Assert.Single(ParseSpellAnimations(owner));
        Assert.Equal(14_470, counter.SpellId);
        Assert.Equal(400_001U, counter.AttackerUid);
        Assert.Equal(TargetPlayerUid, counter.AttackedUid);
        Assert.Equal((ushort)0, counter.X);
        Assert.Equal((ushort)0, counter.Y);
        Assert.Equal(0, counter.SpellLevel);
        var target = Assert.Single(counter.Targets);
        Assert.Equal(400_001U, target.Uid);
        Assert.Equal(7U, target.Damage); // Source minimum13 -> floor(13*250/100)-25=7.
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        var counterIndices = frames.Select((frame, index) => (frame, index)).Where(item =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(item.frame.Bytes), out var spell) && spell.SpellId == 14_470)
            .ToArray();
        Assert.Equal(2, counterIndices.Length);
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), counterIndices.Select(item => item.frame.Recipient).Order());
        Assert.Equal(counterIndices[0].frame.Bytes, counterIndices[1].frame.Bytes);
        var lastHp = Array.FindLastIndex(frames,
            frame => frame.Recipient == TargetPlayerUid && DirectNormalDeathIsHp(frame.Bytes));
        Assert.All(counterIndices, item => Assert.True(item.index > lastHp));
        Assert.Equal(600, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(4_993U, fixture.MonsterHitpoints);
        MonsterPostAssertNoDeath(fixture.Owner);
        Assert.Empty(ParseDeathAttacks(owner));
        Assert.DoesNotContain(owner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
    }

    private static void MonsterPostResponseAssertOneSetter(
        (uint Recipient, byte[] Bytes)[] frames, uint maximum, uint hitpoints)
    {
        DirectNormalDeathAssertPairs(frames, [(maximum, hitpoints)]);
        var hp = frames.Select((frame, index) => (frame, index)).Where(item =>
            item.frame.Recipient == TargetPlayerUid && DirectNormalDeathIsHp(item.frame.Bytes)).ToArray();
        Assert.Equal(2, hp.Length); // One Max-only frame and one accumulated Max/current frame, no duplicate setter.
        var lastIncoming = Array.FindLastIndex(frames, frame => MonsterPostIncoming(frame.Bytes));
        Assert.True(lastIncoming >= 0 && hp[0].index > lastIncoming);
        var team = frames.Select((frame, index) => (frame, index)).Where(item =>
            LegacyTeamMemberInfoPacket.TryParse(ReadFrame(item.frame.Bytes), out var packet) &&
            packet.Count == 1 && packet.Entries.Count == 1 && packet.Entries[0].Uid == TargetPlayerUid).ToArray();
        Assert.Equal(2, team.Length);
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), team.Select(item => item.frame.Recipient).Order());
        Assert.All(team, item =>
        {
            Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(item.frame.Bytes), out var packet));
            var entry = Assert.Single(packet.Entries);
            Assert.Equal(maximum, (uint)entry.MaxHitpoints);
            Assert.Equal(hitpoints, (uint)entry.MinHitpoints);
            Assert.True(item.index > lastIncoming && item.index < hp[0].index);
        });
    }
}
