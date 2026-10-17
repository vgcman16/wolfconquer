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
    private const int LegacyClanMaxPlayers = 11;

    [PacketHandler(WorldPacketOpcodes.LegacyClan)]
    private static async ValueTask HandleLegacyClanAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyClanPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyClan packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan " +
                    $"{FormatSession(context)} mode={packet.Mode} clanId={packet.ClanId}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyClanPacket.MyClanMode)
        {
            if (LegacyClanPacket.TryCreateStartup(session.Character, session.UsersRoot, out var startup))
            {
                await context.Connection.SendAsync(startup.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                await context.Connection.SendAsync(
                        LegacyClanPacket.CreateMyClanWarStatus(startup.ClanId).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);

                var responseCount = 2;
                if (LegacyClanPacket.TryCreateBulletin(session.Character, session.UsersRoot, out var bulletin))
                {
                    await context.Connection.SendAsync(bulletin.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    responseCount++;
                }

                await context.Log.WriteLineAsync(
                        "WorldServer handled LegacyClan " +
                        $"{FormatSession(context)} mode={packet.Mode} clanId={startup.ClanId} responses={responseCount}.")
                    .ConfigureAwait(false);
                return;
            }

            await context.Connection.SendAsync(
                    LegacyClanPacket.CreateCallback(packet).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyClan " +
                $"{FormatSession(context)} mode={packet.Mode} clanId={packet.ClanId} callback=no-clan.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyClanPacket.MembersMode)
        {
            if (LegacyClanPacket.TryCreateMembers(session.Character, session.UsersRoot, out var members, context.Sessions))
            {
                await context.Connection.SendAsync(members.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer handled LegacyClan " +
                        $"{FormatSession(context)} mode={packet.Mode} clanId={packet.ClanId} members={members.Members.Count}.")
                    .ConfigureAwait(false);
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyClan " +
                    $"{FormatSession(context)} mode={packet.Mode} clanId={packet.ClanId} members=no-clan.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyClanPacket.JoinMode)
        {
            await HandleLegacyClanJoinAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyClanPacket.AcceptJoinRequestMode)
        {
            await HandleLegacyClanAcceptJoinRequestAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyClanPacket.TransferLeaderMode)
        {
            await HandleLegacyClanTransferLeaderAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyClanPacket.KickMemberMode)
        {
            await HandleLegacyClanKickMemberAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyClanPacket.QuitMode)
        {
            await HandleLegacyClanQuitAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyClanPacket.DedicateMode)
        {
            await HandleLegacyClanDedicateAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyClanPacket.AnnounceMode)
        {
            await context.Connection.SendAsync(
                    LegacyClanPacket.CreateCallback(packet).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyClan " +
                    $"{FormatSession(context)} mode={packet.Mode} clanId={packet.ClanId} callback=source.")
                .ConfigureAwait(false);
            return;
        }

        if (LegacyClanPacket.IsSourceNoOpMode(packet.Mode))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyClan " +
                    $"{FormatSession(context)} mode={packet.Mode} clanId={packet.ClanId} source=no-op.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyClan " +
                $"{FormatSession(context)} mode={packet.Mode} clanId={packet.ClanId} values={packet.Values.Count}; clan mutation/list is not modeled.")
            .ConfigureAwait(false);
    }

    [PacketHandler(WorldPacketOpcodes.LegacyFamilyOccupy)]
    private static async ValueTask HandleLegacyFamilyOccupyAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyFamilyOccupyPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyFamilyOccupy packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        var session = context.Session;
        if (context.Sessions?.TryGet(context.Connection, out var storedSession) == true &&
            storedSession is not null)
        {
            session = storedSession;
        }

        if (session is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyFamilyOccupy " +
                    $"{FormatSession(context)} type={packet.Type} npcUid={packet.NpcUid}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type == LegacyFamilyOccupyType.ShowInformation)
        {
            var updatedSession = session with { ActiveNpcId = packet.NpcUid };
            context.Sessions?.Set(context.Connection, updatedSession);
            var information = LegacyFamilyOccupyState.CreateShowInformation(session.UsersRoot, packet.NpcUid);

            await context.Connection.SendAsync(
                    LegacyFamilyOccupyPacket.CreateShowInformationFrame(information),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyFamilyOccupy " +
                    $"{FormatSession(context)} type={packet.Type} npcUid={packet.NpcUid} " +
                    $"clanId={information.ClanId} clanName={information.ClanName} city={information.MapName}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type == LegacyFamilyOccupyType.Join)
        {
            var activeNpcUid = session.ActiveNpcId ?? 0;
            if (ReadAttributeUInt64(session.Character.Attributes, "ClanID", "ClanUID") == 0)
            {
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket.SystemTalk(LegacyFamilyOccupyPacket.SourceNoClanJoinMessage)
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer handled LegacyFamilyOccupy " +
                        $"{FormatSession(context with { Session = session })} type={packet.Type} " +
                        $"npcUid={packet.NpcUid} activeNpcUid={activeNpcUid} response=no-clan; " +
                        "sourceName=MsgFamilyOccupy.Process.")
                    .ConfigureAwait(false);
                return;
            }

            if (!LegacyFamilyOccupyPacket.TryGetSourceClanWarJoinCity(activeNpcUid, out var city))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer handled LegacyFamilyOccupy " +
                        $"{FormatSession(context with { Session = session })} type={packet.Type} " +
                        $"npcUid={packet.NpcUid} activeNpcUid={activeNpcUid} tournament=none; " +
                        "sourceName=MsgFamilyOccupy.Process.")
                    .ConfigureAwait(false);
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyFamilyOccupy " +
                    $"{FormatSession(context with { Session = session })} type={packet.Type} " +
                    $"npcUid={packet.NpcUid} activeNpcUid={activeNpcUid} city={city} state=dead; " +
                    "sourceName=MsgClanWar.CityWar.Join; clan war runtime is not modeled.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyFamilyOccupy " +
                $"{FormatSession(context with { Session = session })} type={packet.Type} npcUid={packet.NpcUid}; " +
                "family occupy mutation is not modeled.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClanQuitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClanPacket packet,
        CancellationToken cancellationToken)
    {
        if (ReadAttributeUInt64(session.Character.Attributes, "ClanID", "ClanUID") == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyClan Quit " +
                    $"{FormatSession(context with { Session = session })} source=no-clan.")
                .ConfigureAwait(false);
            return;
        }

        var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["ClanID"] = "0",
            ["ClanUID"] = "0",
            ["ClanRank"] = "0",
            ["ClanDonation"] = "0",
        };
        var updatedCharacter = session.Character with { Attributes = attributes };
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        await TryPersistLegacyClanMembershipAsync(
                context,
                updatedSession,
                "Quit",
                ["ClanID", "ClanUID", "ClanRank", "ClanDonation"])
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyClanPacket.CreateQuitCallback(packet.ClanId, packet.Parameter, packet.Values).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyClan Quit " +
                $"{FormatSession(context with { Session = updatedSession })} callback=source.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClanTransferLeaderAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClanPacket packet,
        CancellationToken cancellationToken)
    {
        var clanId = ReadAttributeUInt64(session.Character.Attributes, "ClanID", "ClanUID");
        var clanRank = ReadAttributeUInt64(session.Character.Attributes, "ClanRank");
        if (clanId == 0 || clanRank != 100 || packet.Values.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan TransferLeader " +
                    $"{FormatSession(context with { Session = session })} clanId={clanId} rank={clanRank} values={packet.Values.Count}.")
                .ConfigureAwait(false);
            return;
        }

        var targetName = packet.Values[0];
        if (string.Equals(targetName, session.Character.Name, StringComparison.Ordinal))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan TransferLeader " +
                    $"{FormatSession(context with { Session = session })} clanId={clanId} target={targetName} reason=self.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindLegacyClanMemberFile(session.UsersRoot, checked((uint)clanId), targetName, out var target))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan TransferLeader " +
                    $"{FormatSession(context with { Session = session })} clanId={clanId} target={targetName} reason=member-not-found.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindActiveLegacyClanMemberSession(
                context.Sessions,
                checked((uint)clanId),
                target.Uid,
                target.Name,
                out var targetActive))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan TransferLeader " +
                    $"{FormatSession(context with { Session = session })} clanId={clanId} target={targetName} reason=target-offline.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryPersistLegacyClanLeaderName(
                session.UsersRoot,
                checked((uint)clanId),
                target.Name,
                out var clanPath,
                out var clanReason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan TransferLeader persistence " +
                    $"{FormatSession(context with { Session = session })} clanId={clanId} target={targetName} reason={clanReason}.")
                .ConfigureAwait(false);
            return;
        }

        var leaderAttributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["ClanRank"] = "10",
        };
        var targetAttributes = new Dictionary<string, string>(targetActive.Session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["ClanRank"] = "100",
        };
        var updatedLeader = session with
        {
            Character = session.Character with { Attributes = leaderAttributes },
        };
        var updatedTarget = targetActive.Session with
        {
            Character = targetActive.Session.Character with { Attributes = targetAttributes },
        };
        context.Sessions?.Set(context.Connection, updatedLeader);
        context.Sessions?.Set(targetActive.Connection, updatedTarget);

        await TryPersistLegacyClanMembershipAsync(
                context,
                updatedLeader,
                "TransferLeaderOldLeader",
                ["ClanRank"])
            .ConfigureAwait(false);

        if (!TryPersistLegacyCharacterAttributes(target.Path, targetAttributes, ["ClanRank"], out var targetReason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan TransferLeader target persistence " +
                    $"{FormatSession(context with { Session = updatedLeader })} target={targetName} reason={targetReason}.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyClan " +
                    $"{FormatSession(context with { Session = updatedTarget })} action=TransferLeaderNewLeader " +
                    $"path={Path.GetFileName(target.Path)} keys=ClanRank.")
                .ConfigureAwait(false);
        }

        if (LegacyClanPacket.TryCreateStartup(updatedTarget.Character, updatedTarget.UsersRoot, out var targetStartup))
        {
            await targetActive.Connection.SendAsync(targetStartup.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        if (LegacyClanPacket.TryCreateStartup(updatedLeader.Character, updatedLeader.UsersRoot, out var leaderStartup))
        {
            await context.Connection.SendAsync(leaderStartup.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyClan TransferLeader " +
                $"{FormatSession(context with { Session = updatedLeader })} clanId={clanId} target={targetName} " +
                $"clanPath={Path.GetFileName(clanPath)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClanKickMemberAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClanPacket packet,
        CancellationToken cancellationToken)
    {
        var clanId = ReadAttributeUInt64(session.Character.Attributes, "ClanID", "ClanUID");
        var clanRank = ReadAttributeUInt64(session.Character.Attributes, "ClanRank");
        if (clanId == 0 || clanRank != 100 || packet.Values.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan KickMember " +
                    $"{FormatSession(context with { Session = session })} clanId={clanId} rank={clanRank} values={packet.Values.Count}.")
                .ConfigureAwait(false);
            return;
        }

        var targetName = packet.Values[0];
        if (!TryFindLegacyClanMemberFile(session.UsersRoot, checked((uint)clanId), targetName, out var target))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan KickMember " +
                    $"{FormatSession(context with { Session = session })} clanId={clanId} target={targetName} reason=member-not-found.")
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeUInt64(target.Attributes, "ClanRank") == 100)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan KickMember " +
                    $"{FormatSession(context with { Session = session })} clanId={clanId} target={targetName} reason=target-leader.")
                .ConfigureAwait(false);
            return;
        }

        var clearedAttributes = new Dictionary<string, string>(target.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["ClanID"] = "0",
            ["ClanUID"] = "0",
            ["ClanRank"] = "0",
            ["ClanDonation"] = "0",
        };
        if (!TryPersistLegacyCharacterAttributes(
                target.Path,
                clearedAttributes,
                ["ClanID", "ClanUID", "ClanRank", "ClanDonation"],
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan KickMember persistence " +
                    $"{FormatSession(context with { Session = session })} target={targetName} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        foreach (var active in context.Sessions?.Snapshot() ?? [])
        {
            if (active.Session.Character.Uid == target.Uid ||
                string.Equals(active.Session.Character.Name, target.Name, StringComparison.OrdinalIgnoreCase))
            {
                context.Sessions?.Set(
                    active.Connection,
                    active.Session with
                    {
                        Character = active.Session.Character with { Attributes = clearedAttributes },
                    });
            }
        }

        var memberCount = 0;
        if (LegacyClanPacket.TryCreateMembers(session.Character, session.UsersRoot, out var members, context.Sessions))
        {
            memberCount = members.Members.Count;
            await context.Connection.SendAsync(members.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyClan KickMember " +
                $"{FormatSession(context with { Session = session })} clanId={clanId} target={targetName} " +
                $"path={Path.GetFileName(target.Path)} members={memberCount}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClanDedicateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClanPacket packet,
        CancellationToken cancellationToken)
    {
        if (ReadAttributeUInt64(session.Character.Attributes, "ClanID", "ClanUID") == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan Dedicate " +
                    $"{FormatSession(context with { Session = session })} amount={packet.ClanId} reason=no-clan.")
                .ConfigureAwait(false);
            return;
        }

        var amount = checked((long)packet.ClanId);
        if (session.Character.Money < amount)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan Dedicate " +
                    $"{FormatSession(context with { Session = session })} amount={amount} money={session.Character.Money}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedMoney = checked(session.Character.Money - amount);
        var updatedDonation = checked(ReadAttributeUInt64(session.Character.Attributes, "ClanDonation") + packet.ClanId);
        var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["ClanDonation"] = updatedDonation.ToString(CultureInfo.InvariantCulture),
        };
        var updatedCharacter = session.Character with
        {
            Money = updatedMoney,
            Attributes = attributes,
        };
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        await TryPersistLegacyClanMembershipAsync(
                context,
                updatedSession,
                "Dedicate",
                ["Money", "ClanDonation"])
            .ConfigureAwait(false);

        await SendScalarUpdateAsync(
                context,
                updatedCharacter,
                LegacyMsgUpdateDataType.Money,
                CheckedUInt64(updatedMoney, nameof(updatedMoney)),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyClanPacket.CreateDedicateCallback(packet.ClanId, packet.Parameter, packet.Values).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyClan Dedicate " +
                $"{FormatSession(context with { Session = updatedSession })} amount={amount} " +
                $"money={updatedMoney} clanDonation={updatedDonation} callback=source.")
            .ConfigureAwait(false);
    }

}
