using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHWRankTests
{
    [Theory]
    [MemberData(nameof(LegacyNativeRankFlagCorruption.Cases), MemberType = typeof(LegacyNativeRankFlagCorruption))]
    public void UnavailableFlagGraphCannotPartiallyRewriteAnExistingWeaponRankEntry(string corruption)
    {
        using var random = new LegacyNativeRankFlagRandomScope();
        using var state = new State();
        var client = LegacyNativeRankFlagCorruption.Client();
        var entry = Entry(); var before = Values(entry);
        var vector = LegacyNativeRankFlagCorruption.Apply(client, corruption);
        Assert.Throws<InvalidDataException>(() => entry.AddInfo(client));
        Assert.Equal(before, Values(entry)); vector.AssertUnchanged();
    }

    [Fact]
    public void RealMembershipLookupCannotTransferPreparedAppearanceToAReplacedPhysicalVector()
    {
        using var random = new LegacyNativeRankFlagRandomScope();
        using var state = new State();
        var client = LegacyNativeRankFlagCorruption.Client();
        var vector = client.Player.BitVector;
        var comparer = new RankVectorReplacementComparer(client.Player, vector);
        vector.ArrayFlags = new(comparer)
        {
            [(int)Flag.TopNinja] = new((int)Flag.TopNinja, 10, false, 0),
        };
        vector.Add((int)Flag.TopNinja);
        var words = vector.bits; var copiedWords = words.ToArray(); var registrations = vector.ArrayFlags;
        var entry = Entry(); var before = Values(entry);
        comparer.Armed = true;
        Assert.Throws<InvalidDataException>(() => entry.AddInfo(client));
        Assert.True(comparer.Triggered); Assert.NotSame(vector, client.Player.BitVector);
        Assert.Equal(before, Values(entry));
        Assert.Same(words, vector.bits); Assert.Equal(copiedWords, vector.bits);
        Assert.Same(registrations, vector.ArrayFlags); Assert.Single(registrations);
        Assert.Empty(client.Player.BitVector.ArrayFlags);
    }

    private sealed class RankVectorReplacementComparer(
        LegacyNativePlayerRuntime player, LegacyNativeStatusFlagsBigVector32 vector) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal bool Triggered;
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int value)
        {
            if (Armed)
            {
                Assert.True(Monitor.IsEntered(vector.SyncRoot));
                Armed = false; Triggered = true;
                player.BitVector = new LegacyNativeStatusFlagsBigVector32(576);
            }
            return value.GetHashCode();
        }
    }
}

public sealed partial class LegacyNativeNinjaRankTests
{
    [Theory]
    [MemberData(nameof(LegacyNativeRankFlagCorruption.Cases), MemberType = typeof(LegacyNativeRankFlagCorruption))]
    public void UnavailableFlagGraphCannotPartiallyRewriteAnExistingNinjaRankEntry(string corruption)
    {
        using var random = new LegacyNativeRankFlagRandomScope();
        using var state = new State();
        var client = LegacyNativeRankFlagCorruption.Client();
        var entry = Entry(); var before = Values(entry);
        var vector = LegacyNativeRankFlagCorruption.Apply(client, corruption);
        Assert.Throws<InvalidDataException>(() => entry.AddInfo(client));
        Assert.Equal(before, Values(entry)); vector.AssertUnchanged();
    }
}

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Theory]
    [MemberData(nameof(LegacyNativeRankFlagCorruption.Cases), MemberType = typeof(LegacyNativeRankFlagCorruption))]
    public void UnavailableFlagGraphCannotPartiallyRewriteAnExistingPrestigeRankEntry(string corruption)
    {
        using var random = new LegacyNativeRankFlagRandomScope();
        using var state = new State();
        var client = LegacyNativeRankFlagCorruption.Client();
        var entry = Entry(); var before = Values(entry); var points = entry.Points;
        var vector = LegacyNativeRankFlagCorruption.Apply(client, corruption);
        Assert.Throws<InvalidDataException>(() => entry.AddInfo(client));
        Assert.Equal(before, Values(entry)); Assert.Same(points, entry.Points); vector.AssertUnchanged();
    }
}

