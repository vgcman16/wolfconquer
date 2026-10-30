using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native private receiver; complete selected responses prepare before publication.
// This remains unregistered and does not make Send a delivery transaction.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgArenaRanking
{
    public enum RankTyp : ushort { Today, History }

    private static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(stream);
        stream.GetArenaRanking(out var rank, out var PageNumber);
        if (rank is not (RankTyp.Today or RankTyp.History)) return;
        if (PageNumber == 0) throw new ArgumentOutOfRangeException(nameof(PageNumber));

        var source = rank == RankTyp.Today
            ? LegacyNativeArenaRankings.Top1000Today
            : LegacyNativeArenaRankings.Top1000;
        ArgumentNullException.ThrowIfNull(source);
        LegacyNativeArenaUser[] rows = source.Where(static row => row is not null).ToArray();
        int start = (PageNumber - 1) * 8;
        int count = Math.Min(8, Math.Max(0, rows.Length - start));
        PreparedRow[] selected = CaptureRows(rows, start, count, rank == RankTyp.Today);

        // The valid query is consumed. All response preparation failures retain
        // that post-query packet state, including complete original input bytes.
        stream.PrepareFrame(2093, prepared =>
        {
            prepared.ArenaRankingCreate(rank, PageNumber, (uint)rows.Length,
                LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
            foreach (PreparedRow row in selected)
                prepared.AddItemArenaRanking(row.Rank, row.Name, row.Unknown, row.Points,
                    row.Class, row.Level, row.Marker, row.Mesh);
        });
        user.Send(stream);
    }

    private static PreparedRow[] CaptureRows(LegacyNativeArenaUser[] rows, int start, int count, bool today)
    {
        var selected = new List<PreparedRow>(count);
        for (int offset = 0; offset < count; offset++)
        {
            LegacyNativeArenaUser row = rows[start + offset];
            var info = row.Info ?? throw new InvalidOperationException("Selected Arena ranking row has no Info.");
            if (info.TodayRank >= 1000) continue;
            string name = row.Name;
            ArgumentNullException.ThrowIfNull(name);
            selected.Add(new PreparedRow((ushort)(start + offset + 1), name,
                today ? (ushort)0 : (ushort)6004,
                today ? info.ArenaPoints : info.HistoryHonor,
                row.Class, row.Level, today ? 1u : 33942209u, row.Mesh));
        }
        return selected.ToArray();
    }

    private readonly record struct PreparedRow(ushort Rank, string Name, ushort Unknown,
        uint Points, uint Class, uint Level, uint Marker, uint Mesh);
}
