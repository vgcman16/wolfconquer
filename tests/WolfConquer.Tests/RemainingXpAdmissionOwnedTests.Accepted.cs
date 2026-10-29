using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class RemainingXpAdmissionOwnedTests
{
    [Theory]
    [InlineData((ushort)1110, 0x17, false)] [InlineData((ushort)1025, 0x12, false)]
    [InlineData((ushort)6011, 0x2F, false)] [InlineData((ushort)11970, 0x96, true)]
    [InlineData((ushort)12860, 0xC0, true)] [InlineData((ushort)15780, 0xEB, true)]
    public Task ValidActualActivationRetainsPaidOrderSavedManaAndXpTransition(ushort id, int openedFlag, bool restores) =>
        F.RemainingXpRun(id, "93", true, async f =>
        {
            var binding = Owner(f);
            await Cast(f, id);
            var current = f.Current(); Assert.Same(binding, Owner(f));
            Assert.Equal(493L, current.Character.MinMana); Assert.Equal(493U, current.NativePlayer!.Mana);
            Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
            Assert.False(Flag(current.Character, LegacyRuntimeUpdateTicker.XpListFlag)); Assert.True(Flag(current.Character, openedFlag));
            Assert.Equal(restores ? "100" : "88", current.Character.Attributes["Stamina"]);
            Assert.True(current.MagicCooldowns!.ContainsKey(id));
            var expected = new List<(LegacyMsgUpdateDataType, ulong)>
                { (LegacyMsgUpdateDataType.Stamina, 88), (LegacyMsgUpdateDataType.Mana, 493) };
            if (restores) expected.Add((LegacyMsgUpdateDataType.Stamina, 100));
            Assert.Equal(expected, Resources(f));
            // Independent accepted cost bytes, separate from packet builders.
            Assert.Contains(f.ActorFrames, bytes => Convert.ToHexString(bytes) == "0E00800808A0FA4E120408081058");
            Assert.Contains(f.ActorFrames, bytes => Convert.ToHexString(bytes) == "0F00800808A0FA4E1205080210ED03");
            if (id is 12860) Assert.Empty(Animations(f));
            else Assert.Single(Animations(f), animation => animation.SpellId == id);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }, classCode: id == 15780 ? 9000 : 142);

    [Theory]
    [InlineData((ushort)11970, false)] [InlineData((ushort)12860, false)] [InlineData((ushort)15780, false)]
    [InlineData((ushort)11970, true)] [InlineData((ushort)12860, true)] [InlineData((ushort)15780, true)]
    public Task ActualActivationAbsentBalanceKeepsTheExistingBlessingDefault(ushort id, bool blessed) =>
        F.RemainingXpRun(id, null, true, async f =>
        {
            Assert.False(f.Current().Character.Attributes.ContainsKey("Stamina"));
            await Cast(f, id); Owner(f);
            Assert.Equal(493L, f.Current().Character.MinMana);
            Assert.False(f.Current().Character.Attributes.ContainsKey("Stamina"));
            Assert.DoesNotContain(Resources(f), resource => resource.Type == LegacyMsgUpdateDataType.Stamina);
            Assert.False(Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
        }, staminaCost: 0, classCode: id == 15780 ? 9000 : 142, blessed: blessed);

    [Fact]
    public Task NonRestoringMap1039ActivationKeepsUnusedMalformedStaminaOptional() =>
        F.RemainingXpRun(1110, "not-a-number", true, async f =>
        {
            await Cast(f, 1110); Owner(f);
            Assert.Equal("not-a-number", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(500L, f.Current().Character.MinMana); Assert.Empty(Resources(f));
            Assert.False(Flag(f.Current().Character, LegacyRuntimeUpdateTicker.XpListFlag));
            Assert.Single(Animations(f), animation => animation.SpellId == 1110);
        }, map: 1039);

    [Fact]
    public Task OmnipotenceWithoutXpListKeepsItsIndependentRealDamagePathAndUnusedStamina() =>
        F.RemainingXpRun(12860, "not-a-number", false, async f =>
        {
            var hp = f.Current(Peer).Character.MinHitPoints;
            await Cast(f, 12860); Owner(f);
            Assert.Equal("not-a-number", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Current().Character.MinMana);
            Assert.DoesNotContain(Resources(f), resource => resource.Type == LegacyMsgUpdateDataType.Stamina);
            var animation = Assert.Single(Animations(f), packet => packet.SpellId == 12860);
            var target = Assert.Single(animation.Targets, row => row.Uid == Peer);
            Assert.True(target.Hit); Assert.True(target.Damage > 0);
            Assert.Equal(hp - target.Damage, f.Current(Peer).Character.MinHitPoints);
            Assert.Contains("non-XP special area damage", f.Log.ToString(), StringComparison.Ordinal);
        }, staminaCost: 0);
}
