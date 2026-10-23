namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgSchedules
{
    // Exact source MsgSchedules47/59/61 implicit-null references. Owners/ctors are supplied,
    // but no startup/Create/worker adoption occurs. Source Create never assigns PowerArena.
    internal static LegacyNativeMsgSaturn? Saturn;
    internal static LegacyNativeMsgDisCity? DisCity;
    internal static LegacyNativeMsgPowerArena? PowerArena;
}
