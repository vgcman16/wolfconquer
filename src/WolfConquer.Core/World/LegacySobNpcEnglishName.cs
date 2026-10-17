using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacySobNpcEnglishName
{
    public static string Resolve(SobNpc sobNpc, bool useLocalizedName)
    {
        if (useLocalizedName && !string.IsNullOrEmpty(sobNpc.LocalizedName))
        {
            return sobNpc.LocalizedName;
        }

        if (!ContainsCjk(sobNpc.Name))
        {
            return sobNpc.Name;
        }

        if (LegacySobNpcNameCatalog.TryTranslateKnownName(sobNpc.Name, out var translated))
        {
            return translated;
        }

        return sobNpc.Kind switch
        {
            21 => "Training Stake",
            22 => "Magic Scarecrow",
            26 => "City Gate",
            _ => "Interactive Object",
        };
    }

    private static bool ContainsCjk(string value)
    {
        return LegacySobNpcNameCatalog.ContainsCjk(value);
    }
}
