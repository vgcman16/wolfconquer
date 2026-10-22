#nullable disable

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WolfConquer.Core.World;

// Genuine source-specialized null-client Guild birth only; unrelated Guild operations are absent.
internal partial class LegacyNativeGuildRuntime
{
    public enum ClassFlag : uint
    {
        None = 0u,
        Trojan = 2u,
        Warrior = 4u,
        Archer = 16u,
        Ninja = 32u,
        Monk = 64u,
        Pirate = 128u,
        DragonWarrior = 256u,
        Thunderstriker = 512u,
        Water = 8192u,
        Fire = 16384u,
        Windwalker = 65536u,
        Full = 91126u
    }

    public class Member
    {
        // Native UnionMem reference is absent until the genuine Union owner is ported.

        public uint UID;

        public string Name;

        public LegacyNativeGuildMemberRank Rank;

        public uint Level;

        public uint Class;

        public uint NobilityRank;

        public long MoneyDonate;

        public uint CpsDonate;

        public uint VirtutePointes;

        public uint Mesh;

        public uint PrestigePoints = 0u;

        public uint BattlePower = 0u;

        public uint Command;

        public uint ArsenalDonation;

        public uint PkDonation;

        public bool IsOnline = false;

        public long LastLogin = 0L;

        public uint CTF_Exploits = 0u;

        public uint RewardConquerPoints = 0u;

        public uint RewardMoney = 0u;

        public byte CTF_Claimed = 0;

        public uint GuildWarPoints;

        public byte Graden
        {
            get
            {
                byte result = 2;
                if (Mesh % 10 == 7)
                {
                    result = 1;
                }
                return result;
            }
        }

        public ulong TotalDonation => (uint)((int)(CpsDonate + VirtutePointes) + (int)MoneyDonate + (int)ArsenalDonation) + PkDonation;
    }

    public class GuildMessage
    {
        public uint Time;

        public string message;
    }

    public class Construct
    {
        public uint ID;

        public uint Level;

        public ulong Exp;

        public ulong Beast;

        public ulong v2;

        public ulong v3;

        public ulong v4;

        // Native Data getter is absent until the genuine syn_formtype owner is ported.
        public Construct(uint id, uint level, ulong beast, ulong _Exp = 0uL, ulong _v2 = 0uL, ulong _v3 = 0uL, ulong _v4 = 0uL)
        {
            ID = id;
            Level = level;
            Exp = _Exp;
            Beast = beast;
            v2 = _v2;
            v3 = _v3;
            v4 = _v4;
        }
    }

    public uint CommandID;

    public List<GuildMessage> Message = new List<GuildMessage>();

    public static LegacyNativeCounter Counter = new LegacyNativeCounter(1000u);

    public static ConcurrentDictionary<uint, LegacyNativeGuildRuntime> GuildPoll = new ConcurrentDictionary<uint, LegacyNativeGuildRuntime>();

    public LegacyNativeMsgGuildInformation Info;

    public uint ClaimCtfReward = 0u;

    public uint CTF_Exploits = 0u;

    public uint CTF_Next_ConquerPoints = 0u;

    public uint CTF_Next_Money = 0u;

    public uint CTF_Rank = 0u;

    public uint GuildID = 0u;

    public uint UnionID;

    public ConcurrentDictionary<uint, Member> Members;

    public ConcurrentDictionary<uint, LegacyNativeGuildRuntime> Ally;

    public ConcurrentDictionary<uint, LegacyNativeGuildRuntime> Enemy;

    public ConcurrentDictionary<uint, Construct> Constructs = new ConcurrentDictionary<uint, Construct>();

    public string Bulletin = "None";

    public string GuildName = "";

    public uint AllowAlly = 0u;

    public int BuletinEnrole = 0;

    public bool CanSave = true;

    public static ConcurrentDictionary<uint, string> ChangeNameRecords = new ConcurrentDictionary<uint, string>();

    // Specializes the actual client==null constructor branch used by GuildTable140.
    // No general native constructor, AddPlayer, membership or host publisher is exposed.
    public static LegacyNativeGuildRuntime CreateLoadedGuild(string name)
    {
        return new LegacyNativeGuildRuntime(name);
    }

    private LegacyNativeGuildRuntime(string Name)
    {
        Info = LegacyNativeMsgGuildInformation.Create();
        Members = new ConcurrentDictionary<uint, Member>();
        Ally = new ConcurrentDictionary<uint, LegacyNativeGuildRuntime>();
        Enemy = new ConcurrentDictionary<uint, LegacyNativeGuildRuntime>();
        GuildName = Name;
        Constructs = new ConcurrentDictionary<uint, Construct>();
        // The decompiled .Add call uses its concrete IDictionary spelling on this runtime.
        Construct construct = new Construct(1u, 1u, 0uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(2u, 1u, 0uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(2u, 1u, 1uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(2u, 1u, 2uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(3u, 1u, 0uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(3u, 1u, 1uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(3u, 1u, 2uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(3u, 1u, 3uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(3u, 1u, 4uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(3u, 1u, 5uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(3u, 1u, 6uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(4u, 1u, 0uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(5u, 1u, 0uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        construct = new Construct(6u, 1u, 0uL, 0uL, 0uL, 0uL, 0uL);
        ((IDictionary<uint, Construct>)Constructs).Add((uint)(construct.ID * 100 + construct.Beast), construct);
        Info.Recruitment_Flag = ClassFlag.Full;
    }
}
