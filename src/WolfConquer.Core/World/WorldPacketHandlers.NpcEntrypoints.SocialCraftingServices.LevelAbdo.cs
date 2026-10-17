using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesLevelNpcId = 19_039;

    private const long LegacySocialCraftingServicesLevelTarget = 140;

    private const MsgActionType LegacySocialCraftingServicesLevelAction = (MsgActionType)92;

    private const string LegacySocialCraftingServicesLevelSource = "NpcHandler.LevelAbdo";

    private const string LegacySocialCraftingServicesLevelLines = "53533-53568";

    private const string LegacySocialCraftingServicesLevelAlreadyMaxed =
        "You already achieved the highest level possible.";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesLevelAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesLevelSource,
                        ["Level 140."],
                        [new("Yes.", 1), new("No thanks.", LegacyNpcClosedOption)],
                        10,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesLevelSource,
                        ["Level 140 is free here."],
                        [new("Thanks!", 3)],
                        10,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await context.Log.WriteLineAsync(
                        "WorldServer ignored LegacyNpc SocialCraftingServices no-op " +
                        $"{FormatSession(context)} source={LegacySocialCraftingServicesLevelSource} " +
                        $"sourceLines={LegacySocialCraftingServicesLevelLines} npcId={LegacySocialCraftingServicesLevelNpcId} optionId={optionId}.")
                    .ConfigureAwait(false);
                return true;

            case 3:
                await TryApplyLegacySocialCraftingServicesLevelAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask TryApplyLegacySocialCraftingServicesLevelAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level == LegacySocialCraftingServicesLevelTarget)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(LegacySocialCraftingServicesLevelAlreadyMaxed).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc LevelAbdo level grant " +
                    $"{FormatSession(context)} source={LegacySocialCraftingServicesLevelSource} " +
                    $"sourceLines={LegacySocialCraftingServicesLevelLines} reason=alreadyLevel140.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions ||
            context.WorldData is null ||
            string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await SendLegacySocialCraftingServicesGapAsync(
                    context,
                    session,
                    LegacySocialCraftingServicesLevelNpcId,
                    optionId,
                    LegacySocialCraftingServicesLevelSource,
                    LegacySocialCraftingServicesLevelLines,
                    "UpdateLevel(140) requires Sessions, WorldData, and UsersRoot for a persistence-first commit",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryCreateLegacySocialCraftingServicesLevelCharacter(
                context,
                session,
                out var updatedCharacter,
                out var sendSourceStats,
                out var attributePointsAdded,
                out var projectionReason))
        {
            await SendLegacySocialCraftingServicesGapAsync(
                    context,
                    session,
                    LegacySocialCraftingServicesLevelNpcId,
                    optionId,
                    LegacySocialCraftingServicesLevelSource,
                    LegacySocialCraftingServicesLevelLines,
                    $"UpdateLevel(140) projection unavailable reason={projectionReason}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var persistenceValues = CreateLegacySocialCraftingServicesLevelPersistenceValues(updatedCharacter);
        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                updatedCharacter.Uid,
                persistenceValues,
                out var persistedPath,
                out var persistenceReason))
        {
            await SendLegacySocialCraftingServicesGapAsync(
                    context,
                    session,
                    LegacySocialCraftingServicesLevelNpcId,
                    optionId,
                    LegacySocialCraftingServicesLevelSource,
                    LegacySocialCraftingServicesLevelLines,
                    $"UpdateLevel(140) persistence failed reason={persistenceReason}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { Character = updatedCharacter };
        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));

        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket
                    .CreateLegacyRuntimeLevelUpdate(
                        uid,
                        checked((ulong)LegacySocialCraftingServicesLevelTarget))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var levelAction = new MsgActionPacket
        {
            ObjId = uid,
            Type = LegacySocialCraftingServicesLevelAction,
            PositionX = checked((uint)LegacySocialCraftingServicesLevelTarget),
        };
        await SendFrameToPlayerAndVisiblePeersAsync(
                updatedContext,
                context.Connection,
                updatedSession,
                levelAction.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (sendSourceStats)
        {
            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyRuntimeSourceStatUpdates(uid, updatedCharacter))
            {
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else if (attributePointsAdded != 0)
        {
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeAttributePointsUpdate(
                            uid,
                            CheckedUInt64(updatedCharacter.AttributePoints, nameof(updatedCharacter.AttributePoints)))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var preHealHitpoints = Math.Min(session.Character.MinHitPoints, updatedCharacter.MinHitPoints);
        var preHealCharacter = WithLegacySocialCraftingServicesLevelHitpoints(
            updatedCharacter,
            preHealHitpoints);
        await SendLegacyEquipmentRuntimeRefreshAsync(
                updatedContext,
                updatedSession with { Character = preHealCharacter },
                cancellationToken)
            .ConfigureAwait(false);

        var maxHitpoints = CheckedUInt32(updatedCharacter.MinHitPoints, nameof(updatedCharacter.MinHitPoints));
        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                     uid,
                     maxHitpoints,
                     maxHitpoints))
        {
            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacySocialCraftingServicesLevelSource,
                ["Okay, you have a Level 140! "],
                [new LegacyNpcDialogOption("Thx ", LegacyNpcClosedOption)],
                avatarId: 15,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc LevelAbdo level grant " +
                $"{FormatSession(updatedContext)} source={LegacySocialCraftingServicesLevelSource} " +
                $"sourceLines={LegacySocialCraftingServicesLevelLines} " +
                $"level={session.Character.Level.ToString(CultureInfo.InvariantCulture)}->" +
                $"{updatedCharacter.Level.ToString(CultureInfo.InvariantCulture)} " +
                $"experience={updatedCharacter.Experience.ToString(CultureInfo.InvariantCulture)} " +
                $"attributePointsAdded={attributePointsAdded.ToString(CultureInfo.InvariantCulture)} " +
                $"hitpoints={session.Character.MinHitPoints.ToString(CultureInfo.InvariantCulture)}->" +
                $"{updatedCharacter.MinHitPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"path={Path.GetFileName(persistedPath)} updateOrder=Level,Action92,StatsOrAtributes,Equipment,Hitpoints,Dialog.")
            .ConfigureAwait(false);
    }

    private static bool TryCreateLegacySocialCraftingServicesLevelCharacter(
        WorldPacketHandlerContext context,
        WorldSession session,
        out CharacterSnapshot updatedCharacter,
        out bool sendSourceStats,
        out long attributePointsAdded,
        out string reason)
    {
        var character = session.Character;
        updatedCharacter = character;
        sendSourceStats = false;
        attributePointsAdded = 0;
        reason = string.Empty;

        if (character.Level is < 0 or > ushort.MaxValue ||
            character.Experience < 0 ||
            character.AttributePoints is < 0 or > ushort.MaxValue ||
            character.MinHitPoints is < 0 or > uint.MaxValue ||
            character.Class is < 0 or > uint.MaxValue ||
            character.Reborn is < 0 or > byte.MaxValue ||
            character.Uid is < 0 or > uint.MaxValue)
        {
            reason = "characterScalarOutsideSourceRange";
            return false;
        }

        if (character.Strength is < 0 or > ushort.MaxValue ||
            character.Agility is < 0 or > ushort.MaxValue ||
            character.Vitality is < 0 or > ushort.MaxValue ||
            character.Spirit is < 0 or > ushort.MaxValue)
        {
            reason = "characterStatOutsideSourceRange";
            return false;
        }

        if (character.Level < LegacySocialCraftingServicesLevelTarget)
        {
            if (character.Reborn == 0)
            {
                var sourceAutoStatMaxLevel = IsLegacySocialCraftingServicesWaterClass(character.Class)
                    ? 110L
                    : 120L;
                // UpdateLevel assigns 140 before GetStatus, so this branch re-sends the existing stats unchanged.
                if (character.Level < sourceAutoStatMaxLevel)
                {
                    sendSourceStats = true;
                }
                else
                {
                    attributePointsAdded = checked(
                        (LegacySocialCraftingServicesLevelTarget - character.Level) * 3);
                }
            }
            else
            {
                attributePointsAdded = checked(
                    (LegacySocialCraftingServicesLevelTarget - character.Level) * 3);
            }
        }

        var updatedAttributePoints = checked(character.AttributePoints + attributePointsAdded);
        if (updatedAttributePoints > ushort.MaxValue)
        {
            reason = "attributePointsOutsideSourceRange";
            return false;
        }

        var projected = WithLegacySocialCraftingServicesLevelHitpoints(
            character with
            {
                Level = LegacySocialCraftingServicesLevelTarget,
                AttributePoints = updatedAttributePoints,
            },
            character.MinHitPoints);
        var maxHitpoints = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
            projected,
            currentHitpoints: 0,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            context.WorldData,
            session.AlternateEquipmentActive);
        updatedCharacter = WithLegacySocialCraftingServicesLevelHitpoints(projected, maxHitpoints);
        return true;
    }

    private static CharacterSnapshot WithLegacySocialCraftingServicesLevelHitpoints(
        CharacterSnapshot character,
        long hitpoints)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Level"] = character.Level.ToString(CultureInfo.InvariantCulture),
            ["Experience"] = character.Experience.ToString(CultureInfo.InvariantCulture),
            ["Strength"] = character.Strength.ToString(CultureInfo.InvariantCulture),
            ["Agility"] = character.Agility.ToString(CultureInfo.InvariantCulture),
            ["Vitaliti"] = character.Vitality.ToString(CultureInfo.InvariantCulture),
            ["Spirit"] = character.Spirit.ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = character.AttributePoints.ToString(CultureInfo.InvariantCulture),
            ["MinHitPoints"] = hitpoints.ToString(CultureInfo.InvariantCulture),
        };
        return character with
        {
            MinHitPoints = hitpoints,
            Attributes = attributes,
        };
    }

    private static IReadOnlyDictionary<string, string> CreateLegacySocialCraftingServicesLevelPersistenceValues(
        CharacterSnapshot character)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Level"] = character.Level.ToString(CultureInfo.InvariantCulture),
            ["Experience"] = character.Experience.ToString(CultureInfo.InvariantCulture),
            ["Strength"] = character.Strength.ToString(CultureInfo.InvariantCulture),
            ["Agility"] = character.Agility.ToString(CultureInfo.InvariantCulture),
            ["Vitaliti"] = character.Vitality.ToString(CultureInfo.InvariantCulture),
            ["Spirit"] = character.Spirit.ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = character.AttributePoints.ToString(CultureInfo.InvariantCulture),
            ["MinHitPoints"] = character.MinHitPoints.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static bool IsLegacySocialCraftingServicesWaterClass(long classCode)
    {
        return classCode is >= 13_002 and <= 13_051;
    }
}
