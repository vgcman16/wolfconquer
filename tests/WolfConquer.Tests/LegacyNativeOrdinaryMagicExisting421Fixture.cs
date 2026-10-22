using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static WorldDataCatalog CreateNativeOrdinaryMagic421Catalog(
        Monster family, params MagicType[] rows)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = "111099"; fields[1] = "NativeOrdinaryMagic421Helmet";
        fields[18] = "4700"; fields[29] = "200"; // Raw HP and MagicAttack, not a status/cache seed.
        return CreateMagicCatalogCore(family, [], [], rows,
            items: [new ItemType(111_099, fields[1], fields)]);
    }

    private static WorldSession CreateNativeOrdinaryMagic421Input(WorldSession original) => original with
    {
        Character = original.Character with { Class = 2_005, FirstClass = 2_005, Spirit = 100 },
        PlayerItems = original.PlayerItems with
        {
            EquipmentItems = original.PlayerItems.EquipmentItems.Concat(
                new[] { new PlayerItemSnapshotItem
                {
                    Uid = checked((uint)original.Character.Uid) + 60_000,
                    ItemId = 111_099, Position = 1, Durability = 100, MaximumDurability = 100,
                } }).ToArray(),
        },
    };

    private static async Task<WorldPacketHandlerContext> PrepareNativeOrdinaryMagic421QueriesAsync(
        WorldPacketHandlerContext context, ConquerTcpConnection targetConnection,
        List<byte[]> ownerFrames, List<byte[]> targetFrames)
    {
        var sessions = Assert.IsType<WorldSessionStore>(context.Sessions);
        WorldSession Current(ConquerTcpConnection connection) =>
            Assert.IsType<WorldSession>(sessions.TryGet(connection, out var current) ? current : null);
        async Task Query(ConquerTcpConnection connection, List<byte[]> frames)
        {
            var before = Current(connection);
            var start = frames.Count;
            await PacketHandlers.HandleAsync(context with { Connection = connection, Session = before },
                ReadFrame(new MsgActionPacket { ObjId = checked((uint)before.Character.Uid),
                    Type = (MsgActionType)251 }.ToFrameBytes()));
            var bytes = Assert.Single(frames.Skip(start),
                bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            Assert.Equal(checked((uint)before.Character.Uid), status.Uid);
            Assert.Equal(301U, status.MagicAttack); // Current queried Spirit100 + raw item200 + native ++.
            Assert.Equal(5_000U, status.MaxHP);      // Native base300 + durable raw item4700.
            Assert.Equal(500U, status.MaxMana);     // Native Class2005 Spirit100 * 5.
            Assert.Equal(5_000L, Current(connection).Character.MinHitPoints);
            Assert.Equal(500L, Current(connection).Character.MinMana);
        }
        await Query(context.Connection, ownerFrames);
        await Query(targetConnection, targetFrames);
        // Positive query proofs above; old assertions observe the following public cast only.
        ownerFrames.Clear(); targetFrames.Clear();
        return context with { Session = Current(context.Connection) };
    }
}
