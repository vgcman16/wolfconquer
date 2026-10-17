using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyAdminLevel140Target = 140;
    private const MsgActionType LegacyAdminLevel140Action = (MsgActionType)92;
    private const string LegacyAdminStuffLevel140SourceLines = "11183-11190";
    private const string LegacyAdminStuff12Level140SourceLines = "10694-10704";
    private const string LegacyAdminStuff12MaxProficiencySourceLines = "10667-10676";
    private const string LegacyAdminLevel140DisabledText = "Level 140 admin service is disabled.";
    private const string LegacyAdminMaxProficiencyDisabledText = "Max weapon proficiency admin service is disabled.";
    private const string LegacyAdminStuffAlreadyLevel140Text = "Sorry, your level is already 140.";
    private const string LegacyAdminStuff12LevelRejectedText = "You do not have enough Conquer Points.";

    private static async ValueTask TryHandleLegacyAdminLevel140Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var sourceLines = optionId == LegacyAdminStuffLevel140Option
            ? LegacyAdminStuffLevel140SourceLines
            : LegacyAdminStuff12Level140SourceLines;

        if (optionId == LegacyAdminStuffLevel140Option &&
            session.Character.Level == LegacyAdminLevel140Target)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(LegacyAdminStuffAlreadyLevel140Text).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AdminUtility level grant " +
                    $"{FormatSession(context)} source={sourceMethod} sourceLines={sourceLines} " +
                    $"option={optionId} reason=alreadyLevel140.")
                .ConfigureAwait(false);
            return;
        }

        if (optionId == LegacyAdminStuff12Level140Option &&
            session.Character.Level >= LegacyAdminLevel140Target)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [LegacyAdminStuff12LevelRejectedText],
                    [new("Oh!", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AdminUtility level grant " +
                    $"{FormatSession(context)} source={sourceMethod} sourceLines={sourceLines} " +
                    $"option={optionId} reason=levelNotBelow140.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions ||
            context.WorldData is null ||
            string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await SendLegacyAdminUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    LegacyAdminLevel140DisabledText,
                    $"sourceLines={sourceLines}; UpdateLevel(140) requires Sessions, WorldData, and UsersRoot for a persistence-first commit.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryCreateLegacyAdminLevel140Character(
                context,
                session,
                out var updatedCharacter,
                out var sendSourceStats,
                out var attributePointsAdded,
                out var projectionReason))
        {
            await SendLegacyAdminUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    LegacyAdminLevel140DisabledText,
                    $"sourceLines={sourceLines}; UpdateLevel(140) projection unavailable reason={projectionReason}.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                updatedCharacter.Uid,
                CreateLegacyAdminLevel140PersistenceValues(updatedCharacter),
                out var persistedPath,
                out var persistenceReason))
        {
            await SendLegacyAdminUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    LegacyAdminLevel140DisabledText,
                    $"sourceLines={sourceLines}; UpdateLevel(140) persistence failed reason={persistenceReason}.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { Character = updatedCharacter };
        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendLegacyAdminLevel140UpdatesAsync(
                updatedContext,
                session,
                updatedSession,
                sendSourceStats,
                attributePointsAdded,
                cancellationToken)
            .ConfigureAwait(false);

        if (optionId == LegacyAdminStuff12Level140Option)
        {
            await SendLegacyNpcDialogAsync(
                    updatedContext,
                    sourceMethod,
                    texts: [],
                    [new("Thanks!", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AdminUtility level grant " +
                $"{FormatSession(updatedContext)} source={sourceMethod} sourceLines={sourceLines} " +
                $"option={optionId} level={session.Character.Level.ToString(CultureInfo.InvariantCulture)}->" +
                $"{updatedCharacter.Level.ToString(CultureInfo.InvariantCulture)} " +
                $"experience={updatedCharacter.Experience.ToString(CultureInfo.InvariantCulture)} " +
                $"attributePointsAdded={attributePointsAdded.ToString(CultureInfo.InvariantCulture)} " +
                $"hitpoints={session.Character.MinHitPoints.ToString(CultureInfo.InvariantCulture)}->" +
                $"{updatedCharacter.MinHitPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"path={Path.GetFileName(persistedPath)} " +
                "updateOrder=Level,Action92,StatsOrAtributes,Equipment,Hitpoints,SourceDialog.")
            .ConfigureAwait(false);
    }

    private static bool TryCreateLegacyAdminLevel140Character(
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

        if (character.Level < LegacyAdminLevel140Target)
        {
            if (character.Reborn == 0)
            {
                var sourceAutoStatMaxLevel = IsLegacyAdminWaterClass(character.Class) ? 110L : 120L;
                // UpdateLevel assigns 140 before GetStatus, so this source branch only re-sends existing stats.
                if (character.Level < sourceAutoStatMaxLevel)
                {
                    sendSourceStats = true;
                }
                else
                {
                    attributePointsAdded = checked((LegacyAdminLevel140Target - character.Level) * 3);
                }
            }
            else
            {
                attributePointsAdded = checked((LegacyAdminLevel140Target - character.Level) * 3);
            }
        }

        var updatedAttributePoints = character.AttributePoints + attributePointsAdded;
        if (updatedAttributePoints > ushort.MaxValue)
        {
            reason = "attributePointsOutsideSourceRange";
            return false;
        }

        var projected = WithLegacyAdminLevel140Hitpoints(
            character with
            {
                Level = LegacyAdminLevel140Target,
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
        updatedCharacter = WithLegacyAdminLevel140Hitpoints(projected, maxHitpoints);
        return true;
    }

    private static async ValueTask SendLegacyAdminLevel140UpdatesAsync(
        WorldPacketHandlerContext context,
        WorldSession originalSession,
        WorldSession updatedSession,
        bool sendSourceStats,
        long attributePointsAdded,
        CancellationToken cancellationToken)
    {
        var updatedCharacter = updatedSession.Character;
        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));

        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket
                    .CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)LegacyAdminLevel140Target))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var levelAction = new MsgActionPacket
        {
            ObjId = uid,
            Type = LegacyAdminLevel140Action,
            PositionX = checked((uint)LegacyAdminLevel140Target),
        };
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
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

        var preHealHitpoints = Math.Min(originalSession.Character.MinHitPoints, updatedCharacter.MinHitPoints);
        var preHealSession = updatedSession with
        {
            Character = WithLegacyAdminLevel140Hitpoints(updatedCharacter, preHealHitpoints),
        };
        await SendLegacyEquipmentRuntimeRefreshAsync(context, preHealSession, cancellationToken)
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
    }

    private static CharacterSnapshot WithLegacyAdminLevel140Hitpoints(
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

    private static IReadOnlyDictionary<string, string> CreateLegacyAdminLevel140PersistenceValues(
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

    private static bool IsLegacyAdminWaterClass(long classCode)
    {
        return classCode is >= 13_002 and <= 13_051;
    }

    private static async ValueTask TryHandleLegacyAdminMaxWeaponProficiencyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions ||
            string.IsNullOrWhiteSpace(context.PlayerProficienciesRoot))
        {
            await SendLegacyAdminUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    LegacyAdminMaxProficiencyDisabledText,
                    $"sourceLines={LegacyAdminStuff12MaxProficiencySourceLines}; max proficiency requires Sessions and PlayerProficienciesRoot for an atomic persistence-first commit.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var changedCount = 0;
        var updatedEntries = session.PlayerProficiencies.Proficiencies
            .Select(proficiency =>
            {
                if (proficiency.Level != 20 || proficiency.Experience != 0)
                {
                    changedCount++;
                }

                return proficiency with
                {
                    Level = 20,
                    Experience = 0,
                };
            })
            .ToArray();
        var updatedProficiencies = new PlayerProficiencySnapshot(updatedEntries);

        try
        {
            new PlayerProficiencySnapshotLoader(context.PlayerProficienciesRoot)
                .SaveByCharacterUid(session.Character.Uid, updatedProficiencies);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await SendLegacyAdminUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    LegacyAdminMaxProficiencyDisabledText,
                    $"sourceLines={LegacyAdminStuff12MaxProficiencySourceLines}; max proficiency persistence failed reason={exception.GetType().Name}.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { PlayerProficiencies = updatedProficiencies };
        sessions.Set(context.Connection, updatedSession);
        await context.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AdminUtility max weapon proficiency " +
                $"{FormatSession(context with { Session = updatedSession })} source={sourceMethod} " +
                $"sourceLines={LegacyAdminStuff12MaxProficiencySourceLines} option={optionId} " +
                $"proficiencyCount={updatedEntries.Length.ToString(CultureInfo.InvariantCulture)} " +
                $"changedCount={changedCount.ToString(CultureInfo.InvariantCulture)} " +
                "level=20 experience=0 output=disconnect-only.")
            .ConfigureAwait(false);
    }
}
