using System.Globalization;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyAttackSpeedMetadata
{
    private const int AttackRangeFieldIndex = 31;
    internal ushort RightRange { get; }
    internal ushort LeftRange { get; }

    private ushort ReadAttackRange(uint id)
    {
        if (id == 0) return 0; // The actual captured query has no item in this slot.
        var row = used.Single(value => value.Item.Id == id);
        if (!row.IsCurrentPlain() || row.Item.Fields.Count <= AttackRangeFieldIndex ||
            !ushort.TryParse(row.Item.Fields[AttackRangeFieldIndex], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var range) || !row.IsCurrentPlain()) throw Invalid();
        return range;
    }
}
