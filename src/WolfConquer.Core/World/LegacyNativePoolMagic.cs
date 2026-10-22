using System.Collections.Generic;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Native Pool76/78 physical owners in relative declaration order; no Load/startup/host route.
internal static class LegacyNativePoolMagic
{
    public static Dictionary<ushort, List<ushort>> WeaponSpells = new Dictionary<ushort, List<ushort>>();

    public static LegacyNativeMagicType Magic = new LegacyNativeMagicType();
}
