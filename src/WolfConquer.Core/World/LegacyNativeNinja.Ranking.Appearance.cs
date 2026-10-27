using WolfConquer.Core.Data;
using System.Text;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private static readonly UTF8Encoding RankingTextEncoding = new(false, true);
    private static LegacyNativeNinjaRank.Entry CaptureRankingAppearance(
        LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
    {
        if (client.Equipment is { } equipment && !equipment.IsOwnedBy(client)) throw UnavailableRanking();
        if (player.Level > byte.MaxValue) throw UnavailableRanking();
        var entry = new LegacyNativeNinjaRank.Entry
        {
            UID = player.UID, Name = player.Name, Class = player.Class,
            Level = (byte)player.Level, Mesh = player.Mesh,
        };
        entry.AddInfo(client);
        // AddInfo's genuine flag comparer may change inputs read before it.
        // These getters are plain fields/arithmetic; no flag read follows.
        if (entry.UID != player.UID || !string.Equals(entry.Name, player.Name, StringComparison.Ordinal) ||
            entry.Class != player.Class || entry.Level != player.Level || entry.Mesh != player.Mesh)
            throw UnavailableRanking();
        ValidateRankingText(entry.Name);
        ValidateRankingText(entry.GuildName);
        return entry;
    }

    private static void ValidateRankingText(string? text)
    {
        if (text is null || text.Contains('/') || text.Any(char.IsControl)) throw UnavailableRanking();
        try { _ = RankingTextEncoding.GetByteCount(text); }
        catch (EncoderFallbackException error)
        { throw new InvalidOperationException("Ninja rank text contains malformed Unicode.", error); }
    }

    private static bool SameRankingAppearance(LegacyNativeNinjaRank.Entry first, LegacyNativeNinjaRank.Entry second) =>
        string.Equals(first.Name, second.Name, StringComparison.Ordinal) &&
        string.Equals(first.GuildName, second.GuildName, StringComparison.Ordinal) &&
        RankingAppearanceValues(first).AsSpan().SequenceEqual(RankingAppearanceValues(second));

    private static uint[] RankingAppearanceValues(LegacyNativeNinjaRank.Entry entry) =>
    [
        entry.UID, entry.Class, entry.Level, entry.Mesh, entry.HairStyle, entry.Head, entry.Garment,
        entry.LeftWeapon, entry.LefttWeaponAccessory, entry.RightWeapon, entry.RightWeaponAccessory,
        entry.MountArmor, entry.Armor, entry.Wing, entry.WingPlus, entry.Title, entry.Flag,
    ];
}
