using System.Collections;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Finite owned copies of native level0 DBServer/magictype.txt rows2659/2699/2719/2739.
    // Their real sorts21/90 are NOT relabeled to invent a public RootFirstFamily route.
    private static MagicType SharedBoundarySigilMagic(ushort id, int? coldTime = null)
    {
        var raw = id switch
        {
            16_460 => "16460@@16460@@21@@DustDetachment@@1@@0@@1@@4@@0@@0@@30300@@0@@100@@5@@3@@12@@0@@0@@0@@0@@0@@0@@511616601@@0@@1032@@0@@0@@0@@0@@0@@1@@0@@0@@0@@0@@30090@@0@@0@@0@@0@@0@@0@@0@@0@@2539@@0@@0@@15000@@0@@0@@0@@0@@0@@",
            16_500 => "16500@@16500@@21@@SickleWind@@1@@1@@1@@4@@0@@0@@30280@@0@@100@@2@@5@@15@@0@@0@@0@@0@@0@@0@@511616601@@0@@1032@@0@@0@@0@@0@@0@@1@@0@@0@@0@@0@@30090@@0@@0@@0@@0@@0@@0@@0@@0@@2533@@0@@0@@16000@@0@@0@@0@@0@@0@@",
            16_520 => "16520@@16520@@90@@WildFireball@@1@@1@@0@@4@@0@@0@@30280@@0@@100@@0@@6@@15@@0@@0@@0@@0@@0@@0@@511616601@@0@@1032@@0@@0@@0@@0@@0@@1@@0@@0@@0@@0@@12000@@30095@@6@@0@@0@@0@@0@@0@@0@@2555@@0@@0@@14000@@0@@0@@0@@0@@0@@",
            16_540 => "16540@@16540@@21@@FlameofDestruction@@1@@0@@1@@4@@0@@0@@0@@0@@100@@3@@4@@14@@0@@0@@0@@0@@0@@0@@511616601@@0@@1032@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@2000@@5000@@0@@0@@0@@0@@0@@0@@0@@2535@@0@@0@@13000@@0@@0@@0@@0@@0@@",
            _ => throw new ArgumentOutOfRangeException(nameof(id)),
        };
        var parsed = MagicTypeParser.ParseLine(raw);
        if (coldTime is null) return parsed;
        var fields = parsed.Fields.ToArray();
        fields[47] = coldTime.Value.ToString(CultureInfo.InvariantCulture);
        return MagicTypeParser.ParseLine(string.Join("@@", fields)); // Only this declared malformed/zero input changes.
    }

    private static int SharedBoundarySigilType(ushort id) => id switch
    {
        16_520 => 102, 16_540 => 105, 16_460 => 304, 16_500 => 402,
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    private static GouyuType SharedBoundaryGouyu(ushort spellId, int power)
    {
        var type = SharedBoundarySigilType(spellId);
        var values = new int[10];
        values[5] = power;
        var fields = new[] { (type * 1_000 + 9).ToString(CultureInfo.InvariantCulture),
            type.ToString(CultureInfo.InvariantCulture), "9" }.Concat(
                values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        return GouyuTypeParser.ParseRow("fixture://shared-can-use-gouyu", 1, string.Join("@@", fields) + "@@");
    }

    private static string SharedBoundaryMsgNinja(ushort spellId) => string.Join('/',
        Enumerable.Repeat("0", 20).Concat(
            [SharedBoundarySigilType(spellId).ToString(CultureInfo.InvariantCulture), "9", "1", "0", "0"])) + "/";

    // Failure-only metadata seam: the actual ItemType.RequiredClass indexer throws.
    // It cannot issue an item/accept a spell/replace an owner or return a desired cast result.
    private sealed class SharedBoundaryRuneFields : IReadOnlyList<string>
    {
        private readonly string[] fields = Enumerable.Repeat("0", 60).ToArray();
        public SharedBoundaryRuneFields()
        {
            fields[0] = "4036109";
            fields[1] = "MPMaster(+9)";
        }

        public bool Armed { get; set; }
        public int RequiredClassReads { get; private set; }
        public int Count => fields.Length;
        public string this[int index]
        {
            get
            {
                if (index == ItemType.RequiredClassFieldIndex && Armed)
                {
                    RequiredClassReads++;
                    throw new FormatException("Owned optional MPMaster metadata fault.");
                }
                return fields[index];
            }
        }
        public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)fields).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
