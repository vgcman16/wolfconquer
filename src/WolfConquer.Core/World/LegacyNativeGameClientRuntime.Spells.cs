namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient281. DataCore.LoadClient87 birth remains unimplemented;
    // neither client construction nor a snapshot/session copy initializes this owner.
    internal LegacyNativeSpellStorage? MySpells;
}
