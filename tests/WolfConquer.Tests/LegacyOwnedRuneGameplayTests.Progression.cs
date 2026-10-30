using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Fact]
    public Task ARealRuneGrantCannotGainSpellXpAndItsUnchangedOwnedClaimCanStillBeUnequipped() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); f.AssertConverged(); f.AssertMarker("14160:16");
        var before = f.Current; var files = f.Data.Files(); int first = f.Sent.Count;
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(before.PlayerSpells, f.Catalog, F.FirstSpell, out var learned, out var spell));
        Assert.Equal(true, spell.IsRuneSkill); Assert.Equal((ushort)16, learned.Level);
        Assert.Equal(18, f.Catalog.MagicByTypeId[F.FirstSpell].Count);
        // This input reaches the former XP branch: level16 is below Count-1,
        // damage is positive, and the real metadata threshold is100000.
        var result = await InvokeOwnedRuneProgression(f.Context, before, learned, spell, 500);
        Assert.Same(before, result); Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count);
        f.Data.AssertFiles(files); f.AssertConverged(); f.AssertMarker("14160:16");
        await f.DispatchAsync(F.Unequip()); f.AssertConverged(); f.AssertMarker("");
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == F.FirstSpell);
        Assert.False(f.Native.MySpells!.ClientSpells.ContainsKey(F.FirstSpell));
    });

    [Fact]
    public Task ProvenOrdinaryMetadataStillCommitsExactXpForAPreexistingUnclaimedSpell() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); f.AssertConverged(); f.AssertMarker(null);
        var before = f.Current; int first = f.Sent.Count;
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(before.PlayerSpells, f.Catalog, F.FirstSpell, out var learned, out var spell));
        Assert.Equal(false, spell.IsRuneSkill); Assert.Equal((ushort)7, learned.Level); Assert.Equal(1234, learned.Experience);
        var expectedEntry = learned with { Experience = 1734 };
        var expectedFiles = f.Data.Files(); var expectedImage = File.ReadAllBytes(f.SpellsPath);
        var rows = PlayerSpellSnapshotLoader.Parse(expectedImage).Spells;
        var ordinal = Enumerable.Range(0, rows.Count).Single(index => rows[index].Id == F.FirstSpell);
        BinaryPrimitives.WriteInt32LittleEndian(expectedImage.AsSpan(sizeof(int) + ordinal * PlayerSpellSnapshotLoader.LegacySpellSize + 4), 1734);
        expectedFiles[Path.GetRelativePath(f.Data.Root, f.SpellsPath)] = expectedImage;
        var result = await InvokeOwnedRuneProgression(f.Context, before, learned, spell, 500);
        Assert.NotSame(before, result); Assert.Same(result, f.Current);
        Assert.Equal(expectedEntry, Assert.Single(result.PlayerSpells.Spells, row => row.Id == F.FirstSpell));
        Assert.Equal(first + 1, f.Sent.Count); Assert.Equal(LegacySpellPacket.FromSnapshot(expectedEntry).ToFrameBytes(), f.Sent[first]);
        f.Data.AssertFiles(expectedFiles); f.AssertMarker(null); AssertOwnedRuneProgressionCoherent(f);
        // This is an unclaimed existing ordinary spell. It grants no authority
        // to convert a Rune-owned row through an unjoined progression owner.
    }, legacy: true, classification: "0");

    [Theory]
    [InlineData("bad")]
    [InlineData("1\r\nIsRuneSkill=0")]
    public Task MalformedOrDuplicateExplicitClassificationCannotSilentlyBecomeOrdinaryXp(string classification) => Run(async f =>
    {
        var before = f.Current; var files = f.Data.Files(); int first = f.Sent.Count;
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(before.PlayerSpells, f.Catalog, F.FirstSpell, out var learned, out var spell));
        Assert.Null(spell.IsRuneSkill);
        Assert.True(LegacyRuneSpellClassification.HasExplicitEntry(f.Catalog, F.FirstSpell));
        var result = await InvokeOwnedRuneProgression(f.Context, before, learned, spell, 500);
        Assert.Same(before, result); Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count);
        f.Data.AssertFiles(files); f.AssertConverged();
    }, legacy: true, classification: classification);

    [Fact]
    public Task AStaleActualOrdinaryClassificationRefusesTheRealSpellWriterBeforeXpPublication() => Run(async f =>
    {
        await f.DispatchAsync(F.Equip()); f.AssertConverged(); f.AssertMarker(null);
        var before = f.Current; var files = f.Data.Files(); int first = f.Sent.Count;
        Assert.True(LegacyRuneSpellClassification.TryCapture(f.Catalog, F.FirstSpell, out var proof));
        Assert.False(proof!.IsRuneSkill); Assert.True(proof.IsCurrent());
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(before.PlayerSpells, f.Catalog, F.FirstSpell, out var learned, out var spell));
        var progress = LegacyMagicRuntime.ApplySpellExperience(learned, spell, 500, 1, 18, 140, isRuneSkill: false);
        Assert.True(progress.Updated);
        var proposed = new PlayerSpellSnapshot(before.PlayerSpells.Spells.Select(row => row.Id == learned.Id ? progress.Spell : row).ToArray());
        var effect = Assert.Single(f.Catalog.MagicEffects, row => row.Id == (long)F.FirstSpell * 100);
        var attributes = Assert.IsType<Dictionary<string, string>>(effect.Attributes); var original = attributes["IsRuneSkill"];
        attributes["IsRuneSkill"] = "1"; // Deliberate actual metadata drift after the valid capture.
        try
        {
            Assert.False(proof.IsCurrent());
            object?[] arguments = [f.Context, before, proposed, null, null, CancellationToken.None, null, null, proof];
            Assert.False(Assert.IsType<bool>(InvokeOwnedRunePrivate("TryCommitLegacyFreeSpellsCore", arguments)));
            Assert.Same(before, arguments[6]); Assert.Equal("spellMutationInputsChanged", arguments[7]);
        }
        finally { attributes["IsRuneSkill"] = original; }
        Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files); f.AssertConverged();
    }, legacy: true, classification: "0");

    private static async Task<WorldSession> InvokeOwnedRuneProgression(WorldPacketHandlerContext context, WorldSession session,
        PlayerSpellSnapshotEntry learned, LegacyMagicRuntimeSpell spell, ulong damage)
    {
        object?[] arguments = [context, session, learned, spell, damage, CancellationToken.None, null];
        return await Assert.IsType<ValueTask<WorldSession>>(InvokeOwnedRunePrivate("TryApplyLegacyPlayerMagicSpellExperienceAsync", arguments));
    }

    private static object? InvokeOwnedRunePrivate(string name, object?[] arguments)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Actual spell progression method was not found: " + name);
        try { return method.Invoke(null, arguments); }
        catch (TargetInvocationException error) when (error.InnerException is { } original)
        { ExceptionDispatchInfo.Capture(original).Throw(); throw; }
    }

    private static void AssertOwnedRuneProgressionCoherent(F fixture)
    {
        var current = fixture.Current; var native = fixture.Native;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
        Assert.True(fixture.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(fixture.Catalog, binding.ItemCatalog);
        Assert.Equal(current.PlayerSpells.Spells, PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(fixture.SpellsPath)).Spells);
        Assert.Equal(current.PlayerSpells.Spells.Select(row => row.Id).Order(), native.MySpells!.ClientSpells.Keys.Order());
        foreach (var row in current.PlayerSpells.Spells)
        {
            var actual = native.MySpells.ClientSpells[row.Id];
            Assert.Equal(row.Level, actual.Level); Assert.Equal(row.Experience, actual.Experience);
            Assert.Equal(row.PreviousLevel, actual.PreviousLevel); Assert.Equal(row.SoulLevel, actual.SoulLevel);
            Assert.Equal(row.UseSpellSoul, actual.UseSpellSoul);
        }
        Assert.False(LegacyTradeJournal.HasPending(fixture.Data.Users)); Assert.False(fixture.Runtime.Recovery.IsBlocked);
    }
}
