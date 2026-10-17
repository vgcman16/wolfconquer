using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    private const string RuntimeTailedBeastActivatedAttribute = "TailedBeastActivated";
    private const string RuntimeTailedBeastLevelAttribute = "TailedBeastLevel";

    [PacketHandler(WorldPacketOpcodes.LegacyBeastsInfo)]
    private static async ValueTask HandleLegacyBeastsInfoAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyBeastsInfoPacket.TryParse(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyBeastsInfo packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetSessionByUid(context, query.Uid, out var target))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyBeastsInfo {FormatSession(context)} uid={query.Uid}; target session was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (LegacyBeastsInfoPacket.TryCreateStartup(
            target.Session.Character,
            target.Session.PlayerItems,
            target.Session.AlternateEquipmentActive,
            out var response))
        {
            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyBeastsInfo {FormatSession(context)} uid={query.Uid}.")
            .ConfigureAwait(false);
    }

    [PacketHandler(WorldPacketOpcodes.LegacyBeastsOpt)]
    private static async ValueTask HandleLegacyBeastsOptAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyBeastsOptPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyBeastsOpt packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyBeastsOpt {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type == 1)
        {
            await HandleLegacyBeastsOptFeedAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type == 0)
        {
            var updatedSession = session;
            if (!ReadAttributeBool(session.Character.Attributes, RuntimeTailedBeastActivatedAttribute) &&
                HasLegacyBeastActivationRuneSlots(session.PlayerItems, session.AlternateEquipmentActive))
            {
                var updatedCharacter = WithCharacterAttribute(
                    session.Character,
                    RuntimeTailedBeastActivatedAttribute,
                    "1");
                updatedCharacter = WithCharacterAttribute(
                    updatedCharacter,
                    RuntimeTailedBeastLevelAttribute,
                    "1");
                updatedSession = session with { Character = updatedCharacter };
                context.Sessions?.Set(context.Connection, updatedSession);
            }

            await SendLegacyBeastsOptCurrentInfoAndEchoAsync(context, updatedSession, packet, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer handled LegacyBeastsOpt {FormatSession(context)} type={packet.Type}.")
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyBeastsOptCurrentInfoAndEchoAsync(context, session, packet, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyBeastsOpt no-op {FormatSession(context)} type={packet.Type} items={packet.Items.Count}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyBeastsOptFeedAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyBeastsOptPacket packet,
        CancellationToken cancellationToken)
    {
        if (ReadAttributeUInt64(
                session.Character.Attributes,
                RuntimeTailedBeastLevelAttribute,
                "BeastLevel",
                "BeastsLevel") >= 100)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyBeastsOpt feed {FormatSession(context)}; tailed beast is already level 100.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = session.PlayerItems;
        var earnedPoints = 0UL;
        var removedItems = 0;

        foreach (var feedItem in packet.Items)
        {
            var inventoryItem = updatedItems.InventoryItems.FirstOrDefault(item => item.Uid == feedItem.UID);
            if (inventoryItem is null)
            {
                continue;
            }

            earnedPoints = checked(earnedPoints + (ulong)GetLegacyBeastFeedPoints(inventoryItem.ItemId) * feedItem.Stack);
            updatedItems = RemoveInventoryItem(updatedItems, inventoryItem);
            removedItems++;

            await context.Connection.SendAsync(
                    new MsgItemUsagePacket(
                        inventoryItem.Uid,
                        DwParam: 0,
                        MsgItemUsageAction.RemoveInventory,
                        Timestamp: 0,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedCharacter = session.Character;
        if (earnedPoints > 0)
        {
            var currentPoints = ReadAttributeUInt64(updatedCharacter.Attributes, "TailedBeastTotalPoints");
            var updatedPoints = checked(currentPoints + earnedPoints);
            if (updatedPoints > uint.MaxValue)
            {
                updatedPoints = uint.MaxValue;
            }

            updatedCharacter = WithCharacterAttribute(updatedCharacter, "TailedBeastTotalPoints", updatedPoints);
        }

        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                    updatedSession.PlayerItems,
                    updatedSession.AlternateEquipmentActive).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (LegacyBeastsInfoPacket.TryCreateStartup(
            updatedSession.Character,
            updatedSession.PlayerItems,
            updatedSession.AlternateEquipmentActive,
            out var beastsInfo))
        {
            await context.Connection.SendAsync(beastsInfo.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyBeastsOpt feed " +
                $"{FormatSession(context)} matchedItems={removedItems} earnedPoints={earnedPoints}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyBeastsOptCurrentInfoAndEchoAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyBeastsOptPacket packet,
        CancellationToken cancellationToken)
    {
        if (LegacyBeastsInfoPacket.TryCreateStartup(
            session.Character,
            session.PlayerItems,
            session.AlternateEquipmentActive,
            out var beastsInfo))
        {
            await context.Connection.SendAsync(beastsInfo.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
    }





































    private static bool HasLegacyBeastActivationRuneSlots(
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive)
    {
        var start = alternateEquipmentActive ? 121 : 101;
        var end = alternateEquipmentActive ? 135 : 115;
        return playerItems.RuneItems.Count(item => item.Position >= start && item.Position <= end) >= 5;
    }

    private static uint GetLegacyBeastFeedPoints(uint itemId)
    {
        return itemId switch
        {
            3_009_100 => 100,
            3_009_101 => 300,
            3_009_102 => 1_000,
            3_009_103 => 3_000,
            3_009_104 => 10_000,
            _ => 0,
        };
    }

}
