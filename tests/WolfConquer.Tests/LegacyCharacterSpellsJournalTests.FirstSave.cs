using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterSpellsJournalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullSpellPreimageUsesTheGenuinelyAbsentConfiguredParentOnlyAfterPublication(bool interruptAfterPending) => RunJournalAsync(fixture =>
    {
        var changes = Changes(fixture);
        changes[1] = changes[1] with { Before = null };
        fixture.RemoveSeededSpellParent();
        var configured = Recovery(fixture);
        Assert.True(configured.TryEnsureInitialized(out var reason), reason);
        Assert.True(configured.IsReadyForCharacterSpells(fixture.Users, fixture.SpellsRoot));
        Assert.False(Directory.Exists(fixture.SpellsRoot));
        Assert.False(File.Exists(fixture.SpellPath));

        if (interruptAfterPending)
        {
            Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes,
                LegacyCharacterSpellsJournalInterruption.AfterPending, out reason));
            Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
            RememberPending(fixture);
            Assert.Equal(changes[0].Before, fixture.CharacterBytes);
            Assert.False(Directory.Exists(fixture.SpellsRoot));
            fixture.RequireFilesUnchanged();
            var record = Pending(fixture);
            Assert.Null(record["Changes"]![1]!["Before"]);
            Assert.True(Recovery(fixture).TryEnsureInitialized(out reason), reason);
        }
        else Assert.True(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes, out reason), reason);

        Assert.True(Directory.Exists(fixture.SpellsRoot));
        RequireImages(fixture, changes[0].After, changes[1].After);
        fixture.ExpectNoPending();
        fixture.RequireFilesUnchanged();
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "Items")));
        RequireNoExposure(fixture);
    });
}
