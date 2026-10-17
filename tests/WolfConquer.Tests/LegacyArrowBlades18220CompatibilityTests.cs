using System.Collections.Frozen;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task ArrowBladesNatural333MonsterCallerPreservesExplicitAndBlanketCompatibilityFallback()
    {
        foreach (var blanket in new[] { false, true })
        {
            await using var fixture = new ArrowLifecycleFixture("monster");
            await fixture.EarnWaterAsync(); // Both3-hit paths run under neutral/default policy, no status seed.
            var registration = fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000);
            var policy = LegacyClientCompatibilityOptions.DecompiledSourceCompatible with
            {
                DisableLegacyCombatGearMonsterHitPassives = blanket,
                DisabledLegacyCombatGearMonsterHitPassiveSpellIds = blanket
                    ? FrozenSet<ushort>.Empty : new ushort[] { 18_220 }.ToFrozenSet(),
            };
            Assert.True(policy.IsLegacyCombatGearMonsterHitPassiveDisabled(18_220));
            Assert.Equal(blanket, policy.IsLegacyCombatGearMonsterHitPassiveDisabled(17_400));
            fixture.UseArrowAttackCompatibility(policy); // Only the next real public input's existing policy field.
            var hp = fixture.PrimaryHitpoints;
            var start = fixture.Order.Count;
            await fixture.AttackAsync();
            var frames = fixture.FramesSince(PlayerUid, start);
            var ordinary = Assert.Single(frames, bytes =>
                MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.Uid == PlayerUid && packet.Query.OpponentUid == fixture.PrimaryUid &&
                packet.Query.AttackType == MsgAttackType.Physical);
            Assert.True(MsgAttackPacket.TryParse(ReadFrame(ordinary), out var interaction));
            Assert.True(interaction.Query.Damage > 0);
            Assert.Equal(hp - interaction.Query.Damage, fixture.PrimaryHitpoints);
            Assert.DoesNotContain(ParseSpellAnimations(frames), packet => packet.SpellId is 18_220 or 17_400);
            Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
            Assert.Equal(registration, fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000));
            ArrowLifeAssertRetired(fixture.Owner, ArrowLifeFire);
            Assert.Empty(ArrowLifeTyped(frames, 137, ArrowLifeFire));
        }
    }
}
