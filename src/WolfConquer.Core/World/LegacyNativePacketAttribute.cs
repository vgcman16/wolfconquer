using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Complete native MrRayzo.PacketAttribute. This preserves AllowSource and its
// existing native console type-initializer dependency; no dispatcher is installed.
internal class LegacyNativePacketAttribute : Attribute
{
    public static readonly Func<LegacyNativePacketAttribute, ushort> Translator =
        (LegacyNativePacketAttribute attr) => attr.Type;

    public ushort Type { get; private set; }

    public LegacyNativePacketAttribute(ushort type)
    {
        if (LegacyNativeConsole.AllowSource())
        {
            Type = type;
        }
    }
}
