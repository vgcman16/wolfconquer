using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Fact]
    public Task ActualEarnedLevelQueryCommitsDerivedJournal23BeforeGrantAndFinishesAward() =>
        RunDerivedContinuation(async (f, monster, catalog) =>
        {
            var before = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var spellBaseline = CaptureDerivedSpellBaseline(f);
            var grantedImage = DerivedGrantedImage(spellBaseline.Bytes);
            var expected = f.Data.Files();
            var characterKey = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var spellsKey = Path.GetRelativePath(f.Data.Root, DerivedSpellsPath(f));
            LegacyExperienceContinuation? earned = null;
            byte[]? beforeDerived = null;
            int grants = 0;
            f.BeforeSend = (_, bytes) =>
            {
                if (bytes.AsSpan().SequenceEqual(Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 138)))
                {
                    Assert.Null(earned);
                    beforeDerived = File.ReadAllBytes(f.CharacterPath());
                    earned = LegacyExperienceContinuationCodec.ReadCharacterImage(beforeDerived, before.AccountId,
                        checked((uint)before.Character.Uid));
                    Assert.NotNull(earned); Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, earned.Phase);
                    Assert.Equal((ushort)138, earned.CurrentLevel); Assert.Equal((ushort)1, earned.StepIndex);
                    Assert.Equal(1UL, earned.ConsumedExperience); Assert.Equal(0UL, earned.CappedExperience);
                    AssertEarnedFromHit(f.ActorFrames, monster, earned);
                    Assert.False(f.Current().PlayerSpells.ContainsSpell(ContinuationVenom));
                    Assert.Equal(spellBaseline.Bytes, File.ReadAllBytes(DerivedSpellsPath(f)));
                    AssertDerivedSpellState(spellBaseline, binding, f.Current().PlayerSpells, granted: false);
                    return;
                }
                if (!bytes.AsSpan().SequenceEqual(DerivedGrantedFrame())) return;
                grants++; Assert.NotNull(earned); Assert.NotNull(beforeDerived);
                Assert.Equal(AppendDerivedOrigin(beforeDerived), File.ReadAllBytes(f.CharacterPath()));
                Assert.Equal(grantedImage, File.ReadAllBytes(DerivedSpellsPath(f)));
                Assert.Equal(earned, LegacyExperienceContinuationCodec.ReadCharacter(f.Current().Character, before.AccountId));
                Assert.Equal("20000:5", f.Current().Character.Attributes["ManagedDerivedSpellOrigins"]);
                AssertDerivedSpellState(spellBaseline, binding, f.Current().PlayerSpells, granted: true);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            };
            try { await Hit(f, monster); }
            finally { f.BeforeSend = null; }
            Assert.True(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _), Diagnostic(f));
            Assert.NotNull(earned); Assert.Equal(1, grants);
            var after = f.Current();
            Assert.Equal(138, after.Character.Level); Assert.Equal(checked((long)earned.RemainingExperience), after.Character.Experience);
            Assert.Equal(15, after.Character.AttributePoints); Assert.Equal(11136, after.Character.MinHitPoints);
            Assert.Equal((ushort)138, binding.Player.Level); Assert.Equal(earned.RemainingExperience, binding.Player.Experience);
            Assert.Equal((ushort)15, binding.Player.Atributes); Assert.Equal(11136, binding.Player.HitPoints);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(f.Recovery.IsBlocked);
            Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
                before.AccountId, checked((uint)before.Character.Uid)));

            string text = Encoding.UTF8.GetString(expected[characterKey]);
            text = Replace(text, "Level=137", "Level=138");
            text = Replace(text, "Experience=0", "Experience=" + earned.RemainingExperience.ToString(CultureInfo.InvariantCulture));
            text = Replace(text, "Atributes=12", "Atributes=15");
            text = Replace(text, "MinHitPoints=5000", "MinHitPoints=11136");
            expected[characterKey] = AppendDerivedOrigin(Encoding.UTF8.GetBytes(text));
            expected[spellsKey] = grantedImage;
            AssertDerivedSpellState(spellBaseline, binding, after.PlayerSpells, granted: true);
            f.Data.AssertFiles(expected);
            int level = f.ActorFrames.FindIndex(bytes => bytes.AsSpan().SequenceEqual(
                Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 138)));
            int grant = f.ActorFrames.FindIndex(bytes => bytes.AsSpan().SequenceEqual(DerivedGrantedFrame()));
            int status = f.ActorFrames.FindIndex(bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode);
            Assert.True(level >= 0 && grant > level && status > grant);
            Assert.DoesNotContain(f.ActorFrames, bytes => LegacyShowEquipmentPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
            Assert.Equal(Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Experience, earned.RemainingExperience), f.ActorFrames[^1]);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
}
