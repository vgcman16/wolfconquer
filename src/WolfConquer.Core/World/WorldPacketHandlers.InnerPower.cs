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
    [PacketHandler(WorldPacketOpcodes.LegacyInnerStrengthOpt)]
    private static async ValueTask HandleLegacyInnerStrengthOptAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyInnerPowerOptPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyInnerStrengthOpt packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyInnerPowerAction.OpenInner)
        {
            if (TryCreateLegacyInnerPowerGuiResponse(
                    packet.Parameter,
                    context.WorldData,
                    out var response,
                    out var deferReason))
            {
                await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer handled LegacyInnerStrengthOpt " +
                        $"{FormatSession(context)} action={packet.Action} parameter={packet.Parameter} parameter2={packet.Parameter2} gongs={response.Gongs.Count}; sourceName=MsgInnerPower.OpenInner.")
                    .ConfigureAwait(false);
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyInnerStrengthOpt " +
                    $"{FormatSession(context)} action={packet.Action} parameter={packet.Parameter} parameter2={packet.Parameter2}; {deferReason}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyInnerPowerAction.InfoStage)
        {
            if (TryCreateLegacyInnerPowerStageInfoResponses(
                    packet.Parameter,
                    packet.Parameter2,
                    context.WorldData,
                    out var responses,
                    out var deferReason))
            {
                foreach (var response in responses)
                {
                    await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }

                await context.Log.WriteLineAsync(
                        "WorldServer handled LegacyInnerStrengthOpt " +
                        $"{FormatSession(context)} action={packet.Action} parameter={packet.Parameter} parameter2={packet.Parameter2} stageInfoPackets={responses.Count}; sourceName=MsgInnerPower.InfoStage.")
                    .ConfigureAwait(false);
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyInnerStrengthOpt " +
                    $"{FormatSession(context)} action={packet.Action} parameter={packet.Parameter} parameter2={packet.Parameter2}; {deferReason}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyInnerPowerAction.UpdateGong)
        {
            if (TryApplyLegacyInnerPowerUpdateGong(
                    context.Session,
                    packet.Parameter,
                    packet.Parameter2,
                    context.WorldData,
                    out var result,
                    out var deferReason))
            {
                foreach (var potency in result.PotencyUpdates)
                {
                    var potencyUpdate = new LegacyMsgUpdatePacket(
                        result.CharacterId,
                        [
                            new LegacyMsgUpdateEntry(
                                LegacyMsgUpdateDataType.InnerPowerPotency,
                                checked((ulong)potency)),
                        ]);
                    await context.Connection.SendAsync(potencyUpdate.ToFrameBytes(), cancellationToken)
                        .ConfigureAwait(false);
                }

                if (result.UpgradeCount > 0)
                {
                    await context.Connection.SendAsync(result.Gui.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    await context.Connection.SendAsync(result.UpdateStage.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                    await context.Connection.SendAsync(result.UpdateScore.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }

                await context.Log.WriteLineAsync(
                        "WorldServer handled LegacyInnerStrengthOpt " +
                        $"{FormatSession(context)} action={packet.Action} parameter={packet.Parameter} parameter2={packet.Parameter2} " +
                        $"upgrades={result.UpgradeCount} potency={result.Potency}; sourceName=MsgInnerPower.UpdateGong.")
                    .ConfigureAwait(false);
                return;
            }

            await context.Log.WriteLineAsync(
                    "WorldServer deferred LegacyInnerStrengthOpt " +
                    $"{FormatSession(context)} action={packet.Action} parameter={packet.Parameter} parameter2={packet.Parameter2}; {deferReason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyInnerStrengthOpt " +
                $"{FormatSession(context)} action={packet.Action} parameter={packet.Parameter} parameter2={packet.Parameter2}; inner power mutation/gui is not modeled.")
            .ConfigureAwait(false);
    }

    private static bool TryCreateLegacyInnerPowerGuiResponse(
        uint characterId,
        WorldDataCatalog? worldData,
        out LegacyInnerPowerGuiPacket response,
        out string deferReason)
    {
        response = default;
        deferReason = string.Empty;

        if (worldData is null)
        {
            deferReason = "InnerPower.txt rows are not loaded";
            return false;
        }

        if (characterId > int.MaxValue)
        {
            deferReason = $"InnerPower.txt row key {characterId} exceeds source row key range";
            return false;
        }

        if (!worldData.InnerPowerRanksByCharacterId.TryGetValue((int)characterId, out var rows) ||
            rows.Count == 0)
        {
            deferReason = $"InnerPower.txt row for character {characterId} is not loaded";
            return false;
        }

        if (!TryReadLegacyInnerPowerState(rows[0], out var state, out deferReason))
        {
            return false;
        }

        response = new LegacyInnerPowerGuiPacket(CreateLegacyInnerPowerGuiEntries(state));
        return true;
    }

    private static bool TryCreateLegacyInnerPowerStageInfoResponses(
        uint characterId,
        uint stageOrdinal,
        WorldDataCatalog? worldData,
        out IReadOnlyList<LegacyInnerPowerStageInfoPacket> responses,
        out string deferReason)
    {
        responses = [];
        deferReason = string.Empty;

        if (worldData is null)
        {
            deferReason = "InnerPower.txt rows are not loaded";
            return false;
        }

        if (characterId > int.MaxValue)
        {
            deferReason = $"InnerPower.txt row key {characterId} exceeds source row key range";
            return false;
        }

        if (stageOrdinal == 0 || stageOrdinal > int.MaxValue)
        {
            deferReason = $"InnerPower stage index {stageOrdinal} is outside source range";
            return false;
        }

        if (!worldData.InnerPowerRanksByCharacterId.TryGetValue((int)characterId, out var rows) ||
            rows.Count == 0)
        {
            deferReason = $"InnerPower.txt row for character {characterId} is not loaded";
            return false;
        }

        if (!TryReadLegacyInnerPowerState(rows[0], out var state, out deferReason))
        {
            return false;
        }

        if (!TryGetLegacyInnerPowerStageState(
                state,
                (int)stageOrdinal,
                worldData,
                out var stage,
                out deferReason))
        {
            return false;
        }

        if (!TryCreateLegacyInnerPowerStageInfoPacket(
                characterId,
                LegacyInnerPowerStageInfoAction.UpdateStage,
                stage,
                worldData,
                out var updateStage,
                out deferReason) ||
            !TryCreateLegacyInnerPowerStageInfoPacket(
                characterId,
                LegacyInnerPowerStageInfoAction.UpdateScore,
                stage,
                worldData,
                out var updateScore,
                out deferReason))
        {
            return false;
        }

        responses = [updateStage, updateScore];
        return true;
    }

    private static bool TryApplyLegacyInnerPowerUpdateGong(
        WorldSession? session,
        uint gongId,
        uint updateMode,
        WorldDataCatalog? worldData,
        out LegacyInnerPowerGongUpdateResult result,
        out string deferReason)
    {
        result = default!;
        deferReason = string.Empty;

        if (session is null)
        {
            deferReason = "active world session is not available";
            return false;
        }

        if (worldData is null)
        {
            deferReason = "InnerPower.txt rows are not loaded";
            return false;
        }

        if (session.Character.Uid is < 0 or > int.MaxValue)
        {
            deferReason = $"InnerPower.txt row key {session.Character.Uid} exceeds source row key range";
            return false;
        }

        if (gongId > byte.MaxValue ||
            !worldData.NeiGongTypesByTypeId.TryGetValue((int)gongId, out var type))
        {
            deferReason = $"NeiGongInfo.ini gong {gongId} is not loaded";
            return false;
        }

        if (!TryValidateLegacyInnerPowerGongMetadata(type, out deferReason))
        {
            return false;
        }

        var characterId = checked((int)session.Character.Uid);
        if (!worldData.InnerPowerRanksByCharacterId.TryGetValue(characterId, out var rows) ||
            rows.Count == 0)
        {
            deferReason = $"InnerPower.txt row for character {characterId} is not loaded";
            return false;
        }

        lock (rows)
        {
            var row = rows[0];
            if (!TryReadLegacyInnerPowerState(row, out var state, out deferReason))
            {
                return false;
            }

            if (!TryFindLegacyInnerPowerGongState(
                    state,
                    (byte)gongId,
                    out var stageIndex,
                    out var gongIndex,
                    out var stage,
                    out var gong))
            {
                result = new LegacyInnerPowerGongUpdateResult(
                    checked((uint)characterId),
                    state.Potency,
                    UpgradeCount: 0,
                    PotencyUpdates: [],
                    Gui: default,
                    UpdateStage: default,
                    UpdateScore: default);
                return true;
            }

            if (stage.Id != type.SecretId)
            {
                deferReason = $"InnerPower.txt stage for gong {gongId} does not match NeiGongInfo.ini";
                return false;
            }

            if (!stage.Unlocked ||
                !gong.Unlocked ||
                gong.Level >= type.MaxLevel)
            {
                result = new LegacyInnerPowerGongUpdateResult(
                    checked((uint)characterId),
                    state.Potency,
                    UpgradeCount: 0,
                    PotencyUpdates: [],
                    Gui: default,
                    UpdateStage: default,
                    UpdateScore: default);
                return true;
            }

            var cost = type.CultureValues[Math.Min(gong.Level, type.CultureValues.Count - 1)];
            if (state.Potency < cost)
            {
                result = new LegacyInnerPowerGongUpdateResult(
                    checked((uint)characterId),
                    state.Potency,
                    UpgradeCount: 0,
                    PotencyUpdates: [],
                    Gui: default,
                    UpdateStage: default,
                    UpdateScore: default);
                return true;
            }

            var potency = state.Potency;
            var updatedLevel = gong.Level;
            var updatedScore = gong.Score;
            var complete = gong.Complete;
            var potencyUpdates = new List<int>();

            do
            {
                potency -= cost;
                updatedLevel++;
                updatedScore = CalculateLegacyInnerPowerGongScore(type.MaxLevel, updatedLevel);
                complete = updatedLevel == type.MaxLevel;
                potencyUpdates.Add(potency);
            }
            while (updateMode != 0 &&
                potency >= cost &&
                updatedLevel < type.MaxLevel);

            var updatedGong = gong with
            {
                Score = updatedScore,
                Level = updatedLevel,
                Complete = complete,
            };
            var updatedGongs = stage.Gongs.ToArray();
            updatedGongs[gongIndex] = updatedGong;
            var updatedStageState = stage with { Gongs = updatedGongs };
            var updatedStages = state.Stages.ToArray();
            updatedStages[stageIndex] = updatedStageState;
            var updatedState = state with
            {
                Potency = potency,
                Stages = updatedStages,
            };

            if (!TryCreateLegacyInnerPowerStageInfoPacket(
                    checked((uint)characterId),
                    LegacyInnerPowerStageInfoAction.UpdateStage,
                    updatedStageState,
                    worldData,
                    out var updateStage,
                    out deferReason) ||
                !TryCreateLegacyInnerPowerStageInfoPacket(
                    checked((uint)characterId),
                    LegacyInnerPowerStageInfoAction.UpdateScore,
                    updatedStageState,
                    worldData,
                    out var updateScore,
                    out deferReason))
            {
                return false;
            }

            var updatedRow = CreateLegacyInnerPowerRankEntry(row, updatedState);
            if (!TryReplaceLegacyInnerPowerRow(rows, updatedRow, out deferReason))
            {
                return false;
            }

            result = new LegacyInnerPowerGongUpdateResult(
                checked((uint)characterId),
                potency,
                potencyUpdates.Count,
                potencyUpdates,
                new LegacyInnerPowerGuiPacket(CreateLegacyInnerPowerGuiEntries(updatedState)),
                updateStage,
                updateScore);
            return true;
        }
    }

    private static IReadOnlyList<LegacyInnerPowerGuiEntry> CreateLegacyInnerPowerGuiEntries(
        LegacyInnerPowerState state)
    {
        var entries = new List<LegacyInnerPowerGuiEntry>();
        foreach (var stage in state.Stages)
        {
            if (!stage.Unlocked)
            {
                continue;
            }

            foreach (var gong in stage.Gongs)
            {
                if (gong.Unlocked)
                {
                    entries.Add(new LegacyInnerPowerGuiEntry(gong.Id, gong.Score));
                }
            }
        }

        return entries;
    }

    private static bool TryCreateLegacyInnerPowerStageInfoPacket(
        uint characterId,
        LegacyInnerPowerStageInfoAction action,
        LegacyInnerPowerStageState stage,
        WorldDataCatalog worldData,
        out LegacyInnerPowerStageInfoPacket packet,
        out string deferReason)
    {
        packet = default;
        deferReason = string.Empty;

        var unlockedGongs = stage.Gongs.Where(gong => gong.Unlocked).ToArray();
        var gongInfo = new LegacyInnerPowerStageGongInfo[unlockedGongs.Length];
        var attributes = new List<LegacyInnerPowerStageAttributeInfo>();
        for (var index = 0; index < unlockedGongs.Length; index++)
        {
            var gong = unlockedGongs[index];
            gongInfo[index] = new LegacyInnerPowerStageGongInfo(gong.Id, gong.Level, gong.Score, gong.Complete);

            if (!worldData.NeiGongTypeBySecretIdAndSlot.TryGetValue((stage.Id, index + 1), out var type))
            {
                deferReason = $"NeiGongInfo.ini type metadata for stage {stage.Id} slot {index + 1} is not loaded";
                return false;
            }

            if (type.MaxLevel <= 0)
            {
                deferReason = $"parseable NeiGongInfo.ini type metadata for stage {stage.Id} slot {index + 1} is not available";
                return false;
            }

            for (var attributeIndex = 0; attributeIndex < type.AttributeTypes.Count; attributeIndex++)
            {
                var attributeType = type.AttributeTypes[attributeIndex];
                var attributeValue = type.AttributeValues[attributeIndex];
                if (attributeType is < byte.MinValue or > byte.MaxValue ||
                    attributeValue < 0)
                {
                    deferReason = $"parseable NeiGongInfo.ini type metadata for stage {stage.Id} slot {index + 1} is not available";
                    return false;
                }

                var scaledValue = ((long)attributeValue / type.MaxLevel) * gong.Level;
                if (scaledValue > uint.MaxValue)
                {
                    deferReason = $"parseable NeiGongInfo.ini type metadata for stage {stage.Id} slot {index + 1} is not available";
                    return false;
                }

                attributes.Add(new LegacyInnerPowerStageAttributeInfo(
                    gong.Id,
                    (byte)attributeType,
                    (uint)scaledValue));
            }
        }

        packet = new LegacyInnerPowerStageInfoPacket(
            characterId,
            CalculateLegacyInnerPowerStageScore(stage),
            action,
            gongInfo,
            attributes);
        return true;
    }

}
