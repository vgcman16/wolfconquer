using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionQuestFinishDefaultRewardItemId = 3_005_160;
    private const double LegacyPromotionQuestFinishBackendExpBallAmount = 600.0;
    private const double LegacyPromotionQuestFinishVisibleExpBallAmount = 300.0;
    private const long LegacyPromotionQuestFinishBackendExpLevelGate = 130;

    private readonly record struct LegacyPromotionQuestFinishRewardSpec(
        string Source,
        string SourceRange,
        string FinishLookupSourceRange,
        string FinishLookupDescription,
        string LogName,
        uint MissionId,
        uint RewardItemId);

    private static async ValueTask<WorldSession> TryApplyLegacyPromotionQuestFinishRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        LegacyPromotionQuestFinishRewardSpec spec,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(spec.MissionId, out var quest))
        {
            await LogLegacyPromotionQuestFinishSkippedAsync(
                    context,
                    session,
                    optionId,
                    spec,
                    $"finishQuest.MissionId={spec.MissionId.ToString(CultureInfo.InvariantCulture)} is not in the runtime quest snapshot; source FinishQuest returned false because quest is not active; source QuestGUI.FinishQuest returned false-equivalent locally")
                .ConfigureAwait(false);
            return session;
        }

        if (quest.Status == PlayerQuestStatus.Finished)
        {
            await LogLegacyPromotionQuestFinishSkippedAsync(
                    context,
                    session,
                    optionId,
                    spec,
                    $"finishQuest.MissionId={spec.MissionId.ToString(CultureInfo.InvariantCulture)} is already finished; source FinishQuest returned false because quest is already finished; source QuestGUI.FinishQuest would not grant rewards again")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, spec.RewardItemId, out var rewardItemType))
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {spec.LogName} quest-finish reward item type missing " +
                    $"{FormatSession(context with { Session = session })} source={spec.Source} " +
                    $"sourceRange={spec.SourceRange} finishLookup={spec.FinishLookupSourceRange} " +
                    $"option={optionId} {spec.FinishLookupDescription} rewardItemId={spec.RewardItemId.ToString(CultureInfo.InvariantCulture)}; " +
                    "quest finish and EXP reward were left unchanged to avoid a partial source reward.")
                .ConfigureAwait(false);
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyPromotionQuestFinishSkippedAsync(
                    context,
                    session,
                    optionId,
                    spec,
                    "inventory is full, so the source item grant cannot be safely applied")
                .ConfigureAwait(false);
            return session;
        }

        var originalCharacter = session.Character;
        var finishedQuest = quest with { Status = PlayerQuestStatus.Finished };
        var activeSession = WithPlayerQuests(session, session.Character.PlayerQuests.WithQuest(finishedQuest));
        context.Sessions?.Set(context.Connection, activeSession);
        await context.Connection.SendAsync(
                LegacyQuestListPacket.CreateSingleQuestFrame(
                    LegacyQuestListMode.FinishQuest,
                    new LegacyQuestListItem(
                        finishedQuest.Uid,
                        LegacyQuestStatus.Finished,
                        finishedQuest.Time)),
                cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(context.PlayerQuestsRoot))
        {
            try
            {
                new PlayerQuestSnapshotLoader(context.PlayerQuestsRoot).SaveByCharacterUid(
                    activeSession.Character.Uid,
                    activeSession.Character.PlayerQuests);
                await context.Log.WriteLineAsync(
                        $"WorldServer persisted LegacyNpc {spec.LogName} quest finish " +
                        $"{FormatSession(context with { Session = activeSession })} source={spec.Source} " +
                        $"sourceRange={spec.SourceRange} option={optionId} " +
                        $"path={activeSession.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin " +
                        $"questId={finishedQuest.Uid.ToString(CultureInfo.InvariantCulture)}.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        $"PARITY-GAP LegacyNpc {spec.LogName} quest finish persistence " +
                        $"{FormatSession(context with { Session = activeSession })} source={spec.Source} " +
                        $"sourceRange={spec.SourceRange} option={optionId} questId={finishedQuest.Uid.ToString(CultureInfo.InvariantCulture)}; " +
                        $"reason={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {spec.LogName} quest finish persistence " +
                    $"{FormatSession(context with { Session = activeSession })} source={spec.Source} " +
                    $"sourceRange={spec.SourceRange} option={optionId} questId={finishedQuest.Uid.ToString(CultureInfo.InvariantCulture)}; " +
                    "no PlayerQuestsRoot is available in this handler context, so the quest update is session-only.")
                .ConfigureAwait(false);
        }

        activeSession = await ApplyLegacyPromotionQuestFinishExpBallAsync(
                context with { Session = activeSession },
                activeSession,
                optionId,
                spec,
                LegacyPromotionQuestFinishBackendExpBallAmount,
                sendMessage: false,
                effectName: null,
                sourceCall: "QuestGUI.SendSinglePacket FinishQuest GainExpBall(600, sendMsg:false, None)",
                onlyBelowLevel: LegacyPromotionQuestFinishBackendExpLevelGate,
                cancellationToken)
            .ConfigureAwait(false);

        var rewardItem = CreatePurchasedItems(activeSession.PlayerItems, rewardItemType, 1)[0];
        var updatedItems = activeSession.PlayerItems with
        {
            InventoryItems = activeSession.PlayerItems.InventoryItems
                .Concat([rewardItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };

        activeSession = await ApplyLegacyPromotionMasterItemsAsync(
                context with { Session = activeSession },
                activeSession,
                updatedItems,
                optionId,
                spec.Source,
                spec.SourceRange,
                $"finishQuest.MissionId={spec.MissionId.ToString(CultureInfo.InvariantCulture)} rewardItemId={spec.RewardItemId.ToString(CultureInfo.InvariantCulture)} rewardCount=1")
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        activeSession = await ApplyLegacyPromotionQuestFinishExpBallAsync(
                context with { Session = activeSession },
                activeSession,
                optionId,
                spec,
                LegacyPromotionQuestFinishVisibleExpBallAmount,
                sendMessage: true,
                effectName: "angelwing",
                sourceCall: $"{spec.LogName} option 111 GainExpBall(300, sendMsg:true, angelwing)",
                onlyBelowLevel: LegacyMaxPlayerLevel,
                cancellationToken)
            .ConfigureAwait(false);

        var questPersistenceEvidence = string.IsNullOrWhiteSpace(context.PlayerQuestsRoot)
            ? "quest finish persistence is session-only; quest persistence is session-only because WorldPacketHandlerContext has no PlayerQuestsRoot."
            : "quest finish persistence is saved through PlayerQuestsRoot; quest persistence is durable.";

        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyNpc {spec.LogName} quest-finish reward " +
                $"{FormatSession(context with { Session = activeSession })} source={spec.Source} " +
                $"sourceRange={spec.SourceRange} finishLookup={spec.FinishLookupSourceRange} option={optionId} " +
                $"{spec.FinishLookupDescription} finishQuest.MissionId={spec.MissionId.ToString(CultureInfo.InvariantCulture)} " +
                $"questId={spec.MissionId.ToString(CultureInfo.InvariantCulture)} " +
                $"rewardItemId={spec.RewardItemId.ToString(CultureInfo.InvariantCulture)} item={spec.RewardItemId.ToString(CultureInfo.InvariantCulture)} rewardCount=1 expBall=300 " +
                "QuestGUI.SendSinglePacket FinishQuest GainExpBall(600, sendMsg:false, None); " +
                $"{spec.LogName} option 111 GainExpBall(300, sendMsg:true, angelwing); " +
                "GainExpBall(600 hidden, 300 angelwing); GainExpBall(300, angelwing) " +
                $"experience={originalCharacter.Experience.ToString(CultureInfo.InvariantCulture)}->{activeSession.Character.Experience.ToString(CultureInfo.InvariantCulture)} " +
                $"level={originalCharacter.Level.ToString(CultureInfo.InvariantCulture)}->{activeSession.Character.Level.ToString(CultureInfo.InvariantCulture)}; " +
                questPersistenceEvidence)
            .ConfigureAwait(false);

        return activeSession;
    }

    private static async ValueTask<WorldSession> ApplyLegacyPromotionQuestFinishExpBallAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        LegacyPromotionQuestFinishRewardSpec spec,
        double amount,
        bool sendMessage,
        string? effectName,
        string sourceCall,
        long onlyBelowLevel,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level >= onlyBelowLevel)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyNpc {spec.LogName} quest-finish exp ball " +
                    $"{FormatSession(context with { Session = session })} source={spec.Source} " +
                    $"sourceRange={spec.SourceRange} option={optionId} amount={amount.ToString(CultureInfo.InvariantCulture)}; " +
                    $"source level gate level={session.Character.Level.ToString(CultureInfo.InvariantCulture)} " +
                    $"requiredBelow={onlyBelowLevel.ToString(CultureInfo.InvariantCulture)} call=\"{sourceCall}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryCalculateLegacyExpBallUse(
                context.WorldData,
                session.Character,
                amount,
                out var updatedLevel,
                out var updatedExperience))
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {spec.LogName} quest-finish exp ball " +
                    $"{FormatSession(context with { Session = session })} source={spec.Source} " +
                    $"sourceRange={spec.SourceRange} option={optionId} amount={amount.ToString(CultureInfo.InvariantCulture)}; " +
                    $"level experience data is not loaded, so EXP mutation is not modeled for call=\"{sourceCall}\".")
                .ConfigureAwait(false);
            return session;
        }

        var updatedCharacter = session.Character with
        {
            Level = updatedLevel,
            Experience = updatedExperience,
        };
        var updatedSession = await ApplyLegacyPromotionCharacterAsync(
                context,
                session,
                updatedCharacter,
                optionId,
                spec.Source,
                spec.SourceRange)
            .ConfigureAwait(false);

        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
        if (sendMessage)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk($"You have gained experience worth {amount / LegacyExpBallAmount:0.########} exp ball(s).")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (effectName is not null)
        {
            await context.Connection.SendAsync(
                    LegacyStringPacket.PlayerEffect(uid, effectName).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (updatedCharacter.Level != session.Character.Level)
        {
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)updatedCharacter.Level)).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeExperienceUpdate(uid, checked((ulong)updatedCharacter.Experience)).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask LogLegacyPromotionQuestFinishSkippedAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        LegacyPromotionQuestFinishRewardSpec spec,
        string reason)
    {
        await context.Log.WriteLineAsync(
                $"PARITY-GAP LegacyNpc {spec.LogName} quest-finish reward skipped " +
                $"{FormatSession(context with { Session = session })} source={spec.Source} " +
                $"sourceRange={spec.SourceRange} finishLookup={spec.FinishLookupSourceRange} " +
                $"option={optionId} {spec.FinishLookupDescription} item={spec.RewardItemId.ToString(CultureInfo.InvariantCulture)} " +
                "GainExpBall(600 hidden, 300 angelwing); GainExpBall(300, angelwing); " +
                $"reason={reason}.")
            .ConfigureAwait(false);
    }
}
