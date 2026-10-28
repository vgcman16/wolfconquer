using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterItemsSpellsJournalTests
{
    [WindowsCharacterItemsSpellsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoveryClearsEveryAlreadyAfterStageBeforeRemovingPending(bool holdStage)
    {
        Run(f =>
        {
            Assert.False(LegacyTradeJournal.TryCommitCharacterItemsSpells(f.Users, f.Items, f.Spells, f.Changes(),
                LegacyCharacterItemsSpellsJournalInterruption.AfterSpells, out _));
            f.Images(f.Character, f.ItemAfter, f.SpellAfter);
            byte[] pending = File.ReadAllBytes(f.Pending);
            string id = JsonNode.Parse(pending)!["TransactionId"]!.GetValue<string>();
            Assert.True(Guid.TryParseExact(id, "N", out var transaction) && transaction != Guid.Empty);
            var stages = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath }
                .Select(path => path + ".managed-trade-" + id + ".tmp").ToArray();
            var values = new[] { f.Character, f.ItemAfter, f.SpellAfter };
            for (int i = 0; i < stages.Length; i++)
            {
                using var output = new FileStream(stages[i], FileMode.CreateNew, FileAccess.Write, FileShare.None);
                output.Write(values[i]); output.Flush(true);
            }
            if (holdStage)
            {
                using (var held = new FileStream(stages[1], FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var blocked = f.Recovery(); Assert.False(blocked.TryEnsureInitialized(out var reason), reason);
                    Assert.True(blocked.IsBlocked); Assert.Equal(pending, File.ReadAllBytes(f.Pending));
                    Assert.False(blocked.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
                }
            }
            var fresh = f.Recovery(); Assert.True(fresh.TryEnsureInitialized(out var error), error);
            Assert.False(File.Exists(f.Pending)); Assert.All(stages, stage => Assert.False(File.Exists(stage)));
            f.Images(f.Character, f.ItemAfter, f.SpellAfter);
        });
    }

    private sealed class WindowsCharacterItemsSpellsTheoryAttribute : TheoryAttribute
    {
        public WindowsCharacterItemsSpellsTheoryAttribute()
        {
            if (!OperatingSystem.IsWindows())
                Skip = "Requires Windows file-sharing replacement and cleanup controls.";
        }
    }
}
