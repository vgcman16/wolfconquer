using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterScoreTests
{
    [Fact]
    public Task EntireUpdateScorePublishesSameHunterRecordsRanksAndActualScreenPacket() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var peer = s.Peer(20, "Other", 5);
        s.Monster.UpdateScore(s.Packet, s.Client, 10);
        var first = s.Monster.Hunters[17];
        Assert.Equal(17u, first.HunterUID); Assert.Equal(10u, first.HunterScore);
        Assert.Equal("Wolf", first.HunterName); Assert.Equal(2u, first.ServerID); Assert.Equal(1u, first.Rank);
        Assert.Equal(Ranking(2, 3130, first), Assert.Single(s.Queue));
        Assert.Equal(Assert.Single(s.Queue), Assert.Single(peer.Queue));
        s.Queue.Clear(); peer.Queue.Clear();
        s.Monster.UpdateScore(s.Packet, peer.Client, 20);
        var second = s.Monster.Hunters[20];
        Assert.Same(first, s.Monster.Hunters[17]); Assert.Equal(2u, first.Rank); Assert.Equal(1u, second.Rank);
        Assert.Equal(Ranking(2, 3130, Hunter(20, 20, "Other", 1, 5), Hunter(17, 10, "Wolf", 2, 2)), Assert.Single(s.Queue));
        var decoded = Read(Assert.Single(s.Queue));
        Assert.Equal(new[] { 20u, 17u }, decoded.Hunters!.Select(hunter => hunter.HunterUID));
        Assert.False(s.Monster.logged); Assert.Equal(1000u, s.Monster.HitPoints);
    });

    [Fact]
    public Task RepeatedHitsKeepOriginalIdentityFieldsAndMutateExistingRecordInPlace() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Monster.UpdateScore(s.Packet, s.Client, 10);
        var row = s.Monster.Hunters[17];
        s.Client.Player.Name = "Changed"; s.Client.Player.ServerID = 99; s.Queue.Clear();
        s.Monster.UpdateScore(s.Packet, s.Client, 7);
        Assert.Same(row, s.Monster.Hunters[17]);
        Assert.Equal(17u, row.HunterScore); Assert.Equal("Wolf", row.HunterName); Assert.Equal(2u, row.ServerID);
        Assert.Equal(Ranking(2, 3130, Hunter(17, 17, "Wolf", 1, 2)), Assert.Single(s.Queue));
    });

    [Fact]
    public Task ZeroDamageStillCreatesARealRowAndBroadcastsTheWholeRanking() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Monster.Hunters = null!;
        s.Monster.UpdateScore(s.Packet, s.Client, 0);
        var row = Assert.Single(s.Monster.Hunters).Value;
        Assert.Equal(0u, row.HunterScore); Assert.Equal(1u, row.Rank);
        Assert.Equal(Ranking(2, 3130, Hunter(17, 0, "Wolf", 1, 2)), Assert.Single(s.Queue));
    });

    [Fact]
    public Task ScoreAdditionUsesNativeUintWrapBeforeRerankingAllAliases() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var own = Hunter(17, uint.MaxValue - 3, "Original", 1);
        var other = Hunter(20, 4, "Other", 2);
        s.Monster.Hunters.Add(17, own); s.Monster.Hunters.Add(20, other);
        s.Monster.UpdateScore(s.Packet, s.Client, 7);
        Assert.Equal(3u, own.HunterScore); Assert.Equal(2u, own.Rank); Assert.Equal(1u, other.Rank);
        Assert.Equal(Ranking(2, 3130, other, own), Assert.Single(s.Queue));
    });

    [Fact]
    public Task EqualScoresRetainActualDictionaryEnumerationOrderNotUidSorting() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var highUid = Hunter(90, 10, "High"); var lowUid = Hunter(1, 10, "Low");
        s.Monster.Hunters.Add(90, highUid); s.Monster.Hunters.Add(1, lowUid);
        s.Monster.Hunters.Add(17, Hunter(17, 10));
        var before = s.Monster.Hunters.Values.ToArray();
        s.Monster.UpdateScore(s.Packet, s.Client, 0);
        Assert.Equal(before.Select(entry => entry.HunterUID), Read(Assert.Single(s.Queue)).Hunters!.Select(entry => entry.HunterUID));
        for (int i = 0; i < before.Length; i++) Assert.Equal((uint)(i + 1), before[i].Rank);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RankingBroadcastUsesRealMonsterViewDynamicAndDistancePolicy(bool dynamicMismatch) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var peer = s.Peer(20, "Peer", x: dynamicMismatch ? (ushort)12 : (ushort)30);
        if (dynamicMismatch) peer.Client.Player.DynamicID = 9;
        s.Monster.UpdateScore(s.Packet, s.Client, 1);
        Assert.Single(s.Queue); Assert.Empty(peer.Queue);
        Assert.Equal(1u, s.Monster.Hunters[17].HunterScore);
    });

    [Fact]
    public Task ReceivePowerFieldsRemainFiveIndependentNativeUintSlotsAndDefaultZero() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        foreach (string field in new[] { "ThundercloudSight", "CleanSweepPower", "HookMoonPower", "AxeShadowPower", "CelestialDancePower" })
        {
            var slot = typeof(LegacyNativePlayerRuntime).GetField(field)!;
            Assert.Equal(typeof(uint), slot.FieldType); Assert.Equal(0u, slot.GetValue(s.Client.Player));
            slot.SetValue(s.Client.Player, uint.MaxValue); Assert.Equal(uint.MaxValue, slot.GetValue(s.Client.Player));
        }
        Assert.Equal(uint.MaxValue, s.Client.Player.ThundercloudSight);
    });
}
