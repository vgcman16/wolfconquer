using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeTeamArenaUser
{
    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(UID).Add(Name).Add(Level)
            .Add(Class)
            .Add(Mesh)
            .Add(Info.ArenaPoints)
            .Add(Info.CurrentHonor)
            .Add(Info.HistoryHonor)
            .Add(Info.TodayBattles)
            .Add(Info.TodayWin)
            .Add(Info.TotalLose)
            .Add(Info.TotalWin)
            .Add(LastSeasonArenaPoints)
            .Add(LastSeasonWin)
            .Add(LastSeasonLose)
            .Add(LastSeasonRank);
        return writeLine.Close();
    }

    internal void Load(string? Line)
    {
        if (Line != null)
        {
            LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(Line, '/');
            UID = readLine.Read(0u);
            Name = readLine.Read("None")!;
            Level = readLine.Read((ushort)0);
            Class = readLine.Read((byte)0); // Exact native byte parser into UInt32 slot.
            Mesh = readLine.Read(0u);
            Info.ArenaPoints = readLine.Read(0u);
            Info.CurrentHonor = readLine.Read(0u);
            Info.HistoryHonor = readLine.Read(0u);
            Info.TodayBattles = readLine.Read(0u);
            Info.TodayWin = readLine.Read(0u);
            Info.TotalLose = readLine.Read(0u);
            Info.TodayWin = readLine.Read(0u); // Native duplicate; TotalWin remains unchanged.
            LastSeasonArenaPoints = readLine.Read(0u);
            LastSeasonWin = readLine.Read(0u);
            LastSeasonLose = readLine.Read(0u);
            LastSeasonRank = readLine.Read(0u);
        }
    }
}
