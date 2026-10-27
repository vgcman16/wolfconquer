using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    private static LegacyNativeGameClientRuntime SeedPrestigeRefreshClient(Fixture fixture)
    {
        var client = SeedPrestigeClientPreparation(fixture);
        client.Equipment!.CurentEquip = [new() { ItemPoints = 25_000 }];
        client.CreatePrestigePoints();
        Assert.Equal(50_000u, client.MyPrestigePoints);
        Assert.Same(client.PrestrigeEntry, Ranking.BestOfTheWorld);
        return client;
    }

    private static Ranking.Entry PublishPrestigeRefreshRow(uint uid, uint total,
        Ranking.Type category = Ranking.Type.Trojan, uint characterClass = 1000, bool includeClass = true)
    {
        var row = RankPublicationRow(uid, total);
        row.type = category;
        row.Class = characterClass;
        Ranking.PublishPrestigeEntry(row, includeClass);
        return row;
    }

    private static void SetPrestigeRefreshScore(LegacyNativeGameClientRuntime client, uint total)
    {
        client.PrestigeLevel = 0;
        SavedField(client.Player, "_level", (ushort)(total == 19_999 ? 129 : 0));
        client.Equipment!.CurentEquip = total switch
        {
            0 => [],
            // Real level129 gives 1,935; the two equipment contributions give 18,064.
            19_999 => [new() { ItemPoints = 9_032 }],
            40_000 => [new() { ItemPoints = 20_000 }],
            _ => throw new ArgumentOutOfRangeException(nameof(total)),
        };
    }

    private static void AssertPrestigeRefreshClientCommitted(LegacyNativeGameClientRuntime client,
        Ranking.Entry oldEntry, uint[] oldPoints, uint[] oldPointValues,
        LegacyNativePerfectionEffect physicalEffects, uint total)
    {
        Assert.Equal(total, client.MyPrestigePoints);
        Assert.Equal(total, client.Player.MyGuildMember!.PrestigePoints);
        Assert.NotSame(oldPoints, client.PrestigePoints);
        Assert.Equal(oldPointValues, oldPoints);
        var current = Assert.IsType<Ranking.Entry>(client.PrestrigeEntry);
        Assert.NotSame(oldEntry, current);
        Assert.Equal(50_000u, oldEntry.TotalPoints);
        Assert.Equal(total, current.TotalPoints);
        Assert.Equal(client.PrestigePoints, current.Points);
        Assert.NotSame(client.PrestigePoints, current.Points);
        Assert.Same(physicalEffects, client.PerfectionStatus);
        Assert.Equal(new double[16], ClientPreparationEffects(physicalEffects));
    }

    private static void RequirePrestigeRefreshRefusal(LegacyNativeGameClientRuntime client)
    {
        var graph = new PrestigeRefreshGraphImage();
        var outputs = new PrestigeRefreshClientImage(client);
        var error = Record.Exception(client.CreatePrestigePoints);
        outputs.RequireUnchanged();
        graph.RequireUnchanged();
        Assert.IsType<InvalidOperationException>(error);
    }

    private sealed class PrestigeRefreshClientImage
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly uint[] points;
        private readonly uint[] pointValues;
        private readonly uint total;
        private readonly Ranking.Entry? entry;
        private readonly object?[] entryValues;
        private readonly uint[]? entryPoints;
        private readonly uint[]? entryPointValues;
        private readonly LegacyNativePerfectionEffect effects;
        private readonly double[] effectValues;
        private readonly LegacyNativeGuildRuntime.Member? member;
        private readonly uint? memberPoints;
        private readonly PrestigeClientGuildImage guild;
        private readonly PrestigeClientOptionalOwnerImage optionalOwners;

        internal PrestigeRefreshClientImage(LegacyNativeGameClientRuntime client)
        {
            this.client = client;
            points = client.PrestigePoints;
            pointValues = points.ToArray();
            total = client.MyPrestigePoints;
            entry = client.PrestrigeEntry;
            entryValues = ClientPreparationEntryValues(entry);
            entryPoints = entry?.Points;
            entryPointValues = entryPoints?.ToArray();
            effects = client.PerfectionStatus;
            effectValues = ClientPreparationEffects(effects);
            member = client.Player.MyGuildMember;
            memberPoints = member?.PrestigePoints;
            guild = new(client);
            optionalOwners = new(client);
        }

        internal void RequireUnchanged()
        {
            Assert.Same(points, client.PrestigePoints);
            Assert.Equal(pointValues, client.PrestigePoints);
            Assert.Equal(total, client.MyPrestigePoints);
            Assert.Same(entry, client.PrestrigeEntry);
            Assert.Equal(entryValues, ClientPreparationEntryValues(client.PrestrigeEntry));
            Assert.Same(entryPoints, client.PrestrigeEntry?.Points);
            Assert.Equal(entryPointValues, client.PrestrigeEntry?.Points);
            Assert.Same(effects, client.PerfectionStatus);
            Assert.Equal(effectValues, ClientPreparationEffects(effects));
            Assert.Same(member, client.Player.MyGuildMember);
            Assert.Equal(memberPoints, client.Player.MyGuildMember?.PrestigePoints);
            guild.RequireUnchanged(client);
            optionalOwners.RequireUnchanged(client);
        }
    }
}
