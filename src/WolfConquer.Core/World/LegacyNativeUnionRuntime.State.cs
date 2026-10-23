using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Genuine whole native Union126..172 declaration-order storage and implicit birth.
// Other Union gameplay methods remain absent; no host membership is synthesized.
internal partial class LegacyNativeUnionRuntime
{
    public static ConcurrentDictionary<uint, LegacyNativeUnionRuntime> UnionPoll = new();
    public ConcurrentDictionary<uint, LegacyNativeGuildRuntime> Guilds = new();
    public ConcurrentDictionary<uint, LegacyNativeUnionMember> Members = new();
    public ConcurrentDictionary<uint, LegacyNativeUnionMember> ImperialGuards = new();
    public ConcurrentDictionary<uint, LegacyNativeUnionMember> ImperialHarem = new();
    public LegacyNativeDbSafeDictionary<LegacyNativeUnionMember.MilitaryRanks, LegacyNativeUnionMember> UnionRanks = new();
    public string Name = "";
    public uint UID;
    public uint GoldBrick;
    public ulong Treasury = 0uL;
    public uint Stipend = 0u;
    public string Emperor = "";
    public uint EmperrorUID = 0u;
    public byte IsKingdom = 0;
    public uint PlunderID;
    public uint InvadingID;
    public string Bulletin = "None";
    public string Title = "";
    public string PlunderTarget = "None";
    public string InvadingUnion = "None";
    public string RecruitDeclaration = "None";
    public bool CanSave = true;
}
