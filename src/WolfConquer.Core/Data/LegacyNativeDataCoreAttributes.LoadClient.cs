using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeDataCoreAttributes
{
    // Entire native DataCore80..94. This is explicit composition, not a login route.
    public static void LoadClient(LegacyNativePlayerRuntime player)
    {
        player.Owner.Rune = new LegacyNativeRuneItemStorage(player.Owner);
        player.Owner.Inventory = new LegacyNativeInventoryRuntime(player.Owner);
        player.Owner.Equipment = new LegacyNativeEquipItemStorage(player.Owner);
        player.Owner.Warehouse = new LegacyNativeWarehouseRuntime(player.Owner);
        player.Owner.MyProfs = new LegacyNativeProficiencyStorage(player.Owner);
        player.Owner.MySpells = new LegacyNativeSpellStorage(player.Owner);
        player.Owner.MyMail = new LegacyNativeRoleMailRuntime(player.Owner);
        if (player.Owner.Achievement == null)
        {
            player.Owner.Achievement = new LegacyNativeAchievementCollection();
        }
        player.Achievement = new LegacyNativeClientAchievement(player.Owner.Achievement.Value, player.UID);
    }
}
