using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyAuthenticatedCharacterCreationProjection
{
    private static void RequireIdentity(uint account, string name, ushort body)
    {
        // The existing full HP calculation indexes InnerPower by signed UID.
        // Refuse an unrepresentable fresh key instead of dropping that input.
        if (account is 0 or > int.MaxValue || name is null || name != name.Trim() || name.Length == 0 ||
            Encoding.UTF8.GetByteCount(name) >= LegacyNewRolePacket.NameLength || body is not (1008 or 2007) ||
            name.Any(value => char.IsControl(value) || value is '[' or ']' or '=')) throw Invalid();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var legacy = Encoding.GetEncoding(1252);
        if (legacy.GetString(legacy.GetBytes(name)) != name) throw Invalid();
    }

    private static byte[] CharacterImage(uint account, string name, ushort body, long code, DateTime at,
        LegacyFirstCharacterStarterProfile profile, uint hitpoints)
    {
        string N(long value) => value.ToString(CultureInfo.InvariantCulture);
        var fields = new[]
        {
            "[Character]", "UID=" + N(account), "Body=" + N(body), "Face=154", "Name=" + name, "Class=" + N(code),
            "Map=1002", "X=410", "Y=350", "FirstClass=0", "SecoundeClass=0", "Conquer=0", "Reborn=0", "Level=1", "Haire=27",
            "Experience=0", "PMap=1000", "PMapX=500", "PMapY=650", "Agility=" + N(profile.Agility), "Strength=" + N(profile.Strength),
            "Vitaliti=" + N(profile.Vitality), "Spirit=" + N(profile.Spirit), "Atributes=0", "MinHitPoints=" + N(hitpoints),
            "MinMana=" + N(LegacyFirstCharacterStarterProfiles.CalculateMana(code, profile.Spirit)),
            "MaxMana=" + N(LegacyFirstCharacterStarterProfiles.CalculateMana(code, profile.Spirit)),
            "ConquerPoints=" + N(LegacyFirstCharacterStarterProfiles.TunedConquerPoints), "BankCps=0", "BoundConquerPoints=0",
            "Money=" + N(LegacyFirstCharacterStarterProfiles.TunedMoney), "GuildDontion=0", "PkPoints=0", "Online=0",
            "Day=" + N(at.DayOfYear), "DonationNobility=0", "ExpBallUsed=0", "", "[PaidNobility]", "Rank=Serf", "Identifier=0",
            "LastNobilityDonation=0", "PaidRank=0", "PeriodTime=0", "IsActive=False", "Position=-1", "",
        };
        return CharacterSnapshotFileEncoding.DefaultWriteEncoding.GetBytes(string.Join("\r\n", fields));
    }
}
