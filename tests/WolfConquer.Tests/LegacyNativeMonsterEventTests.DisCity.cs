using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterEventTests
{
    [Fact]
    public void DisCityConstructorPreservesAllPhysicalDefaultsAndOwnsAFreshRewardList()
    {
        using var state = new State(); var dis = new LegacyNativeMsgDisCity(); var another = new LegacyNativeMsgDisCity();
        Assert.Equal(LegacyNativeProcesType.Dead, Field<LegacyNativeProcesType>(dis, "Mode"));
        Assert.Equal(default, Field<DateTime>(dis, "FinishTime")); Assert.Equal(default, Field<DateTime>(dis, "TeleportToMap4"));
        Assert.Equal(0, Field<int>(dis, "PlayersMap2")); Assert.Equal(0, Field<int>(dis, "PlayersMap3"));
        Assert.Empty(dis.RewardPlayers); Assert.NotSame(dis.RewardPlayers, another.RewardPlayers);
        foreach (string field in new[] { "Map1", "Map2", "Map3", "Map4" }) Assert.Null(Field<LegacyNativeGameMap?>(dis, field));
        Assert.False(dis.AllowJoin()); Assert.Null(LegacyNativeMsgSchedules.DisCity);
    }
    [Theory]
    [InlineData(2021u, true)] [InlineData(2022u, true)] [InlineData(2023u, true)] [InlineData(2024u, true)]
    [InlineData(2020u, false)] [InlineData(2025u, false)] [InlineData(0u, false)] [InlineData(uint.MaxValue, false)]
    public void DisCityMapPredicateUsesAllAndOnlyTheFourSourceMaps(uint map, bool expected) =>
        Assert.Equal(expected, new LegacyNativeMsgDisCity().IsInDisCity(map));
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, true)] [InlineData(3, false)] [InlineData(255, false)]
    public void JoinPolicyReadsActualPrivateProcessStateWithoutInventingAStartProducer(int mode, bool expected)
    {
        var dis = new LegacyNativeMsgDisCity(); Put(dis, "Mode", (LegacyNativeProcesType)mode); Assert.Equal(expected, dis.AllowJoin());
    }
    [Fact]
    public void CompleteCreateMapsCachesExactGenuinePoolReferencesAndNeverRefreshesNonNullSlots()
    {
        using var state = new State(); var dis = new LegacyNativeMsgDisCity();
        foreach (uint id in new uint[] { 2021, 2022, 2023, 2024 }) LegacyNativePoolMaps.ServerMaps!.Add(id, Map(id));
        dis.CreateMaps(); var original = new List<LegacyNativeGameMap>();
        for (int n = 1; n <= 4; n++)
        {
            var cached = Field<LegacyNativeGameMap>(dis, "Map" + n); original.Add(cached);
            Assert.Same(LegacyNativePoolMaps.ServerMaps![(uint)(2020 + n)], cached);
        }
        LegacyNativePoolMaps.ServerMaps = null; dis.CreateMaps();
        for (int n = 1; n <= 4; n++) Assert.Same(original[n - 1], Field<LegacyNativeGameMap>(dis, "Map" + n));
    }
    [Fact]
    public void CreateMapsKeepsRealLazyMissesNullAndFillsThemOnALaterCall()
    {
        using var state = new State(); var dis = new LegacyNativeMsgDisCity(); var one = Map(2021); var two = Map(2022);
        LegacyNativePoolMaps.ServerMaps!.Add(2021, one); LegacyNativePoolMaps.ServerMaps.Add(2022, two);
        // MapContents is the actual empty native table: missing-map lookups execute real EnterMap,
        // find no metadata and do not touch original map files or synthesize a map.
        dis.CreateMaps(); Assert.Same(one, Field<LegacyNativeGameMap>(dis, "Map1")); Assert.Same(two, Field<LegacyNativeGameMap>(dis, "Map2"));
        Assert.Null(Field<LegacyNativeGameMap?>(dis, "Map3")); Assert.Null(Field<LegacyNativeGameMap?>(dis, "Map4"));
        var three = Map(2023); var four = Map(2024); LegacyNativePoolMaps.ServerMaps.Add(2023, three); LegacyNativePoolMaps.ServerMaps.Add(2024, four);
        dis.CreateMaps(); Assert.Same(three, Field<LegacyNativeGameMap>(dis, "Map3")); Assert.Same(four, Field<LegacyNativeGameMap>(dis, "Map4"));
    }
    [Fact]
    public void MissingPoolFaultsAtFirstUncachedMapWithoutChangingEventState()
    {
        using var state = new State(); var dis = new LegacyNativeMsgDisCity(); dis.RewardPlayers.Add(17);
        LegacyNativePoolMaps.ServerMaps = null;
        Assert.Throws<NullReferenceException>(dis.CreateMaps); Assert.Equal(new uint[] { 17 }, dis.RewardPlayers);
        Assert.Equal(LegacyNativeProcesType.Dead, Field<LegacyNativeProcesType>(dis, "Mode")); Assert.Null(Field<LegacyNativeGameMap?>(dis, "Map1"));
    }
    [Theory]
    [InlineData(0u, 600)] [InlineData(999u, 600)] [InlineData(1000u, 800)] [InlineData(1005u, 800)] [InlineData(1006u, 600)]
    [InlineData(2000u, 900)] [InlineData(2005u, 900)] [InlineData(2006u, 600)]
    [InlineData(4000u, 1300)] [InlineData(4005u, 1300)] [InlineData(4006u, 600)]
    [InlineData(5000u, 800)] [InlineData(5005u, 800)] [InlineData(6000u, 800)] [InlineData(6005u, 800)]
    [InlineData(7000u, 1300)] [InlineData(7005u, 1300)] [InlineData(7006u, 600)]
    [InlineData(13005u, 600)] [InlineData(13006u, 1000)] [InlineData(uint.MaxValue, 1000)]
    public void KillRecordsRetainSourceNarrowClassBandsAndBroadLowClassFallback(uint profession, int kills) =>
        Assert.Equal(kills, new LegacyNativeMsgDisCity().KillsMap2Records(profession));
    [Fact]
    public Task UltimatePlutoAnnouncementUsesActualGlobalPublicationWithoutModeOrMapFilter() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); packets.Publish();
        var otherQueue = new Queue<byte[]>(); var other = NativeSpellTestFixture.CreateOwner(otherQueue); other.Player.UID = 18; other.Player.Map = 0;
        LegacyNativePoolClients.GamePoll![18] = other; var dis = new LegacyNativeMsgDisCity(); dis.KillTheUltimatePluto(packets.Client);
        var frame = Assert.Single(packets.Queue); Assert.Equal(frame, Assert.Single(otherQueue));
        var chat = Chat(frame); Assert.Equal("Wolf~has~defeated~UltimatePluto!", chat.Content[3]);
        Assert.Equal((uint)LegacyNativeMsgMessage.ChatMode.Talk, chat.ChatType); Assert.Equal((uint)LegacyNativeMsgMessage.MsgColor.red, chat.Color);
        Assert.Equal(LegacyNativeProcesType.Dead, Field<LegacyNativeProcesType>(dis, "Mode"));
    });
    [Fact]
    public Task PlutoPublicationPreservesMissingPlayerAndGlobalPoolFaults() => NativePacketTestThread.Run(() =>
    {
        using var state = new State(); using var packets = new Packets(); var dis = new LegacyNativeMsgDisCity();
        Assert.Throws<NullReferenceException>(() => dis.KillTheUltimatePluto(null!));
        LegacyNativePoolClients.GamePoll = null; Assert.Throws<NullReferenceException>(() => dis.KillTheUltimatePluto(packets.Client)); Assert.Empty(packets.Queue);
    });
}
