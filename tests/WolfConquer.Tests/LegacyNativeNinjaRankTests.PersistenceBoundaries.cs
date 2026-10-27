using System.Text;
using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    // No attributes: these adapters are invoked only by the separate corrected-only class.
    // The old Reader can invoke NativeConsole on header/access/decoder failures,
    // allocate from enormous headers, or reach an encoding-fault writer lifetime.
    internal static void RunCorrectedOnlyRankHeader(string header) => WithOwnedRankPersistence(files =>
    {
        files.Seed(header);
        RequireRejectedRankLoad(files);
    });

    internal static void RunCorrectedOnlyRankEncoding(bool incomplete) => WithOwnedRankPersistence(files =>
    {
        var prefix = Encoding.UTF8.GetBytes("Count=1\n41/");
        var suffix = Encoding.UTF8.GetBytes("/2/404/505/206/707/808/909/1010/1111/1212/1313/1414/1515/1616/1717/1818/1919/2020/Guild/\n");
        files.SeedBytes(prefix.Concat(incomplete ? new byte[] { 0xF0, 0x9F } : new byte[] { 0xFF }).Concat(suffix).ToArray());
        RequireRejectedRankLoad(files);
    });

    internal static void RunCorrectedOnlyRankAccess(bool sharing) => WithOwnedRankPersistence(files =>
    {
        if (sharing) files.Seed(Text(FullRow));
        var before = SeedPersistenceGraph();
        Exception? error;
        if (sharing)
        {
            using var held = new FileStream(files.PathFor(), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            error = Record.Exception(Ranking.Load);
        }
        else error = Record.Exception(Ranking.Load);
        before.RequireCurrent();
        if (sharing) { files.Expect(Text(FullRow)); Assert.IsAssignableFrom<IOException>(error); }
        else { Assert.False(File.Exists(files.PathFor())); Assert.IsType<FileNotFoundException>(error); }
    });

    internal static void RunCorrectedOnlyRankSaveUnicode(bool guild, bool low) => WithOwnedRankPersistence(files =>
    {
        files.Seed("owned prior bytes");
        _ = SeedPersistenceGraph();
        var row = Ranking.Ranks[Ranking.Type.Water].RankingList[502];
        var malformed = new string(low ? '\uDC00' : '\uD800', 1);
        if (guild) row.GuildName = malformed; else row.Name = malformed;
        var before = new PersistenceGraphImage();
        var error = Record.Exception(Ranking.Save);
        before.RequireCurrent();
        files.Expect("owned prior bytes");
        Assert.IsType<InvalidOperationException>(error);
    });
}
