using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerShockReceiveMutationAttackerNeptunePublishesAdjustedDamageAndOneHpPair()
    {
        var result = await RunShockPhysicalTailAsync(attackerNeptune: true);

        // Receive.Player:561-565 changes SpellObj, then ordinary :799 assigns HP.
        AssertShockPhysicalTailIncoming(result, 99_999, MsgAttackEffect.None);
        Assert.Equal("true", result.Caster.Character.Attributes["AttackHit"]);
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            Assert.Single(ParseSpellAnimations(frames), animation => animation.SpellId == CrackingShockSpellId);
        }
        AssertShockReceiveMutationHpPair(result, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerShockReceiveMutationInitialTargetNeptuneStillFailsSpellGate(bool attackerNeptune)
    {
        var result = await RunShockPhysicalTailAsync(targetNeptune: true, attackerNeptune: attackerNeptune);

        // CanAttackPlayer:75 rejects a target already cursed when DBSpell is non-null.
        Assert.Equal(100_000, result.Target.Character.MinHitPoints);
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == CrackingShockSpellId);
            Assert.Empty(animation.Targets);
        }
        Assert.Empty(result.Observations);
        Assert.DoesNotContain(ParseMsgUpdates(result.TargetSent), update =>
            update.Uid == TargetPlayerUid && update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
    }

    [Theory]
    [InlineData("FreezeWar")]
    [InlineData("SkillTournament")]
    public async Task HandlerShockReceiveMutationTournamentZeroDoesNotPublishOrdinaryHpPair(string tournamentType)
    {
        var result = await RunShockPhysicalTailAsync(
            statusFlag: LegacyPhysicalSpellsShurikenVortexFlag,
            tournamentType: tournamentType);

        // Receive.Player:577-611 returns before the ordinary HP setter in these branches.
        Assert.Equal(100_000, result.Target.Character.MinHitPoints);
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == CrackingShockSpellId);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(0U, hit.Damage);
            Assert.True(hit.Hit);
        }
        Assert.DoesNotContain(ParseMsgUpdates(result.TargetSent), update =>
            update.Uid == TargetPlayerUid && update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        if (tournamentType == "SkillTournament")
        {
            Assert.Equal("1", result.Target.Character.Attributes["SkillTournamentLifes"]);
        }
    }

    [Theory]
    [InlineData(0U, 261U, 0U)]
    [InlineData(100U, 161U, 0U)]
    [InlineData(200U, 61U, 39U)]
    public async Task HandlerShockReceiveMutationAnimationKeepsAdjustedUncappedDamage(
        uint shieldLeft, uint expectedDamage, uint expectedHitpoints)
    {
        var result = await RunShockPhysicalTailAsync(lightningShieldLeft: shieldLeft, targetHitpoints: 100);

        // The same SpellObj is queued after absorption, not rewritten to the capped HP loss.
        Assert.Equal(expectedHitpoints, result.Target.Character.MinHitPoints);
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == CrackingShockSpellId);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, hit.Uid);
            Assert.Equal(expectedDamage, hit.Damage);
            Assert.True(hit.Hit);
            Assert.Equal(MsgAttackEffect.None, hit.Effect);
            Assert.Equal((ushort)2239, ReadFrame(frames[ShockPhysicalTailAnimationIndex(frames, CrackingShockSpellId)]).Header.Opcode);
        }
        Assert.Equal(expectedHitpoints == 0,
            HasLegacyStatusFlag(result.Target.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        AssertShockReceiveMutationHpPair(result, expectedHitpoints);
    }

    private static void AssertShockReceiveMutationHpPair(ShockPhysicalTailResult result, uint expectedHitpoints)
    {
        if (expectedHitpoints == 0)
        {
            var deathHp = AssertExistingNormalDeathCombatPhases(result.TargetSent, TargetPlayerUid,
                (100_000, 100), (100_000, 100), (100_000, 100), (100_000, 0));
            Assert.True(ShockPhysicalTailSolidIndex(result.TargetSent) < deathHp[0]);
            Assert.All(deathHp, index =>
                Assert.True(index < ShockPhysicalTailAnimationIndex(result.TargetSent, CrackingShockSpellId)));
            return;
        }

        var hpIndices = Enumerable.Range(0, result.TargetSent.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[index]), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(2, hpIndices.Length);
        Assert.All(hpIndices, index => Assert.Equal((ushort)2176, ReadFrame(result.TargetSent[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[hpIndices[0]]), out var maximum));
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000)], maximum.Updates);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.TargetSent[hpIndices[1]]), out var current));
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000),
             new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, expectedHitpoints)], current.Updates);
        Assert.True(ShockPhysicalTailSolidIndex(result.TargetSent) < hpIndices[0]);
        Assert.True(hpIndices[0] < hpIndices[1]);
        Assert.True(hpIndices[1] < ShockPhysicalTailAnimationIndex(result.TargetSent, CrackingShockSpellId));
    }
}
