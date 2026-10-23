using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeFlowersRecordTests
{
    [Fact]
    public Task FullRankMessagesUseNativeGradeTieBreakAndEveryActualGenericRankingFrame() => NativePacketTestThread.Run(() =>
    {
        using var globals = new FlowersScope();
        using var fixture = new NativeSpellTestFixture();
        fixture.Owner.Player.Name = "Wolf";
        var flowers = fixture.Owner.Player.Flowers = new(17, "Wolf");
        flowers.RedRoses.Rank = 2; flowers.RedRoses.Amount = 20;
        flowers.Lilies.Rank = 2; flowers.Lilies.Amount = 30;
        flowers.UpdateMyRank(fixture.Owner);
        Assert.Equal(30010102u, fixture.Owner.Player.FlowerRank); // Lily grade4 beats Rose grade1 at equal rank.
        Assert.Equal(Frame(5, 0, 1), fixture.Queue.Dequeue());
        Assert.Equal(Frame(2, 30010102, 1, 2, 30, 17, "Wolf"), fixture.Queue.Dequeue());
        Assert.Equal(Frame(2, 30010002, 1, 2, 20, 17, "Wolf"), fixture.Queue.Dequeue());
        Assert.Equal(Frame(5, 0, 0, 0, 0, 0, ""), fixture.Queue.Dequeue());
        Assert.Empty(fixture.Queue);
    });

    [Fact]
    public Task TodayFallbackUsesActualDictionaryOrderWhileUnrankedNoTodayRetainsOldIcon() => NativePacketTestThread.Run(() =>
    {
        using var globals = new FlowersScope();
        using var fixture = new NativeSpellTestFixture();
        fixture.Owner.Player.Name = "Wolf";
        var flowers = fixture.Owner.Player.Flowers = new(17, "Wolf");
        fixture.Owner.Player.FlowerRank = 999;
        flowers.UpdateMyRank(fixture.Owner);
        Assert.Equal(999u, fixture.Owner.Player.FlowerRank);
        Assert.Equal(Frame(5, 0, 1), fixture.Queue.Dequeue());
        Assert.Equal(Frame(5, 0, 0, 0, 0, 0, ""), fixture.Queue.Dequeue());
        LegacyNativePoolFlowers.FlowersRankToday.Flowers[99] = 100;
        LegacyNativePoolFlowers.FlowersRankToday.Flowers[17] = 50;
        flowers.UpdateMyRank(fixture.Owner);
        Assert.Equal(30010001u, fixture.Owner.Player.FlowerRank);
        Assert.Equal(Frame(5, 0, 1), fixture.Queue.Dequeue());
        Assert.Equal(Frame(2, 30010001, 1, 2, 50, 17, "Wolf"), fixture.Queue.Dequeue());
        Assert.Equal(Frame(5, 0, 0, 0, 0, 0, ""), fixture.Queue.Dequeue());
        Assert.Empty(fixture.Queue);
    });

    // Independent native wire layout:21-byte header/body plus optional109-byte entry.
    private static byte[] Frame(byte mode, uint type, int count, int? rank = null, uint amount = 0, uint uid = 0, string name = "")
    {
        var bytes = new byte[rank.HasValue ? 130 : 21];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2165);
        bytes[4] = mode;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(5), type);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(13), count);
        if (rank.HasValue)
        {
            bytes[21] = (byte)rank.Value;
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(22), amount);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(30), uid);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(34), uid);
            Encoding.UTF8.GetBytes(name).CopyTo(bytes, 38);
            Encoding.UTF8.GetBytes(name).CopyTo(bytes, 70);
        }
        return bytes;
    }
}
