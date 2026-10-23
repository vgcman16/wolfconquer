using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player633/953/955, whole predicates1365..1367 and property1498..1513.
    internal LegacyNativeUnionRuntime? MyUnion = null;
    private uint _KingDomExploits;
    internal LegacyNativePlayerExploitsRank ExploitsRank = (LegacyNativePlayerExploitsRank)0u;
    public bool InUnion => MyUnion != null && UnionMemeber != null;
    public bool IsUnionEmperror => MyUnion != null && UnionMemeber != null && UnionMemeber.Rank == LegacyNativeUnionMember.MilitaryRanks.Emperor;

    public uint KingDomExploits
    {
        get { return _KingDomExploits; }
        set
        {
            _KingDomExploits = value;
            if (InUnion) UnionMemeber!.Exploits = value;
            UpdateExploitsRank();
        }
    }

    // Whole native Player3380..3437. No downgrade/reset below200 is invented.
    public void UpdateExploitsRank()
    {
        if (KingDomExploits >= 200)
        {
            if (KingDomExploits >= 23000) SetExploitsRank(LegacyNativePlayerExploitsRank.GeneralinChief);
            else if (KingDomExploits >= 15000) SetExploitsRank(LegacyNativePlayerExploitsRank.FlyingCavalryGeneral);
            else if (KingDomExploits >= 10000) SetExploitsRank(LegacyNativePlayerExploitsRank.ChariotsandCavalryGeneral);
            else if (KingDomExploits >= 7500) SetExploitsRank(LegacyNativePlayerExploitsRank.ChiefofStaff);
            else if (KingDomExploits >= 6000) SetExploitsRank(LegacyNativePlayerExploitsRank.General);
            else if (KingDomExploits >= 4700) SetExploitsRank(LegacyNativePlayerExploitsRank.AssistantGeneral);
            else if (KingDomExploits >= 3700) SetExploitsRank(LegacyNativePlayerExploitsRank.DeputyGeneral);
            else if (KingDomExploits >= 2800) SetExploitsRank(LegacyNativePlayerExploitsRank.MasterSergeant);
            else if (KingDomExploits >= 2100) SetExploitsRank(LegacyNativePlayerExploitsRank.StaffSergeant);
            else if (KingDomExploits >= 1500) SetExploitsRank(LegacyNativePlayerExploitsRank.Sergeant);
            else if (KingDomExploits >= 1000) SetExploitsRank(LegacyNativePlayerExploitsRank.Centurion);
            else if (KingDomExploits >= 500) SetExploitsRank(LegacyNativePlayerExploitsRank.Decurion);
            else if (KingDomExploits >= 200) SetExploitsRank(LegacyNativePlayerExploitsRank.Corporal);
        }
    }

    // Whole native Player3439..3446, using actual RoleView broadcast after owner send.
    internal void SetExploitsRank(LegacyNativePlayerExploitsRank rank)
    {
        ExploitsRank = rank;
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        SendUpdate(stream, (long)rank, LegacyNativeMsgUpdate.DataType.ExploitsRank, scren: true);
    }
}

// Complete native Role.Flags.ExploitsRank51..66.
internal enum LegacyNativePlayerExploitsRank : uint
{
    Corporal = 1u, Decurion, Centurion, Sergeant, StaffSergeant, MasterSergeant,
    DeputyGeneral, AssistantGeneral, General, ChiefofStaff, ChariotsandCavalryGeneral,
    FlyingCavalryGeneral, GeneralinChief,
}
