using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicFusePreparation
{
    internal bool TryGenerateWords(PlayerItemSnapshot owner, Random random, out uint[] words, out string reason)
    {
        ArgumentNullException.ThrowIfNull(random);
        words = []; reason = string.Empty;
        if (!IsCurrent(owner)) { reason = "relicFusePreparedOwnerChanged"; return false; }
        var prepared = new uint[5];
        for (var index = 0; index < prepared.Length; index++)
        {
            var inherited = first[index];
            // Preserve the source's input2 gate, including its empty/None
            // behavior. Validate the type ACTUALLY emitted from input1 as well.
            var secondGate = second[index].Type == 0 || allowed[second[index].Type];
            var type = secondGate && inherited.Type != 0 && allowed[inherited.Type] ? inherited.Type : 0;
            if (random.Next(0, 100) < 30) type = SelectType(random);
            if (type == 0) type = SelectType(random);
            int value;
            bool epic;
            if (type == (int)LegacyChiAttributeType.LukyStrike)
            { value = 100; epic = true; }
            else
            {
                var limit = caps[type];
                var lower = type == inherited.Type ? Math.Min(inherited.Value, limit) : 30;
                value = random.Next(lower, limit + 1);
                epic = value == limit;
            }
            prepared[index] = checked((uint)value * 1000 + (uint)type + (epic ? 100u : 0u));
        }
        if (!IsCurrent(owner)) { reason = "relicFusePreparedOwnerChanged"; return false; }
        words = prepared;
        return true;
    }

    // Uniform over the allowed enum1..14 set, matching conditional rejection
    // sampling without an unbounded loop. PRNG draw-count identity is not claimed.
    private int SelectType(Random random) => candidates[random.Next(0, candidates.Length)];

    private static bool TryDecodeWords(PlayerItemSnapshotItem item, out AttributeValue[] attributes)
    {
        attributes = new AttributeValue[5];
        uint[] words = [item.Attribute1, item.Attribute2, item.Attribute3, item.Attribute4, item.Attribute5];
        for (var index = 0; index < words.Length; index++)
        {
            var raw = words[index];
            var type = (int)(raw % 100);
            var value = raw / 1000;
            var marker = raw % 1000 - (uint)type;
            if (value > ushort.MaxValue || marker is not (0 or 100) || raw != 0 && (type is < 1 or > 14)) return false;
            attributes[index] = new(type, (int)value);
        }
        return true;
    }

    private readonly record struct AttributeValue(int Type, int Value);
}
