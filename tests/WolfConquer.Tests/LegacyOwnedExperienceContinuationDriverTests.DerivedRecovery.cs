using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Fact]
    public Task ActualEarnedPendingDerivedJournalPrefixRecoversTwiceWithoutRecreditOrGrantOutput() =>
        RunDerivedContinuation(async (f, monster, catalog) =>
        {
            var before = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var spellBaseline = CaptureDerivedSpellBaseline(f);
            var expected = f.Data.Files();
            var characterKey = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var spellsKey = Path.GetRelativePath(f.Data.Root, DerivedSpellsPath(f));
            byte[]? beforeDerived = null;
            LegacyExperienceContinuation? earned = null;
            f.BeforeSend = (_, bytes) =>
            {
                if (!bytes.AsSpan().SequenceEqual(Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 138))) return;
                Assert.Null(beforeDerived);
                beforeDerived = File.ReadAllBytes(f.CharacterPath());
                earned = LegacyExperienceContinuationCodec.ReadCharacterImage(beforeDerived,
                    before.AccountId, checked((uint)before.Character.Uid));
                Assert.NotNull(earned); Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, earned.Phase);
                Assert.Equal((ushort)1, earned.StepIndex); Assert.Equal((ushort)138, earned.CurrentLevel);
                Assert.Equal(1UL, earned.ConsumedExperience); AssertEarnedFromHit(f.ActorFrames, monster, earned);
                Assert.Equal(spellBaseline.Bytes, File.ReadAllBytes(DerivedSpellsPath(f)));
                AssertDerivedSpellState(spellBaseline, binding, f.Current().PlayerSpells, granted: false);
            };
            using (var interruption = LegacyJournalInterruption.StopAfter(1))
            {
                f.Handler.DerivedSpellQueryJournalInterruption = interruption;
                try { await Hit(f, monster); }
                finally { f.BeforeSend = null; f.Handler.DerivedSpellQueryJournalInterruption = null; }
                Assert.True(interruption.PendingObserved, Diagnostic(f));
                Assert.Equal(new[] { 0, 1 }, interruption.Visited);
            }
            Assert.NotNull(beforeDerived); Assert.NotNull(earned);
            Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
            Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
            Assert.False(binding.Storage.ClientSpells.ContainsKey(ContinuationVenom));
            AssertDerivedSpellState(spellBaseline, binding, null, granted: false);
            Assert.DoesNotContain(f.ActorFrames, bytes => bytes.AsSpan().SequenceEqual(DerivedGrantedFrame()));
            Assert.DoesNotContain(f.ActorFrames, bytes => LegacyMsgUpdatePacket.TryParse(
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) &&
                update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Experience));
            var pendingPath = Path.Combine(f.Data.Users, ".managed-trade-journal", "pending.json");
            var journal = JsonNode.Parse(File.ReadAllBytes(pendingPath))!;
            Assert.Equal(23, journal["Version"]!.GetValue<int>());
            Assert.Equal(OwnedEquipmentBehaviorFixture.Actor, journal["DerivedSpellQuery"]!["TargetUid"]!.GetValue<uint>());
            Assert.Equal(2, journal["DerivedSpellQuery"]!["Source"]!.GetValue<int>());
            Assert.Equal(20006, journal["DerivedSpellQuery"]!["GrantedRow"]!["Id"]!.GetValue<int>());
            var changes = journal["Changes"]!.AsArray(); Assert.Equal(2, changes.Count);
            expected[characterKey] = AppendDerivedOrigin(beforeDerived);
            expected[spellsKey] = DerivedGrantedImage(spellBaseline.Bytes);
            Assert.Equal(beforeDerived, Convert.FromBase64String(changes[0]!["Before"]!.GetValue<string>()));
            Assert.Equal(expected[characterKey], Convert.FromBase64String(changes[0]!["After"]!.GetValue<string>()));
            Assert.Equal(spellBaseline.Bytes, Convert.FromBase64String(changes[1]!["Before"]!.GetValue<string>()));
            Assert.Equal(expected[spellsKey], Convert.FromBase64String(changes[1]!["After"]!.GetValue<string>()));
            Assert.Equal(expected[characterKey], File.ReadAllBytes(f.CharacterPath()));
            Assert.Equal(spellBaseline.Bytes, File.ReadAllBytes(DerivedSpellsPath(f)));
            int outputCount = f.ActorFrames.Count;
            for (int restart = 0; restart < 2; restart++)
            {
                await f.ReopenDerivedContinuationRecoveryOnlyAsync();
                Assert.True(f.Runtime.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
                Assert.False(f.Runtime.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                f.Data.AssertFiles(expected);
                Assert.Equal(earned, LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
                    before.AccountId, checked((uint)before.Character.Uid)));
                Assert.Equal(outputCount, f.ActorFrames.Count);
                Assert.True(binding.IsRetired); Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
                AssertDerivedSpellState(spellBaseline, binding, null, granted: false);
            }
        });
}