internal static class LegacyNativeRankFlagCorruption
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (string value in new[] { "null-vector", "null-words", "null-registry", "null-row",
            "key-mismatch", "registration-no-word", "word-no-registration", "negative-key",
            "aliased-row", "invalid-deadline", "early-priority-tail" }) yield return [value];
    }

    internal static LegacyNativeGameClientRuntime Client()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Equipment = new(client) { HeadID = 901, LeftWeapon = 902, RightWeapon = 903 };
        client.Player.GarmentId = 904; client.Player.ArmorId = 905; client.Player.WingId = 906;
        client.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Prepared guild");
        return client;
    }

    internal static LegacyNativeRankFlagVectorState Apply(LegacyNativeGameClientRuntime client, string corruption)
    {
        var vector = client.Player.BitVector;
        switch (corruption)
        {
            case "null-vector": client.Player.BitVector = null!; break;
            case "null-words": vector.bits = null!; break;
            case "null-registry": vector.ArrayFlags = null!; break;
            case "null-row": vector.ArrayFlags[6] = null!; vector.Add(6); break;
            case "key-mismatch": vector.ArrayFlags[6] = new(7, 10, false, 0); vector.Add(6); break;
            case "registration-no-word": vector.ArrayFlags[6] = new(6, 10, false, 0); break;
            case "word-no-registration": vector.Add(6); break;
            case "negative-key": vector.ArrayFlags[-1] = new(-1, 10, false, 0); break;
            case "aliased-row":
                var row = new LegacyNativeStatusFlagsBigVector32.Flag(6, 10, false, 0);
                vector.ArrayFlags[6] = row; vector.ArrayFlags[7] = row; vector.Add(6); vector.Add(7);
                break;
            case "invalid-deadline":
                vector.ArrayFlags[6] = new(6, 1, false, 0) { Timer = DateTime.MaxValue }; vector.Add(6);
                break;
            case "early-priority-tail":
                Assert.True(vector.TryAdd((int)Flag.WeeklyPKChampion, 10, false, 0));
                vector.ArrayFlags[6] = null!; vector.Add(6);
                break;
            default: throw new InvalidOperationException(corruption);
        }
        return new(client.Player);
    }
}

internal sealed class LegacyNativeRankFlagVectorState
{
    private readonly LegacyNativePlayerRuntime player;
    private readonly LegacyNativeStatusFlagsBigVector32? vector;
    private readonly uint[]? words;
    private readonly uint[]? copiedWords;
    private readonly ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>? registry;
    private readonly KeyValuePair<int, LegacyNativeStatusFlagsBigVector32.Flag>[]? rows;

    internal LegacyNativeRankFlagVectorState(LegacyNativePlayerRuntime player)
    {
        this.player = player; vector = player.BitVector;
        words = vector?.bits; copiedWords = words?.ToArray();
        registry = vector?.ArrayFlags; rows = registry?.ToArray();
    }

    internal void AssertUnchanged()
    {
        Assert.Same(vector, player.BitVector);
        if (vector is null) return;
        Assert.Same(words, vector.bits); Assert.Equal(copiedWords, vector.bits);
        Assert.Same(registry, vector.ArrayFlags);
        if (registry is null) return;
        Assert.Equal(rows!.Length, registry.Count);
        foreach (var row in rows) Assert.Same(row.Value, registry[row.Key]);
    }
}

internal sealed class LegacyNativeRankFlagRandomScope : IDisposable
{
    private readonly LegacyNativeSafeRandom previous = LegacyNativePoolRandom.GetRandom;
    internal LegacyNativeRankFlagRandomScope() => LegacyNativePoolRandom.GetRandom = new(71);
    public void Dispose() => LegacyNativePoolRandom.GetRandom = previous;
}
