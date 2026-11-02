namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicDecomposeProjection
{
    private static byte[][] CreateFrames(LegacyRelicDecomposeRecipe recipe,
        IReadOnlyList<WolfConquer.Core.Data.PlayerItemSnapshotItem> rewards)
    {
        var frames = new byte[recipe.FrameCount][];
        var cursor = 0;
        for (var selected = 0; selected < recipe.Selections.Length; selected++)
        {
            frames[cursor++] = new MsgItemUsagePacket(recipe.Selections[selected].Uid, 0,
                MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes();
            for (var index = 0; index < LegacyRelicDecomposeRecipe.RewardsPerInput; index++)
                frames[cursor++] = LegacyGameItemPacket.FromSnapshotItem(
                    rewards[selected * LegacyRelicDecomposeRecipe.RewardsPerInput + index],
                    mode: LegacyGameItemPacket.AddItemMode).ToFrameBytes();
        }
        frames[cursor] = recipe.Request.CopyEchoFrame();
        return frames;
    }
}
