using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class RestorationStaminaOwnedTests
{
    [Theory]
    [InlineData((ushort)11960, "93", 88UL)]
    [InlineData((ushort)12890, "104", 99UL)]
    public Task RestorationStaminaRejectsAnIndependentMalformedPublicationAndContinuesFromItsRealAcceptedOwner(
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
                var files = f.Data.Files();
                // Declared invalid replacement, never an admitted malformed
                // owner. The actual joint physical participant refuses it.
                Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection(uid), accepted with
                    { Character = accepted.Character with { Attributes = attributes } }));
                Assert.Same(accepted, f.Current()); Assert.Equal(checked((ushort)spent), binding.Player.Stamina);
                f.Data.AssertFiles(files);
            };

            try { await Cast(f, id); } finally { f.BeforeSend = null; }

            Assert.True(published); Assert.Same(binding, Owner(f));
            ulong restored = id == 11960 ? 100UL : spent + 10;
            Assert.Equal(restored.ToString(System.Globalization.CultureInfo.InvariantCulture), f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(checked((ushort)restored), binding.Player.Stamina);
            Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal(493U, binding.Player.Mana);
            Assert.Equal(493L, Saved(f).MinMana); Assert.True(f.Current().MagicCooldowns!.ContainsKey(id));
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, spent), (LegacyMsgUpdateDataType.Mana, 493UL),
                (LegacyMsgUpdateDataType.Stamina, restored) }, Resources(f, start));
            Assert.Single(f.ActorFrames.Skip(start), bytes => LegacySpellAnimationPacket.TryParse(
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) && animation.SpellId == id);
            var expectedBook = bookBefore.ToArray();
            if (id == 11960) System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(expectedBook.AsSpan(8), 1000);
            Assert.Equal(expectedBook, File.ReadAllBytes(spellPath));
            Assert.DoesNotContain("requiredStaminaInvalid", f.Log.ToString(), StringComparison.Ordinal);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
}
