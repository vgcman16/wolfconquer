namespace WolfConquer.Core.World;

// Native MsgSpell physical state and properties. No substitute cooldown clock is used.
internal partial class LegacyNativeMsgSpell
{
    public enum UseSpellSoulTyp : uint
    {
        None = 0u,
        One = 9u,
        Two = 12u,
        Three = 16u,
        Four = 24u
    }

    public DateTime ColdTime = default(DateTime);
    public bool IsSpellWithColdTime = false;
    public ushort Size;
    public ushort PacketType;
    public int PacketStamp;
    public int Experience;
    public ushort ID;
    public ushort UnKnow;
    public ushort Level;
    public ushort Type;
    public UseSpellSoulTyp UseSoul;
    public byte Soul;
    public int HashLevelSoul;
    public DateTime StampSpell = default(DateTime);
    public byte PreviousLevel;
    private byte _levelHu;

    public int GetColdTime => ColdTime.AllMilliseconds() - DateTime.Now.AllMilliseconds();

    public byte SoulLevel
    {
        get
        {
            return _levelHu;
        }
        set
        {
            _levelHu = value;
            if (value > 0)
            {
                HashLevelSoul |= 2;
            }
            if (value > 1)
            {
                HashLevelSoul |= 16;
            }
            if (value > 2)
            {
                HashLevelSoul |= 256;
            }
            if (value > 3)
            {
                HashLevelSoul |= 65536;
            }
        }
    }

    public byte UseSpellSoul
    {
        get
        {
            if (UseSoul == UseSpellSoulTyp.One)
            {
                return 1;
            }
            if (UseSoul == UseSpellSoulTyp.Two)
            {
                return 2;
            }
            if (UseSoul == UseSpellSoulTyp.Three)
            {
                return 3;
            }
            if (UseSoul == UseSpellSoulTyp.Four)
            {
                return 4;
            }
            return 0;
        }
        set
        {
            switch (value)
            {
                case 1:
                    UseSoul = UseSpellSoulTyp.One;
                    break;
                case 2:
                    UseSoul = UseSpellSoulTyp.Two;
                    break;
                case 3:
                    UseSoul = UseSpellSoulTyp.Three;
                    break;
                case 4:
                    UseSoul = UseSpellSoulTyp.Four;
                    break;
                default:
                    UseSoul = UseSpellSoulTyp.None;
                    break;
            }
        }
    }
}
