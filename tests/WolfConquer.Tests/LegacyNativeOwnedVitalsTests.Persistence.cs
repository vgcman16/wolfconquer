using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeOwnedVitalsTests
{
    [Fact]
    public Task RealJointJournalKeepsBothPhysicalValuesOldUntilItsExactCompleteImageCommits() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); var before = f.Current; var binding = Binding(f);
        var original = File.ReadAllBytes(f.CharacterPath);
        var target = before.Character with { MinHitPoints = 222, MinMana = 333 };
        var image = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(original, target);
        var changes = Changes(f, original, image); int calls = 0;
        Assert.True(f.Store.TryCommitSession(f.Connection, before, before with { Character = target }, () =>
        {
            calls++; Assert.Same(before, f.Current); Assert.Equal(1000, f.Player.HitPoints); Assert.Equal((ushort)5000, f.Player.Mana);
            var committed = LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out var reason);
            Assert.True(committed, reason);
            Assert.Equal(222L, Saved(f).MinHitPoints); Assert.Equal(333L, Saved(f).MinMana);
            Assert.Equal(1000, f.Player.HitPoints); Assert.Equal((ushort)5000, f.Player.Mana);
            return committed; // Only the actual journal result, never an accepting saver.
        }, out var final));
        Assert.Equal(1, calls); Assert.Same(final, f.Current); Assert.Same(binding, Binding(f));
        Assert.Equal(image, File.ReadAllBytes(f.CharacterPath)); Vitals(f, 222, 333);
        Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Fact]
    public Task RealJointJournalDefiniteBeforeImageRefusalPreservesBothVitalsAndAllowsActualRetry() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); var before = f.Current; var files = f.Files();
        var original = File.ReadAllBytes(f.CharacterPath);
        var target = before.Character with { MinHitPoints = 222, MinMana = 333 };
        var image = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(original, target);
        var stale = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(original, before.Character with { MinMana = 4999 });
        var changes = Changes(f, stale, image);
        Assert.False(f.Store.TryCommitSession(f.Connection, before, before with { Character = target },
            () => LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out _), out var refused));
        Assert.Null(refused); Assert.Same(before, f.Current); Vitals(f, 1000, 5000); f.RequireFiles(files);
        Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked);
        var actual = Changes(f, original, image);
        Assert.True(f.Store.TryCommitSession(f.Connection, before, before with { Character = target },
            () => LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, actual, out _), out var final));
        Assert.Same(final, f.Current); Vitals(f, 222, 333); Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RealJournalWrongAfterimageOrPostCommitRawConflictFencesWithoutPartialPhysicalPublication(bool conflict) => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); var before = f.Current; var binding = Binding(f);
        var original = File.ReadAllBytes(f.CharacterPath);
        var target = before.Character with { MinHitPoints = 222, MinMana = 333 };
        var image = conflict ? LegacyBoundHitpointsScalarWrite.ProjectAfterImage(original, target) : original;
        var changes = Changes(f, original, image); bool committed = false;
        Assert.Throws<LegacyCommittedSpellPublicationException>(() => f.Store.TryCommitSession(f.Connection,
            before, before with { Character = target }, () =>
            {
                committed = LegacyTradeJournal.TryCommitCharacterSpells(f.Users, f.Spells, changes, out var reason);
                Assert.True(committed, reason);
                if (conflict)
                {
                    var map = Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells).ClientSpells;
                    var row = map[6001];
                    // Genuine exposed map with a same-value row replacement;
                    // no resource seeding, owner substitution or fake writer.
                    map[6001] = new LegacyNativeMsgSpell { ID = row.ID, Level = row.Level, Experience = row.Experience,
                        PreviousLevel = row.PreviousLevel, SoulLevel = row.SoulLevel, UseSpellSoul = row.UseSpellSoul };
                }
                return committed;
            }));
        Assert.True(committed); Assert.Equal(image, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(conflict ? 222L : 1000L, Saved(f).MinHitPoints); Assert.Equal(conflict ? 333L : 5000L, Saved(f).MinMana);
        Assert.Equal(1000, f.Player.HitPoints); Assert.Equal((ushort)5000, f.Player.Mana);
        Assert.Null(f.CurrentOrNull); Assert.True(binding.IsRetired); Assert.True(f.Recovery.IsBlocked);
        Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.Empty(f.Sent);
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
        Assert.ThrowsAny<InvalidOperationException>(() => f.Store.Set(f.Connection, before));
        Assert.Throws<InvalidOperationException>(() => f.Player.Mana = 5);
    });

    [WindowsTradeJournalFailureFact]
    public Task ActualWindowsFileReplacementRefusalLeavesBothVitalsAndHolderUnchanged() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); var before = f.Current; var files = f.Files();
        var target = before with { Character = before.Character with { MinHitPoints = 222, MinMana = 333 } };
        using (var held = new FileStream(f.CharacterPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<LegacyNativeHitpointsRefusalException>(() => f.Store.Set(f.Connection, target));
            Assert.False(f.Store.TryCompareAndSet(f.Connection, before, target, out var refused)); Assert.Null(refused);
            Assert.Same(before, f.Current); Vitals(f, 1000, 5000); f.RequireFiles(files);
            Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked);
        }
        Assert.True(f.Store.TryCompareAndSet(f.Connection, before, target, out var final));
        Assert.Same(final, f.Current); Vitals(f, 222, 333); Assert.Empty(f.Sent);
    });
}
