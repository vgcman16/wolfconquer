#nullable disable

using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Native nested state and constructors; Powers Send/AddPoints are in their own partial.
internal partial class LegacyNativeCombatGear
{
    public class VRANK
    {
        public double LukyStrike = 0.0;

        public double Parry = 0.0;

        public double CoreStrike = 0.0;

        public ushort Magic;

        public ushort MagicLevel;

        public uint PAttack;

        public uint PDefense;

        public uint MDefense;

        public uint PrestigeScore;
    }

    public partial class Powers
    {
        public uint Id = 0u;

        public uint SenseLevel = 0u;

        public uint SenseAipPower = 0u;

        public uint ArmedLevel = 0u;

        public uint ArmedAipPower = 0u;

        public uint OverLoadLevel = 0u;

        public uint OverLoadAipPower = 0u;

        public uint Points;

    }

    public class BlackJade
    {
        public uint Type;

        public uint Data;

        public LegacyNativeDaoqiDictTypeTable.CQ DBItem
        {
            get
            {
                if (LegacyNativeDaoqiDictTypeTable.Info.TryGetValue(Type, out var value))
                {
                    return value;
                }
                return new LegacyNativeDaoqiDictTypeTable.CQ();
            }
        }

        public BlackJade()
        {
        }

        public BlackJade(uint type, uint data)
        {
            Type = type;
            Data = data;
        }
    }

    public class Weapon
    {
        public BlackJade[] BlackJades;

        public uint[][] TypeAnima;

        public TypeID ItemID;

        public uint Level;

        public uint Exp;

        public uint Equip;

        public uint Transform;

        public uint MasteryScore;

        public LegacyNativeCombatGearTable.CQ DBItem
        {
            get
            {
                if (LegacyNativeCombatGearTable.TryGetValue((uint)ItemID, Level, out var type))
                {
                    return type;
                }
                return new LegacyNativeCombatGearTable.CQ();
            }
        }

        public Weapon()
        {
            BlackJades = new BlackJade[6]
            {
                new BlackJade(),
                new BlackJade(),
                new BlackJade(),
                new BlackJade(),
                new BlackJade(),
                new BlackJade()
            };
            TypeAnima = new uint[2][]
            {
                new uint[7],
                new uint[7]
            };
        }
    }
}
