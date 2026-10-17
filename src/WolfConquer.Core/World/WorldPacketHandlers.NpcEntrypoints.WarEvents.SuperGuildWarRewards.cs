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
                        session,
                        optionId,
                        sourceMethod,
                        claim,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case LegacySuperGuildWarRewardClaimStatus.Granted
                when claim.Kind == LegacySuperGuildWarRewardKind.DeputyLeader:
                await GrantLegacySuperGuildWarDeputyRewardAsync(
                        context,
                        session,
                        optionId,
                        sourceMethod,
                        claim,
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
        WorldSession session,
        byte optionId,
        string sourceMethod,
        LegacySuperGuildWarRewardClaim claim,
        CancellationToken cancellationToken)
    {
        var grantedItem = CreateLegacyNpcItemGrant(
            context,
            session.PlayerItems,
            LegacySuperGuildWarLeaderRewardItemId);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([grantedItem]).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedConquerPoints = checked(session.Character.ConquerPoints + LegacySuperGuildWarSourceConquerPointsReward);
        var updatedMoney = checked(session.Character.Money + TunedLegacySuperGuildWarMoneyReward);
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
        updatedCharacter = WithLegacyStatusFlag(updatedCharacter, LegacySuperGuildWarFiveStarsFlag);
        updatedCharacter = WithLegacySuperGuildWarRewardClaimed(updatedCharacter, claim.Kind);

        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacySuperGuildWarRewardAsync(updatedContext, updatedSession, sourceMethod, claim.Kind)
            .ConfigureAwait(false);
        await PersistLegacyNpcItemsAsync(
                updatedContext,
                LegacySuperGuildWarLeaderRewardItemId,
                sourceMethod)
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
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacySuperGuildWarStatusFlagsAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {LegacySuperGuildWarSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs, {TunedLegacySuperGuildWarMoneyReward.ToString(CultureInfo.InvariantCulture)} silver, and item {LegacySuperGuildWarLeaderRewardItemId.ToString(CultureInfo.InvariantCulture)} for winning Super Guild War."],
                LegacyCompetitionCloseOptions,
                LegacyCaptureTheFlagRewardAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacySuperGuildWarRewardBroadcastAsync(updatedContext, updatedCharacter, claim.Kind, cancellationToken)
            .ConfigureAwait(false);
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
        WorldSession session,
        byte optionId,
        string sourceMethod,
        LegacySuperGuildWarRewardClaim claim,
        CancellationToken cancellationToken)
    {
        var updatedCharacter = WithLegacyStatusFlag(session.Character, LegacySuperGuildWarThreeStarsFlag);
        updatedCharacter = WithLegacySuperGuildWarRewardClaimed(updatedCharacter, claim.Kind);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacySuperGuildWarRewardAsync(updatedContext, updatedSession, sourceMethod, claim.Kind)
            .ConfigureAwait(false);
        await SendLegacySuperGuildWarStatusFlagsAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                ["You claimed the Deputy Leader Super Guild War title flag."],
                LegacyCompetitionCloseOptions,
                LegacyCaptureTheFlagRewardAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacySuperGuildWarRewardBroadcastAsync(updatedContext, updatedCharacter, claim.Kind, cancellationToken)
            .ConfigureAwait(false);
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

    private static async ValueTask PersistLegacySuperGuildWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        LegacySuperGuildWarRewardKind kind)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["Money"] = session.Character.Money.ToString(CultureInfo.InvariantCulture),
            ["StatusFlags"] = ReadAttributeString(session.Character.Attributes, "StatusFlags"),
            ["BitVector"] = ReadAttributeString(session.Character.Attributes, "BitVector"),
            [LegacySuperGuildWarRewardClaimedAttribute] = "true",
            ["SuperGuildWarRewardClaimed"] = "true",
            ["LegacySuperGuildWarRewardKind"] = kind.ToString(),
            ["SuperGuildWarRewardKind"] = kind.ToString(),
        };

        if (kind == LegacySuperGuildWarRewardKind.Leader)
        {
            values[LegacySuperGuildWarLeaderRewardClaimedAttribute] = "true";
            values["SuperGuildWarLeaderRewardClaimed"] = "true";
        }
        else if (kind == LegacySuperGuildWarRewardKind.DeputyLeader)
        {
            values[LegacySuperGuildWarDeputyRewardClaimedAttribute] = "true";
            values["SuperGuildWarDeputyRewardClaimed"] = "true";
        }

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc SuperGuildWar reward persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc SuperGuildWar reward " +
                $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static ValueTask SendLegacySuperGuildWarStatusFlagsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken)
    {
        return context.Connection.SendAsync(
            LegacyMsgUpdatePacket
                .CreateLegacyRuntimeStatusFlagVectorUpdate(
                    CheckedUInt32(character.Uid, nameof(character.Uid)),
                    CreateLegacyRuntimeStatusFlagVector(character))
                .ToFrameBytes(),
            cancellationToken);
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

    private static async ValueTask SendLegacySuperGuildWarRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        LegacySuperGuildWarRewardKind kind,
        CancellationToken cancellationToken)
    {
        var role = kind == LegacySuperGuildWarRewardKind.Leader
            ? "GuildLeader"
            : "DeputyLeader";
        var guildName = ReadAttributeString(winner.Attributes, "GuildName", "LegacyGuildName", "SuperGuildWarWinnerGuildName");
        if (string.IsNullOrWhiteSpace(guildName))
        {
            guildName = "the winning guild";
        }

        var message = kind == LegacySuperGuildWarRewardKind.Leader
            ? $"{winner.Name} , {role} from {guildName} was rewarded with {LegacySuperGuildWarSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} Cps for the domination the SuperGuildWar."
            : $"{winner.Name} , {role} from {guildName} claimed the SuperGuildWar title reward.";
        var frame = new LegacyMsgMessagePacket(
                LegacyMsgMessagePacket.RedColor,
                ChatType: 2_010,
                MessageUid1: 0,
                MessageUid2: 0,
                ServerId: 0,
                Mesh: 0,
                UnionType: 0,
                Count: 1,
                New1: 0,
                New2: 0,
                ["SYSTEM", "ALL", string.Empty, message, string.Empty, string.Empty, string.Empty])
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }
}
