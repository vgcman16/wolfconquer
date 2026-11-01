namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    // Observation of an ALREADY resolved native row only. Never create a lazy
    // row, replace it or turn absence into a default during a final proof.
    internal bool TryReadOwnedPhysicalMagicRow(MagicType source, out LegacyNativeMagicRow? row) =>
        nativeMagicRows.TryGetValue(source, out row);
}
