using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyGuild6PoleWarConquerPointsReward = 150_000;
    private const long LegacyGuild6PoleWarMoneyReward = 150_000;
    private const uint LegacyGuild6PoleWarCelestialRainbowTitleId = 8_049;
    private const uint LegacyGuild6PoleWarRadiantWingsTitleId = 6_002;
    private const uint LegacyGuild6PoleWarScholarTitleId = 2_027;
    private const uint LegacyGuild6PoleWarSlideCrossServerChatType = 2_401;

    private static readonly uint[] LegacyGuild6PoleWarSpecialTitleRewards =
    [
        LegacyGuild6PoleWarCelestialRainbowTitleId,
        LegacyGuild6PoleWarRadiantWingsTitleId,
        LegacyGuild6PoleWarScholarTitleId,
    ];

    private static async ValueTask TryClaimLegacyGuild6PoleWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyGuild6PoleWarSchedule schedule,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (IsLegacyGuild6PoleWarProcessAlive(session.Character, schedule.ScheduleName))
        {
            await SendLegacyGuild6PoleWarRewardDialogAndLogAsync(
                    context,
                    schedule,
                    optionId,
                    "Please come back when GuildPoleWar is finished.",
                    "skipped active process",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(
                session.Character.Attributes,
                GetLegacyGuild6PoleWarScheduleAttributeName(schedule.ScheduleName, "RewardClaimed"),
                GetLegacyGuild6PoleWarScheduleAttributeName("Legacy" + schedule.ScheduleName, "RewardClaimed"),
                GetLegacyGuild6PoleWarScheduleAttributeName("Runtime" + schedule.ScheduleName, "RewardClaimed"),
                $"GuildPole{schedule.PoleNumber.ToString(CultureInfo.InvariantCulture)}WarRewardClaimed"))
        {
            await SendLegacyGuild6PoleWarRewardDialogAndLogAsync(
                    context,
                    schedule,
                    optionId,
                    $"This GuildPole[{schedule.PoleNumber.ToString(CultureInfo.InvariantCulture)}]War reward was already claimed.",
                    "already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyGuild6PoleWarWinnerGuildId(session.Character, schedule.ScheduleName, out var winnerGuildId))
        {
            await SendLegacyGuild6PoleWarRewardDialogAndLogAsync(
                    context,
                    schedule,
                    optionId,
                    "There is no reward available now.",
                    "skipped missing round owner",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var playerGuildId = ReadAttributeUInt64(
            session.Character.Attributes,
            "GuildID",
            "GuildId",
            "MyGuildID",
            "MyGuildId",
            "GuildUID",
            "GuildUid");
        if (playerGuildId != winnerGuildId)
        {
            await SendLegacyGuild6PoleWarRewardDialogAndLogAsync(
                    context,
                    schedule,
                    optionId,
                    "You do not have any reward to claim.",
                    $"skipped wrong guild winnerGuildId={winnerGuildId.ToString(CultureInfo.InvariantCulture)} playerGuildId={playerGuildId.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!IsLegacyGuildLeader(session.Character))
        {
            await SendLegacyGuild6PoleWarRewardDialogAndLogAsync(
                    context,
                    schedule,
                    optionId,
                    "Only the guild leader can claim the reward.",
                    "skipped non-guild-leader rank",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(session.Character.ConquerPoints + LegacyGuild6PoleWarConquerPointsReward);
        var updatedMoney = checked(session.Character.Money + LegacyGuild6PoleWarMoneyReward);
        var updatedCharacter = session.Character with
        {
            ConquerPoints = updatedConquerPoints,
            Money = updatedMoney,
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            CheckedUInt64(updatedConquerPoints, nameof(updatedConquerPoints)));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "Money",
            CheckedUInt64(updatedMoney, nameof(updatedMoney)));
        updatedCharacter = WithLegacyGuild6PoleWarRewardTitles(updatedCharacter);
        updatedCharacter = WithLegacyGuild6PoleWarRewardClaimCleared(updatedCharacter, schedule.ScheduleName, schedule.PoleNumber);

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacyGuild6PoleWarRewardAsync(updatedContext, updatedSession, schedule)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedCharacter.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        await SendWalletMoneyUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        foreach (var titleId in LegacyGuild6PoleWarSpecialTitleRewards)
        {
            await context.Connection.SendAsync(
                    LegacySpecialTitlePacket.FromTitleId(titleId).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyGuild6PoleWarSourceMethod,
                [$"You claimed {LegacyGuild6PoleWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs and {LegacyGuild6PoleWarMoneyReward.ToString(CultureInfo.InvariantCulture)} silver for winning GuildPole[{schedule.PoleNumber.ToString(CultureInfo.InvariantCulture)}]War."],
                LegacyPoleWarEventOkayOptions,
                LegacyGuild6PoleWarAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyGuild6PoleWarRewardBroadcastAsync(updatedContext, updatedCharacter, schedule, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc Guild6PoleWar reward " +
                $"{FormatSession(updatedContext)} source={LegacyGuild6PoleWarSourceMethod} option={optionId} " +
                $"schedule={schedule.ScheduleName} pole={schedule.PoleNumber.ToString(CultureInfo.InvariantCulture)} " +
                $"winnerGuildId={winnerGuildId.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPoints={LegacyGuild6PoleWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"money={LegacyGuild6PoleWarMoneyReward.ToString(CultureInfo.InvariantCulture)} " +
                "specialTitles=8049,6002,2027 " +
                "sourceReward=NewSystem.PrizeAllTops.Guild6PoleWar " +
                "sourceMoney=NewSystem.PrizeAllTops.Guild6PoleWarmo " +
                "sourceBroadcast=Center+SlideCrosTheServer.")
            .ConfigureAwait(false);
    }

}
