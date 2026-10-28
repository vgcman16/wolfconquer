using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterItemsSpellsJournalTests
{
    [Theory]
    [InlineData("extra-root")]
    [InlineData("missing-domain")]
    [InlineData("extra-domain")]
    [InlineData("wrong-domain")]
    [InlineData("wrong-order")]
    [InlineData("wrong-kind")]
    [InlineData("wrong-id")]
    [InlineData("duplicate-version")]
    [InlineData("truncated-json")]
    public void MalformedRetainedRecordFailsFreshRecoveryWithoutChangingTargets(string fault)
    {
        Run(f =>
        {
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, f.Changes(),
                LegacyCharacterItemsSpellsJournalInterruption.AfterPending, out _));
            var record = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllBytes(f.Pending)));
            switch(fault)
            {
                case "extra-root": record["Unexpected"] = true; break;
                case "missing-domain": record.Remove("CharacterItemsSpellsDomain"); break;
                case "extra-domain": record["CharacterItemsSpellsDomain"]!["Extra"] = "x"; break;
                case "wrong-domain": record["CharacterItemsSpellsDomain"]!["Items"] = f.Spells; break;
                case "wrong-order": var entries = record["Changes"]!.AsArray(); var first = entries[0]; entries.RemoveAt(0); entries.Add(first); break;
                case "wrong-kind": record["Changes"]![1]!["Kind"] = 5; break;
                case "wrong-id": record["Changes"]![2]!["FileId"] = Fixture.Uid + 1; break;
            }
            string json = record.ToJsonString();
            if (fault == "duplicate-version") json = json.Replace("\"Version\":9", "\"Version\":9,\"Version\":9", StringComparison.Ordinal);
            if (fault == "truncated-json") json = json[..^1];
            File.WriteAllText(f.Pending, json, new UTF8Encoding(false)); byte[] pending = File.ReadAllBytes(f.Pending);
            var fresh = f.Recovery(); Assert.False(fresh.TryEnsureInitialized(out var reason));
            Assert.StartsWith("tradeJournalRecoveryRequired:", reason); Assert.True(fresh.IsBlocked);
            Assert.False(fresh.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            Assert.Equal(pending, File.ReadAllBytes(f.Pending)); f.Images(f.Character, f.ItemBefore, f.SpellBefore);
        });
    }

    [Fact]
    public void SharedPendingSlotRefusesAnotherWriterAndWrongRecoveryDomainCannotConsumeIt()
    {
        Run(f =>
        {
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, f.Changes(),
                LegacyCharacterItemsSpellsJournalInterruption.AfterItems, out _));
            byte[] pending = File.ReadAllBytes(f.Pending);
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, f.Changes(), out _));
            Assert.Equal(pending, File.ReadAllBytes(f.Pending));
            var wrong = f.Recovery(spells: Path.Combine(f.Root, "OtherSpells"));
            Assert.False(wrong.TryEnsureInitialized(out var reason)); Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
            Assert.Equal(pending, File.ReadAllBytes(f.Pending)); f.Images(f.Character, f.ItemAfter, f.SpellBefore);
            var fresh = f.Recovery(); Assert.True(fresh.TryEnsureInitialized(out reason), reason);
            f.Images(f.Character, f.ItemAfter, f.SpellAfter); Assert.False(File.Exists(f.Pending));
        });
    }

    [Fact]
    public void ForeignCurrentImageAndMissingItemsDirectoryFailRecoveryWithoutDiscardingIntent()
    {
        Run(f =>
        {
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, f.Changes(),
                LegacyCharacterItemsSpellsJournalInterruption.AfterPending, out _));
            byte[] pending = File.ReadAllBytes(f.Pending); byte[] foreign = f.ItemBefore.ToArray(); foreign[20] ^= 1;
            File.WriteAllBytes(f.ItemsPath, foreign);
            var first = f.Recovery(); Assert.False(first.TryEnsureInitialized(out _));
            Assert.True(first.IsBlocked); Assert.Equal(pending, File.ReadAllBytes(f.Pending));
            f.Images(f.Character, foreign, f.SpellBefore);
            File.WriteAllBytes(f.ItemsPath, f.ItemBefore);
            string moved = Path.Combine(f.Root, "ItemsUnavailable"); Directory.Move(f.Items, moved);
            try
            {
                var missing = f.Recovery(); Assert.False(missing.TryEnsureInitialized(out _)); Assert.True(missing.IsBlocked);
                Assert.Equal(pending, File.ReadAllBytes(f.Pending));
            }
            finally { Directory.Move(moved, f.Items); }
            var fresh = f.Recovery(); Assert.True(fresh.TryEnsureInitialized(out var reason), reason);
            f.Images(f.Character, f.ItemAfter, f.SpellAfter); Assert.False(File.Exists(f.Pending));
        });
    }
}
