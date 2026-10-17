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

    private const long LegacyBroadcastBlockedMapId = 5_040;
    private const long LegacyBroadcastCost = 5;
    private const long LegacyBroadcastUrgent5Cost = 100_000;
    private const long LegacyBroadcastUrgent15Cost = 150_000;
    private const int LegacyBroadcastMessageMaxLength = 80;

    [PacketHandler(WorldPacketOpcodes.LegacyBroadcast)]
    private static async ValueTask HandleLegacyBroadcastAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyBroadcastPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyBroadcast packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyBroadcast {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        switch (packet.Action)
        {
            case LegacyBroadcastAction.ReleaseSoonMessages:
                await SendLegacyBroadcastListAsync(context, packet.Parameter, onlyMine: false, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyBroadcastAction.MyMessages:
                await SendLegacyBroadcastListAsync(context, packet.Parameter, onlyMine: true, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyBroadcastAction.BroadcastMessage:
                await HandleLegacyBroadcastCreateAsync(context, packet, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyBroadcastAction.Urgent5ConquerPoints:
                await HandleLegacyBroadcastUrgentAsync(
                        context,
                        packet,
                        session,
                        LegacyBroadcastUrgent5Cost,
                        moveToTop: false,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyBroadcastAction.Urgent15ConquerPoints:
                await HandleLegacyBroadcastUrgentAsync(
                        context,
                        packet,
                        session,
                        LegacyBroadcastUrgent15Cost,
                        moveToTop: true,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask SendLegacyBroadcastListAsync(
        WorldPacketHandlerContext context,
        uint page,
        bool onlyMine,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var ownerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var allEntries = LegacyBroadcastRuntime.Snapshot(onlyMine, ownerUid);
        var pageStart = checked((int)Math.Min(page, int.MaxValue / 10) * 10);
        var pageEntries = pageStart >= allEntries.Count
            ? []
            : allEntries.Skip(pageStart).Take(10).ToArray();

        if (pageEntries.Length == 0)
        {
            await context.Connection.SendAsync(
                    new LegacyBroadcastListPacket(page, Segment: 0, Count: 0, []).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        for (var index = 0; index < pageEntries.Length; index += 5)
        {
            var entries = pageEntries.Skip(index).Take(5).ToArray();
            await context.Connection.SendAsync(
                    new LegacyBroadcastListPacket(
                        page,
                        checked((ushort)(index / 5)),
                        checked((ushort)entries.Length),
                        entries).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyBroadcast list {FormatSession(context)} page={page} onlyMine={onlyMine} count={pageEntries.Length}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyBroadcastCreateAsync(
        WorldPacketHandlerContext context,
        LegacyBroadcastPacket packet,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (LegacyBroadcastRuntime.IsFull())
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You cannot send any broadcasts for now. The limit has been reached. Wait until a broadcast is chopped down.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Strings.Count == 0 ||
            string.IsNullOrEmpty(packet.Strings[0]) ||
            session.Character.ConquerPoints < LegacyBroadcastCost)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyBroadcast create {FormatSession(context)} conquerPoints={session.Character.ConquerPoints}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedCharacter = session.Character with
        {
            ConquerPoints = session.Character.ConquerPoints - LegacyBroadcastCost,
        };
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        if (ReadAttributeBool(session.Character.Attributes, "IsStillBanned", "StillBanned"))
        {
            var message = ReadAttributeBool(session.Character.Attributes, "PermenantBannedChat", "PermanentBannedChat")
                ? "Sorry, you still banned from chatting Permenatly."
                : "Sorry, you still banned from chatting.";
            await context.Connection.SendAsync(LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.MapId == LegacyBroadcastBlockedMapId)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Sorry, you still banned from chatting Permenatly. using BotJail, HurdLuck").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var broadcastMessage = packet.Strings[0].Length > LegacyBroadcastMessageMaxLength
            ? packet.Strings[0][..LegacyBroadcastMessageMaxLength]
            : packet.Strings[0];
        var linkedItems = context.ChatItems?.CaptureLinks(
            updatedSession,
            broadcastMessage,
            context.WorldData?.ItemsById,
            context.TimeProvider) ?? 0;
        var unionRank = ReadAttributeUInt64(updatedCharacter.Attributes, "UnionRank");
        if (!LegacyBroadcastRuntime.TryAdd(
                CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                updatedCharacter.Name,
                broadcastMessage,
                unionRank > uint.MaxValue ? uint.MaxValue : (uint)unionRank,
                out var entry))
        {
            return;
        }

        var messageFrame = LegacyMsgMessagePacket.PlayerBroadcast(
            updatedCharacter.Name,
            broadcastMessage,
            entry.UnionRank).ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is { Count: > 0 })
        {
            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await context.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                new LegacyBroadcastPacket(
                    LegacyBroadcastAction.BroadcastMessage,
                    entry.Index + 1,
                    [broadcastMessage]).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyBroadcast create {FormatSession(context)} id={entry.Id} " +
                $"messageLength={broadcastMessage.Length} linkedItems={linkedItems}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyBroadcastUrgentAsync(
        WorldPacketHandlerContext context,
        LegacyBroadcastPacket packet,
        WorldSession session,
        long cost,
        bool moveToTop,
        CancellationToken cancellationToken)
    {
        if (session.Character.ConquerPoints <= cost ||
            !LegacyBroadcastRuntime.TrySpendToUrgent(packet.Parameter, checked((uint)cost), moveToTop))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyBroadcast urgent {FormatSession(context)} id={packet.Parameter} cost={cost} conquerPoints={session.Character.ConquerPoints}.")
                .ConfigureAwait(false);
            return;
        }

        context.Sessions?.Set(
            context.Connection,
            session with
            {
                Character = session.Character with { ConquerPoints = session.Character.ConquerPoints - cost },
            });
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyBroadcast urgent {FormatSession(context)} id={packet.Parameter} cost={cost}.")
            .ConfigureAwait(false);
    }





}
