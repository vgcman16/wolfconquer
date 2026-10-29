using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeOwnedHitpointsTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public Task RealJournalKeepsOldPhysicalHpUntilItsSavedResultAndPublishesExactSignedInt(int hp) => Run(async f =>
    {
        await f.LoginAsync(); AssertHp(f, 1000); f.Sent.Clear();
        var before = f.Current; var binding = Binding(f);
        var oldCharacter = File.ReadAllBytes(f.CharacterPath);
        var newCharacter = HpImage(f, oldCharacter, hp);
        var changes = HpChanges(f, oldCharacter, newCharacter);
        var proposed = before with { Character = before.Character with { MinHitPoints = hp } };
        int writerCalls = 0;

        Assert.True(f.Store.TryCommitSession(f.Connection, before, proposed, () =>
        {
            writerCalls++;
            Assert.Same(before, f.Current); Assert.Equal(1000, f.Player.HitPoints);
            Assert.True(LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out var reason), reason);
            Assert.Equal((long)hp, SavedHp(f));
            Assert.Equal(1000, f.Player.HitPoints); // The writer does not publish native state.
            return true; // This is the actual journal's proven true result above.
        }, out var final));

        Assert.Equal(1, writerCalls); Assert.Same(f.Current, final); Assert.Same(binding, Binding(f));
        Assert.Equal(newCharacter, File.ReadAllBytes(f.CharacterPath)); AssertHp(f, hp);
        Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Fact]
    public Task RealJournalBeforeImageRefusalLeavesBothOwnersFilesAndOutputsUnchanged() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); AssertHp(f, 1000);
        var before = f.Current; var files = f.Files(); var binding = Binding(f);
        var actual = File.ReadAllBytes(f.CharacterPath);
        var changes = HpChanges(f, HpImage(f, actual, 999), HpImage(f, actual, 555));
        int calls = 0;

        Assert.False(f.Store.TryCommitSession(f.Connection, before,
            before with { Character = before.Character with { MinHitPoints = 555 } }, () =>
            {
                calls++;
                return LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out _);
            }, out var final));

        Assert.Equal(1, calls); Assert.Null(final); Assert.Same(before, f.Current);
        Assert.Same(binding, Binding(f)); AssertHp(f, 1000); f.RequireFiles(files);
        Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked);
    });

    [Fact]
    public Task JournalTrueWithoutTheRequiredSavedHpIsCommittedUncertaintyAndRetiresOnlyItsOwner() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear();
        var before = f.Current; var binding = Binding(f);
        var bytes = File.ReadAllBytes(f.CharacterPath);
        var changes = HpChanges(f, bytes, bytes);
        bool journalTrue = false;

        Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
            f.Store.TryCommitSession(f.Connection, before,
                before with { Character = before.Character with { MinHitPoints = 555 } }, () =>
                {
                    journalTrue = LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out var reason);
                    Assert.True(journalTrue, reason);
                    return journalTrue;
                }));

        Assert.True(journalTrue); Assert.Equal(bytes, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(1000, f.Player.HitPoints); Assert.Equal(1000L, SavedHp(f));
        Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
        Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.Null(f.CurrentOrNull);
        Assert.Empty(f.Sent); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, before));
    });

    [Fact]
    public Task PostJournalActualRawSpellConflictKeepsSavedHpButNeverPartiallyPublishesPhysicalHp() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear();
        var before = f.Current; var binding = Binding(f);
        var oldCharacter = File.ReadAllBytes(f.CharacterPath);
        var newCharacter = HpImage(f, oldCharacter, 555);
        var changes = HpChanges(f, oldCharacter, newCharacter);
        var map = Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells).ClientSpells;
        var original = map[6001];
        bool wrote = false;

        Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
            f.Store.TryCommitSession(f.Connection, before,
                before with { Character = before.Character with { MinHitPoints = 555 } }, () =>
                {
                    wrote = LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out var reason);
                    Assert.True(wrote, reason);
                    Assert.Equal(1000, f.Player.HitPoints);
                    // Real exposed native map, actual same-count row replacement;
                    // no HP setter, synthetic comparer or owner injection.
                    map[6001] = new LegacyNativeMsgSpell { ID = original.ID, Level = original.Level,
                        Experience = original.Experience, PreviousLevel = original.PreviousLevel,
                        SoulLevel = original.SoulLevel, UseSpellSoul = original.UseSpellSoul };
                    return wrote;
                }));

        Assert.True(wrote); Assert.Equal(newCharacter, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(555L, SavedHp(f)); Assert.Equal(1000, f.Player.HitPoints);
        Assert.Null(f.CurrentOrNull); Assert.True(binding.IsRetired); Assert.True(f.Recovery.IsBlocked);
        Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.Empty(f.Sent);
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });
}
