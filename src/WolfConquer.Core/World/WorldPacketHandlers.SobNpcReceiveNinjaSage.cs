using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyNinjaSageSpeedModeFlag = 0x111;
    private const string RuntimeLegacyNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string RuntimeLegacyNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const int LegacyNinjaSageModeLevelCap = 17;
    private const int LegacyGouyuImmortalLevelValueIndex = 0;
    private const int LegacyGouyuImmortalChanceValueIndex = 5;
    private const int LegacyGouyuImmortalSecondsValueIndex = 6;
    private const int LegacyGouyuImmortalRateValueIndex = 7;
    private const uint LegacyNinjaSageModeRollDenominator = 100;

    private static ValueTask<(WorldSession Session, bool Applied)> TrySendLegacySobNpcReceiveNinjaSageModeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldDataCatalog? worldData,
        CancellationToken cancellationToken)
    {
        return TrySendLegacyReceiveNinjaSageModeAsync(
            context,
            session,
            worldData,
            rollValueIndex: LegacyGouyuImmortalChanceValueIndex,
            targetKind: "SobNpc",
            sourceDescription: "ReceiveAttack.Npc CombatGear.Update then NinjaFile.gouyu_immortals SageMode using Chance / 100.",
            cancellationToken);
    }

    private static ValueTask<(WorldSession Session, bool Applied)> TrySendLegacyMonsterReceiveNinjaSageModeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldDataCatalog? worldData,
        CancellationToken cancellationToken)
    {
        return TrySendLegacyReceiveNinjaSageModeAsync(
            context,
            session,
            worldData,
            rollValueIndex: LegacyGouyuImmortalRateValueIndex,
            targetKind: "Monster",
            sourceDescription: "ReceiveAttack.Monster SageMode branch via NinjaFile.gouyu_immortals.",
            cancellationToken);
    }

    private static ValueTask<(WorldSession Session, bool Applied)> TrySendLegacyPlayerReceiveNinjaSageModeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldDataCatalog? worldData,
        CancellationToken cancellationToken)
    {
        return TrySendLegacyReceiveNinjaSageModeAsync(
            context,
            session,
            worldData,
            rollValueIndex: LegacyGouyuImmortalChanceValueIndex,
            targetKind: "Player",
            sourceDescription: "ReceiveAttack.Player SageMode branch via NinjaFile.gouyu_immortals Chance / 100.",
            cancellationToken);
    }

    private static async ValueTask<(WorldSession Session, bool Applied)> TrySendLegacyReceiveNinjaSageModeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldDataCatalog? worldData,
        int rollValueIndex,
        string targetKind,
        string sourceDescription,
        CancellationToken cancellationToken)
    {
        if (worldData is null ||
            !IsLegacyNinjaClass(session.Character.Class) ||
            HasLegacyStatusFlag(session.Character, LegacyNinjaSageSpeedModeFlag) ||
            !TryGetLegacyNinjaEffectiveLevels(session, out var effectiveLevels) ||
            effectiveLevels == 0 ||
            !TryGetLegacyGouyuImmortalSageMode(
                worldData,
                effectiveLevels,
                rollValueIndex,
                out var rollPercent,
                out var seconds) ||
            !CheckLegacyNinjaSageModeRateRoll(session.Character, rollPercent))
        {
            return (session, false);
        }

        var updatedSession = session with
        {
            Character = WithLegacyStatusFlag(session.Character, LegacyNinjaSageSpeedModeFlag),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNinjaSageModeArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                seconds,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy receive Ninja SageMode " +
                $"{FormatSession(updatedContext)} effectiveLevels={effectiveLevels} " +
                $"targetKind={targetKind} statusFlag={LegacyNinjaSageSpeedModeFlag} " +
                $"rollPercent={rollPercent} duration={seconds}; source={sourceDescription}")
            .ConfigureAwait(false);
        return (updatedSession, true);
    }

    private static bool TryGetLegacyNinjaEffectiveLevels(WorldSession session, out uint effectiveLevels)
    {
        if (TryReadUInt32Attribute(
                session.Character.Attributes,
                out var deterministicLevels,
                RuntimeLegacyNinjaLevelsAttribute) &&
            deterministicLevels != 0)
        {
            effectiveLevels = Math.Min(deterministicLevels, LegacyNinjaSageModeLevelCap);
            return true;
        }

        if (!session.Character.Attributes.TryGetValue("MsgNinja", out var raw) ||
            !TryReadLegacyMsgNinjaLevels(raw, session.AlternateEquipmentActive, out var levels) ||
            levels == 0)
        {
            effectiveLevels = 0;
            return false;
        }

        effectiveLevels = Math.Min(levels, LegacyNinjaSageModeLevelCap);
        return true;
    }

    private static bool TryReadLegacyMsgNinjaLevels(
        string raw,
        bool alternateEquipmentActive,
        out uint levels)
    {
        levels = 0;
        var fields = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length <= LegacyMsgNinjaHeaderFieldCount)
        {
            return false;
        }

        var normalLevels = new Dictionary<uint, uint>();
        var alternateLevels = new Dictionary<uint, uint>();
        for (var index = LegacyMsgNinjaHeaderFieldCount;
             index + 2 < fields.Length;
             index += LegacyMsgNinjaItemRecordFieldCount)
        {
            if (!uint.TryParse(fields[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ||
                level == 0 ||
                !uint.TryParse(fields[index + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var position))
            {
                continue;
            }

            if (position is >= 1 and <= 8)
            {
                normalLevels[position] = level;
            }
            else if (position is >= 9 and <= 16)
            {
                alternateLevels[position] = level;
            }
        }

        for (uint slot = 1; slot <= 8; slot++)
        {
            if (alternateEquipmentActive)
            {
                levels += alternateLevels.TryGetValue(slot + 8, out var alternateLevel)
                    ? alternateLevel
                    : normalLevels.GetValueOrDefault(slot);
            }
            else
            {
                levels += normalLevels.GetValueOrDefault(slot);
            }
        }

        return levels != 0;
    }

    private static bool TryGetLegacyGouyuImmortalSageMode(
        WorldDataCatalog worldData,
        uint effectiveLevels,
        int rollValueIndex,
        out uint rollPercent,
        out uint seconds)
    {
        rollPercent = 0;
        seconds = 0;
        if (effectiveLevels > int.MaxValue)
        {
            return false;
        }

        foreach (var row in worldData.GouyuImmortals)
        {
            if (row.Values.Count <= LegacyGouyuImmortalSecondsValueIndex ||
                row.Values.Count <= rollValueIndex ||
                row.Values[LegacyGouyuImmortalLevelValueIndex] != (int)effectiveLevels ||
                row.Values[rollValueIndex] <= 0 ||
                row.Values[LegacyGouyuImmortalSecondsValueIndex] <= 0)
            {
                continue;
            }

            rollPercent = checked((uint)(row.Values[rollValueIndex] / 100));
            seconds = checked((uint)row.Values[LegacyGouyuImmortalSecondsValueIndex]);
            return rollPercent != 0;
        }

        return false;
    }

    private static bool CheckLegacyNinjaSageModeRateRoll(CharacterSnapshot character, uint chancePercent)
    {
        var roll = TryReadUInt32Attribute(
                character.Attributes,
                out var forcedRoll,
                RuntimeLegacyNinjaSageModeRollAttribute)
            ? forcedRoll % LegacyNinjaSageModeRollDenominator
            : checked((uint)Random.Shared.Next(0, (int)LegacyNinjaSageModeRollDenominator));
        return roll < chancePercent;
    }

    private static async ValueTask SendLegacyNinjaSageModeArchiveSkillUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint seconds,
        CancellationToken cancellationToken)
    {
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var packet = new LegacyMsgUpdatePacket(
            uid,
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.ArchiveSkill,
                    Value: 0,
                    Flag: LegacyNinjaSageSpeedModeFlag,
                    Time: seconds)
            ]);
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                session,
                packet.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
