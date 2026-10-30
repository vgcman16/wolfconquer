using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyBirdIslandGateItemId = 1_060_024;

    private const uint LegacyMsgItemUsageTeleportTownId = 1_060_020;

    private const uint LegacyMsgItemUsageTeleportWildMapId = 1_060_021;

    private const uint LegacyMsgItemUsageTeleportTempleId = 1_060_022;

    private const uint LegacyMsgItemUsageTeleportWaterId = 1_060_023;

    private const uint LegacyMsgItemUsageTeleportDualMapId = 1_060_025;

    private const uint LegacyMsgItemUsageTeleportNorthGateId = 1_060_026;

    private const uint LegacyMsgItemUsageTeleportSouthGateId = 1_060_027;

    private const uint LegacyMsgItemUsageTeleportTempleWestId = 1_060_028;

    private const uint LegacyMsgItemUsageTeleportTempleEastId = 1_060_029;

    private const uint LegacyArroyoScrollItemId = 1_060_039;

    private const long LegacyBirdIslandGateMapId = 1_015;

    private const long LegacyBirdIslandGateX = 717;

    private const long LegacyBirdIslandGateY = 571;

    private static readonly IReadOnlyDictionary<uint, IReadOnlyList<LegacyNpcTeleportDestination>> LegacyMsgItemUsageTeleportDestinations =
            new Dictionary<uint, IReadOnlyList<LegacyNpcTeleportDestination>>
            {
                [LegacyBirdIslandGateItemId] = new[]
                {
                new LegacyNpcTeleportDestination(
                    LegacyBirdIslandGateMapId,
                    LegacyBirdIslandGateX,
                    LegacyBirdIslandGateY,
                    "MsgItemUsuagePacket"),
                },
                [LegacyMsgItemUsageTeleportTownId] = new[]
                {
                new LegacyNpcTeleportDestination(1_002, 410, 354, "MsgItemUsuagePacket"),
                },
                [LegacyMsgItemUsageTeleportWildMapId] = new[]
                {
                new LegacyNpcTeleportDestination(1_000, 500, 650, "MsgItemUsuagePacket"),
                },
                [LegacyMsgItemUsageTeleportTempleId] = new[]
                {
                new LegacyNpcTeleportDestination(1_020, 565, 562, "MsgItemUsuagePacket"),
                },
                [LegacyMsgItemUsageTeleportWaterId] = new[]
                {
                new LegacyNpcTeleportDestination(1_011, 188, 264, "MsgItemUsuagePacket"),
                },
                [LegacyMsgItemUsageTeleportDualMapId] = new[]
                {
                new LegacyNpcTeleportDestination(1_002, 410, 354, "MsgItemUsuagePacket"),
                new LegacyNpcTeleportDestination(1_002, 405, 655, "MsgItemUsuagePacket"),
                },
                [LegacyMsgItemUsageTeleportNorthGateId] = new[]
                {
                new LegacyNpcTeleportDestination(1_002, 65, 252, "MsgItemUsuagePacket"),
                },
                [LegacyMsgItemUsageTeleportSouthGateId] = new[]
                {
                new LegacyNpcTeleportDestination(1_002, 443, 372, "MsgItemUsuagePacket"),
                },
                [LegacyMsgItemUsageTeleportTempleWestId] = new[]
                {
                new LegacyNpcTeleportDestination(1_011, 537, 767, "MsgItemUsuagePacket"),
                },
                [LegacyMsgItemUsageTeleportTempleEastId] = new[]
                {
                new LegacyNpcTeleportDestination(1_011, 734, 448, "MsgItemUsuagePacket"),
                },
            };

    private static readonly IReadOnlySet<long> LegacyItemUseBlockedTeleportMaps = new HashSet<long>
    {
        601,
        6_000,
        5_040,
        6_001,
        1_005,
        700,
        1_858,
        1_860,
        3_852,
        2_068,
        1_768,
    };

    private static async ValueTask HandleLegacyItemUsageTeleportItemUseAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            PlayerItemSnapshotItem item,
            IReadOnlyList<LegacyNpcTeleportDestination> destinations,
            CancellationToken cancellationToken)
    {
        var teleportBlocked = LegacyItemUseBlockedTeleportMaps.Contains(session.Character.MapId);
        LegacyTeleportPreflightOutcome? firstPreflight = null;
        if (!teleportBlocked)
        {
            firstPreflight = await PrepareLegacyTeleportAsync(
                    context,
                    session,
                    new LegacyTeleportRequest(
                        "MsgItemUsuagePacket.UseItem -> GameClient.Teleport",
                        destinations[0].MapId),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!firstPreflight.Value.Allowed)
            {
                return;
            }

            session = firstPreflight.Value.Session;
        }

        var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, item, out var consumeFrame);
        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);

        if (teleportBlocked)
        {
            var blockedSession = session with { PlayerItems = updatedItems };
            blockedSession = PublishLegacyItemUsageSession(context, blockedSession);
            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgItemUsage teleport item consume only " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} " +
                    $"map={session.Character.MapId}; legacy BlockTeleportMap blocks destination " +
                    $"{destinations[0].MapId} ({destinations[0].X},{destinations[0].Y}).")
                .ConfigureAwait(false);
            return;
        }

        var updated = session with
        {
            PlayerItems = updatedItems,
        };
        var expectedSession = session;

        for (var index = 0; index < destinations.Count; index++)
        {
            var destination = destinations[index];
            if (index > 0)
            {
                var preflight = await PrepareLegacyTeleportAsync(
                        context with { Session = updated },
                        updated,
                        new LegacyTeleportRequest(
                            "MsgItemUsuagePacket.UseItem -> GameClient.Teleport",
                            destination.MapId),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!preflight.Allowed)
                {
                    return;
                }

                updated = preflight.Session;
                expectedSession = preflight.Session;
            }

            var teleportedSession = await ExecuteLegacyTeleportDestinationAsync(
                    context with { Session = updated },
                    updated,
                    new LegacyTeleportDestination(
                        destination.MapId,
                        destination.X,
                        destination.Y),
                    timestamp: 0,
                    cancellationToken,
                    expectedSession: expectedSession)
                .ConfigureAwait(false);
            if (teleportedSession is null)
            {
                return;
            }

            updated = teleportedSession;
            expectedSession = teleportedSession;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage teleport item " +
                $"{FormatSession(context with { Session = updated })} itemUid={item.Uid} itemId={item.ItemId} " +
                $"map={destinations[^1].MapId} x={destinations[^1].X} y={destinations[^1].Y} " +
                $"count={destinations.Count}; source=MsgItemUsuagePacket item teleport -> GameClient.Teleport.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyConsumeOnlyItemUseAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            PlayerItemSnapshotItem item,
            string itemFamily,
            string sourceEvidence,
            CancellationToken cancellationToken)
    {
        var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, item, out var consumeFrame);
        var updated = session with { PlayerItems = updatedItems };
        updated = PublishLegacyItemUsageSession(context, updated);

        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage consume-only item " +
                $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} family={itemFamily}; " +
                $"{sourceEvidence}.")
            .ConfigureAwait(false);
    }
}
