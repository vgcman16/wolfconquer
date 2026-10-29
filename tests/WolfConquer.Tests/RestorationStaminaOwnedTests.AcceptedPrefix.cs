using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class RestorationStaminaOwnedTests
{
    [Theory]
    [InlineData((ushort)11960, "93", 88UL)]
    [InlineData((ushort)12890, "104", 99UL)]
    public Task RestorationStaminaLaterIndependentInvalidPublicationStopsOnlyUnperformedContinuation(
        ushort id, string raw, ulong spent) => F.RestorationStaminaRun(id, raw, false, id == 12890, 5, 7, async f =>
        {
            var binding = Owner(f); var start = f.ActorFrames.Count;
            var spellPath = Path.Combine(f.Data.Spells, OwnedEquipmentBehaviorFixture.Actor + ".bin");
            var bookBefore = File.ReadAllBytes(spellPath);
            bool published = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (published || uid != OwnedEquipmentBehaviorFixture.Actor ||
                    !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) ||
                    !packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Stamina && row.Value == spent)) return;
                published = true;
                var accepted = f.Current();
                Assert.Equal(493L, accepted.Character.MinMana); Assert.Equal(493U, binding.Player.Mana);
                Assert.Equal(493L, Saved(f).MinMana); Assert.True(accepted.MagicCooldowns!.ContainsKey(id));
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                var attributes = new Dictionary<string, string>(accepted.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                    { ["Stamina"] = "not-a-number" };
                // A genuine independently accepted Store publication at the real
                // sender, not a replacement saver or fabricated native owner.
                f.Store.Set(f.Connection(uid), accepted with { Character = accepted.Character with { Attributes = attributes } });
            };

            await Cast(f, id); f.BeforeSend = null;

            Assert.True(published); Assert.Same(binding, Owner(f));
            Assert.Equal("not-a-number", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal(493U, binding.Player.Mana);
            Assert.Equal(493L, Saved(f).MinMana); Assert.True(f.Current().MagicCooldowns!.ContainsKey(id));
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, spent), (LegacyMsgUpdateDataType.Mana, 493UL) }, Resources(f, start));
            Assert.DoesNotContain(f.ActorFrames.Skip(start), bytes =>
                LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _) ||
                LegacySpellPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
            Assert.Equal(bookBefore, File.ReadAllBytes(spellPath));
            Assert.Contains("restoration after accepted admission", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Contains("accepted accounting retained, no refund or retry authorization", f.Log.ToString(), StringComparison.Ordinal);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
}
