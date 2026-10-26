using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeHousePersistenceTests
{
    [Fact]
    public void WholeConstructorPublishesOnlyFirstOwnerButAlwaysPreservesTheActualBlockMapList()
    {
        using var state = new HouseState();
        var first = new LegacyNativeHouseRuntime(17);
        var second = new LegacyNativeHouseRuntime(17);
        Assert.Same(first, LegacyNativeHouseRuntime.HousePoll[17]);
        Assert.NotSame(first.Furnitures, second.Furnitures);
        Assert.Equal((byte)1, first.Level);
        Assert.Equal(1, LegacyNativeConstants.BlockAttackMap.Count(value => value == 17));
        LegacyNativeConstants.BlockAttackMap.Remove(17);
        _ = new LegacyNativeHouseRuntime(17);
        Assert.Contains(17u, LegacyNativeConstants.BlockAttackMap);
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 17;
        LegacyNativeHouseRuntime.Load(client); // Cached owner: no file or DbLocation used.
        Assert.Same(first, client.MyHouse);
    }

    [Fact]
    public void FullRecordCopiesAllTenFieldsButDoesNotInventNpcFactoryOrMapAdoptionState()
    {
        var npc = Npc();
        npc.Alive = true; npc.AllowDynamic = true; npc.PacketID = 999; npc.Leng = 55;
        var row = LegacyNativeHouseRuntime.DBNpc.Create(npc);
        Assert.Equal(28, Marshal.SizeOf<LegacyNativeHouseRuntime.DBNpc>());
        var result = LegacyNativeHouseRuntime.DBNpc.GetServerNpc(row);
        Assert.NotSame(npc, result);
        Assert.Equal(npc.UID, result.UID); Assert.Equal(npc.UnKnow, result.UnKnow);
        Assert.Equal(npc.X, result.X); Assert.Equal(npc.Y, result.Y);
        Assert.Equal(npc.Mesh, result.Mesh); Assert.Equal(npc.NpcType, result.NpcType);
        Assert.Equal(npc.ObjType, result.ObjType); Assert.Equal(npc.Sort, result.Sort);
        Assert.Equal(npc.DynamicID, result.DynamicID); Assert.Equal(npc.Map, result.Map);
        Assert.False(result.Alive); Assert.False(result.AllowDynamic);
        Assert.Equal((ushort)0, result.PacketID); Assert.Equal((ushort)0, result.Leng);
        var poll = LegacyNativeHouseRuntime.HousePoll;
        var entries = poll.ToArray();
        var blocked = LegacyNativeConstants.BlockAttackMap.ToArray();
        Assert.Throws<ArgumentNullException>("npc", () => LegacyNativeHouseRuntime.DBNpc.Create(null!));
        Assert.Same(poll, LegacyNativeHouseRuntime.HousePoll);
        Assert.Equal(entries.OrderBy(pair => pair.Key), poll.OrderBy(pair => pair.Key));
        Assert.Equal(blocked, LegacyNativeConstants.BlockAttackMap.ToArray());
    }

    [Fact]
    public void GenuineBinarySaveLoadHasExactFiveByteHeaderAndTwentyEightByteFurnitureRecord()
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        Directory.CreateDirectory(Path.Combine(files.Root, "Houses"));
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 17;
        Assert.Null(client.MyHouse);
        LegacyNativeHouseRuntime.Save(client);
        var path = Path.Combine(files.Root, "Houses", "17.bin");
        Assert.False(File.Exists(path));
        client.MyHouse = new(17) { Level = 6 };
        var original = Npc();
        client.MyHouse.Furnitures[original.UID] = original; // Native placement keys furniture by its actual UID.
        LegacyNativeHouseRuntime.Save(client);
        Assert.Equal(Convert.FromHexString("0601000000040302014433221166558877AA99CCBB0500EEDD4030201080706050"), File.ReadAllBytes(path));
        LegacyNativeHouseRuntime.HousePoll.Clear();
        client.MyHouse = null!;
        LegacyNativeHouseRuntime.Load(client);
        Assert.Equal((byte)6, client.MyHouse!.Level);
        Assert.Same(client.MyHouse, LegacyNativeHouseRuntime.HousePoll[17]);
        var loaded = Assert.Single(client.MyHouse.Furnitures).Value;
        Assert.Equal(0x01020304u, loaded.UID);
        Assert.NotSame(original, loaded);
        Assert.Equal(original.Map, loaded.Map);
        Assert.Equal(original.DynamicID, loaded.DynamicID);
        Assert.False(loaded.Alive);
        Assert.Contains(17u, LegacyNativeConstants.BlockAttackMap);
    }

    [Fact]
    public void LoadMissingFileAndDuplicateFurnitureUidsPreserveOldClientWithoutPublishingPartialOwner()
    {
        using var state = new HouseState();
        using var files = new NativeSaveRecordFiles();
        Directory.CreateDirectory(Path.Combine(files.Root, "Houses"));
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 17;
        var old = client.MyHouse = new(99);
        LegacyNativeHouseRuntime.Load(client);
        Assert.Same(old, client.MyHouse);
        // Corrupt-loader input is explicit fixture data, not a demand that Save
        // create a malformed duplicate-UID file.
        var one = HouseBytes();
        var duplicate = new byte[61];
        one.CopyTo(duplicate, 0);
        one.AsSpan(5).CopyTo(duplicate.AsSpan(33));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(duplicate.AsSpan(1), 2);
        File.WriteAllBytes(Path.Combine(files.Root, "Houses", "17.bin"), duplicate);
        Assert.Equal(61, new FileInfo(Path.Combine(files.Root, "Houses", "17.bin")).Length);
        LegacyNativeHouseRuntime.HousePoll.Clear(); client.MyHouse = null!;
        LegacyNativeHouseRuntime.Load(client);
        Assert.Null(client.MyHouse);
        Assert.Empty(LegacyNativeHouseRuntime.HousePoll);
    }

    [Theory]
    [InlineData((byte)0, 0, (ushort)0)] [InlineData((byte)1, 0, (ushort)1098)]
    [InlineData((byte)2, 8, (ushort)1099)] [InlineData((byte)3, 9, (ushort)2080)]
    [InlineData((byte)4, 10, (ushort)601)] [InlineData((byte)5, 12, (ushort)3024)]
    [InlineData((byte)6, 20, (ushort)3995)] [InlineData(byte.MaxValue, 0, (ushort)0)]
    public void WholeHouseTableKeepsTheLevelOneFurnitureZeroAndExactMapPartition(byte level, int furniture, ushort map)
    {
        Assert.Equal(furniture, LegacyNativeHouseTable.CountFurnitures(level));
        Assert.Equal(map, LegacyNativeHouseTable.GetHouseId(level));
        Assert.Equal(map != 0, LegacyNativeHouseTable.InHouse(map));
        Assert.False(LegacyNativeHouseTable.InHouse(1002));
    }

    private static LegacyNativeNpcRuntime Npc() => new()
    {
        UID = 0x01020304, UnKnow = 0x11223344, X = 0x5566, Y = 0x7788,
        Mesh = 0x99AA, NpcType = (LegacyNativeNpcType)0xBBCC,
        ObjType = LegacyNativeMapObjectType.Npc, Sort = 0xDDEE,
        DynamicID = 0x10203040, Map = 0x50607080
    };

    private sealed class HouseState : IDisposable
    {
        private readonly ConcurrentDictionary<uint, LegacyNativeHouseRuntime> poll = LegacyNativeHouseRuntime.HousePoll;
        private readonly uint[] blocked = LegacyNativeConstants.BlockAttackMap.ToArray();
        internal HouseState() { LegacyNativeHouseRuntime.HousePoll = new(); }
        public void Dispose()
        {
            LegacyNativeHouseRuntime.HousePoll = poll;
            LegacyNativeConstants.BlockAttackMap.Clear();
            LegacyNativeConstants.BlockAttackMap.AddRange(blocked);
        }
    }
}
