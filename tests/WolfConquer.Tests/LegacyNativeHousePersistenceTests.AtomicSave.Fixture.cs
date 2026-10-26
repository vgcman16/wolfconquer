using System.Buffers.Binary;
using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHousePersistenceTests
{
    private static LegacyNativeGameClientRuntime HouseSaveClient(int count)
    {
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = 17;
        client.MyHouse = new(17) { Level = 5 };
        for (var index = 0; index < count; index++)
        {
            var row = Npc();
            row.UID += (uint)index;
            row.X += (ushort)index;
            client.MyHouse.Furnitures[row.UID] = row;
        }
        Assert.Same(client, client.Player.Owner);
        Assert.Same(client.MyHouse, LegacyNativeHouseRuntime.HousePoll[17]);
        return client;
    }

    private static void MakeHouseSaveInvalid(LegacyNativeGameClientRuntime client, string invalid)
    {
        var house = client.MyHouse!;
        switch (invalid)
        {
            case "null-client": break;
            case "null-player": client.Player = null!; break;
            case "foreign-player":
                client.Player = new LegacyNativeGameClientRuntime().Player; client.Player.UID = 17; break;
            case "zero-player-uid": client.Player.UID = 0; break;
            case "changed-player-uid": client.Player.UID = 99; break;
            case "foreign-house": client.MyHouse = new(99); break;
            case "missing-poll": Assert.True(LegacyNativeHouseRuntime.HousePoll.TryRemove(17, out _)); break;
            case "stale-house": client.MyHouse = new(17); break; // Constructor cannot replace the current poll entry.
            case "wrong-poll-owner": LegacyNativeHouseRuntime.HousePoll[17] = new(99); break;
            case "level-zero": house.Level = 0; break;
            case "level-seven": house.Level = 7; break;
            case "null-furniture": house.Furnitures = null!; break;
            case "key-mismatch":
                var mismatched = Npc(); mismatched.UID = 778; house.Furnitures[777] = mismatched; break;
            case "null-row": house.Furnitures[777] = null!; break;
            case "zero-row-uid": var row = Npc(); row.UID = 0; house.Furnitures[0] = row; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalid));
        }
    }

    // Independent explicit wire image, preserving the dictionary snapshot order rather than inventing UID sorting.
    private static byte[] ExpectedHouseSaveBytes(LegacyNativeHouseRuntime house)
    {
        var rows = house.Furnitures.ToArray();
        var bytes = new byte[5 + rows.Length * 28];
        bytes[0] = house.Level;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index].Value;
            var target = bytes.AsSpan(5 + index * 28, 28);
            BinaryPrimitives.WriteUInt32LittleEndian(target, row.UID);
            BinaryPrimitives.WriteUInt32LittleEndian(target[4..], row.UnKnow);
            BinaryPrimitives.WriteUInt16LittleEndian(target[8..], row.X);
            BinaryPrimitives.WriteUInt16LittleEndian(target[10..], row.Y);
            BinaryPrimitives.WriteUInt16LittleEndian(target[12..], row.Mesh);
            BinaryPrimitives.WriteUInt16LittleEndian(target[14..], (ushort)row.NpcType);
            target[16] = (byte)row.ObjType;
            target[17] = 0; // Native alignment byte has no modeled source field.
            BinaryPrimitives.WriteUInt16LittleEndian(target[18..], row.Sort);
            BinaryPrimitives.WriteUInt32LittleEndian(target[20..], row.DynamicID);
            BinaryPrimitives.WriteUInt32LittleEndian(target[24..], row.Map);
        }
        return bytes;
    }

    private static void AssertHouseSaveReload(LegacyNativeGameClientRuntime client, byte[] expected)
    {
        var original = client.MyHouse;
        Assert.True(LegacyNativeHouseRuntime.HousePoll.TryRemove(17, out var removed));
        Assert.Same(original, removed);
        client.MyHouse = null!;
        Assert.True(LegacyNativeHouseRuntime.TryLoad(client));
        Assert.NotSame(original, client.MyHouse);
        Assert.Same(client.MyHouse, LegacyNativeHouseRuntime.HousePoll[17]);
        Assert.Equal(17U, client.MyHouse!.OwnerUid);
        Assert.Equal((byte)5, client.MyHouse.Level);
        Assert.Equal(expected, ExpectedHouseSaveBytes(client.MyHouse));
        Assert.Contains(17U, LegacyNativeConstants.BlockAttackMap);
    }

    private sealed record HouseSaveState(LegacyNativePlayerRuntime? Player, LegacyNativeHouseRuntime? House,
        byte? Level, ConcurrentDictionary<uint, LegacyNativeNpcRuntime>? Furniture,
        KeyValuePair<uint, LegacyNativeNpcRuntime>[] Rows,
        KeyValuePair<uint, LegacyNativeHouseRuntime>[] Poll, uint[] Blocked);

    private static HouseSaveState CaptureHouseSaveState(LegacyNativeGameClientRuntime client) => new(
        client.Player, client.MyHouse, client.MyHouse?.Level, client.MyHouse?.Furnitures,
        client.MyHouse?.Furnitures?.ToArray() ?? [], LegacyNativeHouseRuntime.HousePoll.ToArray(),
        LegacyNativeConstants.BlockAttackMap.ToArray());

    private static void AssertHouseSaveState(LegacyNativeGameClientRuntime client, HouseSaveState before)
    {
        Assert.Same(before.Player, client.Player);
        Assert.Same(before.House, client.MyHouse);
        Assert.Equal(before.Level, client.MyHouse?.Level);
        Assert.Same(before.Furniture, client.MyHouse?.Furnitures);
        Assert.Equal(before.Rows.OrderBy(pair => pair.Key), (client.MyHouse?.Furnitures?.ToArray() ?? []).OrderBy(pair => pair.Key));
        Assert.Equal(before.Poll.OrderBy(pair => pair.Key), LegacyNativeHouseRuntime.HousePoll.OrderBy(pair => pair.Key));
        Assert.Equal(before.Blocked, LegacyNativeConstants.BlockAttackMap.ToArray());
    }

    private static Dictionary<string, byte[]> CaptureHouseSaveFiles(NativeSaveRecordFiles files) =>
        Directory.GetFiles(files.Root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(files.Root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private static void AssertHouseSaveFiles(NativeSaveRecordFiles files, Dictionary<string, byte[]> expected)
    {
        var actual = CaptureHouseSaveFiles(files);
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }
}
