using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneChanceTests
{
    [Theory]
    [InlineData(149U, (ushort)0, false)] [InlineData(149U, (ushort)1, false)]
    [InlineData(15000U, (ushort)0, true)] [InlineData(15000U, (ushort)1, true)]
    public Task ActualOwnedChanceCommitsTheZeroAndHundredPercentBoundariesBeforeTheSingleCompleteItemUpdate(
        uint experience, ushort stack, bool passed) => Run(async f =>
    {
        Assert.Equal("14160:16", f.Origins); Assert.Equal((ushort)16, f.Native.MySpells!.ClientSpells[14160].Level);
        Assert.Equal(stack, Assert.Single(f.Native.OrdinarySpellBinding!.Items.Rune.Objects, row => row.Uid == ChanceFixture.Target).StackSize);
        await ChanceAndAssert(f, passed ? ChanceFixture.TargetType + 1 : ChanceFixture.TargetType, 0);
        Assert.Equal("14160:16", f.Origins); Assert.Equal((ushort)16, f.Native.MySpells.ClientSpells[14160].Level);
        Assert.Equal((byte)0xA5, File.ReadAllBytes(f.SpellsPath)[15]);
    }, experience, stack);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualZeroProgressOrMaximumRuneOnlyUpdatesTheExactUnchangedExistingItem(bool maximum) => Run(async f =>
    {
        var files = f.Data.Files(); var before = Assert.Single(f.Current.PlayerItems.RuneItems);
        await ChanceAndAssert(f, before.ItemId, before.RuneExperience); f.Data.AssertFiles(files);
        Assert.Equal(maximum ? "14160:26" : "14160:16", f.Origins);
    }, experience: maximum ? 7U : 0U, mode: maximum ? "maximum" : "active");

    [Fact]
    public Task ChanceDoesNotImportEquipRestrictionsForAnAlreadyOwnedStoredLockedAndBoundRune() => Run(async f =>
    {
        var before = Assert.Single(f.Current.PlayerItems.RuneItems); Assert.Equal((byte)1, before.Locked);
        Assert.Equal((byte)1, before.Bound); Assert.Equal((ushort)212, before.Position);
        Assert.Null(f.Origins); Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
        await ChanceAndAssert(f, ChanceFixture.TargetType + 1, 0);
        var after = Assert.Single(f.Current.PlayerItems.RuneItems); Assert.Equal(before.Locked, after.Locked);
        Assert.Equal(before.Bound, after.Bound); Assert.Null(f.Origins); Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
    }, experience: 15000, mode: "stored-restricted");

    [Fact]
    public Task ActualChancePreservesSavedAndNativeHealthWhileTheRealStoreOwnsAnActiveTransform() => Run(async f =>
    {
        var before = f.Current;
        f.Store.Set(f.Connection, before with
        { Character = LegacyTransformState.Apply(before.Character, 207, 3000, DateTimeOffset.UtcNow.AddHours(1)) });
        var transformed = f.Current;
        var attributes = new Dictionary<string, string>(transformed.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["MinHitPoints"] = "100000" };
        // A genuine bound scalar publication raises current/native/saved HP
        // above the transient cap. Chance must not silently perform a query clamp.
        f.Store.Set(f.Connection, transformed with
        { Character = transformed.Character with { MinHitPoints = 100000, Attributes = attributes } });
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(f.Current.Character, out var cap)); Assert.Equal(3000U, cap);
        Assert.Equal(100000L, f.Current.Character.MinHitPoints); Assert.Equal(100000, f.Native.Player!.HitPoints);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        Assert.Equal(100000L, saved.MinHitPoints); Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(saved, out _));
        Assert.False(saved.Attributes.ContainsKey(LegacyTransformState.MaxHitpointsAttribute));
        Assert.False(saved.Attributes.ContainsKey(LegacyTransformState.MaxHitpointsAliasAttribute));
        var character = File.ReadAllBytes(f.CharacterPath);
        await ChanceAndAssert(f, ChanceFixture.TargetType + 1, 0);
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(100000L, f.Current.Character.MinHitPoints);
        Assert.Equal(100000, f.Native.Player.HitPoints);
    }, experience: 15000);
}
