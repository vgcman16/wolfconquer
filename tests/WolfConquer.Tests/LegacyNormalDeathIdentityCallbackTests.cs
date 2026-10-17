using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DirectNormalDeathIdentitySpell = 17_490;
    private const uint DirectNormalDeathIdentityOtherTarget = TargetPlayerUid + 1;

    [Theory]
    [InlineData("dead-talk", "attacker", "remove")]
    [InlineData("dead-talk", "attacker", "rebind")]
    [InlineData("dead-talk", "target", "remove")]
    [InlineData("dead-talk", "target", "rebind")]
    [InlineData("prequery", "attacker", "remove")]
    [InlineData("prequery", "attacker", "rebind")]
    [InlineData("prequery", "target", "remove")]
    [InlineData("prequery", "target", "rebind")]
    [InlineData("hp-zero-max", "attacker", "remove")]
    [InlineData("hp-zero-max", "attacker", "rebind")]
    [InlineData("hp-zero-max", "target", "remove")]
    [InlineData("hp-zero-max", "target", "rebind")]
    public async Task DirectNormalDeathOriginalIdentityLossAtEachAwaitAbortsCasterOrSkipsOnlyLostVictim(
        string boundary, string participant, string mutation)
    {
        var armed = false;
        var mutationCount = 0;
        var mutationIndex = -1;
        uint victimUid = 0;
        WorldSession? attackerAtBoundary = null;
        WorldSession? victimAtBoundary = null;
        WorldSession? replacement = null;
        using var fixture = new DirectNormalDeathFixture(deathIdentityLine: true,
            onSend: (current, recipient, bytes) =>
            {
                if (!armed || recipient is not (TargetPlayerUid or DirectNormalDeathIdentityOtherTarget) ||
                    !current.TryIdentitySlot(recipient, out var target) || target is null) return ValueTask.CompletedTask;
                var firstMaximum = LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var maximum) &&
                    maximum.Uid == recipient && maximum.Updates.Count == 1 &&
                    maximum.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints;
                var matches = boundary switch
                {
                    "dead-talk" => DirectNormalDeathTalk(bytes),
                    "prequery" => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _),
                    _ => firstMaximum && target.Character.MinHitPoints == 0,
                };
                if (!matches || Interlocked.CompareExchange(ref mutationCount, 1, 0) != 0)
                    return ValueTask.CompletedTask;
                victimUid = recipient; // The first actual receive, not assumed dictionary order.
                victimAtBoundary = target;
                Assert.True(current.TryIdentitySlot(PlayerUid, out attackerAtBoundary));
                Assert.NotNull(attackerAtBoundary);
                Assert.Equal((long)PlayerUid, attackerAtBoundary.Character.Uid);
                Assert.Equal(boundary == "hp-zero-max" ? 0 : 1, target.Character.MinHitPoints);
                Assert.False(HasLegacyStatusFlag(target.Character.Attributes, 5));
                Assert.True(target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.GhostStampAttribute));
                Assert.Equal(boundary != "dead-talk",
                    target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
                if (boundary == "hp-zero-max") Assert.Equal(500UL, maximum.Updates[0].Value);
                mutationIndex = current.Order.Count - 1; // Trigger delivery is allowed; later sends are fenced.
                var slot = participant == "attacker" ? PlayerUid : victimUid;
                if (mutation == "remove")
                {
                    var removed = current.RemoveIdentitySlot(slot);
                    Assert.Same(participant == "attacker" ? attackerAtBoundary : victimAtBoundary, removed);
                }
                else
                {
                    replacement = current.RebindIdentitySlot(slot);
                }
                return ValueTask.CompletedTask;
            });
        Assert.Equal(0U, (await fixture.StatusAsync(PlayerUid)).MinAttack);
        Assert.Equal(1U, (await fixture.StatusAsync(PlayerUid)).MaxAttack);
        foreach (var uid in new[] { TargetPlayerUid, DirectNormalDeathIdentityOtherTarget })
        {
            var status = await fixture.StatusAsync(uid);
            Assert.Equal(500U, status.MaxHP); // Real helmetHP500, not initial woundedHP1 as a maximum oracle.
            Assert.Equal(0U, status.Defence);
            Assert.Equal(0U, status.ItemBless);
            Assert.True(fixture.TryIdentitySlot(uid, out var target));
            Assert.NotNull(target);
            Assert.Equal(1, target.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(target.Character.Attributes, 5));
        }
        Assert.True(fixture.TryIdentitySlot(TargetPlayerUid, out var initialFirst));
        Assert.True(fixture.TryIdentitySlot(DirectNormalDeathIdentityOtherTarget, out var initialSecond));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.KillIdentityLineAsync();
        armed = false;
        Assert.Equal(1, mutationCount);
        Assert.True(mutationIndex >= start);
        Assert.Contains(victimUid, new[] { TargetPlayerUid, DirectNormalDeathIdentityOtherTarget });
        Assert.NotNull(attackerAtBoundary);
        Assert.NotNull(victimAtBoundary);
        var otherUid = victimUid == TargetPlayerUid ? DirectNormalDeathIdentityOtherTarget : TargetPlayerUid;
        var affectedSlot = participant == "attacker" ? PlayerUid : victimUid;
        var stillBound = fixture.TryIdentitySlot(affectedSlot, out var affected);
        if (mutation == "remove")
        {
            Assert.False(stillBound);
            Assert.Null(affected);
        }
        else
        {
            Assert.True(stillBound);
            Assert.Same(replacement, affected);
            Assert.NotNull(affected);
            Assert.Equal((long)affectedSlot + 9_000, affected.Character.Uid);
            Assert.Equal(77_777, affected.Character.MinHitPoints);
            Assert.Equal(77, affected.Character.MinMana);
            Assert.Equal("33", affected.Character.Attributes["Stamina"]);
            Assert.Equal("replacement", affected.Character.Attributes["DirectNormalDeathIdentity"]);
            Assert.False(HasLegacyStatusFlag(affected.Character.Attributes, 5));
            Assert.Null(affected.EquipmentHitpointsStatus);
            Assert.Null(affected.PhysicalStatusOverlay);
            Assert.Null(affected.FineRainEquipmentAdmission);
        }

        var prefix = fixture.Order.ToArray().Skip(start).Take(mutationIndex - start + 1).ToArray();
        var victimHpPrefix = prefix.Where(frame => frame.Recipient == victimUid &&
            DirectNormalDeathIdentityHp(frame.Bytes, victimUid)).ToArray();
        Assert.Equal(boundary == "hp-zero-max" ? 7 : 0, victimHpPrefix.Length);
        Assert.Equal(boundary == "dead-talk" ? 0 : 1, prefix.Count(frame => frame.Recipient == victimUid &&
            LegacyShowEquipmentPacket.TryParse(ReadFrame(frame.Bytes), out _)));
        if (boundary == "hp-zero-max")
        {
            var six = victimHpPrefix.Take(6).Select(frame => frame.Bytes).ToArray();
            DirectNormalDeathIdentityAssertThreePairs(six, victimUid);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(victimHpPrefix[6].Bytes), out var firstMax));
            Assert.Equal(500UL, Assert.Single(firstMax.Updates).Value);
        }

        var after = fixture.Order.ToArray().Skip(mutationIndex + 1).ToArray();
        Assert.DoesNotContain(after, frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame.Bytes), out var update) &&
            update.Uid == victimUid);
        Assert.DoesNotContain(after, frame => MsgAttackPacket.TryParse(ReadFrame(frame.Bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Death && attack.Query.OpponentUid == victimUid);
        Assert.DoesNotContain(after, frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame.Bytes), out var status) &&
            status.Uid == victimUid);
        if (replacement is not null)
        {
            var replacementUid = replacement.Character.Uid;
            Assert.DoesNotContain(after, frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame.Bytes), out var update) &&
                update.Uid == replacementUid);
            Assert.All(ParseSpellAnimations(after.Select(frame => frame.Bytes)), packet =>
                Assert.DoesNotContain(packet.Targets, candidate => candidate.Uid == replacementUid));
        }
        Assert.True(fixture.TryIdentitySlot(otherUid, out var other));
        Assert.NotNull(other);
        if (participant == "attacker")
        {
            Assert.Empty(after); // Stop THIS outer owner, including the second target and final animation.
            Assert.Same(otherUid == TargetPlayerUid ? initialFirst : initialSecond, other);
            Assert.Equal(1, other.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(other.Character.Attributes, 5));
            Assert.True(fixture.TryIdentitySlot(victimUid, out var survivor));
            Assert.Same(victimAtBoundary, survivor);
            Assert.Equal(boundary == "hp-zero-max" ? 0 : 1, survivor!.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(survivor.Character.Attributes, 5));
        }
        else
        {
            // Only that ORIGINAL target is lost. A rebound connection may now
            // view the OTHER target's legitimate vectors/animation, never a stale hit for itself.
            Assert.True(fixture.TryIdentitySlot(PlayerUid, out var owner));
            Assert.NotNull(owner);
            Assert.Equal((long)PlayerUid, owner.Character.Uid);
            Assert.Equal(300, owner.Character.MinHitPoints);
            Assert.Equal(0, other.Character.MinHitPoints);
            Assert.True(HasLegacyStatusFlag(other.Character.Attributes, 5));
            DirectFineRainHpAssertOwnedMaximum(other, 500);
            var animation = Assert.Single(ParseSpellAnimations(after.Where(frame => frame.Recipient == PlayerUid)
                .Select(frame => frame.Bytes)), packet => packet.SpellId == DirectNormalDeathIdentitySpell);
            Assert.Equal(PlayerUid, animation.AttackerUid);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(otherUid, hit.Uid);
            Assert.Equal(1U, hit.Damage); // [0,1) -> defense-floor1 -> half-upDefense2=1 -> no-break+0.
            Assert.True(hit.Hit);
            Assert.Equal(MsgAttackEffect.None, hit.Effect);
            Assert.Contains(after, frame => MsgAttackPacket.TryParse(ReadFrame(frame.Bytes), out var attack) &&
                attack.Query.AttackType == MsgAttackType.Death && attack.Query.OpponentUid == otherUid);
            Assert.All(ParseSpellAnimations(after.Select(frame => frame.Bytes)), packet =>
                Assert.DoesNotContain(packet.Targets, candidate => candidate.Uid == victimUid));
        }
        Assert.DoesNotContain(fixture.Order.ToArray().Skip(start), frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame.Bytes), out var attack) && attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    private static bool DirectNormalDeathIdentityHp(byte[] bytes, uint uid) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == uid &&
        update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);

    private static void DirectNormalDeathIdentityAssertThreePairs(byte[][] bytes, uint uid)
    {
        var packets = ParseMsgUpdates(bytes);
        Assert.Equal(6, packets.Count);
        for (var index = 0; index < 3; index++)
        {
            Assert.Equal(uid, packets[index * 2].Uid);
            var max = Assert.Single(packets[index * 2].Updates);
            Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type);
            Assert.Equal(500UL, max.Value);
            Assert.Collection(packets[index * 2 + 1].Updates,
                maximum => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type); Assert.Equal(500UL, maximum.Value); },
                hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal(1UL, hp.Value); });
        }
    }
}
