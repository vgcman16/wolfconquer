using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Theory]
    [InlineData("items")]
    [InlineData("spells")]
    public Task ARealRunePublicationProofRefusesSubstitutedItemsOrSpellsBeforeItsWriter(string changed) => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); f.AssertMarker("14160:16"); f.AssertConverged();
        var before = f.Current; var files = f.Data.Files(); int first = f.Sent.Count;
        var native = f.Native; var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
        var nativeMap = native.MySpells!.ClientSpells; var nativeItems = binding.Items.Image;
        var evidence = LegacyRuneOperationEvidence.Capture(f.Context, before, false, F.First, 212);
        var projected = WorldPacketHandlers.ProjectRecordedRuneOperation(before.Character, before.PlayerItems,
            before.PlayerSpells, evidence.Operation);
        var proposed = before with { Character = projected.Character, PlayerItems = projected.Items, PlayerSpells = projected.Spells };
        var writer = f.Store.PrepareSpellFileWrite(f.Connection, before, f.Runtime.Recovery, f.Data.Users, f.Data.Spells);
        writer.PrepareImage(proposed.PlayerSpells);
        var characterBefore = File.ReadAllBytes(f.CharacterPath);
        LegacyTradeJournalChange[] changes =
        [
            new(LegacyTradeJournalFileKind.Character, F.Uid, characterBefore,
                LegacyRuneSpellOrigins.ProjectCharacterImage(characterBefore, proposed.Character)),
            new(LegacyTradeJournalFileKind.Items, F.Uid, File.ReadAllBytes(f.ItemsPath), PlayerItemSnapshotLoader.ToLegacyBytes(proposed.PlayerItems)),
            new(LegacyTradeJournalFileKind.Spells, F.Uid, writer.CopyBeforeImage(), writer.CopyAfterImage()),
        ];
        var publication = writer.PrepareRuneCharacterPublication(proposed, changes, evidence, null);
        var substituted = changed == "items" ? proposed with { PlayerItems = before.PlayerItems }
            : proposed with { PlayerSpells = before.PlayerSpells };
        Assert.Same(proposed.Character, substituted.Character);
        bool invoked = false;
        var refusal = Assert.Throws<InvalidOperationException>(() => f.Store.TryCommitRuneItemSpellSession(
            f.Connection, before, substituted, publication,
            () => { invoked = true; throw new InvalidOperationException("Substituted proposal reached the writer."); }, out _));
        Assert.Equal("ordinarySpellSavedImageRequiresCurrentOwner", refusal.Message); Assert.False(invoked);
        Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
        Assert.Same(nativeMap, native.MySpells.ClientSpells); Assert.Same(nativeItems, binding.Items.Image);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); f.AssertConverged(); f.AssertMarker("14160:16");
    });

    [Fact]
    public Task ARealPreparedRuneProofCannotPublishWhenTheCallbackDoesNotRunItsJournalWriter() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); f.AssertMarker("14160:16"); f.AssertConverged();
        var before = f.Current; var files = f.Data.Files(); int first = f.Sent.Count;
        var native = f.Native; var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
        var nativeMap = native.MySpells!.ClientSpells; var nativeItems = binding.Items.Image;
        var evidence = LegacyRuneOperationEvidence.Capture(f.Context, before, false, F.First, 212);
        var projected = WorldPacketHandlers.ProjectRecordedRuneOperation(before.Character, before.PlayerItems,
            before.PlayerSpells, evidence.Operation);
        var proposed = before with { Character = projected.Character, PlayerItems = projected.Items, PlayerSpells = projected.Spells };
        Assert.Equal(before.Character.MinHitPoints, proposed.Character.MinHitPoints);
        var writer = f.Store.PrepareSpellFileWrite(f.Connection, before, f.Runtime.Recovery, f.Data.Users, f.Data.Spells);
        writer.PrepareImage(proposed.PlayerSpells);
        var characterBefore = File.ReadAllBytes(f.CharacterPath);
        LegacyTradeJournalChange[] changes =
        [
            new(LegacyTradeJournalFileKind.Character, F.Uid, characterBefore,
                LegacyRuneSpellOrigins.ProjectCharacterImage(characterBefore, proposed.Character)),
            new(LegacyTradeJournalFileKind.Items, F.Uid, File.ReadAllBytes(f.ItemsPath), PlayerItemSnapshotLoader.ToLegacyBytes(proposed.PlayerItems)),
            new(LegacyTradeJournalFileKind.Spells, F.Uid, writer.CopyBeforeImage(), writer.CopyAfterImage()),
        ];
        var publication = writer.PrepareRuneCharacterPublication(proposed, changes, evidence, null);
        int called = 0;
        // Deliberately false persistence testimony is NEGATIVE input, never an
        // accepting saver or positive owner. The actual writer is not invoked.
        Assert.Throws<LegacyCommittedSpellPublicationException>(() => f.Store.TryCommitRuneItemSpellSession(
            f.Connection, before, proposed, publication, () => { called++; return true; }, out _));
        Assert.Equal(1, called); Assert.False(f.Store.TryGet(f.Connection, out _));
        Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
        Assert.Same(nativeMap, native.MySpells.ClientSpells); Assert.Same(nativeItems, binding.Items.Image);
        Assert.Contains(F.FirstSpell, nativeMap.Keys); Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    });

    [Fact]
    public Task ARealPreparedRuneWriterRefusesACharacterImageFromBeforeItsOriginRetirement() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); f.AssertMarker("14160:16"); f.AssertConverged();
        var before = f.Current; var files = f.Data.Files(); int first = f.Sent.Count;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var nativeMap = f.Native.MySpells!.ClientSpells; var nativeItems = binding.Items.Image;
        var evidence = LegacyRuneOperationEvidence.Capture(f.Context, before, false, F.First, 212);
        var projected = WorldPacketHandlers.ProjectRecordedRuneOperation(before.Character, before.PlayerItems,
            before.PlayerSpells, evidence.Operation);
        var proposed = before with { Character = projected.Character, PlayerItems = projected.Items, PlayerSpells = projected.Spells };
        var writer = f.Store.PrepareSpellFileWrite(f.Connection, before, f.Runtime.Recovery, f.Data.Users, f.Data.Spells);
        writer.PrepareImage(proposed.PlayerSpells);
        var characterBefore = File.ReadAllBytes(f.CharacterPath);
        var correctCharacter = LegacyRuneSpellOrigins.ProjectCharacterImage(characterBefore, proposed.Character);
        Assert.False(characterBefore.AsSpan().SequenceEqual(correctCharacter));
        // Negative only: the real prepared writer must not authorize a valid
        // old image in place of its exact new origin image. No journal is made.
        LegacyTradeJournalChange[] wrong =
        [
            new(LegacyTradeJournalFileKind.Character, F.Uid, characterBefore, characterBefore),
            new(LegacyTradeJournalFileKind.Items, F.Uid, File.ReadAllBytes(f.ItemsPath), PlayerItemSnapshotLoader.ToLegacyBytes(proposed.PlayerItems)),
            new(LegacyTradeJournalFileKind.Spells, F.Uid, writer.CopyBeforeImage(), writer.CopyAfterImage()),
        ];
        var failure = Assert.Throws<InvalidOperationException>(() => writer.PrepareRuneCharacterPublication(proposed, wrong, evidence, null));
        Assert.Equal("ordinarySpellSavedImageRequiresCurrentOwner", failure.Message);
        Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
        Assert.Same(nativeMap, f.Native.MySpells.ClientSpells); Assert.Same(nativeItems, binding.Items.Image);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); f.AssertConverged(); f.AssertMarker("14160:16");
    });
}
