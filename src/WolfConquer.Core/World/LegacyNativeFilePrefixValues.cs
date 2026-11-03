using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Closed typed DATA read through the real INI while its caller holds the actual
// complete baseline/file lease. No converter delegate or accepting reader seam.
internal sealed partial class LegacyNativeFilePrefixValues
{
    private LegacyNativeFilePrefixValues(LegacyNativePrefixValues player, bool activated, uint points, uint flag, string hairface)
    { SavedPlayerValues = player; BeastActivated = activated; BeastTotalPoints = points; BeastFlag = flag; HairfaceLine = hairface; }

    internal LegacyNativePrefixValues SavedPlayerValues { get; }
    internal bool BeastActivated { get; }
    internal uint BeastTotalPoints { get; }
    internal uint BeastFlag { get; }
    internal string HairfaceLine { get; }

    internal static bool TryRead(LegacyNativeItemCounterShellIni ini, CharacterSnapshot snapshot,
        CharacterSnapshotFileBaseline.Snapshot baseline, out LegacyNativeFilePrefixValues? values, out string reason)
    {
        values = null; reason = "normalPrefixRequiredValueInvalid";
        if (ini is null || snapshot is null || baseline is null || snapshot.Uid is <= 0 or > uint.MaxValue ||
            snapshot.Class is < 0 or > uint.MaxValue || snapshot.Level is < 1 or > ushort.MaxValue ||
            snapshot.Online is < 0 or > uint.MaxValue) return false;
        var reader = new Reader(ini, baseline);
        if (!reader.Bool("RelicSpirit", false, out var relic) ||
            !reader.Bool("SpawinEffect", false, out var spawn) || !reader.Bool("SpawinEffectSkills", false, out var spawnSkills) ||
            !reader.UInt("DeityLandLuckyPoints", out var lucky) || !reader.UInt("HeroPoints", out var hero) ||
            !reader.UInt("ClassExperience", out var classExperience) || !reader.UInt("SavePromote", out var promote) ||
            !reader.Byte("SkinUnlock", out var skin) || !reader.Byte("DoomosaurSkin", out var doomosaur) ||
            !reader.Byte("PrayingElfSkin", out var elf) || !reader.Byte("PolarDragonSkin", out var dragon) ||
            !reader.Byte("ChargeChi", out var chargeChi) || !reader.Byte("ChargeGuildSkill", out var chargeGuild) ||
            !reader.UShort("Body", 1002, out var body) || !reader.UShort("Face", 0, out var face) ||
            !reader.Text("Name", "None", out var name) || !reader.Text("Spouse", "None", out var spouse) ||
            !reader.Bool("TailedBeastActivated", false, out var activated) ||
            !reader.UInt("TailedBeastTotalPoints", out var points) || !reader.UInt("TailedBeastFlag", out var flag) ||
            !reader.Text("HairfaceStorage", "0/", out var hairface))
        { reason = reader.Reason; return false; }
        var player = new LegacyNativePrefixValues(checked((uint)snapshot.Uid), checked((uint)snapshot.Class),
            checked((ushort)snapshot.Level), relic, checked((uint)snapshot.Online), spawn, spawnSkills,
            lucky, hero, classExperience, promote, skin, doomosaur, elf, dragon, chargeChi, chargeGuild,
            body, face, name, spouse);
        values = new(player, activated, points, flag, hairface);
        reason = string.Empty; return true;
    }
}
