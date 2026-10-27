using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyPerformerDanceLearningTests
{
    [Fact]
    public async Task RealFileSharingRefusalKeepsTheSessionAndExistingSpellFileUnchanged()
    {
        await WithDurableDanceAsync(async fixture =>
        {
            var connection = fixture.Connection();
            var store = fixture.Store();
            var original = DurableSession();
            store.Set(connection, original);
            fixture.Seed(CharacterUid, original.PlayerSpells);
            var before = new DurableSessionImage(original);
            var handled = false;
            Exception? error;
            using (var held = new FileStream(fixture.SpellPath(CharacterUid), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                error = await Record.ExceptionAsync(async () =>
                { handled = await fixture.InvokeAsync(connection, store, original); });
            }

            fixture.RequireFilesUnchanged();
            before.RequireUnchanged();
            Assert.True(store.TryGet(connection, out var current));
            Assert.Same(original, current);
            Assert.Empty(fixture.Sent);
            Assert.True(handled);
            Assert.Null(error);
            Assert.Contains("Performer dance persistence", fixture.Log.ToString());
            Assert.Contains("reason=IOException; no session mutation was committed", fixture.Log.ToString());
        });
    }

    [Fact]
    public async Task LearningPreservesTheExistingOpaqueSpellByteAndAllUnrelatedResources()
    {
        await WithDurableDanceAsync(async fixture =>
        {
            var connection = fixture.Connection();
            var store = fixture.Store();
            var original = DurableSession();
            store.Set(connection, original);
            fixture.Seed(CharacterUid, original.PlayerSpells);
            fixture.SeedOpaqueByte(CharacterUid, 0xA5);
            var image = fixture.Bytes(CharacterUid);
            var before = new DurableSessionImage(original);
            Assert.Equal(original.PlayerSpells.Spells, new PlayerSpellSnapshotLoader(fixture.SpellsRoot)
                .LoadByCharacterUid(CharacterUid).Spells);

            Assert.True(await fixture.InvokeAsync(connection, store, original));

            Assert.True(store.TryGet(connection, out var current));
            Assert.NotNull(current);
            Assert.NotSame(original, current);
            before.RequireUnchanged();
            before.RequireResources(current);
            Assert.Equal(original.PlayerSpells.Spells.Concat([new PlayerSpellSnapshotEntry(1416, 0, 0, 0, 0, 0)]),
                current.PlayerSpells.Spells);
            // Independent count, untouched existing raw rows, and one literal zero-progress 12-byte row.
            var expected = Convert.FromHexString("03000000").Concat(image.Skip(4))
                .Concat(Convert.FromHexString("880500000000000000000000")).ToArray();
            fixture.Expect(CharacterUid, expected);
            Assert.Equal(0xA5, expected[15]);
            var dance = DanceCases.Single(dance => dance.OptionId == 7);
            AssertSourceLearningPacketOrder(fixture.Sent, dance.SpellId, dance.SystemMessage,
                dance.SuccessDialogText, dance.SuccessOptionText);
        });
    }

    [Fact]
    public async Task NewerModeledSpellProgressOnDiskIsNotOverwrittenByLearning()
    {
        await WithDurableDanceAsync(async fixture =>
        {
            var connection = fixture.Connection();
            var store = fixture.Store();
            var original = DurableSession();
            store.Set(connection, original);
            fixture.Seed(CharacterUid, CreateSpells(CreateExistingSpell() with { Experience = 9999 }, original.PlayerSpells.Spells[1]));
            var before = new DurableSessionImage(original);
            var handled = false;
            var error = await Record.ExceptionAsync(async () =>
            { handled = await fixture.InvokeAsync(connection, store, original); });

            fixture.RequireFilesUnchanged();
            before.RequireUnchanged();
            Assert.True(store.TryGet(connection, out var current));
            Assert.Same(original, current);
            Assert.Empty(fixture.Sent);
            Assert.True(handled);
            Assert.Null(error);
            Assert.Contains("reason=savedSpellsChanged", fixture.Log.ToString());
        });
    }

    [Fact]
    public async Task TwoDifferentDancesComposeThroughTheActualCurrentSessionAndSameFile()
    {
        await WithDurableDanceAsync(async fixture =>
        {
            var connection = fixture.Connection();
            var store = fixture.Store();
            var original = DurableSession();
            store.Set(connection, original);
            fixture.Seed(CharacterUid, original.PlayerSpells);
            var originalImage = fixture.Bytes(CharacterUid);
            var before = new DurableSessionImage(original);

            Assert.True(await fixture.InvokeAsync(connection, store, original, optionId: 7));
            Assert.True(store.TryGet(connection, out var first));
            Assert.NotNull(first);
            Assert.NotSame(original, first);
            var firstImage = Convert.FromHexString("03000000").Concat(originalImage.Skip(4))
                .Concat(Convert.FromHexString("880500000000000000000000")).ToArray();
            fixture.Expect(CharacterUid, firstImage);
            var triumph = DanceCases.Single(dance => dance.OptionId == 7);
            AssertSourceLearningPacketOrder(fixture.Sent, triumph.SpellId, triumph.SystemMessage,
                triumph.SuccessDialogText, triumph.SuccessOptionText);
            var firstBefore = new DurableSessionImage(first);
            fixture.Sent.Clear();

            Assert.True(await fixture.InvokeAsync(connection, store, first, optionId: 12));

            Assert.True(store.TryGet(connection, out var second));
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            before.RequireUnchanged();
            firstBefore.RequireUnchanged();
            before.RequireResources(second);
            Assert.Equal(new ushort[] { 900, 901, 1416, 1420 }, second.PlayerSpells.Spells.Select(spell => spell.Id));
            var expected = Convert.FromHexString("04000000").Concat(firstImage.Skip(4))
                .Concat(Convert.FromHexString("8C0500000000000000000000")).ToArray();
            fixture.Expect(CharacterUid, expected);
            Assert.Equal(second.PlayerSpells.Spells, new PlayerSpellSnapshotLoader(fixture.SpellsRoot)
                .LoadByCharacterUid(CharacterUid).Spells);
            var phoenix = DanceCases.Single(dance => dance.OptionId == 12);
            AssertSourceLearningPacketOrder(fixture.Sent, phoenix.SpellId, phoenix.SystemMessage,
                phoenix.SuccessDialogText, phoenix.SuccessOptionText);
        });
    }
}
