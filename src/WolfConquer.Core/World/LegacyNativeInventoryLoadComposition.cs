namespace WolfConquer.Core.World;

// Only DataCore.LoadClient's Inventory MEMBER assignment (native line 83).
// NOT the full LoadClient sequence: prior Rune construction and following owners
// are not implemented here. No current login/raw-load/host calls this phase.
internal static class LegacyNativeInventoryLoadComposition
{
    internal static void AssignInventoryAtNativeMemberPhase(LegacyNativePlayerRuntime player)
    {
        player.Owner.Inventory = new LegacyNativeInventoryRuntime(player.Owner);
    }
}
