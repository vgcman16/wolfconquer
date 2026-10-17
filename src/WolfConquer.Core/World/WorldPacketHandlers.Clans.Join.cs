using System.Globalization;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyClanJoinAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClanPacket packet,
        CancellationToken cancellationToken)
    {
        if (ReadAttributeUInt64(session.Character.Attributes, "ClanID", "ClanUID") != 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan Join " +
                    $"{FormatSession(context with { Session = session })} targetUid={packet.ClanId} reason=already-in-clan.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindActiveLegacyClanLeaderSession(
                context.Sessions,
                packet.ClanId,
                out var leaderSession))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan Join " +
                    $"{FormatSession(context with { Session = session })} targetUid={packet.ClanId} reason=leader-not-found.")
                .ConfigureAwait(false);
            return;
        }

        await leaderSession.Connection.SendAsync(
                LegacyClanSingleClientPacket
                    .Create(
                        LegacyClanPacket.JoinMode,
                        CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                        session.Character.Name)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyClan Join " +
                $"{FormatSession(context with { Session = session })} leaderUid={leaderSession.Session.Character.Uid} " +
                $"requesterUid={session.Character.Uid} requesterName={session.Character.Name}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClanAcceptJoinRequestAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClanPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.Values.Count == 0)
        {
            await context.Connection.SendAsync(
                    LegacyClanPacket.CreateCallback(packet).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyClan AcceptJoinRequest " +
                    $"{FormatSession(context with { Session = session })} targetUid={packet.ClanId} callback=source.")
                .ConfigureAwait(false);
            return;
        }

        var clanId = ReadAttributeUInt64(session.Character.Attributes, "ClanID", "ClanUID");
        var clanRank = ReadAttributeUInt64(session.Character.Attributes, "ClanRank");
        if (clanId == 0 || clanRank != 100)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan AcceptJoinRequest " +
                    $"{FormatSession(context with { Session = session })} targetUid={packet.ClanId} " +
                    $"clanId={clanId} rank={clanRank}.")
                .ConfigureAwait(false);
            return;
        }

        if (CountLegacyClanMembers(session.UsersRoot, checked((uint)clanId)) >= LegacyClanMaxPlayers)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("I'm sorry , but your clan already is at the max number of members of 11.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan AcceptJoinRequest " +
                    $"{FormatSession(context with { Session = session })} targetUid={packet.ClanId} " +
                    $"clanId={clanId} reason=clan-full max={LegacyClanMaxPlayers}.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindActiveLegacyNoClanSession(context.Sessions, packet.ClanId, out var requester))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan AcceptJoinRequest " +
                    $"{FormatSession(context with { Session = session })} targetUid={packet.ClanId} reason=requester-not-found.")
                .ConfigureAwait(false);
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClanID"] = clanId.ToString(CultureInfo.InvariantCulture),
            ["ClanUID"] = clanId.ToString(CultureInfo.InvariantCulture),
            ["ClanRank"] = "10",
            ["ClanDonation"] = "0",
        };
        var requesterAttributes = new Dictionary<string, string>(
            requester.Session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            requesterAttributes[pair.Key] = pair.Value;
        }

        var requesterUsersRoot = string.IsNullOrWhiteSpace(requester.Session.UsersRoot)
            ? session.UsersRoot
            : requester.Session.UsersRoot;
        var updatedRequester = requester.Session with
        {
            Character = requester.Session.Character with { Attributes = requesterAttributes },
            UsersRoot = requesterUsersRoot,
        };
        context.Sessions?.Set(requester.Connection, updatedRequester);

        if (TryPersistLegacyCharacterAttributes(
                requesterUsersRoot,
                requester.Session.AccountId,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyClan " +
                    $"{FormatSession(context with { Session = updatedRequester })} action=AcceptJoinRequest " +
                    $"path={Path.GetFileName(path)} keys=ClanID,ClanUID,ClanRank,ClanDonation.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan AcceptJoinRequest persistence " +
                    $"{FormatSession(context with { Session = updatedRequester })} targetUid={packet.ClanId} reason={reason}.")
                .ConfigureAwait(false);
        }

        var responseCount = 0;
        if (LegacyClanPacket.TryCreateStartup(updatedRequester.Character, updatedRequester.UsersRoot, out var startup))
        {
            await requester.Connection.SendAsync(startup.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            responseCount++;
        }

        if (LegacyClanPacket.TryCreateBulletin(updatedRequester.Character, updatedRequester.UsersRoot, out var bulletin))
        {
            await requester.Connection.SendAsync(bulletin.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            responseCount++;
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyClan AcceptJoinRequest " +
                $"{FormatSession(context with { Session = session })} clanId={clanId} " +
                $"requesterUid={updatedRequester.Character.Uid} requesterName={updatedRequester.Character.Name} " +
                $"responses={responseCount}.")
            .ConfigureAwait(false);
    }

    private static bool TryFindActiveLegacyClanLeaderSession(
        WorldSessionStore? sessions,
        uint uid,
        out WorldActiveSession activeSession)
    {
        activeSession = default!;
        if (sessions is null)
        {
            return false;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (active.Session.Character.Uid == uid &&
                ReadAttributeUInt64(active.Session.Character.Attributes, "ClanID", "ClanUID") != 0 &&
                ReadAttributeUInt64(active.Session.Character.Attributes, "ClanRank") == 100)
            {
                activeSession = active;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindActiveLegacyNoClanSession(
        WorldSessionStore? sessions,
        uint uid,
        out WorldActiveSession activeSession)
    {
        activeSession = default!;
        if (sessions is null)
        {
            return false;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (active.Session.Character.Uid == uid &&
                ReadAttributeUInt64(active.Session.Character.Attributes, "ClanID", "ClanUID") == 0)
            {
                activeSession = active;
                return true;
            }
        }

        return false;
    }

    private static int CountLegacyClanMembers(string? usersRoot, uint clanId)
    {
        if (string.IsNullOrWhiteSpace(usersRoot) || !Directory.Exists(usersRoot))
        {
            return 0;
        }

        var count = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(usersRoot, "*.ini"))
            {
                if (TryReadLegacyClanMemberFile(path, out var candidate) &&
                    ReadAttributeUInt64(candidate.Attributes, "ClanID", "ClanUID") == clanId)
                {
                    count++;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return count;
    }
}
