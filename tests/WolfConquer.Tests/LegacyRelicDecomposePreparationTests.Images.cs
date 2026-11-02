using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicDecomposePreparationTests
{
    [Fact]
    public void MixedBoundBatchKeepsDeclaredOrderAndIndependentRemoveAddEchoGoldens()
    {
        var owner = Owner(); var request = Request(Second, First); var prepared = Prepare(owner, request);
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(owner);
        Assert.True(prepared.Recipe.TryProject(owner, 1_000, 18, out var result, out var reason), reason);
        var projection = Assert.IsType<LegacyRelicDecomposeProjection>(result);
        var frames = projection.CopyFrames(); var items = projection.Items;
        Assert.Equal(21, frames.Length); Assert.Equal(19, items.InventoryItems.Count);
        Assert.Equal(Kept, items.InventoryItems[0].Uid);
        Assert.DoesNotContain(items.InventoryItems, row => row.Uid == First || row.Uid == Second);
        for (var input = 0; input < 2; input++)
        {
            var uid = input == 0 ? Second : First; var bound = input == 0 ? (byte)1 : (byte)0;
            Assert.Equal(RemoveGolden(uid), frames[input * 10]);
            for (var offset = 0; offset < 9; offset++)
            {
                var rewardUid = 1_000U + (uint)(input * 9 + offset);
                Assert.Equal(AddGolden(rewardUid, bound), frames[input * 10 + offset + 1]);
                var row = items.InventoryItems[1 + input * 9 + offset];
                Assert.Equal(rewardUid, row.Uid); Assert.Equal(Crystal, row.ItemId);
                Assert.Equal(bound, row.Bound); Assert.Equal((ushort)1, row.StackSize);
            }
        }
        Assert.Equal(request.CopyEchoFrame(), frames[^1]);
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(owner));
        Assert.Empty(owner.RelicFuseItems);
    }

    [Fact]
    public void ReturnedProjectionBytesFramesAndRowsCannotMutatePrivateImagesOrOriginalOwner()
    {
        var owner = Owner(); owner.InventoryItems[^1].OwnerName = "retained";
        var prepared = Prepare(owner, Request(First));
        Assert.True(prepared.Recipe.TryProject(owner, 1_000, 9, out var result, out var reason), reason);
        var projection = Assert.IsType<LegacyRelicDecomposeProjection>(result);
        var image = projection.CopyAfterImage(); var expected = image.ToArray();
        var frames = projection.CopyFrames(); var firstFrame = frames[0].ToArray();
        Array.Clear(image); Array.Clear(frames[0]);
        var returned = projection.Items;
        returned.InventoryItems.Single(row => row.Uid == Kept).OwnerName = "changed-copy";
        owner.InventoryItems[^1].OwnerName = "changed-live";
        Assert.Equal(expected, projection.CopyAfterImage());
        Assert.Equal(firstFrame, projection.CopyFrames()[0]);
        Assert.Equal("retained", projection.Items.InventoryItems.Single(row => row.Uid == Kept).OwnerName);
        Assert.Contains(owner.InventoryItems, row => row.Uid == First);
    }

    [Fact]
    public void RecordedRecipeCannotChangeSelectedIdentityTypeOrderOrBoundness()
    {
        var owner = Owner(); var request = Request(First, Second);
        LegacyRelicDecomposeSelection[][] invalid =
        [
            [new(First, 4_100_001, 0)],
            [new(Second, 4_100_002, 1), new(First, 4_100_001, 0)],
            [new(First, 1_000_000, 0), new(Second, 4_100_002, 1)],
            [new(First, 4_100_001, 2), new(Second, 4_100_002, 1)],
        ];
        foreach (var rows in invalid)
        {
            Assert.False(LegacyRelicDecomposeRecipe.TryFromRecorded(request, rows, 17, out var recipe, out var reason));
            Assert.Null(recipe); Assert.NotEmpty(reason);
        }
        Assert.True(LegacyRelicDecomposeRecipe.TryFromRecorded(request,
            [new(First, 4_100_001, 1), new(Second, 4_100_002, 1)], 17, out var wrongBound, out _));
        Assert.False(wrongBound!.TryProject(owner, 1_000, 18, out var projection, out _)); Assert.Null(projection);
        Assert.True(LegacyRelicDecomposeRecipe.TryFromRecorded(request,
            [new(First, 4_100_005, 0), new(Second, 4_100_002, 1)], 17, out var wrongType, out _));
        Assert.False(wrongType!.TryProject(owner, 1_000, 18, out projection, out _)); Assert.Null(projection);
    }

    private static byte[] RemoveGolden(uint uid)
    {
        var frame = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(frame, 91);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(16), 3); return frame;
    }

    private static byte[] AddGolden(uint uid, byte bound)
    {
        // Independently recorded legacy2116 layout: payload202, bound28, stack50.
        var frame = new byte[206]; BinaryPrimitives.WriteUInt16LittleEndian(frame, 206);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2116);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), 3_306_885);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(12), 17);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(14), 17);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(16), 1); frame[32] = bound;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(54), 1); return frame;
    }
}
