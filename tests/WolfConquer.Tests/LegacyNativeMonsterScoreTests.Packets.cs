using System.Reflection;
using ProtoBuf;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterScoreTests
{
    [Fact]
    public void WholeNativeRankingContractAndEnumMetadataRemainSourceExact()
    {
        Assert.True(typeof(LegacyNativeMsgBossHarmRanking).IsValueType);
        Assert.NotNull(typeof(LegacyNativeMsgBossHarmRanking).GetCustomAttribute<ProtoContractAttribute>());
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(LegacyNativeMsgBossHarmRanking.RankAction)));
        Assert.NotNull(typeof(LegacyNativeMsgBossHarmRanking.RankAction).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(new[] { 0, 1, 2 }, Enum.GetValues<LegacyNativeMsgBossHarmRanking.RankAction>().Select(value => (int)value));
        var fields = typeof(LegacyNativeMsgBossHarmRanking).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Type", "MonsterID", "Hunters" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(LegacyNativeMsgBossHarmRanking.RankAction), typeof(uint), typeof(LegacyNativeMsgBossHarmRankingEntry[]) },
            fields.Select(field => field.FieldType));
        for (int i = 0; i < fields.Length; i++)
        {
            var metadata = fields[i].GetCustomAttribute<ProtoMemberAttribute>()!;
            Assert.Equal(i + 1, metadata.Tag); Assert.True(metadata.IsRequired); Assert.False(fields[i].IsInitOnly);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task EntirePacketWritesRequiredZeroFieldsRawHunterOrderAndUnicode(byte type) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var first = Hunter(19, 0, "Wolf雪", 0, 0);
        var second = Hunter(uint.MaxValue, uint.MaxValue, "Second", 7, uint.MaxValue);
        var info = new LegacyNativeMsgBossHarmRanking
        {
            Type = (LegacyNativeMsgBossHarmRanking.RankAction)type,
            MonsterID = 0, Hunters = new[] { first, second }
        };
        var returned = s.Packet.CreateBossHarmRankList(info);
        Assert.Same(s.Packet, returned);
        byte[] bytes = NativeSpellTestFixture.Bytes(returned);
        Assert.Equal(Ranking(type, 0, first, second), bytes);
        var decoded = Read(bytes);
        Assert.Equal(type, (byte)decoded.Type); Assert.Equal(0u, decoded.MonsterID);
        Assert.Equal(new[] { 19u, uint.MaxValue }, decoded.Hunters!.Select(hunter => hunter.HunterUID));
        Assert.Equal("Wolf雪", decoded.Hunters[0].HunterName);
        Assert.Equal(uint.MaxValue, decoded.Hunters[1].HunterScore);
        Assert.Same(first, info.Hunters[0]); Assert.Same(second, info.Hunters[1]);
        Assert.Equal(0u, first.Rank); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task WholePacketKeepsNullAndEmptyArraysAsNativeNoEntryMessages(bool empty) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var info = new LegacyNativeMsgBossHarmRanking
        {
            Hunters = empty ? Array.Empty<LegacyNativeMsgBossHarmRankingEntry>() : null
        };
        Assert.Equal(Ranking(0, 0), NativeSpellTestFixture.Bytes(s.Packet.CreateBossHarmRankList(info)));
    });

    [Fact]
    public Task PacketNullStreamFaultsBeforeSerializationWithoutChangingCallerRecord() => NativePacketTestThread.Run(() =>
    {
        var row = Hunter(17, 10);
        var info = new LegacyNativeMsgBossHarmRanking { MonsterID = 3130, Hunters = new[] { row } };
        Assert.Throws<NullReferenceException>(() => LegacyNativeBossHarmRankingPacket.CreateBossHarmRankList(null!, info));
        Assert.Same(row, info.Hunters[0]); Assert.Equal(0u, row.Rank);
    });
}
