#nullable disable

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinjaFile
{
    private sealed record NinjaTables(Dictionary<uint, NinjaSprintItem> Types, Dictionary<uint, Item> Immortals);
    private static readonly object tableLoadGate = new();
    private static NinjaTables tables = new(null, null);

    // Source names/types remain writable; these aliases are not immutable
    // metadata authority. Actual complete Load publishes the pair once.
    public static Dictionary<uint, NinjaSprintItem> gouyu_type
    {
        get => Volatile.Read(ref tables).Types;
        set { lock (tableLoadGate) Volatile.Write(ref tables, Volatile.Read(ref tables) with { Types = value }); }
    }
    public static Dictionary<uint, Item> gouyu_immortals
    {
        get => Volatile.Read(ref tables).Immortals;
        set { lock (tableLoadGate) Volatile.Write(ref tables, Volatile.Read(ref tables) with { Immortals = value }); }
    }
}
