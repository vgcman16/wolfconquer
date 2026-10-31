using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    [Theory]
    [InlineData("in")] [InlineData("in-fragment")] [InlineData("in-rich")]
    [InlineData("out")] [InlineData("out-rich")] [InlineData("in-zero")] [InlineData("out-zero")]
    public Task ActualOwnedDirectBagMovesPreserveEveryPayloadAndPublishTheSourceOrderedSequence(string mode) => Run(async f =>
    {
        var selected = f.Selected(); var expected = await MoveAndAssert(f);
        Assert.Equal(selected.StackSize, expected.Result.StackSize); Assert.Equal(selected.Locked, expected.Result.Locked);
        Assert.Equal(selected.Bound, expected.Result.Bound); Assert.Equal(selected.RuneExperience, expected.Result.RuneExperience);
        Assert.Equal(selected.DepositedItems.Select(row => row.Uid), expected.Result.DepositedItems.Select(row => row.Uid));
        if (!f.IntoBag)
        {
            Assert.NotEqual(BagFixture.Target, expected.Result.Uid); Assert.Equal(expected.NewUid, expected.Result.Uid);
            Assert.DoesNotContain(f.Current.PlayerItems.RuneItems, row => row.Uid == BagFixture.Target);
        }
        if (mode.Contains("rich", StringComparison.Ordinal))
        {
            Assert.Equal(BagFixture.Child, Assert.Single(expected.Result.DepositedItems).Uid);
            Assert.Equal(9U, expected.Result.PerfectionLevel); Assert.Equal("sealed", expected.Result.Signature);
            Assert.Equal(7U, (uint)expected.Result.Inscribed); Assert.Equal(112001U, expected.Result.Attribute1);
            Assert.Equal(23U, expected.Result.TimeLeftInMinutes); Assert.Equal(1234, expected.Result.UnlockTimer);
            Assert.Equal(2U, Assert.Single(expected.Result.DepositedItems).PerfectionLevel);
        }
    }, mode);

    [Theory]
    [InlineData("out-merge")] [InlineData("out-merge-full")] [InlineData("out-merge-lowest")]
    public Task ActualWholeMergeRetiresOnlyTheBagRootAndPreservesLockedReceiverAtFortySlots(string mode) => Run(async f =>
    {
        var state = File.ReadAllBytes(f.Data.State);
        if (mode == "out-merge-full") Assert.Equal(40, f.Current.PlayerItems.InventoryItems.Count);
        var expected = await MoveAndAssert(f, BagFixture.Receiver);
        Assert.Null(expected.NewUid); Assert.Equal((ushort)30, expected.Result.StackSize); Assert.Equal((byte)1, expected.Result.Locked);
        Assert.Equal(state, File.ReadAllBytes(f.Data.State));
        if (mode == "out-merge-lowest")
        {
            Assert.Equal(BagFixture.Receiver + 1, f.Current.PlayerItems.InventoryItems[0].Uid);
            Assert.Equal((ushort)30, f.Current.PlayerItems.InventoryItems[0].StackSize);
            Assert.Equal(BagFixture.Receiver, expected.Result.Uid);
        }
    }, mode);

    [Fact]
    public Task ActualNonzeroReturnParameterIsEchoedWithoutChangingItsOneRegisteredFreshUidOutcome() => Run(async f =>
    {
        await MoveAndAssert(f, returnParameter: ulong.MaxValue);
    }, "out");

    [Fact]
    public Task ActualBagReturnPreservesHealthAboveTheCurrentTransformCapWithoutQueryOrHpPackets() => Run(async f =>
    {
        var current = f.Current;
        f.Store.Set(f.Connection, current with { Character = LegacyTransformState.Apply(current.Character, 207, 3000, DateTimeOffset.UtcNow.AddHours(1)) });
        current = f.Current;
        var values = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["MinHitPoints"] = "100000" };
        f.Store.Set(f.Connection, current with { Character = current.Character with { MinHitPoints = 100000, Attributes = values } });
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(f.Current.Character, out var cap)); Assert.Equal(3000U, cap);
        Assert.Equal(100000L, f.Current.Character.MinHitPoints); Assert.Equal(100000, f.Native.Player!.HitPoints);
        // Store.Set used the actual owned scalar HP writer before publishing
        // the native/holder value; this is not a transient disk mismatch.
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        Assert.Equal(100000L, saved.MinHitPoints); Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(saved, out _));
        await MoveAndAssert(f); Assert.Equal(100000L, f.Current.Character.MinHitPoints); Assert.Equal(100000, f.Native.Player.HitPoints);
    }, "out");
}
