namespace WolfConquer.Core.World;

// Complete native MsgTeamArena.User17..114; not a Team/tournament facade.
internal partial class LegacyNativeTeamArenaUser
{
    public LegacyNativeMsgTeamArenaInfo Info;
    public string Name = "None";
    public uint UID;
    public ushort Level;
    public uint Class;
    public uint Mesh;
    public uint LastSeasonArenaPoints;
    public uint LastSeasonWin;
    public uint LastSeasonLose;
    public uint LastSeasonRank;
    public uint Cheers;

    public byte GetGender
    {
        get
        {
            if (Mesh % 10 >= 3)
            {
                return 0;
            }
            return 1;
        }
    }

    public void Reset()
    {
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

    public LegacyNativeTeamArenaUser()
    {
        Info = LegacyNativeMsgTeamArenaInfo.Create();
    }
}
