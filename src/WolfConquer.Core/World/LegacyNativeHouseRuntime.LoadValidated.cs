using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHouseRuntime
{
    private const int HouseHeaderSize = 5;
    private const int HouseFurnitureSize = 28;
    private const int MaximumHouseFileBytes = 16 * 1024 * 1024;
    private static readonly object LoadGate = new();

    // This is the owner encoded by the server-selected Houses/{UID}.bin path,
    // not a map/DynamicID claim and not permission to address arbitrary banks.
    internal uint OwnerUid { get; }

    private LegacyNativeHouseRuntime(uint uid, byte level,
        ConcurrentDictionary<uint, LegacyNativeNpcRuntime> furniture)
    {
        OwnerUid = uid;
        Level = level;
        Furnitures = furniture;
    }

    internal static bool TryLoad(LegacyNativeGameClientRuntime client)
    {
        if (client?.Player is not { } player || player.UID == 0 ||
            !ReferenceEquals(player.Owner, client)) return false;
        var uid = player.UID;
        lock (LoadGate)
        {
            if (HousePoll.TryGetValue(uid, out var cached))
            {
                if (!ValidOwner(cached, uid) || !SamePlayer(client, player, uid)) return false;
                client.MyHouse = cached;
                return true;
            }

            if (!TryReadHouse(uid, out var parsed) || !SamePlayer(client, player, uid)) return false;
            // The constructor is deliberately not used until parsing finishes:
            // it publishes to both HousePoll and BlockAttackMap immediately.
            var candidate = new LegacyNativeHouseRuntime(uid, parsed.Level, parsed.Furniture);
            if (!HousePoll.TryAdd(uid, candidate))
            {
                if (!HousePoll.TryGetValue(uid, out candidate) || !ValidOwner(candidate, uid)) return false;
            }
            if (!LegacyNativeConstants.BlockAttackMap.Contains(uid))
                LegacyNativeConstants.BlockAttackMap.Add(uid);
            client.MyHouse = candidate;
            return true;
        }
    }

    private static bool SamePlayer(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, uint uid) => ReferenceEquals(client.Player, player) &&
        ReferenceEquals(player.Owner, client) && player.UID == uid;

    private static bool ValidOwner(LegacyNativeHouseRuntime? house, uint uid) =>
        house is not null && house.OwnerUid == uid && house.Level is >= 1 and <= 6 && house.Furnitures is not null;

    private sealed record ParsedHouse(byte Level, ConcurrentDictionary<uint, LegacyNativeNpcRuntime> Furniture);

    private static bool TryReadHouse(uint uid, out ParsedHouse parsed)
    {
        parsed = null!;
        try
        {
            var configured = LegacyNativeServerConfig.DbLocation;
            if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured)) return false;
            var path = Path.Combine(Path.GetFullPath(configured), "Houses", uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            if (!HasOrdinaryExistingAncestors(path)) return false;
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is < HouseHeaderSize or > MaximumHouseFileBytes) return false;
            var bytes = new byte[checked((int)input.Length)];
            input.ReadExactly(bytes);
            if (input.Position != input.Length || !HasOrdinaryExistingAncestors(path)) return false;
            return TryParseHouse(bytes, out parsed);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static bool HasOrdinaryExistingAncestors(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }

    private static bool TryParseHouse(ReadOnlySpan<byte> bytes, out ParsedHouse parsed)
    {
        parsed = null!;
        if (bytes.Length < HouseHeaderSize || bytes[0] is < 1 or > 6) return false;
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes[1..]);
        if (count < 0 || (long)HouseHeaderSize + (long)count * HouseFurnitureSize != bytes.Length) return false;
        var furniture = new ConcurrentDictionary<uint, LegacyNativeNpcRuntime>();
        for (var index = 0; index < count; index++)
        {
            var row = bytes.Slice(HouseHeaderSize + index * HouseFurnitureSize, HouseFurnitureSize);
            var record = new DBNpc
            {
                UID = BinaryPrimitives.ReadUInt32LittleEndian(row),
                UnKnow = BinaryPrimitives.ReadUInt32LittleEndian(row[4..]),
                X = BinaryPrimitives.ReadUInt16LittleEndian(row[8..]),
                Y = BinaryPrimitives.ReadUInt16LittleEndian(row[10..]),
                Mesh = BinaryPrimitives.ReadUInt16LittleEndian(row[12..]),
                NpcType = (LegacyNativeNpcType)BinaryPrimitives.ReadUInt16LittleEndian(row[14..]),
                ObjType = (LegacyNativeMapObjectType)row[16],
                Sort = BinaryPrimitives.ReadUInt16LittleEndian(row[18..]),
                DynamicID = BinaryPrimitives.ReadUInt32LittleEndian(row[20..]),
                Map = BinaryPrimitives.ReadUInt32LittleEndian(row[24..]),
            };
            if (record.UID == 0 || !furniture.TryAdd(record.UID, DBNpc.GetServerNpc(record))) return false;
        }
        parsed = new(bytes[0], furniture);
        return true;
    }
}
