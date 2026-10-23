using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyNativeAchievementMemberPhases
{
    // Complete contiguous DataCore.LoadClient89..93 achievement member phase only.
    // Explicit and unwired; not a replacement for the full DataCore caller.
    internal static void AssignAchievementAtNativeLoadClientSlot(LegacyNativePlayerRuntime player)
    {
        if (player.Owner!.Achievement == null)
        {
            player.Owner.Achievement = new LegacyNativeAchievementCollection();
        }
        player.Achievement = new LegacyNativeClientAchievement(player.Owner.Achievement.Value, player.UID);
    }
}
