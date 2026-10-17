using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyFlameFirstNpcId = 657;
    private const uint LegacyFlameLastNpcId = 666;
    private const uint LegacyFlameTaoistNpcId = 681;
    private const ushort LegacyFlameAvatarId = 67;
    private const uint LegacyFlameRewardRuneItemId = 729_970;
    private const double LegacyFlameTaoistRewardExpBallAmount = 7_200.0;

    private static readonly LegacyFlameStoneDefinition[] LegacyFlameStones =
    [
        new(657, "NpcHandler.Flame1", 729_960, 729_961, "(317,270) on the right of the Guild Arena."),
        new(658, "NpcHandler.Flame2", 729_961, 729_962, "(236,291) near the bridge in the Guild Arena."),
        new(659, "NpcHandler.Flame3", 729_962, 729_963, "(194,168) near the right gate of the Guild City."),
        new(660, "NpcHandler.Flame4", 729_963, 729_964, "(115,53) on the right of the Guild City."),
        new(661, "NpcHandler.Flame5", 729_964, 729_965, "(316,378) on the left of the Guild Arena."),
        new(662, "NpcHandler.Flame6", 729_965, 729_966, "(136,182) near the left gate of the Guild City."),
        new(663, "NpcHandler.Flame7", 729_966, 729_967, "(38,94) on the left of the Guild City."),
        new(664, "NpcHandler.Flame8", 729_967, 729_968, "(350,321) near the Flame Taoist."),
        new(665, "NpcHandler.Flame9", 729_968, 729_969, "(350,321) near the Flame Taoist."),
        new(666, "NpcHandler.Flame10", 0, 0, ""),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyFlameOkayOption =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    internal static async ValueTask<bool> TryHandleLegacyFlameWarEventNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId == LegacyFlameTaoistNpcId)
        {
            return await TryHandleLegacyFlameTaoistAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (npcId is < LegacyFlameFirstNpcId or > LegacyFlameLastNpcId)
        {
            return false;
        }

        var stone = LegacyFlameStones[(int)(npcId - LegacyFlameFirstNpcId)];
        return await TryHandleLegacyFlameStoneAsync(context, session, stone, optionId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyFlameStoneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyFlameStoneDefinition stone,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId != 0)
        {
            return false;
        }

        if (stone.NpcId == LegacyFlameLastNpcId)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer handled source-empty LegacyNpc Flame stone {FormatSession(context)} source={stone.SourceMethod} option=0.")
                .ConfigureAwait(false);
            return true;
        }

        if (context.GuildWarStates is not { } guildWar)
        {
            await SendLegacyFlameGapAsync(
                    context,
                    stone.SourceMethod,
                    optionId,
                    "source checks MsgSchedules.NEWGUILDWAR.Proces before rune mutation; modern guild-war process state is unavailable")
                .ConfigureAwait(false);
            await SendLegacyNpcDialogAsync(
                    context,
                    stone.SourceMethod,
                    ["It is not the right time. You cannot light this Flame Stone yet."],
                    LegacyFlameOkayOption,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!guildWar.IsActive)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("It's not the right time. You can't light up the Flame Stone.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyNpc Flame inactive guild-war message " +
                    $"{FormatSession(context)} source={stone.SourceMethod} option={optionId}.")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyFlameRune(session.PlayerItems, stone.RequiredItemId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    stone.SourceMethod,
                    [$"You cannot light this stone without the proper rune. Light the Flame Stone {stone.LocationText}"],
                    [],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await SendLegacyFlameGapAsync(
                    context,
                    stone.SourceMethod,
                    optionId,
                    "source removes one rune and grants the next rune; PlayerItemsRoot is missing, so inventory mutation is not persisted")
                .ConfigureAwait(false);
            await SendLegacyNpcDialogAsync(
                    context,
                    stone.SourceMethod,
                    ["The Flame Stone rune exchange is not available from this runtime yet."],
                    LegacyFlameOkayOption,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var requiredItem = session.PlayerItems.InventoryItems
            .Where(item => item.ItemId == stone.RequiredItemId)
            .OrderBy(item => item.Uid)
            .First();
        var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, requiredItem, out var consumeFrame);
        var grantedItem = CreateLegacyFlameRuneGrant(updatedItems, stone.RewardItemId);
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([grantedItem]).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken).ConfigureAwait(false);

        try
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            await SendLegacyFlameGapAsync(
                    updatedContext,
                    stone.SourceMethod,
                    optionId,
                    $"source rune mutation persistence failed; reason={ex.Message}")
                .ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                stone.SourceMethod,
                [$"Congratulations! You lit the Flame Stone {stone.LocationText}"],
                [],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc Flame rune exchange " +
                $"{FormatSession(updatedContext)} source={stone.SourceMethod} option={optionId} " +
                $"requiredItemId={stone.RequiredItemId.ToString(CultureInfo.InvariantCulture)} " +
                $"rewardItemId={stone.RewardItemId.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyFlameTaoistAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.FlameTaoist";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "You must have heard of the Olympic Games. I have been waiting for it my entire life. There are 10 Flame Stones I would like you to light.",
                            "You will get some rewards, of course. It really means something to us, or at least to me.",
                        ],
                        [
                            new("Rewards? Tell me more!", 1),
                            new("Can you give me a rune?", 4),
                            new("I have a rune. Give me the reward!", 5),
                            new("Not interested.", LegacyNpcClosedOption),
                        ],
                        LegacyFlameAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "Ah, you seem very interested! Good. You are eligible to light the flame, but it is not the right time.",
                            "The ceremony will be held during the last two hours of Guild War. Come and sign up then.",
                        ],
                        [new("Would you please give me more information?", 2)],
                        LegacyFlameAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "I prepared 10 Flame Stones in Wind Plain and the Guild Arena. When the time comes, I will give you a Jade Rune.",
                            "Take it to light all the Flame Stones in sequence.",
                        ],
                        [new("Sure.", LegacyNpcClosedOption)],
                        LegacyFlameAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
            case 4:
                await context.Log.WriteLineAsync(
                        $"WorldServer handled source-empty LegacyNpc FlameTaoist option {FormatSession(context)} source={sourceMethod} option={optionId}.")
                    .ConfigureAwait(false);
                return true;

            case 5:
                if (!TryGetLegacyFlameRune(session.PlayerItems, LegacyFlameRewardRuneItemId, out var rewardRune))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["You cannot claim experience if you do not have a rune."],
                            [new("Ahh", LegacyNpcClosedOption)],
                            LegacyFlameAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (context.GuildWarStates is not { } guildWar)
                {
                    await SendLegacyFlameTaoistRewardGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "local guild-war process state is unavailable",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (guildWar.IsActive)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["You cannot claim experience while guild war."],
                            [new("Ahh", LegacyNpcClosedOption)],
                            LegacyFlameAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
                {
                    await SendLegacyFlameTaoistRewardGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "PlayerItemsRoot is missing, so final rune removal is not persisted",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (string.IsNullOrWhiteSpace(session.UsersRoot))
                {
                    await SendLegacyFlameTaoistRewardGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "UsersRoot is missing, so experience reward persistence is not safe",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var modeledExperience = TryCalculateLegacyExpBallUse(
                    context.WorldData,
                    session.Character,
                    LegacyFlameTaoistRewardExpBallAmount,
                    out var updatedLevel,
                    out var updatedExperience);
                if (!modeledExperience && session.Character.Level < LegacyMaxPlayerLevel)
                {
                    await SendLegacyFlameTaoistRewardGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "level experience data is not loaded, so GainExpBall(7200) cannot be modeled",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, rewardRune!, out var consumeFrame);
                var updatedCharacter = modeledExperience
                    ? session.Character with
                    {
                        Level = updatedLevel,
                        Experience = updatedExperience,
                    }
                    : session.Character;
                var updatedSession = session with
                {
                    Character = updatedCharacter,
                    PlayerItems = updatedItems,
                };
                context.Sessions?.Set(context.Connection, updatedSession);
                var updatedContext = context with { Session = updatedSession };

                await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);

                var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
                if (modeledExperience)
                {
                    await context.Connection.SendAsync(
                            LegacyMsgMessagePacket
                                .SystemTalk("You have gained experience worth 12 exp ball(s).")
                                .ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Connection.SendAsync(
                            LegacyStringPacket.PlayerEffect(uid, "angelwing").ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);

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

                await PersistLegacyFlameTaoistRewardAsync(updatedContext, updatedSession).ConfigureAwait(false);

                var experienceEvidence = modeledExperience
                    ? $"experience={session.Character.Experience}->{updatedCharacter.Experience} level={session.Character.Level}->{updatedCharacter.Level}"
                    : "source GainExpBall returns without EXP mutation at level 140 or above";
                await context.Log.WriteLineAsync(
                        "WorldServer applied LegacyNpc FlameTaoist final rune reward " +
                        $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                        $"itemId={LegacyFlameRewardRuneItemId.ToString(CultureInfo.InvariantCulture)} " +
                        $"GainExpBall(7200) {experienceEvidence}.")
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static bool HasLegacyFlameRune(PlayerItemSnapshot items, uint itemId)
    {
        return items.InventoryItems.Any(item => item.ItemId == itemId && Math.Max(1, (int)item.StackSize) >= 1);
    }

    private static bool TryGetLegacyFlameRune(
        PlayerItemSnapshot items,
        uint itemId,
        out PlayerItemSnapshotItem? rune)
    {
        rune = items.InventoryItems
            .Where(item => item.ItemId == itemId && Math.Max(1, (int)item.StackSize) >= 1)
            .OrderBy(item => item.Uid)
            .FirstOrDefault();
        return rune is not null;
    }

    private static PlayerItemSnapshotItem CreateLegacyFlameRuneGrant(PlayerItemSnapshot items, uint itemId)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = itemId,
            Position = 0,
            StackSize = 1,
        };
    }

    private static async ValueTask SendLegacyFlameGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc FlameWarEvent " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyFlameTaoistRewardGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyFlameGapAsync(
                context,
                sourceMethod,
                optionId,
                "source removes item 729970 and grants GainExpBall(7200) when NEWGUILDWAR.Proces is Dead; " + reason)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["The Flame Taoist experience reward is not available from this runtime yet."],
                LegacyFlameOkayOption,
                LegacyFlameAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyFlameTaoistRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    session.Character.Uid,
                    session.PlayerItems);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await SendLegacyFlameGapAsync(
                        context,
                        "NpcHandler.FlameTaoist",
                        optionId: 5,
                        $"source final rune reward item persistence failed; reason={ex.Message}")
                    .ConfigureAwait(false);
            }
        }

        if (CharacterSnapshotScalarPersistence.TryPersist(
                session.UsersRoot,
                session.Character,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc FlameTaoist character reward " +
                    $"{FormatSession(context)} path={Path.GetFileName(path)}.")
                .ConfigureAwait(false);
        }
        else
        {
            await SendLegacyFlameGapAsync(
                    context,
                    "NpcHandler.FlameTaoist",
                    optionId: 5,
                    $"source final rune reward character persistence failed; reason={reason}")
                .ConfigureAwait(false);
        }
    }

    private readonly record struct LegacyFlameStoneDefinition(
        uint NpcId,
        string SourceMethod,
        uint RequiredItemId,
        uint RewardItemId,
        string LocationText);
}
