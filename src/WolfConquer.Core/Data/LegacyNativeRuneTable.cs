using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native RuneTable row/cache declarations. No startup or modern catalog adapter.
internal partial class LegacyNativeRuneTable
{
    public class RuneLevelEXP
    {
        public uint ItemType;
        public byte Level;
        public uint RequiredProgress;
    }

    public class Attribute
    {
        public uint Score;
        public AttributeValue[]? AttributeValue;
    }

    public class AttributeValue
    {
        public RuneAttribute Type;
        public uint Value;
    }

    public enum RuneAttribute
    {
        None = 0,
        HPAdd = 1,
        AddAttack = 2,
        AddMagicAttack = 3,
        CriticalStrike = 10,
        SkillCriticalStrike = 11,
        Immunity = 12,
        Breakthrough = 13,
        Counteraction = 14
    }

    public class RandomAttribute
    {
        public uint ItemID;
        public LegacyNativeChiAttribute Attribute;
        public uint Min;
        public uint Max;
        public bool dwParam;
    }

    public static Dictionary<uint, RuneLevelEXP> RuneLevelEXPList = new Dictionary<uint, RuneLevelEXP>();
    public static Dictionary<uint, Attribute> Attributes = new Dictionary<uint, Attribute>();
    public static List<RandomAttribute> RandomAttributes = new List<RandomAttribute>();
}
