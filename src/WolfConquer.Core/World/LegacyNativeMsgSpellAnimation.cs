#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native animation state and GameClient send paths. SendRole and the MonsterRole
// overload remain absent until their actual Role/Monster owners are ported; neither is a no-op.
internal partial class LegacyNativeMsgSpellAnimation : IDisposable
{
    public class SpellObj
    {
        public uint UID;
        public uint Damage;
        public uint Hit;
        public LegacyNativeMsgAttackPacket.AttackEffect Effect;
        public uint MoveX;
        public uint MoveY;
        public uint x1;
        public uint y2;
        public uint xx1;
        public uint yy2;
        public uint xxx1;
        public uint yyy2;
        public uint TailedBeast;

        public static SpellObj ShallowCopy(SpellObj obj)
        {
            return (SpellObj)obj.MemberwiseClone();
        }

        public SpellObj()
        {
            Hit = 1u;
        }

        public SpellObj(uint _target, uint _damage, LegacyNativeMsgAttackPacket.AttackEffect _effect)
        {
            UID = _target;
            Damage = _damage;
            Effect = _effect;
            Hit = 1u;
        }

        public SpellObj(uint _target, uint _damage, LegacyNativeMsgAttackPacket.AttackEffect _effect, ushort x, ushort y)
        {
            UID = _target;
            Damage = _damage;
            Effect = _effect;
            Hit = 1u;
            MoveY = y;
            MoveX = x;
            x1 = (xx1 = (xx1 = x));
            y2 = (yy2 = (yyy2 = y));
        }
    }

    public Queue<SpellObj> Targets;
    public uint UID;
    public uint OpponentUID;
    public ushort X;
    public ushort Y;
    public ushort SpellID;
    public byte LevelSoul;
    public ushort SpellLevel;
    public ushort NextSpell;
    public uint bomb = 0u;
    public LegacyNativePacket _Stream;

    public LegacyNativeMsgSpellAnimation()
    {
        Targets = new Queue<SpellObj>();
    }

    public LegacyNativeMsgSpellAnimation(uint _uid, uint oponnent, ushort _x, ushort _y,
        ushort _spell, ushort _spelllevel, byte _levelsoul, uint boomb = 0u)
    {
        Targets = new Queue<SpellObj>();
        UID = _uid;
        OpponentUID = oponnent;
        X = _x;
        Y = _y;
        SpellID = _spell;
        SpellLevel = _spelllevel;
        LevelSoul = _levelsoul;
        bomb = boomb;
    }

    private LegacyNativePacket CreateAnimation(Queue<SpellObj> Spells, LegacyNativePacket stream)
    {
        stream.InitWriter();
        SpellUseProto spellUseProto = CreateAnimationValue(Spells);
        SerializeTypedSpellUseProtoIntoPacket(stream, spellUseProto);
        stream.Finalize(2239);
        return stream;
    }

    // The whole existing projection is shared by legacy serialization and private
    // preparation. GamePoll lookup, all targets and beast branches remain genuine.
    private SpellUseProto CreateAnimationValue(Queue<SpellObj> Spells)
    {
        SpellUseProto spellUseProto = new SpellUseProto
        {
            Attacker = UID,
            SpellID = SpellID,
            SpellLevel = (byte)SpellLevel,
            SpellEffect = (byte)bomb,
            NextSpell = NextSpell,
            LevelHu = LevelSoul
        };
        if (OpponentUID != 0)
        {
            spellUseProto.Attacked = OpponentUID;
        }
        else
        {
            spellUseProto.X = X;
            spellUseProto.Y = Y;
        }
        spellUseProto.Targets = new Target[Spells.Count];
        LegacyNativeGameClientRuntime gameClient = (LegacyNativePoolClients.GamePoll.ContainsKey(UID)
            ? LegacyNativePoolClients.GamePoll[UID] : null);
        SpellObj[] array = Spells.ToArray();
        for (int i = 0; i < spellUseProto.Targets.Count(); i++)
        {
            spellUseProto.Targets[i] = new Target();
            spellUseProto.Targets[i].UID = array[i].UID;
            spellUseProto.Targets[i].Damage = array[i].Damage;
            spellUseProto.Targets[i].Hit = array[i].Hit != 0;
            spellUseProto.Targets[i].Flag = (uint)array[i].Effect;
            spellUseProto.Targets[i].newX = (ushort)array[i].MoveX;
            spellUseProto.Targets[i].newY = (ushort)array[i].MoveY;
            if (spellUseProto.Targets[i].Damage != 0)
            {
                spellUseProto.Targets[i].TailedBeast = ((gameClient != null && gameClient.Beasts.Activated)
                    ? ((uint)((10 + (gameClient.Beasts.Phase - 1)) * 150)) : 0u);
            }
        }
        return spellUseProto;
    }

    public void SetStream(LegacyNativePacket stream)
    {
        _Stream = stream;
    }

    private bool TryDequeue(Queue<SpellObj> Data, out SpellObj obj)
    {
        if (Data.Count != 0)
        {
            obj = Data.Dequeue();
            return true;
        }
        obj = null;
        return false;
    }

    public void JustMe(LegacyNativeGameClientRuntime user)
    {
        if (Targets.Count <= 30)
        {
            user.Send(CreateAnimation(Targets, _Stream));
            return;
        }
        Dictionary<uint, Queue<SpellObj>> dictionary = new Dictionary<uint, Queue<SpellObj>>();
        SpellObj[] array = Targets.ToArray();
        uint num = 0u;
        for (int i = 0; i < array.Length; i++)
        {
            if (i % 30 == 0)
            {
                num++;
                dictionary.Add(num, new Queue<SpellObj>());
            }
            dictionary[num].Enqueue(array[i]);
        }
        foreach (Queue<SpellObj> value in dictionary.Values)
        {
            user.Send(CreateAnimation(value, _Stream));
        }
    }

    public void Send(LegacyNativeGameClientRuntime user, bool self = true)
    {
        if (Targets.Count <= 30)
        {
            user.Player.View.SendView(CreateAnimation(Targets, _Stream), self);
            return;
        }
        Dictionary<uint, Queue<SpellObj>> dictionary = new Dictionary<uint, Queue<SpellObj>>();
        SpellObj[] array = Targets.ToArray();
        uint num = 0u;
        for (int i = 0; i < array.Length; i++)
        {
            if (i % 30 == 0)
            {
                num++;
                dictionary.Add(num, new Queue<SpellObj>());
            }
            dictionary[num].Enqueue(array[i]);
        }
        foreach (Queue<SpellObj> value in dictionary.Values)
        {
            user.Player.View.SendView(CreateAnimation(value, _Stream), self);
        }
    }

    public void Dispose()
    {
        Targets = null;
    }
}
