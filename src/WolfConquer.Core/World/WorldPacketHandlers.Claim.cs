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
    [PacketHandler(WorldPacketOpcodes.LegacyClaim)]
    private static async ValueTask HandleLegacyClaimAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyClaimPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyClaim packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClaim " +
                    $"{FormatSession(context)} mode={packet.Mode}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyClaimAction.Show)
        {
            var response = CreateLegacyClaimPacket(
                session.Character,
                packet.MentorUid,
                packet.OnlineBlessingTraining,
                packet.HuntBlessing);
            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyClaim " +
                    $"{FormatSession(context)} mode={packet.Mode} mentorUid={packet.MentorUid} " +
                    $"mentorExperience={response.MentorExperience}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyClaimAction.ClaimAmountExpBalls)
        {
            await HandleLegacyClaimAmountExpBallsAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyClaimAction.ClaimExperience)
        {
            await HandleLegacyClaimExperienceAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyClaimAction.ClaimHeavenBlessing)
        {
            await HandleLegacyClaimHeavenBlessingAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyClaimAction.ClaimPlus)
        {
            await HandleLegacyClaimPlusAsync(context, session, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer source-backed no-op LegacyClaim " +
                $"{FormatSession(context)} mode={packet.Mode}; " +
                "source MsgClaim.HandlerMentorPrize returns immediately for unsupported modes.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClaimExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClaimPacket packet,
        CancellationToken cancellationToken)
    {
        var mentorExpBalls = ReadAttributeUInt64(
            session.Character.Attributes,
            "Mentor_ExpBalls",
            "MentorExpBalls",
            "MentorExperience",
            "MentorExperienceBalls");
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var updatedCharacter = WithLegacyMentorExpBalls(session.Character, 0);
        var modeledExperience = TryCalculateLegacyExpBallUse(
            context.WorldData,
            session.Character,
            mentorExpBalls,
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

        if (session.Character.Level < LegacyMaxPlayerLevel)
        {
            var gainedExpBalls = mentorExpBalls / LegacyExpBallAmount;
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk($"You have gained experience worth {gainedExpBalls.ToString(CultureInfo.InvariantCulture)} exp ball(s).")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyStringPacket.PlayerEffect(uid, "angelwing").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (modeledExperience)
        {
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

        var response = CreateLegacyClaimPacket(
            updatedCharacter,
            packet.MentorUid,
            packet.OnlineBlessingTraining,
            packet.HuntBlessing) with
        {
            Mode = uid,
        };
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        var experienceEvidence = modeledExperience
            ? $"experience={session.Character.Experience}->{updatedCharacter.Experience} level={session.Character.Level}->{updatedCharacter.Level}"
            : session.Character.Level >= LegacyMaxPlayerLevel
                ? "source GainExpBall returns without EXP mutation at level 140 or above"
            : "level experience data is not loaded, so EXP mutation is not modeled";
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyClaim experience " +
                $"{FormatSession(context with { Session = updatedSession })} mentorUid={packet.MentorUid} " +
                $"claimed={mentorExpBalls} mentorExpBalls={mentorExpBalls}->0 " +
                $"{experienceEvidence}; source=MsgClaim.HandlerMentorPrize ClaimExperience.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClaimAmountExpBallsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClaimPacket packet,
        CancellationToken cancellationToken)
    {
        var mentorExpBalls = ReadAttributeUInt64(
            session.Character.Attributes,
            "Mentor_ExpBalls",
            "MentorExpBalls",
            "MentorExperience",
            "MentorExperienceBalls");
        var requestedExpBalls = packet.MentorExperience;
        if (requestedExpBalls == 0 ||
            requestedExpBalls > mentorExpBalls ||
            requestedExpBalls > uint.MaxValue)
        {
            var unchanged = CreateLegacyClaimPacket(
                session.Character,
                packet.MentorUid,
                packet.OnlineBlessingTraining,
                packet.HuntBlessing);
            await context.Connection.SendAsync(unchanged.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClaim amount exp balls " +
                    $"{FormatSession(context)} mentorUid={packet.MentorUid} requested={requestedExpBalls} " +
                    $"available={mentorExpBalls}; source MsgClaim only grants when Mentor_ExpBalls >= requested amount.")
                .ConfigureAwait(false);
            return;
        }

        var updatedCharacter = WithLegacyMentorExpBalls(
            session.Character,
            checked(mentorExpBalls - requestedExpBalls));
        var modeledExperience = TryCalculateLegacyExpBallUse(
            context.WorldData,
            session.Character,
            requestedExpBalls,
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
        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
        if (modeledExperience)
        {
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

        var response = CreateLegacyClaimPacket(
            updatedCharacter,
            packet.MentorUid,
            packet.OnlineBlessingTraining,
            packet.HuntBlessing) with
        {
            Mode = uid,
        };
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        var experienceEvidence = modeledExperience
            ? $"experience={session.Character.Experience}->{updatedCharacter.Experience} level={session.Character.Level}->{updatedCharacter.Level}"
            : "level experience data is not loaded, so EXP mutation is not modeled";
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyClaim amount exp balls " +
                $"{FormatSession(context with { Session = updatedSession })} mentorUid={packet.MentorUid} " +
                $"claimed={requestedExpBalls} mentorExpBalls={mentorExpBalls}->{response.MentorExperience} " +
                $"{experienceEvidence}; source=MsgClaim.HandlerMentorPrize ClaimAmountExpBalls.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClaimHeavenBlessingAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClaimPacket packet,
        CancellationToken cancellationToken)
    {
        var mentorBlessing = ReadAttributeUInt64(
            session.Character.Attributes,
            "Mentor_Blessing",
            "MentorBlessing",
            "MentorHeavensBlessing");
        var currentBlessing = ReadAttributeUInt64(
            session.Character.Attributes,
            "HeavenBlessing",
            "HeavensBlessing");
        var updatedBlessing = checked(currentBlessing + mentorBlessing);

        var updatedCharacter = WithLegacyMentorBlessing(session.Character, 0);
        if (updatedBlessing > 0)
        {
            updatedCharacter = WithCharacterAttribute(updatedCharacter, "HeavenBlessing", updatedBlessing);
            updatedCharacter = WithCharacterAttribute(updatedCharacter, "HeavensBlessing", updatedBlessing);
            updatedCharacter = WithLegacyStatusFlag(updatedCharacter, LegacyHeavenBlessingFlag);
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));

        if (mentorBlessing > 0)
        {
            var message = mentorBlessing > 86_400
                ? $"You`ve received {mentorBlessing / 86_400} days` blessing time."
                : $"You`ve received {mentorBlessing / 60 / 60} hours` blessing time.";
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (updatedBlessing > 0)
        {
            await context.Connection.SendAsync(
                    new LegacyMsgUpdatePacket(
                        uid,
                        [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.HeavensBlessing, updatedBlessing)])
                    .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    new LegacyMsgUpdatePacket(
                        uid,
                        [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.OnlineTraining, 0)])
                    .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (updatedCharacter.MapId is 601 or 1_039)
            {
                await context.Connection.SendAsync(
                        new LegacyMsgUpdatePacket(
                            uid,
                            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.OnlineTraining, 1)])
                        .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeStatusFlagVectorUpdate(uid, CreateLegacyRuntimeStatusFlagVector(updatedCharacter))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyStringPacket.PlayerEffect(uid, "bless").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var response = CreateLegacyClaimPacket(
            updatedCharacter,
            packet.MentorUid,
            packet.OnlineBlessingTraining,
            packet.HuntBlessing) with
        {
            Mode = uid,
        };
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyClaim heaven blessing " +
                $"{FormatSession(context with { Session = updatedSession })} mentorUid={packet.MentorUid} " +
                $"mentorBlessing={mentorBlessing}->0 heavenBlessing={currentBlessing}->{updatedBlessing}; " +
                "source=MsgClaim.HandlerMentorPrize ClaimHeavenBlessing.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyClaimPlusAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyClaimPacket packet,
        CancellationToken cancellationToken)
    {
        const uint plusStoneItemId = 730_001;
        const ulong stonesPerPlusStone = 100;

        var mentorStones = ReadAttributeUInt64(
            session.Character.Attributes,
            "Mentor_Stones",
            "MentorStones",
            "MentorPlusStone",
            "PlusStone");
        var sourceClaimable = unchecked((ushort)(mentorStones / stonesPerPlusStone));
        var availableSlots = Math.Max(0, LegacyInventorySlots - session.PlayerItems.InventoryItems.Count);
        var grantedCount = Math.Min(sourceClaimable, availableSlots);

        var grantedItems = new List<PlayerItemSnapshotItem>(grantedCount);
        var updatedStones = mentorStones;
        if (grantedCount > 0)
        {
            var durability = TryReadItemType(context.WorldData, plusStoneItemId, out var itemType)
                ? checked((ushort)itemType.LegacyDurability)
                : (ushort)0;
            var nextUid = GetNextSessionItemUid(session.PlayerItems);
            for (var index = 0; index < grantedCount; index++)
            {
                grantedItems.Add(
                    new PlayerItemSnapshotItem
                    {
                        Uid = checked(nextUid + (uint)index),
                        ItemId = plusStoneItemId,
                        Durability = durability,
                        MaximumDurability = durability,
                        Position = 0,
                        StackSize = 1,
                        Plus = 1,
                    });
                updatedStones = updatedStones > stonesPerPlusStone
                    ? updatedStones - stonesPerPlusStone
                    : 0;
            }
        }

        var updatedCharacter = WithLegacyMentorStones(session.Character, updatedStones);
        var updatedItems = grantedItems.Count == 0
            ? session.PlayerItems
            : session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).ToArray(),
            };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        foreach (var item in grantedItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
        var response = CreateLegacyClaimPacket(
            updatedCharacter,
            packet.MentorUid,
            packet.OnlineBlessingTraining,
            packet.HuntBlessing) with
        {
            Mode = uid,
        };
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyClaim plus " +
                $"{FormatSession(context with { Session = updatedSession })} mentorUid={packet.MentorUid} " +
                $"mentorStones={mentorStones}->{updatedStones} sourceClaimable={sourceClaimable} " +
                $"granted={grantedItems.Count} availableSlots={availableSlots}; " +
                "source=MsgClaim.HandlerMentorPrize ClaimPlus.")
            .ConfigureAwait(false);
    }

    private static LegacyClaimPacket CreateLegacyClaimPacket(
        CharacterSnapshot character,
        uint mentorUid,
        uint onlineBlessingTraining,
        uint huntBlessing)
    {
        return LegacyClaimPacket.Show(
            mentorUid,
            onlineBlessingTraining,
            huntBlessing,
            ReadAttributeUInt64(
                character.Attributes,
                "Mentor_ExpBalls",
                "MentorExpBalls",
                "MentorExperience",
                "MentorExperienceBalls"),
            ReadAttributeUInt16OrZero(character.Attributes, "Mentor_Blessing", "MentorBlessing", "MentorHeavensBlessing"),
            ReadAttributeUInt16OrZero(character.Attributes, "Mentor_Stones", "MentorStones", "MentorPlusStone", "PlusStone"));
    }

    private static CharacterSnapshot WithLegacyMentorExpBalls(CharacterSnapshot character, ulong value)
    {
        var formatted = value.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Mentor_ExpBalls"] = formatted,
            ["MentorExpBalls"] = formatted,
            ["MentorExperience"] = formatted,
            ["MentorExperienceBalls"] = formatted,
        };

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyHuntBlessing(CharacterSnapshot character, uint value)
    {
        var formatted = value.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["HuntingBlessing"] = formatted,
            ["HuntBlessing"] = formatted,
        };

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyOnlineTrainingPoints(CharacterSnapshot character, uint value)
    {
        var formatted = value.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["OnlineTrainingPoints"] = formatted,
        };

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyMentorBlessing(CharacterSnapshot character, ulong value)
    {
        var formatted = value.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Mentor_Blessing"] = formatted,
            ["MentorBlessing"] = formatted,
            ["MentorHeavensBlessing"] = formatted,
        };

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyMentorStones(CharacterSnapshot character, ulong value)
    {
        var formatted = value.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Mentor_Stones"] = formatted,
            ["MentorStones"] = formatted,
            ["MentorPlusStone"] = formatted,
            ["PlusStone"] = formatted,
        };

        return character with { Attributes = attributes };
    }

}
