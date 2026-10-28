using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterSpellsJournalTests
{
    [Fact]
    public Task DistinctAccountAndCharacterKeysCommitTheirExactTwoImagesWithoutSessionOrPacketPublication() =>
        RunJournalAsync(fixture =>
        {
            var changes = Changes(fixture);
            Assert.True(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot, changes, out var reason), reason);

            RequireImages(fixture, changes[0].After, changes[1].After);
            fixture.ExpectNoPending();
            fixture.RequireFilesUnchanged();
            RequireNoExposure(fixture);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
            Assert.True(fixture.Recovery.IsReadyForCharacterSpells(fixture.Users, fixture.SpellsRoot));
        });

    [Theory]
    [InlineData((int)LegacyCharacterSpellsJournalInterruption.AfterPending, 0)]
    [InlineData((int)LegacyCharacterSpellsJournalInterruption.AfterCharacter, 1)]
    [InlineData((int)LegacyCharacterSpellsJournalInterruption.AfterSpells, 2)]
    public Task FreshConfiguredRecoveryFinishesEachRetainedRealPrefixExactlyOnce(
        int interruption, int replacements) => RunJournalAsync(fixture =>
    {
        var changes = Changes(fixture);
        // Reverse caller order: canonical target1 is still Character, target2 Spells.
        Assert.False(LegacyTradeJournal.TryCommitCharacterSpells(fixture.Users, fixture.SpellsRoot,
            changes.Reverse().ToArray(), (LegacyCharacterSpellsJournalInterruption)interruption, out var reason));
        Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        RequireImages(fixture, replacements >= 1 ? changes[0].After : changes[0].Before!,
            replacements >= 2 ? changes[1].After : changes[1].Before!);
        var retained = RememberPending(fixture);
        var record = Pending(fixture);
        Assert.Equal(8, record["Version"]!.GetValue<int>());
        Assert.Equal(new[] { "Changes", "CharacterSpellsDomain", "TransactionId", "Version" }, record.Select(pair => pair.Key).Order(StringComparer.Ordinal));
        var domain = record["CharacterSpellsDomain"]!.AsObject();
        Assert.Equal(new[] { "Spells", "Users" }, domain.Select(pair => pair.Key).Order(StringComparer.Ordinal));
        Assert.Equal(fixture.Users, domain["Users"]!.GetValue<string>());
        Assert.Equal(fixture.SpellsRoot, domain["Spells"]!.GetValue<string>());
        var targets = record["Changes"]!.AsArray();
        Assert.Equal(2, targets.Count);
        Assert.Equal(0, targets[0]!["Kind"]!.GetValue<int>());
        Assert.Equal(5, targets[1]!["Kind"]!.GetValue<int>());
        Assert.Equal(fixture.Original.AccountId, targets[0]!["FileId"]!.GetValue<long>());
        Assert.Equal(fixture.Original.Character.Uid, targets[1]!["FileId"]!.GetValue<long>());
        Assert.False(fixture.Recovery.TryRequireLiveJournalClear(out _));
        Assert.False(fixture.Recovery.IsReadyForCharacterSpells(fixture.Users, fixture.SpellsRoot));
        Assert.Equal(retained, File.ReadAllBytes(fixture.PendingPath));

        var fresh = Recovery(fixture);
        Assert.False(fresh.IsReadyForCharacterSpells(fixture.Users, fixture.SpellsRoot));
        Assert.True(fresh.TryEnsureInitialized(out reason), reason);
        Assert.True(fresh.IsReadyForCharacterSpells(fixture.Users, fixture.SpellsRoot));
        RequireImages(fixture, changes[0].After, changes[1].After);
        fixture.ExpectNoPending();
        Assert.False(fixture.Recovery.IsReadyForCharacterSpells(fixture.Users, fixture.SpellsRoot));
        RequireNoExposure(fixture);

        Assert.True(Recovery(fixture).TryEnsureInitialized(out reason), reason);
        RequireImages(fixture, changes[0].After, changes[1].After);
        fixture.ExpectNoPending();
        fixture.RequireFilesUnchanged();
    });
}
