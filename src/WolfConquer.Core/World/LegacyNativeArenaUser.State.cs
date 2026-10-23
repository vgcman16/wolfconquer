namespace WolfConquer.Core.World;

// Complete native MsgArena.User22..136, split by real state/persistence family.
// This is the actual record owner, not the unimplemented tournament/Match owner.
internal partial class LegacyNativeArenaUser
{
    public enum StateType : byte { None, FindMatch, WaitForBox, WaitForOther, Fight }
    public enum MatchStatus : byte { None, Winner, Loser }

    public LegacyNativeMsgArenaInfo Info;
    public string Name = "None";
    public uint UID;
    public ushort Level;
    public uint Class;
    public uint Mesh;
    public StateType ArenaState = StateType.None;
    public DateTime AcceptBoxShow = default(DateTime);
    public bool AcceptBox = false;
    public MatchStatus QualifierStatus = MatchStatus.None;
    public uint Damage;
    public uint LastSeasonArenaPoints;
    public uint LastSeasonWin;
    public uint LastSeasonLose;
    public uint LastSeasonRank;
    public uint Cheers;

    public void Reset()
    {
        ArenaState = StateType.None;
        AcceptBox = false;
        Info.Status = LegacyNativeMsgArenaInfo.Action.NotSignedUp;
        Cheers = 0u;
    }

    public void ApplayInfo(LegacyNativePlayerRuntime player)
    {
        Name = player.Name;
        UID = player.UID;
        Level = player.Level;
        Class = player.Class;
        Mesh = player.Mesh;
    }

    public LegacyNativeArenaUser()
    {
        Info = new LegacyNativeMsgArenaInfo();
    }
}
