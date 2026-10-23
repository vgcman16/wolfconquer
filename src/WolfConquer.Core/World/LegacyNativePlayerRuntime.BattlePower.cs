using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native physical BP fields911/917/919/921 and properties1421..1476.
    private uint _extbattle;
    private uint guildBP;
    private uint _clanbp;
    private uint _mentorBp;

    public uint ExtraBattlePower
    {
        get { return _extbattle; }
        set { _extbattle = value; }
    }

    public uint GuildBattlePower
    {
        get { return guildBP; }
        set
        {
            ExtraBattlePower -= guildBP;
            guildBP = value;
            ExtraBattlePower += guildBP;
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            SendUpdate(stream, guildBP, LegacyNativeMsgUpdate.DataType.GuildBattlePower);
        }
    }

    public uint ClanBp
    {
        get { return _clanbp; }
        set
        {
            ExtraBattlePower -= _clanbp;
            _clanbp = value;
            ExtraBattlePower += _clanbp;
        }
    }

    private uint MentorBp
    {
        get { return _mentorBp; }
        set
        {
            ExtraBattlePower -= _mentorBp;
            ExtraBattlePower += value;
            _mentorBp = value;
        }
    }

    // Complete native Player1519..1544. Owner/benefit faults are not repaired.
    public int BattlePower
    {
        get
        {
            int num = (int)((LegacyNativeProfessionTable.Benefits.ContainsKey(Class) ? LegacyNativeProfessionTable.Benefits[Class].BattlePower : 0) + Level + Reborn * 5 + Owner.Equipment!.BattlePower + (int)NobilityRank + ExtraBattlePower);
            if (num > MaxBP()) return MaxBP();
            return Math.Min(522, num);
        }
    }

    public int RealBattlePower
    {
        get
        {
            int num = Level + Reborn * 5 + Owner.Equipment!.BattlePower + (int)NobilityRank;
            if (num > MaxBP()) return MaxBP();
            return num;
        }
    }

    // Whole native Player3620..3633. Actual Nobility owner, not NobilityRank field.
    public int MaxBP()
    {
        if (Nobility == null) return 0;
        return Nobility.Rank switch
        {
            LegacyNativeNobilityRank.King => 522,
            LegacyNativeNobilityRank.Prince => 519,
            LegacyNativeNobilityRank.Duke => 517,
            _ => 516,
        };
    }
}
