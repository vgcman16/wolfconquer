using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneDecomposeTests
{
    [Theory]
    [InlineData("active")] [InlineData("bound")] [InlineData("raw-target")]
    [InlineData("split")] [InlineData("shared")] [InlineData("stored-sibling")] [InlineData("merge")]
    [InlineData("zero-merge")] [InlineData("near-full")]
    [InlineData("capacity-new")] [InlineData("capacity-merge")]
    [InlineData("bag-boundary")] [InlineData("stored-bag")]
    [InlineData("red")] [InlineData("yellow")]
    public Task ActualFrameDecomposeRegistersItsCompleteRewardsBeforePublishingTheExactSourceSequence(string mode) => Run(async f =>
    {
        var expected = await ConsumeAndAssert(f);
        if (mode == "split") Assert.Equal(new ushort[] { 10000, 10000, 7320 }, expected.Essences.Select(row => row.StackSize));
        if (mode is "merge" or "zero-merge" or "capacity-merge")
        {
            Assert.True(expected.Merged); Assert.Equal((byte)1, expected.Fragment.Locked); Assert.Equal(6U, expected.Fragment.Color);
            Assert.Equal(mode == "zero-merge" ? (ushort)11 : (ushort)10000, expected.Fragment.StackSize);
            Assert.Single(expected.NewUids);
        }
        if (mode == "shared")
        {
            Assert.Empty(expected.RetiredSpells); Assert.Equal("14160:16", f.Origins);
            Assert.Equal((ushort)16, f.Native.MySpells!.ClientSpells[14160].Level);
        }
        if (mode == "stored-sibling")
        {
            Assert.Equal(new ushort[] { 211, 212 }, f.Current.PlayerItems.RuneItems.Select(row => row.Position));
            Assert.Equal(DecomposeFixture.Shared, f.Current.PlayerItems.RuneItems[1].Uid);
            Assert.Equal(new ushort[] { 14160 }, expected.RetiredSpells);
        }
        if (mode == "active")
        {
            // Repeating the actual request after target retirement creates no
            // second reservation, item, reply or persisted/native mutation.
            var files = f.Data.Files(); var current = f.Current; int first = f.Sent.Count;
            await f.SendAsync(DecomposeFixture.Request()); Assert.Equal(first, f.Sent.Count);
            Assert.Same(current, f.Current); f.Data.AssertFiles(files); f.AssertConverged();
        }
    }, mode: mode);

    [Theory]
    [InlineData("ambiguous")] [InlineData("ambiguous-bag")]
    [InlineData("absent")] [InlineData("absent-bag")]
    public Task ActualStored8140ConsumptionPreservesEveryCharacterAndSpellByteWithoutInventingAClassification(string mode) => Run(async f =>
    {
        var character = File.ReadAllBytes(f.CharacterPath); var spells = File.ReadAllBytes(f.SpellsPath);
        Assert.Equal(4_028_001U, Assert.Single(f.Current.PlayerItems.RuneItems).ItemId);
        Assert.Equal((byte)0xC7, spells[27]); Assert.Null(f.Origins);
        var expected = await ConsumeAndAssert(f); Assert.Empty(expected.RetiredSpells);
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(spells, File.ReadAllBytes(f.SpellsPath));
        Assert.Contains(f.Current.PlayerSpells.Spells, row => row.Id == 8140 && row.Level == 7 && row.Experience == 1234);
    }, mode: mode);

    [Fact]
    public Task ActualDecomposePreservesHealthAboveItsCurrentTransientCapWithoutAHiddenQueryOrHpFrame() => Run(async f =>
    {
        var current = f.Current;
        f.Store.Set(f.Connection, current with { Character = LegacyTransformState.Apply(current.Character, 207, 3000, DateTimeOffset.UtcNow.AddHours(1)) });
        current = f.Current;
        var values = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["MinHitPoints"] = "100000" };
        f.Store.Set(f.Connection, current with { Character = current.Character with { MinHitPoints = 100000, Attributes = values } });
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(f.Current.Character, out var cap)); Assert.Equal(3000U, cap);
        Assert.Equal(100000L, f.Current.Character.MinHitPoints); Assert.Equal(100000, f.Native.Player!.HitPoints);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        Assert.Equal(100000L, saved.MinHitPoints); Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(saved, out _));
        await ConsumeAndAssert(f); Assert.Equal(100000L, f.Current.Character.MinHitPoints); Assert.Equal(100000, f.Native.Player.HitPoints);
    });

    private static async Task<ExpectedDecompose> ConsumeAndAssert(DecomposeFixture f)
    {
        var seed = Seed(f); ExpectedDecompose? expected = null; int first = f.Sent.Count;
        f.BeforeSend = _ =>
        {
            expected ??= Expect(seed, ActualFragmentColor(f, seed));
            AssertWholeFiles(f, seed, expected); f.AssertConverged();
        };
        try { await f.SendAsync(DecomposeFixture.Request()); } finally { f.BeforeSend = null; }
        expected ??= Expect(seed, ActualFragmentColor(f, seed));
        AssertWholeFiles(f, seed, expected); f.AssertConverged();
        var output = f.Sent.Skip(first).ToArray(); Assert.Equal(expected.RetiredSpells.Length + expected.Essences.Length + 2, output.Length);
        int index = 0;
        foreach (var id in expected.RetiredSpells)
            Assert.Equal(new MsgActionPacket { ObjId = DecomposeFixture.Uid, DwParam = id, Type = (MsgActionType)109 }.ToFrameBytes(), output[index++]);
        Assert.Equal(RemoveGolden(DecomposeFixture.Target), output[index++]);
        foreach (var row in expected.Essences) Assert.Equal(RewardGolden(row), output[index++]);
        Assert.Equal(RewardGolden(expected.Fragment, expected.Merged), output[index]);
        foreach (var packet in output.Skip(expected.RetiredSpells.Length + 1))
        { Assert.True(LegacyGameItemPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(packet), out _)); }
        Assert.Equal(seed.Session.Character.Money, f.Current.Character.Money); Assert.Equal(seed.Session.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        Assert.Equal(seed.Session.Character.MinHitPoints, f.Current.Character.MinHitPoints);
        var score = LegacyRuneStoragePacket.ComputeStorageScore(f.Current.PlayerItems);
        if (score >= LegacyRuneRankingState.MinimumScore) Assert.Equal(score, Assert.Single(f.Rankings.Snapshot(), row => row.CharacterUid == DecomposeFixture.Uid).Score);
        else Assert.DoesNotContain(f.Rankings.Snapshot(), row => row.CharacterUid == DecomposeFixture.Uid);
        return expected;
    }

    private static byte? ActualFragmentColor(DecomposeFixture f, DecomposeSeed seed)
    {
        var selected = Assert.Single(seed.Session.PlayerItems.RuneItems, row => row.Uid == DecomposeFixture.Target);
        uint family = selected.ItemId / 10000;
        uint fragmentId = family switch { 401 => selected.Bound == 0 ? 3_306_366U : 3_306_369U,
            402 => selected.Bound == 0 ? 3_306_368U : 3_306_371U, _ => selected.Bound == 0 ? 3_306_367U : 3_306_370U };
        bool merged = seed.Session.PlayerItems.InventoryItems.Any(row => row.ItemId == fragmentId && row.Bound == 1 && row.Position == 0 &&
            row.WarehouseId == 0 && Math.Max((uint)row.StackSize, 1U) + 10 <= 10000);
        if (merged) return null;
        var actual = Assert.Single(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath)).InventoryItems,
            row => row.Uid > seed.HighWater && row.ItemId == fragmentId);
        Assert.InRange(actual.Color, 3U, 8U); return checked((byte)actual.Color);
    }
}
