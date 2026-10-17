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
    [PacketHandler(WorldPacketOpcodes.LegacyBlessingInfo)]
    private static async ValueTask HandleLegacyBlessingInfoAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyBlessingInfoPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyBlessingInfo packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyBlessingInfo " +
                    $"{FormatSession(context)} action={packet.Action}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyBlessingInfoAction.Show)
        {
            var onlineTrainingPoints = ReadAttributeUInt32(session.Character.Attributes, "OnlineTrainingPoints");
            var huntBlessing = ReadAttributeUInt32(session.Character.Attributes, "HuntingBlessing", "HuntBlessing");
            var blessingInfo = new LegacyBlessingInfoPacket(
                LegacyBlessingInfoAction.Show,
                onlineTrainingPoints,
                huntBlessing);
            var claim = CreateLegacyClaimPacket(
                session.Character,
                CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                onlineBlessingTraining: 0,
                huntBlessing: 0);

            await context.Connection.SendAsync(blessingInfo.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Connection.SendAsync(claim.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyBlessingInfo " +
                    $"{FormatSession(context)} action={packet.Action} onlineTrainingPoints={onlineTrainingPoints} " +
                    $"huntBlessing={huntBlessing} mentorExperience={claim.MentorExperience}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyBlessingInfoAction.ClaimHuntBlessing)
        {
            await HandleLegacyBlessingInfoClaimHuntBlessingAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyBlessingInfoAction.ClaimOnlineTraining)
        {
            await HandleLegacyBlessingInfoClaimOnlineTrainingAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer source-backed no-op LegacyBlessingInfo " +
                $"{FormatSession(context)} action={packet.Action}; " +
                "source MsgClaim.MsgBlessingInfo switch has no default handler for this mode.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyBlessingInfoClaimOnlineTrainingAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var onlineTrainingPoints = ReadAttributeUInt32(session.Character.Attributes, "OnlineTrainingPoints");
        if (session.Character.Level >= LegacyMaxPlayerLevel)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Sorry,you are high level! ").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyBlessingInfo online training claim " +
                    $"{FormatSession(context)} onlineTrainingPoints={onlineTrainingPoints}; source MsgBlessingInfo only grants below level 140.")
                .ConfigureAwait(false);
            return;
        }

        if (onlineTrainingPoints == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyBlessingInfo online training claim " +
                    $"{FormatSession(context)} onlineTrainingPoints=0; source MsgBlessingInfo has no mutation when the counter is empty.")
                .ConfigureAwait(false);
            return;
        }

        var expBallAmount = onlineTrainingPoints / 10_000u;
        var updatedCharacter = WithLegacyOnlineTrainingPoints(session.Character, 0);
        var modeledExperience = TryCalculateLegacyExpBallUse(
            context.WorldData,
            session.Character,
            expBallAmount,
            out var updatedLevel,
            out var updatedExperience);
        if (modeledExperience)
        {
            updatedCharacter = updatedCharacter with
            {
                Level = updatedLevel,
                Experience = updatedExperience,
            };
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        if (modeledExperience)
        {
            var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
            if (updatedCharacter.Level != session.Character.Level)
            {
                await context.Connection.SendAsync(
                        LegacyMsgUpdatePacket
                            .CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)updatedCharacter.Level))
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeExperienceUpdate(uid, checked((ulong)updatedCharacter.Experience))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var experienceEvidence = modeledExperience
            ? $"experience={session.Character.Experience}->{updatedCharacter.Experience} level={session.Character.Level}->{updatedCharacter.Level}"
            : "level experience data is not loaded, so EXP mutation is not modeled";
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyBlessingInfo online training claim " +
                $"{FormatSession(context with { Session = updatedSession })} onlineTrainingPoints={onlineTrainingPoints}->0 " +
                $"expBallAmount={expBallAmount.ToString(CultureInfo.InvariantCulture)} {experienceEvidence}; " +
                "source=MsgClaim.MsgBlessingInfo ClaimOnlineTraining.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyBlessingInfoClaimHuntBlessingAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var huntBlessing = ReadAttributeUInt32(session.Character.Attributes, "HuntingBlessing", "HuntBlessing");
        if (session.Character.Level >= LegacyMaxPlayerLevel)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Sorry,you are high level! ").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyBlessingInfo hunt blessing claim " +
                    $"{FormatSession(context)} huntBlessing={huntBlessing}; source MsgBlessingInfo only grants below level 140.")
                .ConfigureAwait(false);
            return;
        }

        if (huntBlessing == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyBlessingInfo hunt blessing claim " +
                    $"{FormatSession(context)} huntBlessing=0; source MsgBlessingInfo has no mutation when the counter is empty.")
                .ConfigureAwait(false);
            return;
        }

        var expBallAmount = huntBlessing / 10_000u;
        var updatedCharacter = WithLegacyHuntBlessing(session.Character, 0);
        var modeledExperience = TryCalculateLegacyExpBallUse(
            context.WorldData,
            session.Character,
            expBallAmount,
            out var updatedLevel,
            out var updatedExperience);
        if (modeledExperience)
        {
            updatedCharacter = updatedCharacter with
            {
                Level = updatedLevel,
                Experience = updatedExperience,
            };
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        if (modeledExperience)
        {
            var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
            if (updatedCharacter.Level != session.Character.Level)
            {
                await context.Connection.SendAsync(
                        LegacyMsgUpdatePacket
                            .CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)updatedCharacter.Level))
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeExperienceUpdate(uid, checked((ulong)updatedCharacter.Experience))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var experienceEvidence = modeledExperience
            ? $"experience={session.Character.Experience}->{updatedCharacter.Experience} level={session.Character.Level}->{updatedCharacter.Level}"
            : "level experience data is not loaded, so EXP mutation is not modeled";
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyBlessingInfo hunt blessing claim " +
                $"{FormatSession(context with { Session = updatedSession })} huntBlessing={huntBlessing}->0 " +
                $"expBallAmount={expBallAmount.ToString(CultureInfo.InvariantCulture)} {experienceEvidence}; " +
                "source=MsgClaim.MsgBlessingInfo ClaimHuntBlessing.")
            .ConfigureAwait(false);
    }

}
