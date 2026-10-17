using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task StarFlow10950ManaReadsActuallyWornMpMasterAfterItsStaminaPublication()
    {
        var armed = false;
        var staminaCallbacks = 0;
        var manaCallbacks = 0;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true,
            starFlowRow: StarAdmissionRow(), mpMasterCatalog: true, onSend: (current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid) return ValueTask.CompletedTask;
            if (StarAdmissionScalar(bytes, LegacyMsgUpdateDataType.Stamina, 90))
            {
                staminaCallbacks++;
                Assert.Equal((long)PlayerUid, current.Owner.Character.Uid);
                Assert.Equal(2, current.Owner.Character.Reborn);
                Assert.Equal(120, current.Owner.Character.Level);
                Assert.Equal(1_000, current.Owner.Character.MinMana);
                Assert.Empty(current.Owner.PlayerItems.RuneItems);
                Assert.False(current.Owner.MagicCooldowns?.ContainsKey(StarAdmissionSpell) == true);
                current.ChangeOwnerDuringArrowSend(value => value with
                {
                    PlayerItems = value.PlayerItems with
                    {
                        RuneItems = [new PlayerItemSnapshotItem
                        {
                            Uid = 98_405, ItemId = 4_030_803, Position = 103,
                            Durability = 100, MaximumDurability = 100,
                        }],
                    },
                });
            }
            if (StarAdmissionScalar(bytes, LegacyMsgUpdateDataType.Mana, 970))
            {
                manaCallbacks++;
                Assert.Equal(970, current.Owner.Character.MinMana);
                Assert.False(current.Owner.MagicCooldowns?.ContainsKey(StarAdmissionSpell) == true);
            }
            return ValueTask.CompletedTask;
        });
        Assert.Equal(2, fixture.Owner.Character.Reborn);
        Assert.Empty(fixture.Owner.PlayerItems.RuneItems);
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, staminaCallbacks);
        Assert.Equal(1, manaCallbacks);
        var rune = Assert.Single(fixture.Owner.PlayerItems.RuneItems);
        Assert.Equal(4_030_803U, rune.ItemId);
        Assert.Equal((ushort)103, rune.Position);
        Assert.Equal("90", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(970, fixture.Owner.Character.MinMana); // 40-floor(40*25/100)=30, then1000-30.
        Assert.Equal(ArrowLifeOrigin.AddSeconds(12), fixture.Owner.MagicCooldowns![StarAdmissionSpell]);
        var owner = fixture.FramesSince(PlayerUid, start);
        Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 970UL) },
            StarAdmissionCosts(owner));
        Assert.Empty(StarAdmissionCosts(fixture.FramesSince(TargetPlayerUid, start)));
        var stamina = Array.FindIndex(owner, bytes => StarAdmissionScalar(bytes, LegacyMsgUpdateDataType.Stamina, 90));
        var mana = Array.FindIndex(owner, bytes => StarAdmissionScalar(bytes, LegacyMsgUpdateDataType.Mana, 970));
        var cast = Array.FindIndex(owner, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == StarAdmissionSpell));
        Assert.True(stamina >= 0 && mana > stamina && cast > mana);
        Assert.Single(ParseSpellAnimations(owner), packet => packet.SpellId == StarAdmissionSpell);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }
}
