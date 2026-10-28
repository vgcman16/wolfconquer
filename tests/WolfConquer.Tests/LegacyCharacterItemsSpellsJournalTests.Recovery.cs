using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterItemsSpellsJournalTests
{
    [Fact]
    public void DistinctAccountAndUidCommitRealConsumedItemAndAddedSpell()
    {
        Run(f =>
        {
            var changes = f.Changes(); var recovery = f.Recovery();
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(recovery.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            Assert.True(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, changes, out reason), reason);
            f.Images(f.Character, f.ItemAfter, f.SpellAfter);
            Assert.Equal(0xA5, File.ReadAllBytes(f.SpellsPath)[15]); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });
    }

    [Theory]
    [InlineData((int)LegacyCharacterItemsSpellsJournalInterruption.AfterPending, 0)]
    [InlineData((int)LegacyCharacterItemsSpellsJournalInterruption.AfterCharacter, 1)]
    [InlineData((int)LegacyCharacterItemsSpellsJournalInterruption.AfterItems, 2)]
    [InlineData((int)LegacyCharacterItemsSpellsJournalInterruption.AfterSpells, 3)]
    public void FreshSevenRouteOwnerFinishesEveryRetainedPrefixAndIsIdempotent(int step, int replaced)
    {
        Run(f =>
        {
            var changes = f.Changes(); var original = f.Recovery(); Assert.True(original.TryEnsureInitialized(out var reason), reason);
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, changes,
                (LegacyCharacterItemsSpellsJournalInterruption)step, out reason));
            Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
            f.Images(f.Character, replaced >= 2 ? f.ItemAfter : f.ItemBefore, replaced >= 3 ? f.SpellAfter : f.SpellBefore);
            Assert.True(File.Exists(f.Pending)); byte[] retained = File.ReadAllBytes(f.Pending);
            var record = Assert.IsType<JsonObject>(JsonNode.Parse(retained));
            Assert.Equal(9, record["Version"]!.GetValue<int>());
            Assert.Equal(new[] { "Changes", "CharacterItemsSpellsDomain", "TransactionId", "Version" }, record.Select(p => p.Key).Order(StringComparer.Ordinal));
            var domain = Assert.IsType<JsonObject>(record["CharacterItemsSpellsDomain"]);
            Assert.Equal(new[] { "Items", "Spells", "Users" }, domain.Select(p => p.Key).Order(StringComparer.Ordinal));
            Assert.Equal(f.Users, domain["Users"]!.GetValue<string>()); Assert.Equal(f.Items, domain["Items"]!.GetValue<string>());
            Assert.Equal(f.Spells, domain["Spells"]!.GetValue<string>());
            var entries = record["Changes"]!.AsArray();
            Assert.Equal(new[] { 0, 1, 5 }, entries.Select(e => e!["Kind"]!.GetValue<int>()));
            Assert.Equal(new long[] { Fixture.Account, Fixture.Uid, Fixture.Uid }, entries.Select(e => e!["FileId"]!.GetValue<long>()));
            Assert.False(original.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            var fresh = f.Recovery(); Assert.False(fresh.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            Assert.True(fresh.TryEnsureInitialized(out reason), reason);
            Assert.True(fresh.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            f.Images(f.Character, f.ItemAfter, f.SpellAfter); Assert.False(File.Exists(f.Pending));
            var again = f.Recovery(); Assert.True(again.TryEnsureInitialized(out reason), reason);
            f.Images(f.Character, f.ItemAfter, f.SpellAfter); Assert.False(File.Exists(f.Pending));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingSpellParentIsCreatedOnlyAfterPendingAndCanBeRecovered(bool interrupt)
    {
        Run(f =>
        {
            var changes = f.Changes(missingSpells: true); Assert.False(Directory.Exists(f.Spells));
            var recovery = f.Recovery(); Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            if (interrupt)
            {
                Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, changes,
                    LegacyCharacterItemsSpellsJournalInterruption.AfterPending, out reason));
                Assert.StartsWith("tradeJournalRecoveryRequired:", reason); Assert.True(File.Exists(f.Pending));
                Assert.False(Directory.Exists(f.Spells));
                var fresh = f.Recovery(); Assert.True(fresh.TryEnsureInitialized(out reason), reason);
            }
            else Assert.True(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, changes, out reason), reason);
            f.Images(f.Character, f.ItemAfter, f.SpellAfter); Assert.False(File.Exists(f.Pending));
        }, missingSpells: true);
    }
}
