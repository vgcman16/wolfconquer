using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterItemsSpellsJournalTests
{
    [Theory]
    [InlineData("item-zero-uid")]
    [InlineData("item-duplicate")]
    [InlineData("item-truncated")]
    [InlineData("item-tail")]
    [InlineData("perfection-foreign")]
    [InlineData("perfection-duplicate")]
    [InlineData("spell-zero")]
    [InlineData("spell-duplicate")]
    [InlineData("spell-negative")]
    [InlineData("spell-tail")]
    [InlineData("character-uid")]
    [InlineData("character-duplicate")]
    [InlineData("items-id")]
    [InlineData("missing-item-before")]
    [InlineData("missing-character-before")]
    [InlineData("missing-after")]
    [InlineData("wrong-shape")]
    public void InvalidCompleteImagesOrIdentitiesRefuseBeforeAnyOutput(string fault)
    {
        Run(f =>
        {
            var rows = f.Changes(); byte[] bad = f.ItemBefore.ToArray();
            switch(fault)
            {
                case "item-zero-uid": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(4), 0); rows[1] = rows[1] with { After = bad }; break;
                case "item-duplicate": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(4 + PlayerItemSnapshotLoader.LegacyDbItemSize), 900001); rows[1] = rows[1] with { After = bad }; break;
                case "item-truncated": rows[1] = rows[1] with { After = bad[..^1] }; break;
                case "item-tail": rows[1] = rows[1] with { After = bad.Concat(new byte[] { 1 }).ToArray() }; break;
                case "perfection-foreign": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(bad.Length - PlayerItemSnapshotLoader.LegacyPerfectionSize), 900099); rows[1] = rows[1] with { After = bad }; break;
                case "perfection-duplicate":
                    BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(4 + 2 * PlayerItemSnapshotLoader.LegacyDbItemSize), 2);
                    rows[1] = rows[1] with { After = bad.Concat(bad[^PlayerItemSnapshotLoader.LegacyPerfectionSize..]).ToArray() }; break;
                case "spell-zero": bad = f.SpellAfter.ToArray(); bad[4] = bad[5] = 0; rows[2] = rows[2] with { After = bad }; break;
                case "spell-duplicate": bad = f.SpellAfter.ToArray(); bad[16] = bad[4]; bad[17] = bad[5]; rows[2] = rows[2] with { After = bad }; break;
                case "spell-negative": bad = f.SpellAfter.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(8), -1); rows[2] = rows[2] with { After = bad }; break;
                case "spell-tail": rows[2] = rows[2] with { After = f.SpellAfter.Concat(new byte[] { 1 }).ToArray() }; break;
                case "character-uid": rows[0] = rows[0] with { After = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(f.Character).Replace("UID=1294017", "UID=1294018")) }; break;
                case "character-duplicate": rows[0] = rows[0] with { After = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(f.Character).Replace("UID=1294017", "UID=1294017\nUID=1294017")) }; break;
                case "items-id": rows[1] = rows[1] with { FileId = Fixture.Uid + 1 }; break;
                case "missing-item-before": rows[1] = rows[1] with { Before = null }; break;
                case "missing-character-before": rows[0] = rows[0] with { Before = null }; break;
                case "missing-after": rows[2] = rows[2] with { After = null! }; break;
                case "wrong-shape": rows = [rows[0], rows[1], rows[1]]; break;
            }
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, rows, out _));
            f.Images(f.Character, f.ItemBefore, f.SpellBefore); Assert.False(File.Exists(f.Pending));
        });
    }

    [Fact]
    public void WrongRootsAndForeignCurrentBytesRefuseBeforePublishing()
    {
        Run(f =>
        {
            var rows = f.Changes();
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Users, f.Spells, rows, out _));
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, Path.Combine(f.Root, "missing"), f.Spells, rows, out _));
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, Path.Combine(f.Items, "nested"), rows, out _));
            byte[] foreign = f.ItemBefore.ToArray(); foreign[20] ^= 1; File.WriteAllBytes(f.ItemsPath, foreign);
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, rows, out _));
            f.Images(f.Character, foreign, f.SpellBefore); Assert.False(File.Exists(f.Pending));
        });
    }
}
