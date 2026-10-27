using System.Reflection;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    private static LegacyNativeGameClientRuntime RebornUser(byte reborn, uint uid = 41,
        uint firstClass = 1000, uint secondClass = 2000)
    {
        var user = new LegacyNativeGameClientRuntime();
        user.Player.UID = uid;
        user.Player.Name = "Reborn Hero";
        user.Player.Reborn = reborn;
        user.Player.FirstClass = firstClass;
        user.Player.SecoundeClass = secondClass;
        SetRebornCurrentClass(user, 5000); // A known different family for cleanup controls.
        // Genuine existing client fields are unrelated to this targeted cleanup.
        user.PrestigePoints[3] = 777;
        user.MyPrestigePoints = 888;
        user.PrestrigeEntry = Entry(uid, 50_000);
        user.PerfectionStatus.ToxinEraser = 19;
        return user;
    }

    private static void SetRebornCurrentClass(LegacyNativeGameClientRuntime user, uint characterClass) =>
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(user.Player, characterClass);

    private static Ranking.Type RebornSelectedCategory(LegacyNativeGameClientRuntime user) =>
        Ranking.GetIndex(user.Player.Reborn == 1 ? user.Player.FirstClass : user.Player.SecoundeClass);

    private static Ranking.Rank RebornSelectedOwner(LegacyNativeGameClientRuntime user,
        bool targetPresent = true, bool survivor = true)
    {
        var category = RebornSelectedCategory(user);
        var rank = new Ranking.Rank(category) { MaxItems = 30 };
        if (targetPresent) rank.AddItem(user.PrestrigeEntry!);
        if (survivor) rank.AddItem(Entry(42, 40_000));
        Ranking.Ranks.Add(category, rank);
        return rank;
    }

    private sealed class PrestigeRebornUserImage(LegacyNativeGameClientRuntime user)
    {
        private readonly LegacyNativePlayerRuntime? player = user.Player;
        private readonly object?[] playerFields = RebornPlayerFields(user.Player);
        private readonly uint[] points = user.PrestigePoints;
        private readonly uint[] pointValues = user.PrestigePoints.ToArray();
        private readonly uint total = user.MyPrestigePoints;
        private readonly Ranking.Entry? entry = user.PrestrigeEntry;
        private readonly PrestigeMutationEntryImage? entryImage = user.PrestrigeEntry is { } row ? new(row) : null;
        private readonly LegacyNativePerfectionEffect effects = user.PerfectionStatus;
        private readonly double[] effectValues = RebornEffectValues(user.PerfectionStatus);
        private readonly object? guild = user.Player?.MyGuild;
        private readonly object? member = user.Player?.MyGuildMember;

        internal void RequireUnchanged()
        {
            Assert.Same(player, user.Player);
            Assert.Equal(playerFields, RebornPlayerFields(user.Player));
            Assert.Same(points, user.PrestigePoints);
            Assert.Equal(pointValues, user.PrestigePoints);
            Assert.Equal(total, user.MyPrestigePoints);
            Assert.Same(entry, user.PrestrigeEntry);
            entryImage?.RequireUnchanged();
            Assert.Same(effects, user.PerfectionStatus);
            Assert.Equal(effectValues, RebornEffectValues(user.PerfectionStatus));
            Assert.Same(guild, user.Player?.MyGuild);
            Assert.Same(member, user.Player?.MyGuildMember);
        }
    }

    private static object?[] RebornPlayerFields(LegacyNativePlayerRuntime? player) => player is null ? [] :
        [player.Owner, player.UID, player.Name, player.Reborn, player.FirstClass, player.SecoundeClass, player.Class];

    private static double[] RebornEffectValues(LegacyNativePerfectionEffect effects) =>
        typeof(LegacyNativePerfectionEffect).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => field.FieldType == typeof(double)).OrderBy(field => field.Name, StringComparer.Ordinal)
            .Select(field => (double)field.GetValue(effects)!).ToArray();
}
