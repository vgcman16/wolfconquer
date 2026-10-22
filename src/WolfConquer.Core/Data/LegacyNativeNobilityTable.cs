using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native Database.NobilityTable.Load; not a startup registration.
// Reads real Windows INI files and calls the genuine mutable ranking owner.
// This source has no SQL, SaveClient or LoadCharacter implementation.
internal class LegacyNativeNobilityTable
{
    public static void Load()
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("");
        string[] files = Directory.GetFiles(LegacyNativeServerConfig.DbLocation + "\\Users\\");
        foreach (string fileName in files)
        {
            iniFile.FileName = fileName;
            ushort num = iniFile.ReadUInt16("Character", "Body", 1002);
            ushort num2 = iniFile.ReadUInt16("Character", "Face", 0);
            uint uid = iniFile.ReadUInt32("Character", "UID");
            string name = iniFile.ReadString("Character", "Name", "None");
            byte b = 0;
            b = (((byte)(num % 10) < 3) ? ((byte)1) : ((byte)0));
            uint mesh = (uint)(num2 * 10000 + num);
            ulong donation = iniFile.ReadUInt64("Character", "DonationNobility", 0uL);
            LegacyNativeNobility user = new LegacyNativeNobility(uid, name, donation, mesh, b);
            LegacyNativePoolNobility.NobilityRanking.UpdateRank(user);
        }
    }
}
