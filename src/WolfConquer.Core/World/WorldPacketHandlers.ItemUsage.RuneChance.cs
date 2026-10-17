using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRuneChanceSource = "MsgItemUsuagePacket.ChanceRune";
    private const string LegacyRuneChanceLines = "2130-2155";

    private static async ValueTask HandleLegacyChanceRuneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryGetRuneItem(session.PlayerItems, packet.Id, out var rune))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage chance rune " +
                    $"{FormatSession(context)} source={LegacyRuneChanceSource} " +
                    $"sourceLines={LegacyRuneChanceLines} itemUid={packet.Id}; rune item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var updatedRune = rune;
        var chanceRate = 0;
        var chancePassed = false;
        if (rune.ItemId % 100 < GetLegacyMaxRuneLevel(rune.ItemId) &&
            rune.RuneExperience != 0)
        {
            if (!TryGetLegacyRuneLevelRequiredProgress(
                    context.WorldData,
                    GetLegacyRuneDecomposeFamily(rune.ItemId),
                    rune.ItemId % 100,
                    out var currentRequiredProgress) ||
                !TryGetLegacyRuneLevelRequiredProgress(
                    context.WorldData,
                    GetLegacyRuneDecomposeFamily(rune.ItemId),
                    rune.ItemId % 100 + 1,
                    out var nextRequiredProgress) ||
                nextRequiredProgress <= currentRequiredProgress)
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.UnsupportedItemUse(
                            rune.ItemId,
                            action: (uint)packet.Action,
                            actionName: packet.Action.ToString(),
                            itemUid: rune.Uid,
                            source: LegacyRuneChanceSource,
                            reason: "rune chance level experience metadata is missing"))
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "PARITY-GAP MsgItemUsage chance rune missing level experience " +
                        $"{FormatSession(context)} source={LegacyRuneChanceSource} " +
                        $"sourceLines={LegacyRuneChanceLines} itemUid={rune.Uid} itemId={rune.ItemId}.")
                    .ConfigureAwait(false);
                return;
            }

            var requiredDelta = checked(nextRequiredProgress - currentRequiredProgress);
            chanceRate = unchecked((byte)((double)rune.RuneExperience / requiredDelta * 100.0));
            chancePassed = CheckLegacyRuneChanceRate(chanceRate);
            updatedRune = rune.WithRuneExperience(0);
            if (chancePassed)
            {
                updatedRune = updatedRune.WithItemId(checked(rune.ItemId + 1));
            }
        }

        var updatedSession = session with
        {
            PlayerItems = ReplaceRuneItem(session.PlayerItems, rune, updatedRune),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "chance rune",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        updatedRune,
                        LegacyGameItemPacket.CreateRuneSpellOverride(updatedRune, context.WorldData?.ItemsById),
                        LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage chance rune " +
                $"{FormatSession(updatedContext)} source={LegacyRuneChanceSource} " +
                $"sourceLines={LegacyRuneChanceLines} itemUid={rune.Uid} oldItemId={rune.ItemId} " +
                $"newItemId={updatedRune.ItemId} oldRuneExp={rune.RuneExperience} " +
                $"newRuneExp={updatedRune.RuneExperience} chanceRate={chanceRate} " +
                $"chancePassed={chancePassed}.")
            .ConfigureAwait(false);
    }

    private static bool CheckLegacyRuneChanceRate(int ratePercent)
    {
        return ratePercent >= 100 ||
            (ratePercent > 0 && Random.Shared.Next(0, 100) < ratePercent);
    }
}
