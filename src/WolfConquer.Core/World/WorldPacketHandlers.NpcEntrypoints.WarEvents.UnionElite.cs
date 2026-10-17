using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyUnionWarNpcId = 44_443;
    private const uint LegacyEliteGuildWarNpcId = 6_522;
    private const uint LegacyTrepilWarNpcId = 6_523;
    private const ushort LegacyUnionEliteWarEventAvatarId = 36;
    private const long LegacyUnionWarSourceConquerPointsReward = 150_000;
    private const long TunedLegacyUnionWarConquerPointsReward = 150_000;
    private const long LegacyUnionWarSourceMoneyReward = 5_000_000;
    private const long TunedLegacyUnionWarMoneyReward = 500_000;
    private const ulong LegacyUnionWarEmperorRank = 1_000;
    private const string LegacyUnionWarRewardClaimedAttribute = "LegacyUnionWarRewardClaimed";

    private static readonly LegacyNpcDialogOption[] LegacyUnionEliteWarEventOptions =
    [
        new("Enter the battle.", 1),
        new("Claim reward.", 2),
        new("Okay, I understand.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyUnionEliteWarEventClosedOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    internal static async ValueTask<bool> TryHandleLegacyUnionEliteWarEventNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyUnionWarNpcId => await TryHandleLegacyUnionWarAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyEliteGuildWarNpcId => await TryHandleLegacyEliteGuildWarAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyTrepilWarNpcId => await TryHandleLegacyTrepilWarAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyUnionWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.UnionWar";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hi, I manage Union War. It runs every day from 22:30 to 23:00. How can I help?"],
                        LegacyUnionEliteWarEventOptions,
                        LegacyUnionEliteWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyUnionWarNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(2_060, 135, 135, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await TryClaimLegacyUnionWarRewardAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyEliteGuildWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.EliteGuildWar";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hi, I manage Elite Guild War. It runs every day from 20:00 to 20:30. How can I help?"],
                        LegacyUnionEliteWarEventOptions,
                        LegacyUnionEliteWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyEliteGuildWarNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(6_521, 83, 139, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await SendLegacyUnionEliteWarSourceAuditedNoRewardAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "NpcHandler.SacredClassEXP lines 108711-108715 add option Claim~reward.[EliteGuildWar] as option 2; source audit of the same switch through line 108834 found no case 2 reward body; rg found no PrizeAllTops.EliteGuildWar runtime usage except Role/PrizesGame.cs line 27 constant; MsgEliteGuildWar only models process, RoundOwner, and score panels.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyTrepilWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.MsgTrepilWar";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hi, I manage Trepil War. It runs every day from 02:00 to 02:30. How can I help?"],
                        LegacyUnionEliteWarEventOptions,
                        LegacyUnionEliteWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyTrepilWarNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(3_838, 330, 220, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await TryClaimLegacyTrepilWarRewardAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask TryClaimLegacyUnionWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (IsLegacyUnionWarProcessAlive(session.Character))
        {
            await SendLegacyUnionWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Please come back when Union War is finished.",
                    "skipped active process",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(
                session.Character.Attributes,
                LegacyUnionWarRewardClaimedAttribute,
                "UnionWarRewardClaimed"))
        {
            await SendLegacyUnionWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "This Union War reward was already claimed.",
                    "already claimed",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadUInt64(
                session.Character.Attributes,
                out var winnerUnionId,
                "LegacyUnionWarWinnerUnionID",
                "LegacyUnionWarWinnerUnionId",
                "UnionWarWinnerUnionID",
                "UnionWarWinnerUnionId") ||
            winnerUnionId == 0)
        {
            await SendLegacyUnionWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "There is no Union War reward available now.",
                    "skipped missing winner union",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var playerUnionId = ReadAttributeUInt64(
            session.Character.Attributes,
            "UnionUID",
            "UnionID",
            "UnionUid",
            "UnionId",
            "MyUnionUID",
            "MyUnionID",
            "MyUnionUid",
            "MyUnionId",
            "UnionMemberUnionID",
            "UnionMemberUnionId",
            "UnionMemeberUnionID",
            "UnionMemeberUnionId");
        if (playerUnionId != winnerUnionId)
        {
            await SendLegacyUnionWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "You do not have any reward to claim.",
                    $"skipped wrong union winnerUnionId={winnerUnionId.ToString(CultureInfo.InvariantCulture)} playerUnionId={playerUnionId.ToString(CultureInfo.InvariantCulture)}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!IsLegacyUnionWarEmperor(session.Character))
        {
            await SendLegacyUnionWarRewardDialogAndLogAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Only the Union leader can claim the reward.",
                    "skipped non-emperor union rank",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(session.Character.ConquerPoints + TunedLegacyUnionWarConquerPointsReward);
        var updatedMoney = checked(session.Character.Money + TunedLegacyUnionWarMoneyReward);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithCharacterAttribute(
                        session.Character with
                        {
                            ConquerPoints = updatedConquerPoints,
                            Money = updatedMoney,
                        },
                        "ConquerPoints",
                        CheckedUInt64(updatedConquerPoints, nameof(updatedConquerPoints))),
                    "Money",
                    CheckedUInt64(updatedMoney, nameof(updatedMoney))),
                LegacyUnionWarRewardClaimedAttribute,
                "true"),
            "UnionWarRewardClaimed",
            "true");
        updatedCharacter = WithCharacterAttribute(updatedCharacter, "LegacyUnionWarWinnerCleared", "true");
        updatedCharacter = WithCharacterAttribute(updatedCharacter, "UnionWarWinnerCleared", "true");

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacyUnionWarRewardAsync(updatedContext, updatedSession)
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
        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"You claimed {TunedLegacyUnionWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs and {TunedLegacyUnionWarMoneyReward.ToString(CultureInfo.InvariantCulture)} silver for winning Union War."],
                LegacyUnionEliteWarEventClosedOptions,
                LegacyUnionEliteWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyUnionWarRewardBroadcastAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc UnionWar reward " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"winnerUnionId={winnerUnionId.ToString(CultureInfo.InvariantCulture)} " +
                "sourceConquerPoints=" +
                LegacyUnionWarSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture) +
                " tunedConquerPoints=" +
                TunedLegacyUnionWarConquerPointsReward.ToString(CultureInfo.InvariantCulture) +
                " sourceMoney=" +
                LegacyUnionWarSourceMoneyReward.ToString(CultureInfo.InvariantCulture) +
                " tunedMoney=" +
                TunedLegacyUnionWarMoneyReward.ToString(CultureInfo.InvariantCulture) +
                " sourceReward=NewSystem.PrizeAllTops.MsgUnionWar.")
            .ConfigureAwait(false);
    }

    private static bool IsLegacyUnionWarEmperor(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(
                character.Attributes,
                "UnionMemeberRank",
                "UnionMemberRank",
                "MyUnionRank",
                "UnionMilitaryRank",
                "LegacyUnionRank",
                "UnionRank")
            .Trim();
        return raw.Equals("Emperor", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("Leader", StringComparison.OrdinalIgnoreCase) ||
            (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) &&
                rank == LegacyUnionWarEmperorRank);
    }

    private static async ValueTask PersistLegacyUnionWarRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["Money"] = session.Character.Money.ToString(CultureInfo.InvariantCulture),
            [LegacyUnionWarRewardClaimedAttribute] = "true",
            ["UnionWarRewardClaimed"] = "true",
            ["LegacyUnionWarWinnerCleared"] = "true",
            ["UnionWarWinnerCleared"] = "true",
        };

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc UnionWar reward persistence " +
                    $"{FormatSession(context)} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc UnionWar reward " +
                $"{FormatSession(context)} path={Path.GetFileName(path)} keys={string.Join(',', values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyUnionWarRewardDialogAndLogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string text,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [text],
                LegacyUnionEliteWarEventClosedOptions,
                LegacyUnionEliteWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc UnionWar reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyUnionWarRewardBroadcastAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot winner,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter(
                $"{winner.Name} has received {TunedLegacyUnionWarConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs and {TunedLegacyUnionWarMoneyReward.ToString(CultureInfo.InvariantCulture)} silver for winning UnionWar.")
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

    private static async ValueTask SendLegacyUnionEliteWarRewardUnavailableAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string evidence,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["No reward is available from this NPC in the current runtime state."],
                LegacyUnionEliteWarEventClosedOptions,
                LegacyUnionEliteWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc UnionEliteWarEvent reward unavailable " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {evidence}")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyUnionEliteWarSourceAuditedNoRewardAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string evidence,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["No reward is available from this NPC in the current runtime state."],
                LegacyUnionEliteWarEventClosedOptions,
                LegacyUnionEliteWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled source-audited LegacyNpc UnionEliteWarEvent no reward " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {evidence}")
            .ConfigureAwait(false);
    }
}
