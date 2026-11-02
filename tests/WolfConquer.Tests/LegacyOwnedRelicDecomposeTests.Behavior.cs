using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicDecomposeTests
{
    [Theory]
    [InlineData(1, false)] [InlineData(2, false)] [InlineData(2, true)] [InlineData(4, true)]
    public Task ActualRegisteredWholeBatchCommitsNativeFilesAndUniqueIdsBeforeEveryOrderedFrame(int count, bool bound) =>
        Run(f => AssertSuccessfulDecompose(f, DecomposeFixture.Request(f.Selected), bound), count: count, bound: bound);

    [Fact]
    public Task ActualMixedBoundBatchRetainsEachSelectedInputsOwnRewardBinding() =>
        Run(f => AssertSuccessfulDecompose(f, DecomposeFixture.Request(f.Selected), bound: false), input: "mixedBound", count: 2);

    [Fact]
    public Task ActualLegacyZeroStackRelicIsOneUnitAndGrantsNineCanonicalRewards() => Run(async f =>
    {
        var selected = Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == DecomposeFixture.First);
        Assert.Equal((ushort)0, selected.StackSize);
        Assert.Equal((ushort)0, f.Native.Inventory!.ClientItems[DecomposeFixture.First].StackSize);
        Assert.True(f.Native.Inventory.ContainItemWithStack(DecomposeFixture.First, 1));
        var saved = PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath));
        Assert.Equal((ushort)0, Assert.Single(saved.InventoryItems, row => row.Uid == DecomposeFixture.First).StackSize);
        await AssertSuccessfulDecompose(f, DecomposeFixture.Request(f.Selected), bound: false);
        var rewards = f.Current.PlayerItems.InventoryItems.Where(row => row.ItemId == DecomposeFixture.Crystal).ToArray();
        Assert.Equal(9, rewards.Length);
        Assert.All(rewards, row => Assert.Equal((ushort)1, row.StackSize));
    }, input: "legacy-zero");

    private static async Task AssertSuccessfulDecompose(DecomposeFixture f, byte[] bytes, bool bound)
    {
        var expectedInputs = f.Selected.ToArray(); var before = f.Current;
        var expectedBounds = expectedInputs.Select(uid => before.PlayerItems.InventoryItems.Single(row => row.Uid == uid).Bound).ToArray();
        Assert.Equal(bound ? (byte)1 : (byte)0, expectedBounds[0]);
        var high = f.HighWater; var starts = f.Peer!.WireStarts;
        var savedCharacter = File.ReadAllBytes(f.Data.UserPath);
        var savedSpells = File.ReadAllBytes(Path.Combine(f.Data.Spells, DecomposeFixture.Uid + ".bin"));
        int observed = 0;
        f.BeforeSend = _ =>
        {
            Assert.Equal(high + checked((uint)expectedInputs.Length * 9), f.HighWater);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.DoesNotContain(f.Current.PlayerItems.InventoryItems, row => expectedInputs.Contains(row.Uid));
            Assert.Equal(expectedInputs.Length * 9, f.Current.PlayerItems.InventoryItems.Count(row => row.ItemId == DecomposeFixture.Crystal));
            Assert.Equal(savedCharacter, File.ReadAllBytes(f.Data.UserPath));
            Assert.Equal(savedSpells, File.ReadAllBytes(Path.Combine(f.Data.Spells, DecomposeFixture.Uid + ".bin")));
            f.AssertConverged(); observed++;
        };
        try { await f.SendAsync(bytes); } finally { f.BeforeSend = null; }
        Assert.True(observed == expectedInputs.Length * 10 + 1, f.ReadDiagnostics());
        Assert.Equal(observed, f.Sent.Count); Assert.Equal(starts + observed, f.Peer.WireStarts);
        var rewards = f.Current.PlayerItems.InventoryItems.Where(row => row.ItemId == DecomposeFixture.Crystal).ToArray();
        Assert.Equal(expectedInputs.Length * 9, rewards.Length);
        Assert.Equal(Enumerable.Range(1, rewards.Length).Select(i => checked(high + (uint)i)), rewards.Select(row => row.Uid));
        Assert.All(rewards, row =>
        {
            Assert.Equal((ushort)1, row.StackSize);
            Assert.Equal((ushort)1, row.Durability); Assert.Equal((ushort)1, row.MaximumDurability);
            Assert.Equal((ushort)0, row.Position);
        });
        int cursor = 0;
        for (int index = 0; index < expectedInputs.Length; index++)
        {
            Assert.Equal(RemoveGolden(expectedInputs[index]), f.Sent[cursor++]);
            for (int offset = 0; offset < 9; offset++)
            {
                Assert.Equal(expectedBounds[index], rewards[index * 9 + offset].Bound);
                Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(f.Sent[cursor++]), out var added));
                Assert.Equal(rewards[index * 9 + offset].Uid, added.Uid); Assert.Equal(DecomposeFixture.Crystal, added.ItemId);
            }
        }
        Assert.Equal(DecomposeFixture.Request(expectedInputs), f.Sent[cursor++]); Assert.Equal(f.Sent.Count, cursor);
        Assert.Contains(f.Current.PlayerItems.InventoryItems, row => row.Uid == DecomposeFixture.Kept);
        var committed = f.Data.Files(); var owner = f.Current;
        f.Sent.Clear(); await f.SendAsync(DecomposeFixture.Request(expectedInputs));
        Assert.Empty(f.Sent); Assert.Same(owner, f.Current); f.Data.AssertFiles(committed);
        Assert.Equal(high + (uint)rewards.Length, f.HighWater); f.AssertConverged();
        Assert.Equal(before.Character.MinMana, f.Current.Character.MinMana);
    }

    [Theory]
    [InlineData("locked")] [InlineData("stacked")] [InlineData("suspicious")]
    [InlineData("not-relic")] [InlineData("child")] [InlineData("full")] [InlineData("noMetadata")]
    public Task ActualRejectedSelectedStateKeepsWholeNativeAndSavedBatchUnchanged(string kind) =>
        Run(f => AssertRefusal(f, DecomposeFixture.Request(f.Selected)), input: kind);

    internal static async Task AssertRefusal(DecomposeFixture f, byte[] request)
    {
        var owner = f.Current; var files = f.Data.Files(); var image = f.Native.OrdinarySpellBinding!.Items.Image;
        var high = f.HighWater; var rank = Ranking(f).Snapshot();
        await f.SendAsync(request);
        Assert.Empty(f.Sent); Assert.Same(owner, f.Current); Assert.Same(image, f.Native.OrdinarySpellBinding.Items.Image);
        Assert.True(image.IsCurrentPlain()); Assert.Equal(high, f.HighWater); f.Data.AssertFiles(files);
        Assert.Equal(rank, Ranking(f).Snapshot()); f.AssertConverged(); Assert.False(f.Runtime.Recovery.IsBlocked);
    }
    internal static LegacyRuneRankingState Ranking(DecomposeFixture f) => Assert.IsType<LegacyRuneRankingState>(
        typeof(WorldServerFrameHandler).GetField("_runeRankings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Handler));
    private static PacketFrame ReadFrame(byte[] bytes)
    { Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame; }
    private static byte[] RemoveGolden(uint uid)
    {
        var bytes = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 91);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), WorldPacketOpcodes.MsgItemUsage);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3); return bytes;
    }
}
