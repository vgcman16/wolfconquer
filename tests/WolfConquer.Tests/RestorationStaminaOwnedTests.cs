using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class RestorationStaminaOwnedTests
{
    private static Task Cast(OwnedEquipmentBehaviorFixture f, ushort id) => f.SendAsync(OwnedEquipmentBehaviorFixture.Actor,
        new MsgAttackPacket(new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, 0, 842, 584,
            MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: id)).ToFrameBytes());

    private static LegacyOrdinarySpellBinding Owner(OwnedEquipmentBehaviorFixture f)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired);
        Assert.Same(f.Current().NativePlayer, binding.Player); Assert.False(f.Recovery.IsBlocked);
        return binding;
    }
    private static CharacterSnapshot Saved(OwnedEquipmentBehaviorFixture f) => Assert.IsType<CharacterSnapshot>(
        f.Data.Loader.TryLoadByAccountIdForAdmission(OwnedEquipmentBehaviorFixture.Actor).Snapshot);
    private static (LegacyMsgUpdateDataType Type, ulong Value)[] Resources(OwnedEquipmentBehaviorFixture f, int start) =>
        f.ActorFrames.Skip(start).Select(OwnedEquipmentBehaviorFixture.ReadFrame)
            .Where(frame => LegacyMsgUpdatePacket.TryParse(frame, out _))
            .SelectMany(frame => { Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var packet)); return packet.Updates; })
            .Where(row => row.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana)
            .Select(row => (row.Type, row.Value)).ToArray();

    [Theory]
    [InlineData((ushort)11960, "-1")]
    [InlineData((ushort)11960, "not-a-number")]
    [InlineData((ushort)11960, "18446744073709551616")]
    [InlineData((ushort)11960, "")]
    [InlineData((ushort)12890, "-1")]
    [InlineData((ushort)12890, "not-a-number")]
    [InlineData((ushort)12890, "18446744073709551616")]
    [InlineData((ushort)12890, "")]
    public Task RestorationStaminaInvalidSavedInputRefusesBeforeAnyRequiredActionPrefix(ushort id, string raw) =>
        // Zero priced stamina makes these independent restoration-input tests:
        // the shared positive-cost guard cannot make the old handlers pass.
        F.RestorationStaminaRun(id, raw, false, id == 12890, 0, 7, async f =>
        {
            var before = f.Current(); var binding = Owner(f); var files = f.Data.Files(); var count = f.ActorFrames.Count;
            Assert.Equal(raw, before.Character.Attributes["Stamina"]); Assert.Equal(raw, Saved(f).Attributes["Stamina"]);
            Assert.Equal(500U, binding.Player.Mana); Assert.Null(before.MagicCooldowns);

            await Cast(f, id);

            Assert.Same(before, f.Current()); Assert.Same(binding, Owner(f));
            Assert.Equal(raw, f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(500L, f.Current().Character.MinMana); Assert.Equal(500U, binding.Player.Mana);
            Assert.Null(f.Current().MagicCooldowns); Assert.Equal(count, f.ActorFrames.Count);
            Assert.Equal(0, Assert.Single(f.Current().PlayerSpells.Spells, row => row.Id == id).Experience);
            f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Contains("requiredStaminaInvalidBeforeAdmission", f.Log.ToString(), StringComparison.Ordinal);
        });

    [Theory]
    [InlineData((ushort)11960, "93", false, 88UL, 100UL)]
    [InlineData((ushort)12890, "104", false, 99UL, 109UL)]
    [InlineData((ushort)12890, "154", true, 149UL, 159UL)]
    public Task RestorationStaminaValidOwnedCastPreservesDebitsRestorationAndWireOrder(
        ushort id, string raw, bool blessed, ulong spent, ulong restored) =>
        F.RestorationStaminaRun(id, raw, blessed, id == 12890, 5, 7, async f =>
        {
            var binding = Owner(f); var start = f.ActorFrames.Count;
            await Cast(f, id);
            Assert.Same(binding, Owner(f)); Assert.Equal(restored.ToString(CultureInfo.InvariantCulture), f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal(493U, binding.Player.Mana); Assert.Equal(493L, Saved(f).MinMana);
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, spent), (LegacyMsgUpdateDataType.Mana, 493UL),
                (LegacyMsgUpdateDataType.Stamina, restored) }, Resources(f, start));
            var frames = f.ActorFrames.Skip(start).Select(OwnedEquipmentBehaviorFixture.ReadFrame).ToArray();
            var cooldown = Assert.Single(Enumerable.Range(0, frames.Length), index => LegacyMagicColdTimePacket.TryParse(frames[index], out _));
            var restore = Assert.Single(Enumerable.Range(0, frames.Length), index => LegacyMsgUpdatePacket.TryParse(frames[index], out var update) &&
                update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Stamina && row.Value == restored));
            var animation = Assert.Single(Enumerable.Range(0, frames.Length), index => LegacySpellAnimationPacket.TryParse(frames[index], out var packet) && packet.SpellId == id);
            Assert.True(cooldown < restore && restore < animation);
            Assert.True(f.Current().MagicCooldowns!.ContainsKey(id));
            if (id == 11960)
            {
                var progress = Assert.Single(f.Current().PlayerSpells.Spells, row => row.Id == id);
                Assert.Equal(1000, progress.Experience);
                var savedBook = new PlayerSpellSnapshotLoader(f.Data.Spells).LoadByCharacterUid(OwnedEquipmentBehaviorFixture.Actor);
                Assert.Equal(1000, Assert.Single(savedBook.Spells, row => row.Id == id).Experience);
            }
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });

    [Theory]
    [InlineData((ushort)11960, false, 100UL)] [InlineData((ushort)11960, true, 150UL)]
    [InlineData((ushort)12890, false, 100UL)] [InlineData((ushort)12890, true, 150UL)]
    public Task RestorationStaminaAbsentSavedValueRetainsExistingDefaults(ushort id, bool blessed, ulong expected) =>
        F.RestorationStaminaRun(id, null, blessed, id == 12890, 0, 7, async f =>
        {
            Assert.False(f.Current().Character.Attributes.ContainsKey("Stamina")); Assert.False(Saved(f).Attributes.ContainsKey("Stamina"));
            var start = f.ActorFrames.Count; await Cast(f, id); Owner(f);
            Assert.DoesNotContain(Resources(f, start), row => row.Type == LegacyMsgUpdateDataType.Stamina);
            Assert.Equal(493L, f.Current().Character.MinMana);
            Assert.Single(f.ActorFrames.Skip(start), bytes => LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == id);
            Assert.False(f.Current().Character.Attributes.ContainsKey("Stamina"));
            Assert.Contains(id == 11960 ? $"stamina={expected} progressDamage=1000" : "WorldServer handled legacy SwirlingStorm magic", f.Log.ToString(), StringComparison.Ordinal);
        });

    [Theory]
    [InlineData((ushort)11960)] [InlineData((ushort)12890)]
    public Task RestorationStaminaValidUnsignedMaximumIsNotNewlyCapped(ushort id) =>
        F.RestorationStaminaRun(id, ulong.MaxValue.ToString(CultureInfo.InvariantCulture), false, id == 12890, 0, 7, async f =>
        {
            var start = f.ActorFrames.Count; await Cast(f, id); Owner(f);
            Assert.Equal(ulong.MaxValue.ToString(CultureInfo.InvariantCulture), f.Current().Character.Attributes["Stamina"]);
            Assert.DoesNotContain(Resources(f, start), row => row.Type == LegacyMsgUpdateDataType.Stamina);
            Assert.Equal(493L, f.Current().Character.MinMana);
            Assert.Single(f.ActorFrames.Skip(start), bytes => LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == id);
        });

    [Fact]
    public Task RestorationStaminaUnusedSwirlingStormStateDoesNotBecomeAZeroCostPrerequisite() =>
        F.RestorationStaminaRun(12890, "not-a-number", false, false, 0, 7, async f =>
        {
            var start = f.ActorFrames.Count; await Cast(f, 12890); Owner(f);
            Assert.Equal("not-a-number", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Current().Character.MinMana);
            Assert.DoesNotContain(Resources(f, start), row => row.Type == LegacyMsgUpdateDataType.Stamina);
            Assert.Single(f.ActorFrames.Skip(start), bytes => LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == 12890);
        });
}
