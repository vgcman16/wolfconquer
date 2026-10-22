using System.Runtime.InteropServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Native one-byte marker struct, nested storage and declaration initializers.
// The equipment/inventory-dependent packet handler and its attribute are absent.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgSubClass
{
    public class SubClases
    {
        public LegacyNativeDbLevExp.Sort ID;

        public byte Phrase;

        public byte Level;

        public static SubClases Create(LegacyNativeDbLevExp.Sort _id)
        {
            SubClases subClases = new SubClases();
            subClases.ID = _id;
            subClases.Level = 9;
            subClases.Phrase = 9;
            return subClases;
        }
    }

    public static uint[] ItemsPromote = new uint[10]
    {
        0u, 721259u, 721261u, 711188u, 723087u, 1088001u, 711679u, 0u, 0u, 723903u
    };

    public static byte[] ItemsCount = new byte[10]
    {
        0, 5, 10, 1, 20, 10, 1, 0, 0, 40
    };
}
