using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    private static readonly UTF8Encoding PrestigeEntryTextEncoding = new(false, true);

    private static void ValidatePreparedPrestigeText(LegacyNativePrestigeRanking.Entry entry)
    {
        ValidatePreparedPrestigeText(entry.Name, "character name", allowEmpty: false);
        ValidatePreparedPrestigeText(entry.GuildName, "guild name", allowEmpty: true);
    }

    private static void ValidatePreparedPrestigeText(string? value, string name, bool allowEmpty)
    {
        if (value is null || (!allowEmpty && string.IsNullOrWhiteSpace(value)) ||
            value.Any(character => character == '/' || char.IsControl(character)))
            throw new InvalidOperationException($"Prestige preparation requires a valid {name}.");
        try
        {
            _ = PrestigeEntryTextEncoding.GetByteCount(value);
        }
        catch (EncoderFallbackException error)
        {
            throw new InvalidOperationException($"Prestige preparation found invalid Unicode in its {name}.", error);
        }
    }
}
