using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacySuperGuildWarSourceConquerPointsReward = 300_000;
    private const long LegacySuperGuildWarSourceMoneyReward = 50_000_000;
    private const long TunedLegacySuperGuildWarMoneyReward = 500_000;
    private const uint LegacySuperGuildWarLeaderRewardItemId = 2_100_065;
    private const int LegacySuperGuildWarFiveStarsFlag = 151;
    private const int LegacySuperGuildWarThreeStarsFlag = 152;
    private const string LegacySuperGuildWarRewardClaimedAttribute = "LegacySuperGuildWarRewardClaimed";
    private const string LegacySuperGuildWarLeaderRewardClaimedAttribute = "LegacySuperGuildWarLeaderRewardClaimed";
    private const string LegacySuperGuildWarDeputyRewardClaimedAttribute = "LegacySuperGuildWarDeputyRewardClaimed";

    private static async ValueTask TryClaimLegacySuperGuildWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        if (IsLegacySuperGuildWarProcessAlive(session.Character))
        {
            await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "The tournament is still active. Please come back when Super Guild War is finished.",
                    "skipped active MsgSchedules.SuperGuildWar.Proces",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var rewardData = ResolveLegacySuperGuildWarRewardData(context, session.Character);
        var playerGuildId = ReadAttributeUInt64(
            session.Character.Attributes,
            "GuildID",
            "GuildId",
            "MyGuildID",
            "MyGuildId",
            "GuildUID",
            "GuildUid");
        var guildRank = ReadAttributeUInt64(
            session.Character.Attributes,
            "GuildRank",
            "GuildMemberRank",
            "MyGuildRank",
            "MyGuildMemberRank",
            "LegacyGuildRank");

        if (!LegacySuperGuildWarStateStore.TryGetWinnerGuildId(rewardData, out var winnerGuildId))
        {
            await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "The tournament is offline at the moment.",
                    "skipped missing winner guild",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!LegacySuperGuildWarStateStore.IsWinnerGuildMember(playerGuildId, winnerGuildId))
        {
            await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "You do not have any Super Guild War reward to claim.",
                    $"skipped wrong guild winnerGuildId={winnerGuildId.ToString(CultureInfo.InvariantCulture)} playerGuildId={playerGuildId.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (IsLegacySuperGuildWarLeaderRank(session.Character, guildRank))
        {
            if (ReadAttributeBool(
                    session.Character.Attributes,
                    LegacySuperGuildWarLeaderRewardClaimedAttribute,
                    "SuperGuildWarLeaderRewardClaimed"))
            {
                await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                        context,
                        optionId,
                        sourceMethod,
                        "This Super Guild War reward was already claimed.",
                        "already claimed by persisted leader attribute",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (!HasLegacyInventorySpace(session.PlayerItems, 2))
            {
                await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                        context,
                        optionId,
                        sourceMethod,
                        "Please make one more space in your inventory.",
                        "skipped full inventory sourceRequiredSpace=2",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }
        else if (IsLegacySuperGuildWarDeputyRank(session.Character, guildRank))
        {
            if (ReadAttributeBool(
                    session.Character.Attributes,
                    LegacySuperGuildWarDeputyRewardClaimedAttribute,
                    "SuperGuildWarDeputyRewardClaimed"))
            {
                await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                        context,
                        optionId,
                        sourceMethod,
                        "This Super Guild War deputy reward was already claimed.",
                        "already claimed by persisted deputy attribute",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (!HasLegacyInventorySpace(session.PlayerItems, 4))
            {
                await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                        context,
                        optionId,
                        sourceMethod,
                        "Please make 4 more spaces in your inventory.",
                        "skipped full inventory sourceRequiredSpace=4",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }
        else
        {
            await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                    context,
                    optionId,
                    sourceMethod,
                    "Only the Guild Leader or Deputy Leader can claim this reward.",
                    $"skipped unsupported guildRank={guildRank.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        LegacySuperGuildWarPreparedReward? prepared = null;
        var preparedKind = ResolveLegacySuperGuildWarPreparedKind(guildRank);
        if (preparedKind != LegacySuperGuildWarRewardKind.None)
        {
            try
            {
                var leaderItem = preparedKind == LegacySuperGuildWarRewardKind.Leader
                    ? CreateLegacyNpcItemGrant(context, session.PlayerItems, LegacySuperGuildWarLeaderRewardItemId)
                    : null;
                prepared = PrepareLegacySuperGuildWarReward(context, session, preparedKind, leaderItem);
            }
            catch (Exception error) when (IsLegacySuperGuildWarPreparationFailure(error))
            {
                await LogLegacySuperGuildWarPreparationRefusalAsync(context, sourceMethod, error).ConfigureAwait(false);
                return;
            }
        }

        var claim = LegacySuperGuildWarStates.TryClaimReward(
            checked((uint)session.Character.Uid),
            playerGuildId,
            guildRank,
            rewardData);

        switch (claim.Status)
        {
            case LegacySuperGuildWarRewardClaimStatus.Granted
                when claim.Kind == LegacySuperGuildWarRewardKind.Leader:
                await GrantLegacySuperGuildWarLeaderRewardAsync(
                        context,
                        optionId,
                        sourceMethod,
                        claim,
                        prepared!,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case LegacySuperGuildWarRewardClaimStatus.Granted
                when claim.Kind == LegacySuperGuildWarRewardKind.DeputyLeader:
                await GrantLegacySuperGuildWarDeputyRewardAsync(
                        context,
                        optionId,
                        sourceMethod,
                        claim,
                        prepared!,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case LegacySuperGuildWarRewardClaimStatus.MissingWinner:
                await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                        context,
                        optionId,
                        sourceMethod,
                        "The tournament is offline at the moment.",
                        "skipped missing winner guild",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case LegacySuperGuildWarRewardClaimStatus.UnsupportedRank:
                await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                        context,
                        optionId,
                        sourceMethod,
                        "Only the Guild Leader or Deputy Leader can claim this reward.",
                        $"skipped unsupported guildRank={guildRank.ToString(CultureInfo.InvariantCulture)}",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case LegacySuperGuildWarRewardClaimStatus.AlreadyClaimed:
                await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                        context,
                        optionId,
                        sourceMethod,
                        "This Super Guild War reward was already claimed.",
                        $"already claimed kind={claim.Kind}",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case LegacySuperGuildWarRewardClaimStatus.Exhausted:
                await SendLegacySuperGuildWarRewardDialogAndLogAsync(
                        context,
                        optionId,
                        sourceMethod,
                        "There are no Super Guild War rewards left for your rank.",
                        $"reward exhausted kind={claim.Kind}",
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
        }
    }

    private static async ValueTask GrantLegacySuperGuildWarLeaderRewardAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string sourceMethod,
        LegacySuperGuildWarRewardClaim claim,
        LegacySuperGuildWarPreparedReward prepared,
        CancellationToken cancellationToken)
    {
        var updatedSession = prepared.Session;
        var updatedContext = prepared.Context;
        context.Sessions?.Set(context.Connection, updatedSession);
        await PersistLegacySuperGuildWarPreparedRewardAsync(prepared, sourceMethod).ConfigureAwait(false);
        await PersistLegacySuperGuildWarPreparedItemsAsync(prepared, sourceMethod).ConfigureAwait(false);
        await PublishLegacySuperGuildWarClaimFramesAsync(prepared, sourceMethod, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc SuperGuildWar reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} kind={claim.Kind} " +
                $"sourceConquerPoints={LegacySuperGuildWarSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceMoney={LegacySuperGuildWarSourceMoneyReward.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedMoney={TunedLegacySuperGuildWarMoneyReward.ToString(CultureInfo.InvariantCulture)} " +
                $"itemId={LegacySuperGuildWarLeaderRewardItemId.ToString(CultureInfo.InvariantCulture)} " +
                $"statusFlag={LegacySuperGuildWarFiveStarsFlag.ToString(CultureInfo.InvariantCulture)} " +
                $"remainingAfterClaim={claim.RemainingAfterClaim.ToString(CultureInfo.InvariantCulture)} " +
                "sourceReward=NewSystem.PrizeAllTops.SuperGuildWar sourceItem=2100065 sourceFlag=TopSuperGuildWarFiveStars.")
            .ConfigureAwait(false);
    }

    private static async ValueTask GrantLegacySuperGuildWarDeputyRewardAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string sourceMethod,
        LegacySuperGuildWarRewardClaim claim,
        LegacySuperGuildWarPreparedReward prepared,
        CancellationToken cancellationToken)
    {
        var updatedSession = prepared.Session;
        var updatedContext = prepared.Context;
        context.Sessions?.Set(context.Connection, updatedSession);
        await PersistLegacySuperGuildWarPreparedRewardAsync(prepared, sourceMethod).ConfigureAwait(false);
        await PublishLegacySuperGuildWarClaimFramesAsync(prepared, sourceMethod, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc SuperGuildWar reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} kind={claim.Kind} " +
                $"statusFlag={LegacySuperGuildWarThreeStarsFlag.ToString(CultureInfo.InvariantCulture)} " +
                $"remainingAfterClaim={claim.RemainingAfterClaim.ToString(CultureInfo.InvariantCulture)} " +
                "sourceFlag=TopSuperGuildWarThreeStars.")
            .ConfigureAwait(false);
    }

    private static GuildWarData? ResolveLegacySuperGuildWarRewardData(
        WorldPacketHandlerContext context,
        CharacterSnapshot character)
    {
        var catalogData = context.WorldData?.SuperGuildWarInfo;
        if (!TryReadUInt64(
                character.Attributes,
                out var winnerGuildId,
                GetLegacySuperGuildWarAttributeNames(
                    "WinnerGuildID",
                    "WinnerGuildId",
                    "RoundOwnerGuildID",
                    "RoundOwnerGuildId")))
        {
            return catalogData;
        }

        var winnerName = ReadAttributeString(
            character.Attributes,
            GetLegacySuperGuildWarAttributeNames("WinnerGuildName", "RoundOwnerGuildName", "PoleOwnerName"));
        if (string.IsNullOrWhiteSpace(winnerName))
        {
            winnerName = "SuperGuildWarWinner";
        }

        var leaderReward = ReadAttributeUInt32(
            character.Attributes,
            "LegacySuperGuildWarLeaderReward",
            "SuperGuildWarLeaderReward",
            "RuntimeSuperGuildWarLeaderReward");
        var deputyReward = ReadAttributeUInt32(
            character.Attributes,
            "LegacySuperGuildWarDeputyReward",
            "LegacySuperGuildWarDeputiLeaderReward",
            "SuperGuildWarDeputyReward",
            "SuperGuildWarDeputiLeaderReward",
            "RuntimeSuperGuildWarDeputyReward");

        return new GuildWarData(
            sourceFile: "runtime://MsgSuperGuildWar.FinishRound",
            sections: [],
            entries: [],
            diagnostics: [],
            info: new GuildWarInfoData(
                checked((int)Math.Min(winnerGuildId, int.MaxValue)),
                winnerName,
                leaderReward == 0 ? 1 : checked((int)leaderReward),
                deputyReward == 0 ? 7 : checked((int)deputyReward),
                new Dictionary<int, int>(),
                new Dictionary<int, int>()),
            pole: null,
            conductors: []);
    }

    private static bool IsLegacySuperGuildWarLeaderRank(CharacterSnapshot character, ulong guildRank)
    {
        var raw = ReadAttributeString(
                character.Attributes,
                "GuildRank",
                "GuildMemberRank",
                "MyGuildRank",
                "MyGuildMemberRank",
                "LegacyGuildRank")
            .Trim();
        return raw.Equals("GuildLeader", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("Leader", StringComparison.OrdinalIgnoreCase) ||
            guildRank is 1 or 100;
    }

    private static bool IsLegacySuperGuildWarDeputyRank(CharacterSnapshot character, ulong guildRank)
    {
        var raw = ReadAttributeString(
                character.Attributes,
                "GuildRank",
                "GuildMemberRank",
                "MyGuildRank",
                "MyGuildMemberRank",
                "LegacyGuildRank")
            .Trim();
        return raw.Equals("DeputyLeader", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("DeputiLeader", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("Deputy", StringComparison.OrdinalIgnoreCase) ||
            guildRank is 2 or 90;
    }

    private static CharacterSnapshot WithLegacySuperGuildWarRewardClaimed(
        CharacterSnapshot character,
        LegacySuperGuildWarRewardKind kind)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [LegacySuperGuildWarRewardClaimedAttribute] = "true",
            ["SuperGuildWarRewardClaimed"] = "true",
            ["LegacySuperGuildWarRewardKind"] = kind.ToString(),
            ["SuperGuildWarRewardKind"] = kind.ToString(),
        };

        if (kind == LegacySuperGuildWarRewardKind.Leader)
        {
            attributes[LegacySuperGuildWarLeaderRewardClaimedAttribute] = "true";
            attributes["SuperGuildWarLeaderRewardClaimed"] = "true";
        }
        else if (kind == LegacySuperGuildWarRewardKind.DeputyLeader)
        {
            attributes[LegacySuperGuildWarDeputyRewardClaimedAttribute] = "true";
            attributes["SuperGuildWarDeputyRewardClaimed"] = "true";
        }

        return character with { Attributes = attributes };
    }

    private static async ValueTask SendLegacySuperGuildWarRewardDialogAndLogAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string sourceMethod,
        string text,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [text],
                LegacyCompetitionCloseOptions,
                LegacyCaptureTheFlagRewardAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc SuperGuildWar reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

}
