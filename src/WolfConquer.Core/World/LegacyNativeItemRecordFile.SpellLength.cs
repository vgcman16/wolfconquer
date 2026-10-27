namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemRecordFile
{
    // Length does not infer record completeness from a reused ReadSpell buffer.
    // The existing typed reader still owns and advances its actual file handle.
    internal long SpellFileLength => file is not null ? file.Length : throw new IOException("Spell file is not open.");
}
