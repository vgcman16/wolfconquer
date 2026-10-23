namespace WolfConquer.Core.Data;

// Whole native SpiritTable9..29 record, enum and mutable accumulating owner state.
internal partial class LegacyNativeSpiritTable
{
    public enum Type : byte
    {
        Failed,
        Upgraded,
        Awkaned
    }

    public class Spirit
    {
        public uint Index;
        public uint AnimaID;
        public Type Type;
        public bool IsItem;
        public uint NewAnimaID;
    }

    public static List<Spirit> SpiritRates = new List<Spirit>();
}
