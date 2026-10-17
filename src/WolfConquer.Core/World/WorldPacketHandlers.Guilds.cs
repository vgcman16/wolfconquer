using System.Globalization;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    private sealed record LegacyGuildInfo(
            uint GuildId,
            string Name,
            string LeaderName,
            int Level,
            int MemberCount,
            long SilverFund = 0,
            uint ConquerPointFund = 0,
            uint MyRank = 0,
            uint CreateTime = 0,
            string Bulletin = "",
            uint BulletinEnroll = 0,
            uint LeaderId = 0,
            ulong Material = 0,
            ulong Prestige = 0,
            uint RecruitmentFlag = 0,
            uint RecruitmentBattlePower = 0,
            bool RecruitmentOn = false,
            bool RecruitmentOff = false);

    [PacketHandler(WorldPacketOpcodes.LegacyGuildProcess)]
    private static async ValueTask HandleLegacyGuildProcessAsync(
            WorldPacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyGuildProcessPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyGuildProcess packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess " +
                    $"{FormatSession(context)} action={packet.Action} uid={packet.Uid}; " +
                    "no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyGuildProcessAction.SilverDonate)
        {
            await HandleLegacyGuildProcessSilverDonateAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyGuildProcessAction.CpDonate)
        {
            await HandleLegacyGuildProcessCpDonateAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyGuildProcessAction.Bulletin)
        {
            await HandleLegacyGuildProcessBulletinAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyGuildProcessAction.Show)
        {
            await HandleLegacyGuildProcessShowAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyGuildProcessAction.Requirements)
        {
            await HandleLegacyGuildProcessRequirementsAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is LegacyGuildProcessAction.Create
            or LegacyGuildProcessAction.Accept
            or LegacyGuildProcessAction.Invite
            or LegacyGuildProcessAction.Join
            or LegacyGuildProcessAction.Quit
            or LegacyGuildProcessAction.Disband
            or LegacyGuildProcessAction.Promote
            or LegacyGuildProcessAction.Appoint
            or LegacyGuildProcessAction.Resign
            or LegacyGuildProcessAction.Allied
            or LegacyGuildProcessAction.RemoveAlly
            or LegacyGuildProcessAction.Enemy
            or LegacyGuildProcessAction.RemoveEnemy
            or LegacyGuildProcessAction.RequestPromote
            or LegacyGuildProcessAction.RequestAllied
            or LegacyGuildProcessAction.Retire)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess " +
                    $"action={packet.Action} uid={packet.Uid}; " +
                    "source mutates live guild/player state and is not modeled in this parity lane.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action != LegacyGuildProcessAction.InfoName)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyGuildProcess action " +
                    $"{FormatSession(context)} action={packet.Action} uid={packet.Uid}.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetLegacyGuildInfo(session, packet.Uid, out var guild))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess " +
                    $"{FormatSession(context)} action={packet.Action} uid={packet.Uid}; " +
                    "guild info is not available.")
                .ConfigureAwait(false);
            return;
        }

        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{guild.Name} {guild.LeaderName} {guild.Level} {guild.MemberCount}");
        var response = new LegacyStringPacket(packet.Uid, LegacyStringId.GuildName, [text]);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyGuildProcess " +
                $"{FormatSession(context)} action={packet.Action} uid={packet.Uid} " +
                $"name={guild.Name} members={guild.MemberCount}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyGuildProcessShowAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyGuildProcessPacket packet,
            CancellationToken cancellationToken)
    {
        var character = session.Character;
        var guildId = ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId");
        if (guildId == 0 ||
            guildId > uint.MaxValue ||
            !TryGetLegacyGuildInfo(session, (uint)guildId, out var guild))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess Show " +
                    $"{FormatSession(context)} uid={packet.Uid} guildId={guildId}; " +
                    "guild info is not available.")
                .ConfigureAwait(false);
            return;
        }

        var sentBulletin = await SendLegacyGuildInformationAsync(context, guild, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyGuildProcess " +
                "Show " +
                $"{FormatSession(context)} action={packet.Action} uid={packet.Uid} guildId={guild.GuildId} " +
                $"members={guild.MemberCount} rank={guild.MyRank} bulletin={sentBulletin}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyGuildProcessBulletinAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyGuildProcessPacket packet,
            CancellationToken cancellationToken)
    {
        var character = session.Character;
        var guildId = ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId");
        if (guildId == 0 ||
            guildId > uint.MaxValue ||
            !TryGetLegacyGuildInfo(session, (uint)guildId, out var guild))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess Bulletin " +
                    $"{FormatSession(context)} uid={packet.Uid} guildId={guildId}; " +
                    "guild info is not available.")
                .ConfigureAwait(false);
            return;
        }

        if (!string.Equals(character.Name, guild.LeaderName, StringComparison.Ordinal))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess Bulletin " +
                    $"{FormatSession(context)} uid={packet.Uid} guildId={guild.GuildId} " +
                    $"leader={guild.LeaderName}; character is not guild leader.")
                .ConfigureAwait(false);
            return;
        }

        var bulletinText = packet.Strings.Count > 0
            ? packet.Strings[0]
            : string.Empty;
        if (!IsLegacyGuildBulletinAllowed(bulletinText))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess Bulletin " +
                    $"{FormatSession(context)} uid={packet.Uid} guildId={guild.GuildId}; " +
                    "bulletin text failed legacy validation.")
                .ConfigureAwait(false);
            return;
        }

        var bulletinEnroll = GetLegacyGuildBulletinEnroll();
        var updatedGuild = guild with
        {
            Bulletin = bulletinText,
            BulletinEnroll = bulletinEnroll,
        };

        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(character, "GuildBulletin", bulletinText),
            "GuildBulletinEnroll",
            bulletinEnroll);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyGuildProcessBulletinCharacterAsync(updatedContext, updatedSession)
            .ConfigureAwait(false);
        await TryPersistLegacyGuildProcessBulletinGuildAsync(updatedContext, updatedGuild)
            .ConfigureAwait(false);

        var sentBulletin = await SendLegacyGuildInformationAsync(context, updatedGuild, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyGuildProcess " +
                $"{FormatSession(updatedContext)} action={packet.Action} guildId={updatedGuild.GuildId} " +
                $"bulletinEnroll={bulletinEnroll} bulletinLength={bulletinText.Length} " +
                $"bulletin={sentBulletin}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> SendLegacyGuildInformationAsync(
            WorldPacketHandlerContext context,
            LegacyGuildInfo guild,
            CancellationToken cancellationToken)
    {
        var sentBulletin = false;
        if (ShouldSendLegacyGuildBulletin(guild.Bulletin))
        {
            var bulletin = LegacyGuildProcessPacket.Create(
                LegacyGuildProcessAction.Bulletin,
                guild.BulletinEnroll,
                strings: [guild.Bulletin]);
            await context.Connection.SendAsync(bulletin.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            sentBulletin = true;
        }

        var response = new LegacyGuildInformationPacket(
            guild.GuildId,
            guild.SilverFund,
            guild.ConquerPointFund,
            guild.MemberCount,
            guild.MyRank,
            guild.LeaderName,
            ClampToUInt16(guild.Level),
            guild.CreateTime,
            guild.LeaderId,
            guild.Material,
            guild.RecruitmentOff,
            guild.RecruitmentBattlePower,
            guild.RecruitmentOn,
            guild.Prestige,
            guild.RecruitmentFlag);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        return sentBulletin;
    }

    [PacketHandler(WorldPacketOpcodes.LegacyGuildList)]
    private static async ValueTask HandleLegacyGuildListAsync(
            WorldPacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Session is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyGuildList {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var response = LegacyGuildListPacket.Empty();
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyGuildList " +
                $"{FormatSession(context)} rows={response.Items.Count}.")
            .ConfigureAwait(false);
    }
}
