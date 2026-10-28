using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Xunit.Sdk;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task ActualFrameCasAutosaveAdvancesSavedEvidenceButUnrelatedSetKeepsNativeDeltasUnsaved()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; RequireCoherent(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var anchor = binding.SavedSpells; byte[] character = File.ReadAllBytes(f.CharacterPath); int sends = f.Sent.Count;
            var proposed = before with { PlayerSpells = new PlayerSpellSnapshot(before.PlayerSpells.Spells.Select(
                row => row.Id == 6001 ? row with { Experience = 3333, UseSpellSoul = 3 } : row).ToArray()) };
            Assert.True(f.Store.TryCompareAndSet(f.Connection, before, proposed, out var final));
            Assert.Same(f.Current, final); Assert.NotSame(anchor, binding.SavedSpells);
            byte[] saved = File.ReadAllBytes(f.SpellPath); var durable = binding.SavedSpells;
            Assert.True(durable.Matches(saved)); RequireSavedMembership(f.Current, saved);
            RequireSavedOpaqueByte(saved, 6001, 0xA5); Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
            Assert.Equal(sends, f.Sent.Count); Assert.False(LegacyTradeJournal.HasPending(f.Users)); RequireCoherent(f);
            await NativePacketTestThread.Run(() =>
            {
                using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                RequireCoherent(f).Add(packet, 6002, 2);
            });
            var unsaved = f.Current;
            f.Store.Set(f.Connection, unsaved with { ActiveNpcId = 123 });
            Assert.Equal(123U, f.Current.ActiveNpcId);
            Assert.Same(unsaved.PlayerSpells, f.Current.PlayerSpells); Assert.Same(durable, binding.SavedSpells);
            Assert.Equal(saved, File.ReadAllBytes(f.SpellPath)); Assert.False(f.Recovery.IsBlocked); RequireCoherent(f);
        });
    }

    [Fact]
    public async Task WindowsActualFrameAutosaveReplaceDenialKeepsHonestUnsavedMemoryAndOldEvidence()
    {
        if (!OperatingSystem.IsWindows()) throw SkipException.ForSkip("Actual Windows deny-delete sharing is required for deterministic replacement refusal.");
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var storage = RequireCoherent(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var anchor = binding.SavedSpells; byte[] saved = File.ReadAllBytes(f.SpellPath), character = File.ReadAllBytes(f.CharacterPath);
            var proposed = before with { PlayerSpells = new PlayerSpellSnapshot(before.PlayerSpells.Spells.Select(
                row => row.Id == 6001 ? row with { Experience = 4444 } : row).ToArray()) };
            // Real OS sharing permits all preimage reads but denies File.Replace;
            // no production callback, delegate or fault-injection seam is installed.
            using (var denyReplace = new FileStream(f.SpellPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.True(f.Store.TryCompareAndSet(f.Connection, before, proposed));
                Assert.NotSame(before, f.Current); Assert.Equal(4444, Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 6001).Experience);
                Assert.Same(storage, f.Native.MySpells); RequireCoherent(f);
                Assert.Same(anchor, binding.SavedSpells); Assert.Equal(saved, File.ReadAllBytes(f.SpellPath));
                Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
                Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
                Assert.Contains("memoryCommitted:", f.Log.ToString(), StringComparison.Ordinal);
            }
            var unsaved = f.Current;
            await NativePacketTestThread.Run(() => LegacyNativeServerDatabasePersistence.SaveClientSpells(f.Native));
            Assert.Same(unsaved, f.Current); Assert.NotSame(anchor, binding.SavedSpells);
            Assert.True(binding.SavedSpells.Matches(File.ReadAllBytes(f.SpellPath))); RequireSavedMembership(unsaved, File.ReadAllBytes(f.SpellPath));
        });
    }

    [Fact]
    public async Task ActualRetainedV8OutcomeDoesNotInventSavedEvidenceAndRealRecoveryFenceStopsFurtherSave()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var anchor = binding.SavedSpells; byte[] saved = File.ReadAllBytes(f.SpellPath), character = File.ReadAllBytes(f.CharacterPath);
            var proposed = new PlayerSpellSnapshot(before.PlayerSpells.Spells.Select(row => row.Id == 6001 ? row with { Experience = 5555 } : row).ToArray());
            byte[] after = PlayerSpellSnapshotLoader.ToLegacyBytes(proposed);
            LegacyTradeJournalChange[] changes = [new(LegacyTradeJournalFileKind.Character, before.AccountId, character, character),
                new(LegacyTradeJournalFileKind.Spells, before.Character.Uid, saved, after)];
            // Existing closed interruption enum drives the real journal all the
            // way through target writes, retaining its actual pending record.
            Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes,
                LegacyCharacterSpellsJournalInterruption.AfterSpells, out var failure));
            Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, failure);
            Assert.True(LegacyTradeJournal.HasPending(f.Users)); Assert.Equal(after, File.ReadAllBytes(f.SpellPath));
            Assert.Same(before, f.Current); Assert.Same(anchor, binding.SavedSpells); Assert.True(anchor.Matches(saved)); Assert.False(anchor.Matches(after));
            // The actual recovery owner classifies the retained outcome. This
            // control does not pretend the typed saver returned true.
            Assert.False(f.Recovery.TryRequireLiveJournalClear(out var reason));
            Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason); Assert.True(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryCompareAndSet(f.Connection, before, before with { ActiveNpcId = 321 }));
            await NativePacketTestThread.Run(() => Assert.Throws<InvalidOperationException>(
                () => LegacyNativeServerDatabasePersistence.SaveClientSpells(f.Native)));
            Assert.Same(anchor, binding.SavedSpells); Assert.Equal(after, File.ReadAllBytes(f.SpellPath));
            Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Same(before, f.Current);
        });
    }
}
