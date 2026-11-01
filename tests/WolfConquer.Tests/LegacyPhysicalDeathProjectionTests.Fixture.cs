using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA only: neither this recipe nor any projection below creates a current
// binding, damage decision, prepared writer, journal owner or recovery custody.
public sealed partial class LegacyPhysicalDeathProjectionTests
{
    private const uint Uid = 1_293_600;
    private static byte[] EmptyItems => PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty);
    private static CharacterSnapshot Character(long classCode = 142) =>
        OwnedEquipmentBehaviorFixture.Character(Uid, "DeathData", classCode, mapId: 1002, x: 430, y: 380, minHitPoints: 123);

    private static LegacyPhysicalDeathOperation Denied() => new(Uid, false, false, EmptyItems, [], [], []);

    private static byte[] CharacterBytes(string extra = "", long hp = 123, long classCode = 142) => new UTF8Encoding(true).GetPreamble()
        .Concat(Encoding.UTF8.GetBytes("; preserve é\r\n[Character]\r\nUID=" + Uid.ToString(CultureInfo.InvariantCulture) +
            "\r\nName=DeathData\r\nBody=1008\r\nFace=154\r\nHaire=0\r\nClass=" + classCode.ToString(CultureInfo.InvariantCulture) + "\r\nFirstClass=0\r\nSecoundeClass=0\r\n" +
            "Reborn=2\r\nLevel=140\r\nExperience=0\r\nMap=1002\r\nX=430\r\nY=380\r\n" +
            "PMap=1002\r\nPMapX=430\r\nPMapY=380\r\nStrength=10\r\nAgility=10\r\nVitaliti=10\r\nSpirit=10\r\nAtributes=0\r\n" +
            "MinHitpoints=" + hp.ToString(CultureInfo.InvariantCulture) + "\r\nMinMana=45\r\nMaxMana=45\r\nExpBallUsed=0\r\nMoney=987\r\n" +
            "ConquerPoints=65\r\nBoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=1\r\nDynamicID=+0007\r\nOpaque=retain é\r\n" + extra +
            "EpicTrojan=0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/\r\n" +
            "[Other]\r\nUID=777\r\nOpaque=tail\r\n")).ToArray();

    private static MagicType RankOneRow()
    {
        // Exact retained DBServer/magictype.txt21930/level1, also independently
        // used by the actual-admission metadata witness controls.
        const string raw = "21930@@21930@@6@@Dark@@0@@0@@0@@0@@1@@0@@1@@0@@200@@1@@0@@16@@417@@0@@0@@0@@0@@0@@0@@0@@48@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@416@@1@@50@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@";
        return MagicTypeParser.ParseLine(raw);
    }

    private static LegacyPhysicalDeathRankInput Rank(uint uid, byte kind, uint points) => new(checked((int)uid),
        [uid.ToString(CultureInfo.InvariantCulture), "RankData", kind.ToString(CultureInfo.InvariantCulture),
         points.ToString(CultureInfo.InvariantCulture)]);

    private static LegacyPhysicalDeathRankInput[] Tenth(byte kind) => Enumerable.Range(1, 9)
        .Select(index => Rank(checked(Uid + (uint)index), kind, checked(100U - (uint)index)))
        .Append(Rank(Uid, kind, 1)).ToArray();

    private static LegacyPhysicalDeathOperation Ranked(IReadOnlyList<LegacyPhysicalDeathRankInput> ranks) =>
        new(Uid, true, false, EmptyItems, ranks,
            [new WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange(21_930, new(21_930, 1, 0, 0, 0, 0))], [RankOneRow()]);
}
