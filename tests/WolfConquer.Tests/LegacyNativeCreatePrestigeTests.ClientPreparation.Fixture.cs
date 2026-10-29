using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    private static readonly string[] ClientPreparationEffectFields =
    [
        "ToxinEraser", "LightOfStamina", "DivineGuard", "CalmWind", "LuckyStrike", "StrikeLock",
        "FreeSoul", "StraightLife", "DrainingTouch", "BloodSpawn", "CoreStrike", "KillingFlash",
        "MirrorOfSin", "InvisibleArrow", "ShieldBreak", "AbsoluteLuck",
    ];

    private static double[] ClientPreparationEffects(LegacyNativePerfectionEffect effect) =>
        ClientPreparationEffectFields.Select(name => (double)typeof(LegacyNativePerfectionEffect)
            .GetField(name, BindingFlags.Instance | BindingFlags.Public)!.GetValue(effect)!).ToArray();

    private static void RunPrestigeClientPreparation(Action<Fixture> action)
    {
        Fixture? fixture = null;
        var failures = new List<Exception>();
        try { fixture = new(); action(fixture); } catch (Exception error) { failures.Add(error); }
        try { fixture?.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Prestige client preparation assertion and serialized owner restore failed.", failures);
    }

    private static LegacyNativeGameClientRuntime SeedPrestigeClientPreparation(Fixture fixture)
    {
        var client = fixture.Client;
        client.Player.ServerID = 2;
        SavedField(client.Player, "_class", 1000u);
        client.Equipment!.CurentEquip = [new() { ItemPoints = 10_000 }];
        client.PrestigeLevel = 601;
        client.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Owned Guild");
        var member = new LegacyNativeGuildRuntime.Member
            { UID = client.Player.UID, Name = client.Player.Name, PrestigePoints = 777 };
        client.Player.MyGuildMember = member;
        Assert.True(client.Player.MyGuild.Members.TryAdd(member.UID, member));
        // The prior committed state comes from the actual API and its real
        // Fixture owners, not a replacement saver, queue or accepting callback.
        client.CreatePrestigePoints();
        Assert.Equal(20_000u, client.MyPrestigePoints);
        Assert.Same(client.PrestrigeEntry, Ranking.Ranks[Ranking.Type.World].Items[41]);
        Assert.Same(client.PrestrigeEntry, Ranking.Ranks[Ranking.Type.Trojan].Items[41]);
        Assert.Equal(new double[] { 200, 8, 8, 8, 7, 7, 11, 9, 10, 5, 8, 10, 5, 8, 8, 7 },
            ClientPreparationEffects(client.PerfectionStatus));
        _ = Ranking.Ranks.GetValues();
        return client;
    }

    private static void SetNextPrestigeClientInputs(LegacyNativeGameClientRuntime client)
    {
        client.Equipment!.CurentEquip = [new() { ItemPoints = 20_000 }];
        client.PrestigeLevel = 0;
    }

    private static void RequirePrestigeClientPreparationRefusal(LegacyNativeGameClientRuntime client)
    {
        var prior = new PrestigeClientPreparationImage(client);
        var failure = Record.Exception(client.CreatePrestigePoints);
        prior.RequireUnchanged();
        Assert.IsType<InvalidOperationException>(failure);
    }

    private static object?[] ClientPreparationEntryValues(Ranking.Entry? entry) => entry is null ? [] :
    [
        entry.UID, entry.Name, entry.type, entry.Class, entry.Level, entry.Mesh, entry.TotalPoints,
        entry.HairStyle, entry.Head, entry.Garment, entry.LeftWeapon, entry.LefttWeaponAccessory,
        entry.RightWeapon, entry.RightWeaponAccessory, entry.MountArmor, entry.Armor,
        entry.Wing, entry.WingPlus, entry.Title, entry.Flag, entry.GuildName,
    ];

    private sealed class PrestigeClientPreparationImage
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly uint[] points;
        private readonly uint[] pointValues;
        private readonly uint total;
        private readonly uint level;
        private readonly Ranking.Entry? entry;
        private readonly object?[] entryValues;
        private readonly uint[]? entryPoints;
        private readonly uint[]? entryPointValues;
        private readonly LegacyNativePerfectionEffect? effects;
        private readonly double[]? effectValues;
        private readonly object player;
        private readonly uint playerUid;
        private readonly string playerName;
        private readonly object? guild;
        private readonly LegacyNativeGuildRuntime.Member? member;
        private readonly uint? memberPoints;
        private readonly uint? memberUid;
        private readonly string? memberName;
        private readonly PrestigeClientGuildImage guildImage;
        private readonly PrestigeClientOptionalOwnerImage optionalOwners;
        private readonly object? rune;
        private readonly object? equipment;
        private readonly object? hundredWeapons;
        private readonly object? identity;
        private readonly RankPublicationGraph ranks = new();
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> graph = Ranking.Ranks;
        private readonly object gate = Ranking.SynRoot;
        private readonly object cacheGate = Ranking.Ranks.SyncRoot;
        private readonly Ranking.Rank[] cache = Ranking.Ranks.MyArray;
        private readonly Ranking.Rank[] cacheMembers = Ranking.Ranks.MyArray.ToArray();
        private readonly bool update = Ranking.Ranks.Update;
        private readonly long revision = Ranking.Ranks.Revision;
        private readonly (Ranking.Rank Owner, Ranking.Type Category, int Limit)[] owners = Ranking.Ranks.Values
            .Select(rank => (rank, rank._Type, rank.MaxItems)).ToArray();

        internal PrestigeClientPreparationImage(LegacyNativeGameClientRuntime client)
        {
            this.client = client;
            points = client.PrestigePoints;
            pointValues = points.ToArray();
            total = client.MyPrestigePoints;
            level = client.PrestigeLevel;
            entry = client.PrestrigeEntry;
            entryValues = ClientPreparationEntryValues(entry);
            entryPoints = entry?.Points;
            entryPointValues = entryPoints?.ToArray();
            effects = client.PerfectionStatus;
            effectValues = effects is null ? null : ClientPreparationEffects(effects);
            player = client.Player;
            playerUid = client.Player.UID;
            playerName = client.Player.Name;
            guild = client.Player.MyGuild;
            member = client.Player.MyGuildMember;
            memberPoints = member?.PrestigePoints;
            memberUid = member?.UID;
            memberName = member?.Name;
            guildImage = new(client);
            optionalOwners = new(client);
            rune = client.Rune;
            equipment = client.Equipment;
            hundredWeapons = client.HundredWeapons;
            identity = LegacyNativeGroupServerList.MyServerInfo;
        }

        internal void RequireUnchanged()
        {
            Assert.Same(points, client.PrestigePoints);
            Assert.Equal(pointValues, client.PrestigePoints);
            Assert.Equal(total, client.MyPrestigePoints);
            Assert.Equal(level, client.PrestigeLevel);
            Assert.Same(entry, client.PrestrigeEntry);
            Assert.Equal(entryValues, ClientPreparationEntryValues(client.PrestrigeEntry));
            Assert.Same(entryPoints, client.PrestrigeEntry?.Points);
            Assert.Equal(entryPointValues, client.PrestrigeEntry?.Points);
            Assert.Same(effects, client.PerfectionStatus);
            if (effects is not null) Assert.Equal(effectValues, ClientPreparationEffects(effects));
            Assert.Same(player, client.Player);
            Assert.Equal(playerUid, client.Player.UID);
            Assert.Equal(playerName, client.Player.Name);
            Assert.Same(guild, client.Player.MyGuild);
            Assert.Same(member, client.Player.MyGuildMember);
            Assert.Equal(memberPoints, client.Player.MyGuildMember?.PrestigePoints);
            Assert.Equal(memberUid, client.Player.MyGuildMember?.UID);
            Assert.Equal(memberName, client.Player.MyGuildMember?.Name);
            guildImage.RequireUnchanged(client);
            optionalOwners.RequireUnchanged(client);
            Assert.Same(rune, client.Rune);
            Assert.Same(equipment, client.Equipment);
            Assert.Same(hundredWeapons, client.HundredWeapons);
            Assert.Same(identity, LegacyNativeGroupServerList.MyServerInfo);
            ranks.AssertUnchanged();
            Assert.Same(gate, Ranking.SynRoot);
            Assert.Same(cacheGate, graph.SyncRoot);
            Assert.Equal(update, graph.Update);
            Assert.Equal(cache, graph.MyArray);
            Assert.Equal(revision, graph.Revision);
            Assert.Equal(cacheMembers.Length, cache.Length);
            for (var index = 0; index < cache.Length; index++) Assert.Same(cacheMembers[index], cache[index]);
            foreach (var owner in owners)
            {
                Assert.Equal(owner.Category, owner.Owner._Type);
                Assert.Equal(owner.Limit, owner.Owner.MaxItems);
            }
        }
    }
}
