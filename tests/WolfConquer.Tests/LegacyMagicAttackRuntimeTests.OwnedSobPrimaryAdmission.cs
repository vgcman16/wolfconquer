using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData((byte)1, "catalog-dead")]
    [InlineData((byte)48, "catalog-dead")]
    [InlineData((byte)1, "runtime-dead")]
    [InlineData((byte)48, "runtime-dead")]
    [InlineData((byte)1, "neutral")]
    [InlineData((byte)48, "neutral")]
    public Task OwnedDirectMagicRefusesInvalidMandatorySobBeforeAnyCost(byte sort, string kind) =>
        RunOwnedSobPrimaryAsync(sort, kind, async (fixture, sobs, states) =>
        {
            var before = fixture.Current(PlayerUid);
            var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            var saved = ProcFiles(fixture);
            var visibleBefore = sobs.Select(states.GetVisible).ToArray();
            Assert.Equal(500L, before.Character.MinMana);
            Assert.Equal(500U, before.NativePlayer!.Mana);
            Assert.Equal("100", before.Character.Attributes["Stamina"]);
            Assert.Equal((ushort)100, before.NativePlayer.Stamina);
            Assert.Null(before.MagicCooldowns);
            Assert.Equal((ushort)0, before.LegacyRandomSpell);
            var spellXp = Assert.Single(before.PlayerSpells.Spells).Experience;
            var nativeAuthorization = before.NativePlayer.RandomSpell;
            var nativeBook = binding.Storage.ClientSpells;
            var nativeGeneration = binding.Storage.MutationGenerationPlain;
            Assert.True(nativeBook.TryGetValue(ThunderSpellId, out var nativeSpell));
            Assert.NotNull(nativeSpell);
            var nativeExperience = nativeSpell.Experience;
            var nativeColdTime = nativeSpell.ColdTime;
            var nativeCooldownEnabled = nativeSpell.IsSpellWithColdTime;

            await fixture.SendAsync(PlayerUid, OwnedSobQuery(sobs[1], sort));

            var after = fixture.Current(PlayerUid);
            Assert.Same(before, after);
            Assert.True(fixture.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(500L, after.Character.MinMana);
            Assert.Equal(500U, after.NativePlayer!.Mana);
            Assert.Equal("100", after.Character.Attributes["Stamina"]);
            Assert.Equal((ushort)100, after.NativePlayer.Stamina);
            Assert.Null(after.MagicCooldowns);
            Assert.Equal((ushort)0, after.LegacyRandomSpell);
            Assert.Equal(spellXp, Assert.Single(after.PlayerSpells.Spells).Experience);
            Assert.Equal(nativeAuthorization, after.NativePlayer.RandomSpell);
            Assert.Same(nativeBook, binding.Storage.ClientSpells);
            Assert.Equal(nativeGeneration, binding.Storage.MutationGenerationPlain);
            Assert.Same(nativeSpell, binding.Storage.ClientSpells[ThunderSpellId]);
            Assert.Equal(nativeExperience, nativeSpell.Experience);
            Assert.Equal(nativeColdTime, nativeSpell.ColdTime);
            Assert.Equal(nativeCooldownEnabled, nativeSpell.IsSpellWithColdTime);
            Assert.Empty(fixture.ActorFrames);
            ProcFilesUnchanged(fixture, saved);
            Assert.False(fixture.Recovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Data.Users));
            for (int index = 0; index < sobs.Length; index++)
                Assert.Equal(visibleBefore[index], states.GetVisible(sobs[index]));
        });

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)48)]
    [InlineData((byte)5)]
    public Task OwnedValidDirectAndAreaSobCastsChargeOnceAndSkipNeutralRecipient(byte sort) =>
        RunOwnedSobPrimaryAsync(sort, "neutral", async (fixture, sobs, states) =>
        {
            // Bomb uses the neutral object's real coordinates as its center;
            // the two direct sorts have an eligible mandatory primary.
            var selected = sort == 5 ? sobs[1] : sobs[0];
            var savedBefore = fixture.Data.Loader.LoadByAccountId(PlayerUid)!;
            bool hadSavedStamina = savedBefore.Attributes.TryGetValue("Stamina", out var savedStamina);
            await fixture.SendAsync(PlayerUid, OwnedSobQuery(selected, sort));
            var current = fixture.Current(PlayerUid);
            Assert.True(fixture.Store.IsBoundSpellOwnerCurrent(current.NativeGameClient!.OrdinarySpellBinding!));
            Assert.Equal(493L, current.Character.MinMana);
            Assert.Equal(493U, current.NativePlayer!.Mana);
            Assert.Equal("95", current.Character.Attributes["Stamina"]);
            Assert.Equal((ushort)95, current.NativePlayer.Stamina);
            Assert.True(current.MagicCooldowns!.ContainsKey(sort == 5 ? LightningBombSpellId : ThunderSpellId));
            var saved = fixture.Data.Loader.LoadByAccountId(PlayerUid)!;
            Assert.Equal(493L, saved.MinMana);
            Assert.Equal(hadSavedStamina, saved.Attributes.TryGetValue("Stamina", out var afterSavedStamina));
            Assert.Equal(savedStamina, afterSavedStamina);
            var costs = ParseMsgUpdates(fixture.ActorFrames).Where(packet => packet.Uid == PlayerUid)
                .SelectMany(packet => packet.Updates).Where(row => row.Type is
                    LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana).ToArray();
            Assert.Equal(new[]
            {
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95),
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493),
            }, costs);
            var animation = Assert.Single(ParseSpellAnimations(fixture.ActorFrames));
            var expectedIds = sort == 1 ? new[] { checked((uint)sobs[0].Id) } :
                new[] { checked((uint)sobs[0].Id), checked((uint)sobs[2].Id) };
            Assert.Equal(expectedIds, animation.Targets.Select(target => target.Uid).ToArray());
            Assert.All(animation.Targets, target => Assert.True(target.Damage > 0));
            Assert.Equal(sobs[1].Life, states.GetVisible(sobs[1]).Life);
            Assert.True(states.GetVisible(sobs[0]).Life < sobs[0].Life);
            if (sort == 1) Assert.Equal(sobs[2].Life, states.GetVisible(sobs[2]).Life);
            else Assert.True(states.GetVisible(sobs[2]).Life < sobs[2].Life);
            Assert.False(fixture.Recovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Data.Users));
        });
}
