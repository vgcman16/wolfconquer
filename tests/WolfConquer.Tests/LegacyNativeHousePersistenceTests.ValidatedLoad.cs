using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHousePersistenceTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(32)]
    public void TruncatedHouseNeverPublishesClientPollOrBlockMap(int length)
    {
        AssertRejectedHouse(HouseBytes()[..length]);
    }

    [Theory]
    [InlineData((byte)0)] [InlineData((byte)7)] [InlineData(byte.MaxValue)]
    public void InvalidHouseLevelNeverPublishesPartialOwner(byte level)
    {
        var bytes = HouseBytes(); bytes[0] = level;
        AssertRejectedHouse(bytes);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(2)] [InlineData(int.MaxValue)]
    public void InvalidCountOrUnconsumedTailNeverPublishesPartialOwner(int count)
    {
        var bytes = HouseBytes(); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), count);
        AssertRejectedHouse(bytes);
    }

    [Fact]
    public void DuplicateFurnitureAndZeroUidRejectCompleteFileRatherThanKeepingAFirstPartialGraph()
    {
        var one = HouseBytes();
        var duplicate = one.Concat(one.AsSpan(5).ToArray()).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(duplicate.AsSpan(1), 2);
        AssertRejectedHouse(duplicate);
        BinaryPrimitives.WriteUInt32LittleEndian(one.AsSpan(5), 0);
        AssertRejectedHouse(one);
    }

    [Fact]
    public void ValidPhysicalFilePublishesOneRealOwnerAfterCompleteParseAndAllowsExactRetryAfterRepair()
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        var path = SeedHouse(files, HouseBytes()[..32]);
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 17;
        LegacyNativeHouseRuntime.Load(client);
        Assert.Null(client.MyHouse); Assert.Empty(LegacyNativeHouseRuntime.HousePoll);
        File.WriteAllBytes(path, HouseBytes());
        LegacyNativeHouseRuntime.Load(client);
        var house = Assert.IsType<LegacyNativeHouseRuntime>(client.MyHouse);
        Assert.Equal((byte)6, house.Level);
        Assert.Same(house, LegacyNativeHouseRuntime.HousePoll[17]);
        var actual = Assert.Single(house.Furnitures).Value; var expected = Npc();
        Assert.Equal(expected.UID, actual.UID); Assert.Equal(expected.UnKnow, actual.UnKnow);
        Assert.Equal(expected.X, actual.X); Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.Mesh, actual.Mesh); Assert.Equal(expected.NpcType, actual.NpcType);
        Assert.Equal(expected.ObjType, actual.ObjType); Assert.Equal(expected.Sort, actual.Sort);
        Assert.Equal(expected.DynamicID, actual.DynamicID); Assert.Equal(expected.Map, actual.Map);
        Assert.False(actual.Alive); Assert.False(actual.AllowDynamic);
        Assert.Contains(17u, LegacyNativeConstants.BlockAttackMap);
        File.Delete(path);
        LegacyNativeHouseRuntime.Load(client); Assert.Same(house, client.MyHouse);
        Assert.Equal(0u, client.Player.DynamicID); // Loading is not house entry or bank authority.
    }

    [Fact]
    public void WrongKeyCachedOwnerAndInvalidPlayerNeverBecomeHouseAuthority()
    {
        using var state = new HouseState();
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 17;
        var wrong = new LegacyNativeHouseRuntime(99);
        LegacyNativeHouseRuntime.HousePoll[17] = wrong;
        LegacyNativeHouseRuntime.Load(client); Assert.Null(client.MyHouse);
        client.Player.UID = 0;
        LegacyNativeHouseRuntime.Load(client); Assert.Null(client.MyHouse);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyNativeHouseRuntime(0));
    }

    private static void AssertRejectedHouse(byte[] bytes)
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        var path = SeedHouse(files, bytes);
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 17;
        var previous = client.MyHouse = new LegacyNativeHouseRuntime(99);
        var blocked = LegacyNativeConstants.BlockAttackMap.ToArray();
        LegacyNativeHouseRuntime.Load(client);
        Assert.Same(previous, client.MyHouse);
        Assert.False(LegacyNativeHouseRuntime.HousePoll.ContainsKey(17));
        Assert.Same(previous, LegacyNativeHouseRuntime.HousePoll[99]);
        Assert.Equal(blocked, LegacyNativeConstants.BlockAttackMap.ToArray());
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    private static string SeedHouse(NativeSaveRecordFiles files, byte[] bytes)
    {
        Directory.CreateDirectory(Path.Combine(files.Root, "Houses"));
        var path = Path.Combine(files.Root, "Houses", "17.bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] HouseBytes() => Convert.FromHexString(
        "0601000000040302014433221166558877AA99CCBB0500EEDD4030201080706050");
}
