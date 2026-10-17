using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyUnionWarNoUnionDialogText = "Sorry you not have Union.";

    private enum LegacyPlayerMagicGateFailureEffect
    {
        None,
        ForbiddenPirate,
        HellVortexCounter,
    }

    private static IEnumerable<LegacyPlayerSobNpcMagicTarget> EnumerateVisibleSobNpcMagicTargets(
        WorldPacketHandlerContext context,
        CharacterSnapshot character)
    {
        if (character.MapId is < int.MinValue or > int.MaxValue ||
            context.WorldData is not { } worldData ||
            !worldData.SobNpcsByMapId.TryGetValue((int)character.MapId, out var sobNpcs))
        {
            yield break;
        }

        foreach (var sobNpc in sobNpcs)
        {
            if (!CanSeeSobNpc(character, sobNpc) ||
                !CanLegacySobNpcMagicDamageTarget(context, character, sobNpc, out _))
            {
                continue;
            }

            yield return new LegacyPlayerSobNpcMagicTarget(sobNpc);
        }
    }

    private static bool CanLegacySobNpcMagicDamageTarget(SobNpc sobNpc, out string failureDiagnostic)
    {
        return CanLegacySobNpcMagicDamageTarget(
            sobNpc,
            worldData: null,
            caster: null,
            pkMode: null,
            guildWarStates: null,
            sobNpcStates: null,
            out failureDiagnostic);
    }

    private static bool CanLegacySobNpcMagicDamageTarget(
        WorldPacketHandlerContext context,
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        var pkMode = ReadAttributeUInt64(caster.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
        return CanLegacySobNpcMagicDamageTarget(
            sobNpc,
            context.WorldData,
            caster,
            pkMode,
            context.GuildWarStates,
            context.SobNpcStates,
            out failureDiagnostic);
    }

    private static bool TryCreateLegacySobNpcMagicGateDialog(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        string failureDiagnostic,
        out LegacyMsgMessagePacket packet)
    {
        packet = default!;
        if (!LegacySobNpcEventRules.IsUnionWarPoleSob(sobNpc) ||
            !failureDiagnostic.Contains("caster has no union", StringComparison.Ordinal) ||
            ReadAttributeUInt64(
                caster.Attributes,
                "UnionUID",
                "UnionID",
                "UnionUid",
                "UnionId",
                "MyUnionUID",
                "MyUnionID") != 0)
        {
            return false;
        }

        packet = LegacyMsgMessagePacket.BoxDialog(LegacyUnionWarNoUnionDialogText);
        return true;
    }

    private static bool CanLegacySobNpcMagicDamageTarget(
        SobNpc sobNpc,
        WorldDataCatalog? worldData,
        CharacterSnapshot? caster,
        ulong? pkMode,
        LegacyGuildWarRuntimeStateStore? guildWarStates,
        SobNpcRuntimeStateStore? sobNpcStates,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (LegacySobNpcEventRules.IsCanAttackNpcAlwaysAllowedMap(sobNpc))
        {
            return true;
        }

        if (!LegacySobNpcEventRules.IsTrainingSob(sobNpc) &&
            sobNpc.Life <= 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected dead SOB NPC";
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicNewGuildWarAttackBlocked(
                caster,
                sobNpc,
                worldData,
                guildWarStates,
                sobNpcStates,
                out failureDiagnostic))
        {
            return false;
        }

        if (worldData is not null &&
            LegacySobNpcEventRules.IsStaticStatueSob(sobNpc, worldData.StaticStatues))
        {
            if (pkMode == LegacyPkModePk)
            {
                return true;
            }

            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; " +
                $"source CanAttackNpc.Verified rejected static statue because pkMode={pkMode.GetValueOrDefault()}";
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicClanWarPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicUnionWarPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicClanDominationPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicPoleDominationPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicNobilityPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicEliteGuildWarPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicTrepilWarPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicTopPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicWarOfPlayersPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicClassPoleWarPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicWarFightersPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicSuperGuildWarPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicEmperorWarPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicGuildWarScorePoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicClanWarScorePoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicChampionsOfWarrPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicGuild6PoleWarPoleAttackBlocked(caster, sobNpc, out failureDiagnostic))
        {
            return false;
        }

        if (caster is not null &&
            IsLegacySobNpcMagicCaptureTheFlagBaseAttackBlocked(caster, sobNpc, sobNpcStates, out failureDiagnostic))
        {
            return false;
        }

        if (LegacySobNpcEventRules.IsTrainingSob(sobNpc) ||
            LegacySobNpcEventRules.IsEventDamageableSob(sobNpc))
        {
            return true;
        }

        failureDiagnostic =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; SOB NPC is not a legacy damageable training or event object";
        return false;
    }

    private static bool IsLegacySobNpcMagicCaptureTheFlagBaseAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        SobNpcRuntimeStateStore? sobNpcStates,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (caster.MapId != LegacyCaptureTheFlagMapId ||
            sobNpc.Id < 0)
        {
            return false;
        }

        foreach (var bas in ReadCaptureTheFlagBases(caster))
        {
            if (bas.Uid != (uint)sobNpc.Id)
            {
                continue;
            }

            if (!bas.Viable)
            {
                failureDiagnostic =
                    $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified " +
                    $"rejected CaptureTheFlag base uid={bas.Uid} because CaptureTheFlag.Proces is not Alive";
                return true;
            }

            var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
            if (guildId == 0)
            {
                failureDiagnostic =
                    $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified " +
                    $"rejected CaptureTheFlag base uid={bas.Uid} because caster has no guild";
                return true;
            }

            if (sobNpcStates?.GetVisible(sobNpc).Life == 0)
            {
                failureDiagnostic =
                    $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified " +
                    $"rejected CaptureTheFlag base uid={bas.Uid} because value.Npc.HitPoints is 0";
                return true;
            }

            if (bas.CapturerGuildId == guildId)
            {
                failureDiagnostic =
                    $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified " +
                    $"rejected CaptureTheFlag base uid={bas.Uid} because capturerGuildId={bas.CapturerGuildId} matches caster GuildID";
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicNewGuildWarAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        WorldDataCatalog? worldData,
        LegacyGuildWarRuntimeStateStore? guildWarStates,
        SobNpcRuntimeStateStore? sobNpcStates,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsNewGuildWarAuthorizationSob(sobNpc))
        {
            return false;
        }

        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source GUILDWAR.Verified rejected " +
                $"NEWGUILDWAR sobNpcUid={sobNpc.Id} because caster has no guild";
            return true;
        }

        if (sobNpc.Id == LegacySobNpcEventRules.GuildWarPillarUid)
        {
            var winnerGuildId = ResolveLegacyNewGuildWarWinnerGuildId(caster, worldData);
            if (winnerGuildId != 0 &&
                winnerGuildId == guildId)
            {
                failureDiagnostic =
                    $"map={sobNpc.MapId} kind={sobNpc.Kind}; source GUILDWAR.Verified rejected " +
                    $"GuildPillar uid={sobNpc.Id} because winnerGuildId={winnerGuildId} matches caster GuildID";
                return true;
            }

            if (!IsLegacyNewGuildWarProcessAlive(caster, guildWarStates))
            {
                failureDiagnostic =
                    $"map={sobNpc.MapId} kind={sobNpc.Kind}; source GUILDWAR.Verified rejected " +
                    $"GuildPillar uid={sobNpc.Id} because NEWGUILDWAR.Proces is not Alive";
                return true;
            }

            return false;
        }

        var ownerGuildId = ResolveLegacyNewGuildWarSobOwnerGuildId(sobNpc, sobNpcStates);
        if (ownerGuildId != 0 &&
            ownerGuildId == guildId)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source GUILDWAR.Verified rejected " +
                $"NEWGUILDWAR pillar uid={sobNpc.Id} because ownerGuildId={ownerGuildId} matches caster GuildID";
            return true;
        }

        if (!IsLegacyNewGuildWarProcessAlive(caster, guildWarStates))
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source GUILDWAR.Verified rejected " +
                $"NEWGUILDWAR pillar uid={sobNpc.Id} because NEWGUILDWAR.Proces is not Alive";
            return true;
        }

        return false;
    }

    private static bool IsLegacyNewGuildWarProcessAlive(
        CharacterSnapshot caster,
        LegacyGuildWarRuntimeStateStore? guildWarStates)
    {
        if (guildWarStates is not null)
        {
            return guildWarStates.IsActive;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "LegacyNewGuildWarProcess",
            "NewGuildWarProcess",
            "GuildWarProcess");
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static ulong ResolveLegacyNewGuildWarWinnerGuildId(
        CharacterSnapshot caster,
        WorldDataCatalog? worldData)
    {
        var winnerGuildId = ReadAttributeUInt64(
            caster.Attributes,
            "LegacyNewGuildWarWinnerGuildID",
            "NewGuildWarWinnerGuildID",
            "GuildWarWinnerGuildID");
        if (winnerGuildId != 0)
        {
            return winnerGuildId;
        }

        var sourceInfoId = worldData?.GuildWarInfo.Info?.Id;
        return sourceInfoId is > 0 ? (ulong)sourceInfoId.Value : 0UL;
    }

    private static ulong ResolveLegacyNewGuildWarSobOwnerGuildId(
        SobNpc sobNpc,
        SobNpcRuntimeStateStore? sobNpcStates)
    {
        return sobNpcStates?.TryGetOwnerGuildId(sobNpc, out var ownerGuildId) == true
            ? ownerGuildId
            : 0UL;
    }

    private static bool IsLegacySobNpcMagicClanWarPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsClanWarPoleSob(sobNpc))
        {
            return false;
        }

        var clanId = ReadAttributeUInt64(caster.Attributes, "ClanID", "ClanUID");
        if (clanId == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"ClanWar pole uid={sobNpc.Id} because caster has no clan";
            return true;
        }

        if (!ReadAttributeBool(
                caster.Attributes,
                "LegacyClanWarActive",
                "ClanWarActive",
                "RuntimeClanWarActive"))
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"ClanWar pole uid={sobNpc.Id} because MsgSchedules.ClanWar.CurentWar is unavailable";
            return true;
        }

        if (!ReadAttributeBool(
                caster.Attributes,
                "LegacyClanWarInWar",
                "ClanWarInWar",
                "RuntimeClanWarInWar"))
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"ClanWar pole uid={sobNpc.Id} because curentWar.InWar(client) is false";
            return true;
        }

        var winnerClanId = ReadAttributeUInt64(
            caster.Attributes,
            "LegacyClanWarWinnerClanID",
            "LegacyClanWarWinnerClainID",
            "ClanWarWinnerClanID",
            "ClanWarWinnerClainID");
        if (winnerClanId == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"ClanWar pole uid={sobNpc.Id} because curentWar.Winner is unavailable";
            return true;
        }

        if (winnerClanId == clanId)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"ClanWar pole uid={sobNpc.Id} because winnerClanId={winnerClanId} matches caster ClanUID";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicUnionWarPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsUnionWarPoleSob(sobNpc))
        {
            return false;
        }

        var unionId = ReadAttributeUInt64(
            caster.Attributes,
            "UnionUID",
            "UnionID",
            "UnionUid",
            "UnionId",
            "MyUnionUID",
            "MyUnionID");
        if (unionId == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"UnionWar pole uid={sobNpc.Id} because caster has no union";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "LegacyUnionWarPoleHitPoints",
                "LegacyUnionWarPoleHitpoints",
                "UnionWarPoleHitPoints",
                "UnionWarPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"UnionWar pole uid={sobNpc.Id} because MsgSchedules.UnionWar.Furnitures[Pole].HitPoints is zero";
            return true;
        }

        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"UnionWar pole uid={sobNpc.Id} because caster has no guild";
            return true;
        }

        var winnerUnionId = ReadAttributeUInt64(
            caster.Attributes,
            "LegacyUnionWarWinnerUnionID",
            "LegacyUnionWarWinnerUnionId",
            "UnionWarWinnerUnionID",
            "UnionWarWinnerUnionId");
        if (winnerUnionId != 0 &&
            winnerUnionId == ResolveLegacyUnionWarGuildUnionId(caster, unionId))
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"UnionWar pole uid={sobNpc.Id} because winnerUnionId={winnerUnionId} matches caster guild UnionID";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "LegacyUnionWarProcess",
            "UnionWarProcess",
            "RuntimeUnionWarProcess");
        if (string.IsNullOrWhiteSpace(process) ||
            process.Equals("Dead", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("Idle", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"UnionWar pole uid={sobNpc.Id} because MsgSchedules.UnionWar.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static ulong ResolveLegacyUnionWarGuildUnionId(
        CharacterSnapshot caster,
        ulong fallbackUnionId)
    {
        var guildUnionId = ReadAttributeUInt64(
            caster.Attributes,
            "GuildUnionID",
            "GuildUnionId",
            "MyGuildUnionID",
            "MyGuildUnionId");

        return guildUnionId != 0 ? guildUnionId : fallbackUnionId;
    }

    private static bool IsLegacySobNpcMagicClanDominationPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.TryGetClanDominationPoleScheduleName(sobNpc, out var scheduleName))
        {
            return false;
        }

        var clanId = ReadAttributeUInt64(caster.Attributes, "ClanID", "ClanUID");
        if (clanId == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because caster has no clan";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "Legacy" + scheduleName + "PoleHitPoints",
                "Legacy" + scheduleName + "PoleHitpoints",
                scheduleName + "PoleHitPoints",
                scheduleName + "PoleHitpoints",
                "LegacyClanDominationPoleHitPoints",
                "LegacyClanDominationPoleHitpoints",
                "ClanDominationPoleHitPoints",
                "ClanDominationPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because MsgSchedules.{scheduleName}.Furnitures[Pole].HitPoints is zero";
            return true;
        }

        var winnerClanId = ReadAttributeUInt64(
            caster.Attributes,
            "Legacy" + scheduleName + "WinnerClanID",
            "Legacy" + scheduleName + "WinnerClanId",
            scheduleName + "WinnerClanID",
            scheduleName + "WinnerClanId",
            "LegacyClanDominationWinnerClanID",
            "LegacyClanDominationWinnerClanId",
            "ClanDominationWinnerClanID",
            "ClanDominationWinnerClanId");
        if (winnerClanId != 0 &&
            winnerClanId == clanId)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because winnerClanId={winnerClanId} matches caster ClanUID";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "Legacy" + scheduleName + "Process",
            scheduleName + "Process",
            "Runtime" + scheduleName + "Process",
            "LegacyClanDominationProcess",
            "ClanDominationProcess",
            "RuntimeClanDominationProcess");
        if (string.IsNullOrWhiteSpace(process) ||
            process.Equals("Dead", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("Idle", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because MsgSchedules.{scheduleName}.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicPoleDominationPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.TryGetPoleDominationPoleScheduleName(sobNpc, out var scheduleName))
        {
            return false;
        }

        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because caster has no guild";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "Legacy" + scheduleName + "PoleHitPoints",
                "Legacy" + scheduleName + "PoleHitpoints",
                scheduleName + "PoleHitPoints",
                scheduleName + "PoleHitpoints",
                "LegacyPoleDominationPoleHitPoints",
                "LegacyPoleDominationPoleHitpoints",
                "PoleDominationPoleHitPoints",
                "PoleDominationPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because MsgSchedules.{scheduleName}.Furnitures[Pole].HitPoints is zero";
            return true;
        }

        var winnerGuildId = ReadAttributeUInt64(
            caster.Attributes,
            "Legacy" + scheduleName + "WinnerGuildID",
            "Legacy" + scheduleName + "WinnerGuildId",
            scheduleName + "WinnerGuildID",
            scheduleName + "WinnerGuildId",
            "LegacyPoleDominationWinnerGuildID",
            "LegacyPoleDominationWinnerGuildId",
            "PoleDominationWinnerGuildID",
            "PoleDominationWinnerGuildId");
        if (winnerGuildId != 0 &&
            winnerGuildId == guildId)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because winnerGuildId={winnerGuildId} matches caster GuildID";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "Legacy" + scheduleName + "Process",
            scheduleName + "Process",
            "Runtime" + scheduleName + "Process",
            "LegacyPoleDominationProcess",
            "PoleDominationProcess",
            "RuntimePoleDominationProcess");
        if (string.IsNullOrWhiteSpace(process) ||
            process.Equals("Dead", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("Idle", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because MsgSchedules.{scheduleName}.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicEliteGuildWarPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.TryGetEliteGuildWarPole(
                sobNpc,
                out var scheduleName,
                out var furnitureName))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"EliteGuildWar pole uid={sobNpc.Id} because ";
        var sourcePole = $"MsgSchedules.EliteGuildWar.{furnitureName}[Pole]";
        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic = diagnosticPrefix + "caster has no guild";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "Legacy" + scheduleName + "PoleHitPoints",
                "Legacy" + scheduleName + "PoleHitpoints",
                scheduleName + "PoleHitPoints",
                scheduleName + "PoleHitpoints",
                "LegacyEliteGuildWar" + furnitureName + "PoleHitPoints",
                "LegacyEliteGuildWar" + furnitureName + "PoleHitpoints",
                "EliteGuildWar" + furnitureName + "PoleHitPoints",
                "EliteGuildWar" + furnitureName + "PoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic =
                diagnosticPrefix + sourcePole + ".HitPoints is zero";
            return true;
        }

        var guildName = ReadAttributeString(caster.Attributes, "GuildName", "LegacyGuildName");
        if (!string.IsNullOrWhiteSpace(guildName) &&
            string.Equals(guildName, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic =
                diagnosticPrefix + "client.Player.MyGuild.GuildName matches " + sourcePole + ".Name";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "LegacyEliteGuildWarProcess",
            "EliteGuildWarProcess",
            "RuntimeEliteGuildWarProcess");
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic =
                diagnosticPrefix + $"MsgSchedules.EliteGuildWar.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicTrepilWarPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsTrepilWarPole(sobNpc))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgTrepilWar pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgSchedules.MsgTrepilWar.Furnitures[Pole]";
        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic = diagnosticPrefix + "caster has no guild";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "LegacyMsgTrepilWarPoleHitPoints",
                "LegacyMsgTrepilWarPoleHitpoints",
                "MsgTrepilWarPoleHitPoints",
                "MsgTrepilWarPoleHitpoints",
                "LegacyTrepilWarPoleHitPoints",
                "LegacyTrepilWarPoleHitpoints",
                "TrepilWarPoleHitPoints",
                "TrepilWarPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + SourcePole + ".HitPoints is zero";
            return true;
        }

        var guildName = ReadAttributeString(caster.Attributes, "GuildName", "LegacyGuildName");
        if (!string.IsNullOrWhiteSpace(guildName) &&
            string.Equals(guildName, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic =
                diagnosticPrefix + "client.Player.MyGuild.GuildName matches " + SourcePole + ".Name";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "LegacyMsgTrepilWarProcess",
            "MsgTrepilWarProcess",
            "RuntimeMsgTrepilWarProcess",
            "LegacyTrepilWarProcess",
            "TrepilWarProcess",
            "RuntimeTrepilWarProcess");
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"MsgSchedules.MsgTrepilWar.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicTopPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsTopPole(sobNpc))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgTopPole pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgTopPole.Furnitures[Pole]";
        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic = diagnosticPrefix + "caster has no guild";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "LegacyMsgTopPoleHitPoints",
                "LegacyMsgTopPoleHitpoints",
                "MsgTopPoleHitPoints",
                "MsgTopPoleHitpoints",
                "LegacyTopPoleHitPoints",
                "LegacyTopPoleHitpoints",
                "TopPoleHitPoints",
                "TopPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + SourcePole + ".HitPoints is zero";
            return true;
        }

        if (string.Equals(caster.Name, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic = diagnosticPrefix + "client.Player.Name matches " + SourcePole + ".Name";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "LegacyMsgTopPoleProcess",
            "MsgTopPoleProcess",
            "RuntimeMsgTopPoleProcess",
            "LegacyTopPoleProcess",
            "TopPoleProcess",
            "RuntimeTopPoleProcess");
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"MsgTopPole.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicWarOfPlayersPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsWarOfPlayersPole(sobNpc))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgWarOfPlayers pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgWarOfPlayers.Furnitures[Pole]";

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "LegacyMsgWarOfPlayersHitPoints",
                "LegacyMsgWarOfPlayersHitpoints",
                "LegacyMsgWarOfPlayersPoleHitPoints",
                "LegacyMsgWarOfPlayersPoleHitpoints",
                "MsgWarOfPlayersHitPoints",
                "MsgWarOfPlayersHitpoints",
                "MsgWarOfPlayersPoleHitPoints",
                "MsgWarOfPlayersPoleHitpoints",
                "RuntimeMsgWarOfPlayersHitPoints",
                "RuntimeMsgWarOfPlayersHitpoints",
                "RuntimeMsgWarOfPlayersPoleHitPoints",
                "RuntimeMsgWarOfPlayersPoleHitpoints",
                "LegacyWarOfPlayersHitPoints",
                "LegacyWarOfPlayersHitpoints",
                "LegacyWarOfPlayersPoleHitPoints",
                "LegacyWarOfPlayersPoleHitpoints",
                "WarOfPlayersHitPoints",
                "WarOfPlayersHitpoints",
                "WarOfPlayersPoleHitPoints",
                "WarOfPlayersPoleHitpoints",
                "RuntimeWarOfPlayersHitPoints",
                "RuntimeWarOfPlayersHitpoints",
                "RuntimeWarOfPlayersPoleHitPoints",
                "RuntimeWarOfPlayersPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + SourcePole + ".HitPoints is zero";
            return true;
        }

        if (string.Equals(caster.Name, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic = diagnosticPrefix + "client.Player.Name matches " + SourcePole + ".Name";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "LegacyMsgWarOfPlayersProcess",
            "MsgWarOfPlayersProcess",
            "RuntimeMsgWarOfPlayersProcess",
            "LegacyWarOfPlayersProcess",
            "WarOfPlayersProcess",
            "RuntimeWarOfPlayersProcess");
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"MsgWarOfPlayers.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicClassPoleWarPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.TryGetClassPoleWarPole(
                sobNpc,
                out var scheduleName,
                out var requiredClassFamily,
                out var classGateName))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"{scheduleName} pole uid={sobNpc.Id} because ";
        var sourcePole = scheduleName + ".Furnitures[Pole]";

        if (requiredClassFamily != 0)
        {
            var classFamily = NormalizeLegacyClassPoleWarClassFamily(caster.Class);
            if (classFamily != requiredClassFamily)
            {
                failureDiagnostic =
                    diagnosticPrefix +
                    $"wrong class: AtributesStatus.{classGateName}(client.Player.Class) is false " +
                    $"for classFamily={classFamily}, requiredClassFamily={requiredClassFamily}";
                return true;
            }
        }

        if (TryReadLegacyClassPoleWarPoleHitpoints(
                caster.Attributes,
                scheduleName,
                out var poleHitpoints) &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + sourcePole + ".HitPoints is zero";
            return true;
        }

        if (string.Equals(caster.Name, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic = diagnosticPrefix + "client.Player.Name matches " + sourcePole + ".Name";
            return true;
        }

        var process = ReadLegacyClassPoleWarProcess(caster.Attributes, scheduleName);
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"{scheduleName}.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool TryReadLegacyClassPoleWarPoleHitpoints(
        IReadOnlyDictionary<string, string> attributes,
        string scheduleName,
        out ulong poleHitpoints)
    {
        return TryReadUInt64(
            attributes,
            out poleHitpoints,
            GetLegacyClassPoleWarAttributeNames(
                scheduleName,
                "HitPoints",
                "Hitpoints",
                "PoleHitPoints",
                "PoleHitpoints"));
    }

    private static string ReadLegacyClassPoleWarProcess(
        IReadOnlyDictionary<string, string> attributes,
        string scheduleName)
    {
        return ReadAttributeString(
            attributes,
            GetLegacyClassPoleWarAttributeNames(scheduleName, "Process"));
    }

    private static string[] GetLegacyClassPoleWarAttributeNames(
        string scheduleName,
        params string[] suffixes)
    {
        var aliases = GetLegacyClassPoleWarScheduleAliases(scheduleName);
        var names = new List<string>(aliases.Count * suffixes.Length * 3);
        foreach (var alias in aliases)
        {
            foreach (var suffix in suffixes)
            {
                names.Add("Legacy" + alias + suffix);
                names.Add(alias + suffix);
                names.Add("Runtime" + alias + suffix);
            }
        }

        return names.ToArray();
    }

    private static IReadOnlyList<string> GetLegacyClassPoleWarScheduleAliases(string scheduleName)
    {
        var withoutMsgPrefix = scheduleName.StartsWith("Msg", StringComparison.Ordinal)
            ? scheduleName[3..]
            : scheduleName;

        return scheduleName switch
        {
            "MsgClassPolaWar" => [scheduleName, withoutMsgPrefix, "MsgClassPoleWar", "ClassPoleWar"],
            "MsgThnderStrike" => [scheduleName, withoutMsgPrefix, "MsgThunderStrike", "ThunderStrike"],
            _ => [scheduleName, withoutMsgPrefix],
        };
    }

    private static int NormalizeLegacyClassPoleWarClassFamily(long classCode)
    {
        if (classCode <= 0)
        {
            return 0;
        }

        if (classCode >= 1_000)
        {
            return checked((int)(classCode / 1_000));
        }

        return classCode >= 10
            ? checked((int)(classCode / 10))
            : checked((int)classCode);
    }

    private static bool IsLegacySobNpcMagicWarFightersPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsWarFightersPole(sobNpc))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgWarFighters pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgWarFighters.Furnitures[Pole]";

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "LegacyMsgWarFightersHitPoints",
                "LegacyMsgWarFightersHitpoints",
                "LegacyMsgWarFightersPoleHitPoints",
                "LegacyMsgWarFightersPoleHitpoints",
                "MsgWarFightersHitPoints",
                "MsgWarFightersHitpoints",
                "MsgWarFightersPoleHitPoints",
                "MsgWarFightersPoleHitpoints",
                "RuntimeMsgWarFightersHitPoints",
                "RuntimeMsgWarFightersHitpoints",
                "RuntimeMsgWarFightersPoleHitPoints",
                "RuntimeMsgWarFightersPoleHitpoints",
                "LegacyWarFightersHitPoints",
                "LegacyWarFightersHitpoints",
                "LegacyWarFightersPoleHitPoints",
                "LegacyWarFightersPoleHitpoints",
                "WarFightersHitPoints",
                "WarFightersHitpoints",
                "WarFightersPoleHitPoints",
                "WarFightersPoleHitpoints",
                "RuntimeWarFightersHitPoints",
                "RuntimeWarFightersHitpoints",
                "RuntimeWarFightersPoleHitPoints",
                "RuntimeWarFightersPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + SourcePole + ".HitPoints is zero";
            return true;
        }

        if (string.Equals(caster.Name, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic = diagnosticPrefix + "client.Player.Name matches " + SourcePole + ".Name";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "LegacyMsgWarFightersProcess",
            "MsgWarFightersProcess",
            "RuntimeMsgWarFightersProcess",
            "LegacyWarFightersProcess",
            "WarFightersProcess",
            "RuntimeWarFightersProcess");
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"MsgWarFighters.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicSuperGuildWarPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsSuperGuildWarPole(sobNpc))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgSchedules.SuperGuildWar pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgSchedules.SuperGuildWar.Furnitures[Pole]";
        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic = diagnosticPrefix + "client.Player.MyGuild is null";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                GetLegacySuperGuildWarAttributeNames(
                    "HitPoints",
                    "Hitpoints",
                    "PoleHitPoints",
                    "PoleHitpoints")) &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + SourcePole + ".HitPoints is zero";
            return true;
        }

        var winnerGuildId = ReadAttributeUInt64(
            caster.Attributes,
            GetLegacySuperGuildWarAttributeNames("WinnerGuildID", "WinnerGuildId"));
        if (winnerGuildId != 0 &&
            winnerGuildId == guildId)
        {
            failureDiagnostic =
                diagnosticPrefix +
                $"client.Player.GuildID matches MsgSchedules.SuperGuildWar.Winner.GuildID={winnerGuildId}";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            GetLegacySuperGuildWarAttributeNames("Process", "Proces"));
        if (string.IsNullOrWhiteSpace(process) ||
            process.Equals("Dead", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("Idle", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"MsgSchedules.SuperGuildWar.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static string[] GetLegacySuperGuildWarAttributeNames(params string[] suffixes)
    {
        string[] aliases = ["MsgSuperGuildWar", "SuperGuildWar", "MsgSchedulesSuperGuildWar"];
        var names = new List<string>(aliases.Length * suffixes.Length * 3);
        foreach (var alias in aliases)
        {
            foreach (var suffix in suffixes)
            {
                names.Add("Legacy" + alias + suffix);
                names.Add(alias + suffix);
                names.Add("Runtime" + alias + suffix);
            }
        }

        return names.ToArray();
    }

    private static bool IsLegacySobNpcMagicEmperorWarPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsEmperorWarPole(sobNpc))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgEmperorWar pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgEmperorWar.Furnitures[Pole]";
        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic = diagnosticPrefix + "caster has no guild";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "LegacyMsgEmperorWarHitPoints",
                "LegacyMsgEmperorWarHitpoints",
                "LegacyMsgEmperorWarPoleHitPoints",
                "LegacyMsgEmperorWarPoleHitpoints",
                "MsgEmperorWarHitPoints",
                "MsgEmperorWarHitpoints",
                "MsgEmperorWarPoleHitPoints",
                "MsgEmperorWarPoleHitpoints",
                "LegacyEmperorWarHitPoints",
                "LegacyEmperorWarHitpoints",
                "LegacyEmperorWarPoleHitPoints",
                "LegacyEmperorWarPoleHitpoints",
                "EmperorWarHitPoints",
                "EmperorWarHitpoints",
                "EmperorWarPoleHitPoints",
                "EmperorWarPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + SourcePole + ".HitPoints is zero";
            return true;
        }

        if (string.Equals(caster.Name, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic = diagnosticPrefix + "client.Player.Name matches " + SourcePole + ".Name";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            GetLegacyEmperorWarProcessAttributeNames());
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"MsgEmperorWar.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicGuildWarScorePoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsGuildWarScorePole(sobNpc))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"GuildWarScore pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgSchedules.GuildWarScore.Furnitures[Pole]";
        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic = diagnosticPrefix + "caster has no guild";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "LegacyMsgGuildWarScorePoleHitPoints",
                "LegacyMsgGuildWarScorePoleHitpoints",
                "MsgGuildWarScorePoleHitPoints",
                "MsgGuildWarScorePoleHitpoints",
                "LegacyGuildWarScorePoleHitPoints",
                "LegacyGuildWarScorePoleHitpoints",
                "GuildWarScorePoleHitPoints",
                "GuildWarScorePoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + SourcePole + ".HitPoints is zero";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            GetLegacyGuildWarScoreProcessAttributeNames());
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"MsgSchedules.GuildWarScore.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicClanWarScorePoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsClanWarScorePole(sobNpc))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"ClanWarScore pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgSchedules.ClanWarScore.Furnitures[Pole]";
        var clanId = ReadAttributeUInt64(caster.Attributes, "ClanID", "ClanUID");
        if (clanId == 0)
        {
            failureDiagnostic = diagnosticPrefix + "caster has no clan";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "LegacyMsgClanWarScorePoleHitPoints",
                "LegacyMsgClanWarScorePoleHitpoints",
                "MsgClanWarScorePoleHitPoints",
                "MsgClanWarScorePoleHitpoints",
                "RuntimeMsgClanWarScorePoleHitPoints",
                "RuntimeMsgClanWarScorePoleHitpoints",
                "LegacyClanWarScorePoleHitPoints",
                "LegacyClanWarScorePoleHitpoints",
                "ClanWarScorePoleHitPoints",
                "ClanWarScorePoleHitpoints",
                "RuntimeClanWarScorePoleHitPoints",
                "RuntimeClanWarScorePoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + SourcePole + ".HitPoints is zero";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            GetLegacyClanWarScoreProcessAttributeNames());
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase) &&
            !process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) &&
            process != "1")
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"MsgSchedules.ClanWarScore.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicChampionsOfWarrPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.IsChampionsOfWarrPole(sobNpc))
        {
            return false;
        }

        var diagnosticPrefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"ChampionsOfWarr pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgSchedules.ChampionsOfWarr.Furnitures[Pole]";
        var guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            failureDiagnostic = diagnosticPrefix + "caster has no guild";
            return true;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "LegacyMsgChampionsOfWarrPoleHitPoints",
                "LegacyMsgChampionsOfWarrPoleHitpoints",
                "MsgChampionsOfWarrPoleHitPoints",
                "MsgChampionsOfWarrPoleHitpoints",
                "RuntimeMsgChampionsOfWarrPoleHitPoints",
                "RuntimeMsgChampionsOfWarrPoleHitpoints",
                "LegacyChampionsOfWarrPoleHitPoints",
                "LegacyChampionsOfWarrPoleHitpoints",
                "ChampionsOfWarrPoleHitPoints",
                "ChampionsOfWarrPoleHitpoints",
                "RuntimeChampionsOfWarrPoleHitPoints",
                "RuntimeChampionsOfWarrPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic = diagnosticPrefix + SourcePole + ".HitPoints is zero";
            return true;
        }

        var guildName = ReadAttributeString(caster.Attributes, "GuildName", "LegacyGuildName");
        if (!string.IsNullOrWhiteSpace(guildName) &&
            string.Equals(guildName, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic =
                diagnosticPrefix + "client.Player.MyGuild.GuildName matches " + SourcePole + ".Name";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "LegacyMsgChampionsOfWarrProcess",
            "MsgChampionsOfWarrProcess",
            "RuntimeMsgChampionsOfWarrProcess",
            "LegacyChampionsOfWarrProcess",
            "ChampionsOfWarrProcess",
            "RuntimeChampionsOfWarrProcess");
        if (!process.Equals("Alive", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic = diagnosticPrefix + $"MsgSchedules.ChampionsOfWarr.Proces is {sourceProcess}";
            return true;
        }

        return false;
    }

    private static bool IsLegacySobNpcMagicNobilityPoleAttackBlocked(
        CharacterSnapshot caster,
        SobNpc sobNpc,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!LegacySobNpcEventRules.TryGetNobilityPoleSchedule(
                sobNpc,
                out var scheduleName,
                out var requiredRank))
        {
            return false;
        }

        if (TryReadUInt64(
                caster.Attributes,
                out var poleHitpoints,
                "Legacy" + scheduleName + "PoleHitPoints",
                "Legacy" + scheduleName + "PoleHitpoints",
                scheduleName + "PoleHitPoints",
                scheduleName + "PoleHitpoints",
                "LegacyNobilityPoleHitPoints",
                "LegacyNobilityPoleHitpoints",
                "NobilityPoleHitPoints",
                "NobilityPoleHitpoints") &&
            poleHitpoints == 0)
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because MsgSchedules.{scheduleName}.Furnitures[Pole].HitPoints is zero";
            return true;
        }

        if (string.Equals(caster.Name, sobNpc.Name, StringComparison.Ordinal))
        {
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because pole owner name matches caster name";
            return true;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "Legacy" + scheduleName + "Process",
            scheduleName + "Process",
            "Runtime" + scheduleName + "Process",
            "LegacyNobilityPoleProcess",
            "NobilityPoleProcess",
            "RuntimeNobilityPoleProcess");
        if (string.IsNullOrWhiteSpace(process) ||
            process.Equals("Dead", StringComparison.OrdinalIgnoreCase))
        {
            var sourceProcess = string.IsNullOrWhiteSpace(process) ? "Dead" : process;
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because MsgSchedules.{scheduleName}.Proces is {sourceProcess}";
            return true;
        }

        var rank = ReadLegacyNobilityRank(caster);
        if (rank != requiredRank)
        {
            var requiredRankName = LegacyNobilityRanking.RankName(requiredRank);
            failureDiagnostic =
                $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
                $"{scheduleName} pole uid={sobNpc.Id} because caster NobilityRank={rank} is not {requiredRankName}";
            return true;
        }

        return false;
    }

    private static IEnumerable<LegacyPlayerMagicTarget> EnumerateVisiblePlayerMagicTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        if (context.Sessions is not { } sessions)
        {
            yield break;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeePlayer(session.Character, active.Session.Character) ||
                !CanLegacyPlayerMagicDamageTarget(context, session, active.Session, spell, out _))
            {
                continue;
            }

            yield return new LegacyPlayerMagicTarget(active);
        }
    }

    private static bool CanLegacyPlayerMagicDamageTarget(
        WorldPacketHandlerContext context,
        WorldSession caster,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic)
    {
        return CanLegacyPlayerMagicDamageTarget(
            context,
            caster,
            target,
            spell,
            out failureDiagnostic,
            out _);
    }

    private static bool CanLegacyPlayerMagicDamageTarget(
        WorldPacketHandlerContext context,
        WorldSession caster,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic,
        out LegacyPlayerMagicGateFailureEffect failureEffect)
    {
        return CanLegacyPlayerMagicDamageTarget(
            context, caster, target, spell, out failureDiagnostic, out failureEffect,
            out _, deferFinalBlockAttackMap: false);
    }

    private static bool CanLegacyPlayerMagicDamageTarget(
        WorldPacketHandlerContext context,
        WorldSession caster,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic,
        out LegacyPlayerMagicGateFailureEffect failureEffect,
        out bool reachedFlashingNameStage,
        bool deferFinalBlockAttackMap)
    {
        return CanLegacyPlayerMagicDamageTarget(
            context, caster, target, spell, out failureDiagnostic, out failureEffect,
            out reachedFlashingNameStage, out _, deferFinalBlockAttackMap);
    }

    private static bool CanLegacyPlayerMagicDamageTarget(
        WorldPacketHandlerContext context,
        WorldSession caster,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic,
        out LegacyPlayerMagicGateFailureEffect failureEffect,
        out bool reachedFlashingNameStage,
        out bool safeZoneDenied,
        bool deferFinalBlockAttackMap)
    {
        safeZoneDenied = false;
        reachedFlashingNameStage = false;
        failureDiagnostic = string.Empty;
        failureEffect = LegacyPlayerMagicGateFailureEffect.None;
        if (TryGetLegacyCombatGearDamageBlockReason(target.Character, out var reason))
        {
            failureDiagnostic =
                $"target legacy CombatGear CheckDamage blocked player magic reason={reason}; " +
                "source=CanAttackPlayer.Verified attacked.Owner.CombatGear.CheckDamage";
            return false;
        }

        if (IsLegacyPlayerMagicImmortalForceBlocked(
                context,
                caster,
                target,
                spell,
                out failureDiagnostic,
                out failureEffect))
        {
            return false;
        }

        if (IsLegacyPlayerMagicHellVortexCounterBlocked(context, target, out failureDiagnostic))
        {
            failureEffect = LegacyPlayerMagicGateFailureEffect.HellVortexCounter;
            return false;
        }

        if (TryGetLegacyPlayerMagicSafeZone(
                caster.Character.MapId,
                target.Character.X,
                target.Character.Y,
                out var safeZone))
        {
            safeZoneDenied = true;
            failureDiagnostic =
                $"map={caster.Character.MapId} targetX={target.Character.X} targetY={target.Character.Y}; " +
                $"source CanAttackPlayer.Verified rejected {safeZone} safe zone: You can't PK in the safe zone";
            return false;
        }

        if (target.Character.MinHitPoints <= 0 ||
            HasLegacyStatusFlag(target.Character, LegacyDeadFlag) ||
            HasLegacyStatusFlag(target.Character, LegacyGhostFlag))
        {
            failureDiagnostic = "target is dead or ghosted";
            return false;
        }

        if (ReadAttributeBool(target.Character.Attributes, "Invisible"))
        {
            failureDiagnostic =
                "target Invisible attribute is set; source CanAttackPlayer.Verified rejected attacked.Invisible";
            return false;
        }

        if (HasLegacyStatusFlag(target.Character, LegacyNeptuneCurseFlag))
        {
            failureDiagnostic = "target has NeptuneCurse";
            return false;
        }

        if (HasLegacyStatusFlag(target.Character, LegacyFlyFlag) &&
            !spell.AttackInFly)
        {
            failureDiagnostic =
                "target is flying and spell AttackInFly is false; source CanAttackPlayer.Verified rejected !DBSpell.AttackInFly";
            return false;
        }

        if (HasLegacyPlayerMagicWatchingAttribute(target.Character))
        {
            failureDiagnostic =
                "target watching attribute is set; source CanAttackPlayer.Verified rejected attacked.Owner.IsWatching()";
            return false;
        }

        if (HasLegacyPlayerMagicWatchingAttribute(caster.Character))
        {
            failureDiagnostic =
                "caster watching attribute is set; source CanAttackPlayer.Verified rejected client.IsWatching()";
            return false;
        }

        if (IsLegacyPlayerAttackProtected(target.Character, DateTimeOffset.UtcNow))
        {
            failureDiagnostic = "target legacy Protect window is active";
            return false;
        }

        if (target.Character.MapId == 700 &&
            ReadCharacterDynamicId(target.Character) == 0)
        {
            failureDiagnostic = "target map=700 dynamicId=0 blocks player-target magic";
            return false;
        }

        var pkMode = ReadAttributeUInt64(caster.Character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
        if (pkMode == LegacyPkModePeace)
        {
            failureDiagnostic = "caster is in Peace PK mode";
            return false;
        }

        if (IsLegacyPlayerMagicSolidnessBlocked(context, target, spell, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicTournamentStateBlocked(caster.Character, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicCaptureTheFlagAttackBlocked(caster.Character, target.Character, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicSameGarmentMapBlocked(caster, target, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicFriendlyFireBlocked(context, caster, target, pkMode, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicBlockedMap(caster.Character.MapId, pkMode))
        {
            failureDiagnostic = $"map={caster.Character.MapId} blocks player-target magic";
            return false;
        }

        if (IsLegacyPlayerMagicTeamQualifierSameTeamBlocked(context, caster, target, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicOwnServerBlocked(caster.Character, out failureDiagnostic))
        {
            return false;
        }

        if (pkMode == LegacyPkModeCapture)
        {
            if (HasLegacyStatusFlag(target.Character, LegacyFlashingNameStatusFlag) ||
                HasLegacyStatusFlag(target.Character, LegacyBlackNameStatusFlag))
            {
                return true;
            }

            failureDiagnostic = "caster is in Capture PK mode and target is not FlashingName or BlackName";
            return false;
        }

        if (pkMode == LegacyPkModeJiang &&
            TryApplyLegacyPlayerMagicJiangPkGate(caster.Character, target.Character, out var canJiangAttack, out failureDiagnostic))
        {
            return canJiangAttack;
        }

        // Capture/Jiang returned above: both FlashingName and the final map gate are bypassed.
        reachedFlashingNameStage = true;
        if (!deferFinalBlockAttackMap &&
            IsLegacyPlayerMagicBlockAttackMapBlocked(caster.Character, out failureDiagnostic))
        {
            return false;
        }

        return true;
    }

    private static bool IsLegacyPlayerMagicHellVortexCounterBlocked(
        WorldPacketHandlerContext context,
        WorldSession target,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (context.WorldData is not { } worldData ||
            !HasLegacyNinjaHellVortexEquipped(target.Character) ||
            !CheckLegacyHellVortexRateRoll(target.Character) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                target.PlayerSpells,
                worldData,
                LegacyHellVortexSpellId,
                out _,
                out var hellVortexSpell) ||
            !IsLegacyHellVortexSpell(hellVortexSpell))
        {
            return false;
        }

        failureDiagnostic =
            "source CanAttackPlayer.Verified HellVortex counter rejected incoming player magic";
        return true;
    }

    private static bool IsLegacyPlayerMagicImmortalForceBlocked(
        WorldPacketHandlerContext context,
        WorldSession caster,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic,
        out LegacyPlayerMagicGateFailureEffect failureEffect)
    {
        failureDiagnostic = string.Empty;
        failureEffect = LegacyPlayerMagicGateFailureEffect.None;
        if (!HasLegacyStatusFlag(target.Character, LegacyImmortalForceFlag) ||
            HasLegacyStatusFlag(caster.Character, LegacyRiseOfTaoismFlag) ||
            spell.Id == LegacyDevouringStrikeSpellId)
        {
            return false;
        }

        if (TryGetLegacyEquippedRuneLevel(caster.PlayerItems, context.WorldData, "Overwhelm", out var overwhelmLevel) &&
            CheckLegacyPlayerMagicOverwhelmRateRoll(caster.Character, overwhelmLevel))
        {
            return false;
        }

        failureDiagnostic =
            "source CanAttackPlayer.Verified ImmortalForce rejected target because Overwhelm did not bypass";
        failureEffect = LegacyPlayerMagicGateFailureEffect.ForbiddenPirate;
        return true;
    }

    private static bool CheckLegacyPlayerMagicOverwhelmRateRoll(CharacterSnapshot caster, byte itemLevel)
    {
        var rate = itemLevel switch
        {
            >= 1 and <= 7 => checked((uint)(10 + itemLevel * 5)),
            8 => 60U,
            9 => 70U,
            _ => 0U,
        };
        if (rate == 0)
        {
            return false;
        }

        var roll = TryReadUInt32Attribute(caster.Attributes, out var forcedRoll, RuntimeLegacyOverwhelmRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < rate;
    }

    private static bool IsLegacyPlayerMagicSolidnessBlocked(
        WorldPacketHandlerContext context,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!IsLegacyPlayerMagicSolidnessSpell(spell.Id) ||
            !TryGetLegacyEquippedRuneLevel(target.PlayerItems, context.WorldData, "Solidness", out var solidnessLevel) ||
            !CheckLegacyPlayerMagicSolidnessRateRoll(target.Character, solidnessLevel))
        {
            return false;
        }

        failureDiagnostic =
            $"source CanAttackPlayer.Verified Solidness rejected spellId={spell.Id} level={solidnessLevel}";
        return true;
    }

    private static bool IsLegacyPlayerMagicSolidnessSpell(ushort spellId)
    {
        return spellId is 15_940 or 3_090 or 16_550 or 16_540 or 16_520 or 16_530 or 1_165;
    }

    private static bool CheckLegacyPlayerMagicSolidnessRateRoll(CharacterSnapshot target, byte itemLevel)
    {
        var rate = itemLevel switch
        {
            >= 1 and <= 7 => checked((uint)(itemLevel * 10)),
            8 => 85U,
            9 => 100U,
            _ => 0U,
        };
        if (rate == 0)
        {
            return false;
        }

        var roll = TryReadUInt32Attribute(target.Attributes, out var forcedRoll, RuntimeLegacySolidnessRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < rate;
    }

    private static bool IsLegacyPlayerMagicTournamentStateBlocked(
        CharacterSnapshot caster,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (!ReadAttributeBool(
                caster.Attributes,
                "LegacyTournamentInTournament",
                "TournamentInTournament",
                "InTournament"))
        {
            return false;
        }

        var tournamentType = ReadAttributeString(
            caster.Attributes,
            "LegacyTournamentType",
            "TournamentType",
            "CurrentTournamentType");
        if (!IsLegacyPlayerMagicBlockingTournamentType(tournamentType))
        {
            return false;
        }

        var process = ReadAttributeString(
            caster.Attributes,
            "LegacyTournamentProcess",
            "TournamentProcess",
            "CurrentTournamentProcess");
        if (process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1")
        {
            return false;
        }

        failureDiagnostic =
            $"source CanAttackPlayer.Verified tournament type={tournamentType} process={process} rejected non-alive caster";
        return true;
    }

    private static bool IsLegacyPlayerMagicBlockingTournamentType(string tournamentType)
    {
        return tournamentType.Equals("LastManStand", StringComparison.OrdinalIgnoreCase) ||
            tournamentType.Equals("TopFight", StringComparison.OrdinalIgnoreCase) ||
            tournamentType.Equals("KillerOfElite", StringComparison.OrdinalIgnoreCase) ||
            tournamentType.Equals("ExtremePk", StringComparison.OrdinalIgnoreCase) ||
            tournamentType.Equals("DragonWar", StringComparison.OrdinalIgnoreCase) ||
            tournamentType.Equals("TeamDeathMatch", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyPlayerMagicCaptureTheFlagAttackBlocked(
        CharacterSnapshot caster,
        CharacterSnapshot target,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (caster.MapId != LegacyCaptureTheFlagMapId)
        {
            return false;
        }

        if (TryReadAttributeBool(
                target.Attributes,
                out var targetAttackable,
                "LegacyCaptureTheFlagAttackable",
                "CaptureTheFlagAttackable",
                "CtfAttackable",
                "CTFAttackable") &&
            targetAttackable)
        {
            return false;
        }

        failureDiagnostic =
            $"source CanAttackPlayer.Verified map={LegacyCaptureTheFlagMapId} " +
            $"rejected CaptureTheFlag.Attackable targetUid={target.Uid}";
        return true;
    }

    private static bool IsLegacyPlayerMagicSameGarmentMapBlocked(
        WorldSession caster,
        WorldSession target,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (caster.Character.MapId != LegacySameGarmentPkMapId)
        {
            return false;
        }

        var casterGarmentId = GetLegacyPlayerMagicGarmentId(caster);
        var targetGarmentId = GetLegacyPlayerMagicGarmentId(target);
        if (casterGarmentId != targetGarmentId)
        {
            return false;
        }

        failureDiagnostic =
            $"source CanAttackPlayer.Verified map=1082 rejected same garmentId={targetGarmentId}";
        return true;
    }

    private static uint GetLegacyPlayerMagicGarmentId(WorldSession session)
    {
        var specialGarment = ReadAttributeUInt64(session.Character.Attributes, "SpecialGarment", "GarmentId", "GarmentID");
        if (specialGarment != 0)
        {
            return checked((uint)Math.Min(specialGarment, uint.MaxValue));
        }

        var garmentPosition = session.AlternateEquipmentActive
            ? LegacyAlternateGarmentEquipmentPosition
            : LegacyGarmentEquipmentPosition;

        foreach (var item in session.PlayerItems.EquipmentItems)
        {
            if (item.Position == garmentPosition)
            {
                return item.ItemId;
            }
        }

        if (session.AlternateEquipmentActive)
        {
            foreach (var item in session.PlayerItems.EquipmentItems)
            {
                if (item.Position == LegacyGarmentEquipmentPosition)
                {
                    return item.ItemId;
                }
            }
        }

        return 0;
    }

    private static bool TryApplyLegacyPlayerMagicJiangPkGate(
        CharacterSnapshot caster,
        CharacterSnapshot target,
        out bool canAttack,
        out string failureDiagnostic)
    {
        canAttack = false;
        failureDiagnostic = string.Empty;

        if (!ReadAttributeBool(target.Attributes, "JiangHuActive", "JiangActive"))
        {
            failureDiagnostic =
                "source CanAttackPlayer.Verified Jiang PK rejected target because JiangHuActive is not active";
            return true;
        }

        if (!IsLegacyPlayerMagicJiangPkMap(caster.MapId))
        {
            return false;
        }

        var jiangPkFlag = ReadAttributeUInt64(caster.Attributes, "JiangPkFlag", "JiangPkFlags");
        if (jiangPkFlag != LegacyJiangAttackFlagNone)
        {
            if ((jiangPkFlag & LegacyJiangAttackFlagNotHitFriends) != 0)
            {
                if (IsLegacyPlayerMagicFriend(caster, target))
                {
                    failureDiagnostic =
                        $"source CanAttackPlayer.Verified Jiang PK NotHitFriends rejected targetUid={target.Uid}";
                    return true;
                }

                canAttack = true;
                return true;
            }

            if ((jiangPkFlag & LegacyJiangAttackFlagNoHitAlliesClan) != 0 &&
                ReadAttributeUInt64(caster.Attributes, "ClanID", "ClanUID", "ClanId", "ClanUid") != 0)
            {
                if (IsLegacyPlayerMagicClanAlly(caster, target, out var clanAllyId))
                {
                    failureDiagnostic =
                        $"source CanAttackPlayer.Verified Jiang PK NoHitAlliesClan rejected clanId={clanAllyId}";
                    return true;
                }

                canAttack = true;
                return true;
            }

            if ((jiangPkFlag & LegacyJiangAttackFlagNotHitAlliedGuild) != 0 &&
                ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId") != 0)
            {
                if (IsLegacyPlayerMagicGuildAlly(caster, target, out var guildAllyId))
                {
                    failureDiagnostic =
                        $"source CanAttackPlayer.Verified Jiang PK NotHitAlliedGuild rejected guildId={guildAllyId}";
                    return true;
                }

                canAttack = true;
                return true;
            }

            if ((jiangPkFlag & LegacyJiangAttackFlagNotHitClanMembers) != 0)
            {
                if (TryGetLegacySameClanId(caster, target, out var clanId))
                {
                    failureDiagnostic =
                        $"source CanAttackPlayer.Verified Jiang PK NotHitClanMembers rejected clanId={clanId}";
                    return true;
                }

                canAttack = true;
                return true;
            }

            if ((jiangPkFlag & LegacyJiangAttackFlagNotHitGuildMembers) != 0)
            {
                if (TryGetLegacySameGuildId(caster, target, out var guildId))
                {
                    failureDiagnostic =
                        $"source CanAttackPlayer.Verified Jiang PK NotHitGuildMembers rejected guildId={guildId}";
                    return true;
                }

                canAttack = true;
                return true;
            }
        }

        canAttack = true;
        return true;
    }

    private static bool IsLegacyPlayerMagicJiangPkMap(long mapId)
    {
        return mapId is 1_002 or 1_000 or 1_015 or 1_020 or 1_011;
    }

    private static bool IsLegacyPlayerMagicFriendlyFireBlocked(
        WorldPacketHandlerContext context,
        WorldSession caster,
        WorldSession target,
        ulong pkMode,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (pkMode == LegacyPkModeTeam)
        {
            if (context.TeamStates is { } teamStates &&
                teamStates.IsTeamMember(context.Connection, target.Character.Uid))
            {
                failureDiagnostic =
                    "source CanAttackPlayer.Verified Team PK rejected target because they are on the caster's team";
                return true;
            }

            if (IsLegacyPlayerMagicFriend(caster.Character, target.Character))
            {
                failureDiagnostic =
                    $"source CanAttackPlayer.Verified Team PK rejected target because targetUid={target.Character.Uid} is in caster Associate friends";
                return true;
            }

            if (TryGetLegacySameGuildId(caster.Character, target.Character, out var guildId))
            {
                failureDiagnostic =
                    $"source CanAttackPlayer.Verified Team PK rejected target because guildId={guildId} matches caster GuildID";
                return true;
            }

            if (IsLegacyPlayerMagicGuildAlly(caster.Character, target.Character, out var guildAllyId))
            {
                failureDiagnostic =
                    $"source CanAttackPlayer.Verified Team PK rejected target because guildId={guildAllyId} is allied with caster GuildID";
                return true;
            }

            if (TryGetLegacySameClanId(caster.Character, target.Character, out var clanId))
            {
                failureDiagnostic =
                    $"source CanAttackPlayer.Verified Team PK rejected target because clanId={clanId} matches caster ClanUID";
                return true;
            }

            if (IsLegacyPlayerMagicClanAlly(caster.Character, target.Character, out var clanAllyId))
            {
                failureDiagnostic =
                    $"source CanAttackPlayer.Verified Team PK rejected target because clanId={clanAllyId} is allied with caster ClanUID";
                return true;
            }
        }

        if (pkMode == LegacyPkModeGuild)
        {
            if (TryGetLegacySameGuildId(caster.Character, target.Character, out var sameGuildId))
            {
                failureDiagnostic =
                    $"source CanAttackPlayer.Verified Guild PK rejected target because guildId={sameGuildId} matches caster GuildID";
                return true;
            }

            if (IsLegacyPlayerMagicGuildAlly(caster.Character, target.Character, out var guildAllyId))
            {
                failureDiagnostic =
                    $"source CanAttackPlayer.Verified Guild PK rejected target because guildId={guildAllyId} is allied with caster GuildID";
                return true;
            }
        }

        if (pkMode == LegacyPkModeCs &&
            TryGetLegacySameServerId(caster.Character, target.Character, out var serverId))
        {
            failureDiagnostic =
                $"source CanAttackPlayer.Verified CS PK rejected target because serverId={serverId} matches caster ServerID";
            return true;
        }

        if (pkMode == LegacyPkModeUnion &&
            IsLegacyPlayerMagicInUnion(caster.Character) &&
            IsLegacyPlayerMagicInUnion(target.Character) &&
            TryGetLegacySameUnionId(caster.Character, target.Character, out var unionId))
        {
            failureDiagnostic =
                $"source CanAttackPlayer.Verified Union PK rejected target because unionId={unionId} matches caster MyUnion.UID";
            return true;
        }

        return false;
    }

    private static bool IsLegacyPlayerMagicTeamQualifierSameTeamBlocked(
        WorldPacketHandlerContext context,
        WorldSession caster,
        WorldSession target,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (caster.Character.MapId != 700 ||
            !ReadAttributeBool(
                caster.Character.Attributes,
                "InTeamQualifier",
                "TeamQualifier",
                "InTeamQualifierMatch"))
        {
            return false;
        }

        if (context.TeamStates is { } teamStates &&
            teamStates.IsTeamMember(context.Connection, target.Character.Uid))
        {
            failureDiagnostic =
                $"source CanAttackPlayer.Verified TeamQualifier map=700 rejected same-team targetUid={target.Character.Uid}";
            return true;
        }

        if (target.Character.Uid >= 0 &&
            ContainsLegacyRelationId(
                caster.Character.Attributes,
                checked((ulong)target.Character.Uid),
                "TeamMemberUIDs",
                "TeamMemberUids",
                "TeamMemberIDs",
                "TeamMemberIds",
                "TeamMembers"))
        {
            failureDiagnostic =
                $"source CanAttackPlayer.Verified TeamQualifier map=700 rejected attribute same-team targetUid={target.Character.Uid}";
            return true;
        }

        return false;
    }

    private static bool IsLegacyPlayerMagicOwnServerBlocked(
        CharacterSnapshot caster,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (caster.MapId != 1_002 ||
            !TryReadAttributeBool(caster.Attributes, out var onOwnServer, "OnMyOwnServer", "OnOwnServer") ||
            onOwnServer)
        {
            return false;
        }

        failureDiagnostic =
            "source CanAttackPlayer.Verified map=1002 rejected caster because OnMyOwnServer is false";
        return true;
    }

    private static bool TryGetLegacySameServerId(
        CharacterSnapshot caster,
        CharacterSnapshot target,
        out ulong serverId)
    {
        serverId = ReadAttributeUInt64(caster.Attributes, "ServerID", "ServerId");
        return serverId != 0 &&
            serverId == ReadAttributeUInt64(target.Attributes, "ServerID", "ServerId");
    }

    private static bool TryGetLegacySameGuildId(
        CharacterSnapshot caster,
        CharacterSnapshot target,
        out ulong guildId)
    {
        guildId = ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId");
        return guildId != 0 &&
            guildId == ReadAttributeUInt64(target.Attributes, "GuildID", "GuildId");
    }

    private static bool IsLegacyPlayerMagicFriend(CharacterSnapshot caster, CharacterSnapshot target)
    {
        return target.Uid >= 0 &&
            ContainsLegacyRelationId(
                caster.Attributes,
                checked((ulong)target.Uid),
                "FriendUIDs",
                "FriendUids",
                "FriendIds",
                "Friends",
                "AssociateFriendUIDs",
                "AssociateFriendUids",
                "AssociateFriends");
    }

    private static bool IsLegacyPlayerMagicGuildAlly(
        CharacterSnapshot caster,
        CharacterSnapshot target,
        out ulong guildId)
    {
        guildId = ReadAttributeUInt64(target.Attributes, "GuildID", "GuildId");
        return guildId != 0 &&
            ReadAttributeUInt64(caster.Attributes, "GuildID", "GuildId") != 0 &&
            ContainsLegacyRelationId(
                caster.Attributes,
                guildId,
                "GuildAllyIDs",
                "GuildAllyIds",
                "GuildAllyGuildIDs",
                "GuildAllyGuildIds",
                "AllyGuildIDs",
                "AllyGuildIds");
    }

    private static bool TryGetLegacySameClanId(
        CharacterSnapshot caster,
        CharacterSnapshot target,
        out ulong clanId)
    {
        clanId = ReadAttributeUInt64(caster.Attributes, "ClanID", "ClanUID", "ClanId", "ClanUid");
        return clanId != 0 &&
            clanId == ReadAttributeUInt64(target.Attributes, "ClanID", "ClanUID", "ClanId", "ClanUid");
    }

    private static bool IsLegacyPlayerMagicClanAlly(
        CharacterSnapshot caster,
        CharacterSnapshot target,
        out ulong clanId)
    {
        clanId = ReadAttributeUInt64(target.Attributes, "ClanID", "ClanUID", "ClanId", "ClanUid");
        return clanId != 0 &&
            ReadAttributeUInt64(caster.Attributes, "ClanID", "ClanUID", "ClanId", "ClanUid") != 0 &&
            ContainsLegacyRelationId(
                caster.Attributes,
                clanId,
                "ClanAllyUIDs",
                "ClanAllyUids",
                "ClanAllyIDs",
                "ClanAllyIds",
                "AllyClanUIDs",
                "AllyClanUids",
                "AllyClanIDs",
                "AllyClanIds");
    }

    private static bool ContainsLegacyRelationId(
        IReadOnlyDictionary<string, string> attributes,
        ulong expected,
        params string[] keys)
    {
        if (expected == 0)
        {
            return false;
        }

        foreach (var key in keys)
        {
            if (!attributes.TryGetValue(key, out var raw) ||
                string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            foreach (var token in raw.Split(
                ['/', ',', ';', '|', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (ulong.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var relationId) &&
                    relationId == expected)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetLegacySameUnionId(
        CharacterSnapshot caster,
        CharacterSnapshot target,
        out ulong unionId)
    {
        unionId = ReadAttributeUInt64(caster.Attributes, "UnionUID", "UnionID", "UnionUid", "UnionId");
        return unionId != 0 &&
            unionId == ReadAttributeUInt64(target.Attributes, "UnionUID", "UnionID", "UnionUid", "UnionId");
    }

    private static bool IsLegacyPlayerMagicInUnion(CharacterSnapshot character)
    {
        if (TryReadAttributeBool(character.Attributes, out var inUnion, "InUnion"))
        {
            return inUnion;
        }

        return ReadAttributeUInt64(character.Attributes, "UnionUID", "UnionID", "UnionUid", "UnionId") != 0;
    }

    private static bool HasLegacyPlayerMagicWatchingAttribute(CharacterSnapshot character)
    {
        return ReadAttributeBool(
            character.Attributes,
            "Watching",
            "IsWatching",
            "ArenaWatching",
            "RuntimeWatching");
    }

    private static bool TryGetLegacyPlayerMagicSafeZone(
        long mapId,
        long targetX,
        long targetY,
        out string safeZone)
    {
        if (mapId == 10_137 && targetX < 145)
        {
            safeZone = "DragonIsland";
            return true;
        }

        if (mapId == 10_250 && targetX + targetY >= 2_267 && targetX < 1_100)
        {
            safeZone = "DeityLand";
            return true;
        }

        safeZone = string.Empty;
        return false;
    }

    private static bool IsLegacyPlayerMagicBlockedMap(long mapId, ulong pkMode)
    {
        if (mapId is 1_004 or 1_858 or 4_000 or 4_003 or 4_006 or 4_008 or 4_009 or 4_020)
        {
            return true;
        }

        return mapId == 1_002 && pkMode != LegacyPkModeJiang;
    }

    private static bool IsLegacyPlayerMagicBlockAttackMapBlocked(
        CharacterSnapshot caster,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        var dynamicId = ReadCharacterDynamicId(caster);
        if (dynamicId == 0 && LegacyBlockAttackMapIds.Contains(caster.MapId))
        {
            failureDiagnostic =
                $"map={caster.MapId} dynamicId=0; " +
                "source CanAttackPlayer.Verified Constants.BlockAttackMap rejected mapId+dynamicId=0";
            return true;
        }

        if (dynamicId != 0 &&
            dynamicId <= long.MaxValue &&
            LegacyBlockAttackMapIds.Contains((long)dynamicId))
        {
            failureDiagnostic =
                $"map={caster.MapId} dynamicId={dynamicId}; " +
                "source CanAttackPlayer.Verified Constants.BlockAttackMap rejected dynamicId match";
            return true;
        }

        return false;
    }

    private static IEnumerable<LegacyPlayerMonsterMagicTarget> EnumerateVisibleMonsterTargets(
        WorldPacketHandlerContext context,
        CharacterSnapshot character)
    {
        if (character.MapId is < int.MinValue or > int.MaxValue)
        {
            yield break;
        }

        var mapId = (int)character.MapId;
        var now = DateTimeOffset.UtcNow;
        if (context.WorldData is { } worldData &&
            worldData.MonsterSpawnsByMapId.TryGetValue(mapId, out var spawns))
        {
            foreach (var entry in LegacyStaticMonsterMaterializer.Enumerate(worldData, spawns))
            {
                var visible = entry.Instance;
                if (context.MonsterStates is { } monsterStates &&
                    !monsterStates.TryGetVisibleInstance(entry.Instance, now, out visible))
                {
                    continue;
                }

                if (CanSeeMonster(character, visible) &&
                    CanLegacyPlayerAttackMonster(character, visible, entry.MonsterFamily, out _))
                {
                    yield return new LegacyPlayerMonsterMagicTarget(visible, entry.MonsterFamily);
                }
            }
        }

        if (context.MonsterStates is not { } dynamicMonsterStates)
        {
            yield break;
        }

        foreach (var dynamicEntry in dynamicMonsterStates.GetVisibleDynamicMonsters(mapId, ReadCharacterDynamicId(character), now))
        {
            if (CanSeeMonster(character, dynamicEntry.Instance) &&
                CanLegacyPlayerAttackMonster(character, dynamicEntry.Instance, dynamicEntry.MonsterFamily, out _))
            {
                yield return new LegacyPlayerMonsterMagicTarget(dynamicEntry.Instance, dynamicEntry.MonsterFamily);
            }
        }
    }

    private static bool TryFindVisibleMonsterTargetByUid(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint uid,
        out LegacyPlayerMonsterMagicTarget target)
    {
        target = default;
        if (!TryFindMonsterInstanceByUid(context, character, uid, out var candidate, out var monsterFamily))
        {
            return false;
        }

        var visible = candidate;
        if (context.MonsterStates is { } monsterStates &&
            !monsterStates.TryGetVisibleInstance(candidate, DateTimeOffset.UtcNow, out visible))
        {
            return false;
        }

        if (!CanSeeMonster(character, visible))
        {
            return false;
        }

        target = new LegacyPlayerMonsterMagicTarget(visible, monsterFamily);
        return true;
    }

    private static bool IsLegacyInLine(
        long casterX,
        long casterY,
        long originX,
        long originY,
        long targetX,
        long targetY,
        byte range,
        byte width)
    {
        var deltaX = originX - casterX;
        var deltaY = originY - casterY;
        var divisor = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        if (divisor == 0)
        {
            return LegacyGetDistance(casterX, casterY, targetX, targetY) <= width;
        }

        var stepX = (double)deltaX / divisor;
        var stepY = (double)deltaY / divisor;
        var x = (double)casterX;
        var y = (double)casterY;

        if (LegacyGetDistance((long)Math.Round(x), (long)Math.Round(y), targetX, targetY) <= width)
        {
            return true;
        }

        for (var index = 0; index < range; index++)
        {
            x += stepX;
            y += stepY;
            if (LegacyGetDistance((long)Math.Round(x), (long)Math.Round(y), targetX, targetY) <= width)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideLegacySector(
        long casterX,
        long casterY,
        long originX,
        long originY,
        long targetX,
        long targetY,
        int sectorSize,
        byte distance)
    {
        if (LegacyGetDistance(casterX, casterY, targetX, targetY) > distance)
        {
            return false;
        }

        if (sectorSize >= 360)
        {
            return true;
        }

        var facing = GetLegacyDegrees(casterX, casterY, originX, originY);
        var target = GetLegacyDegrees(casterX, casterY, targetX, targetY);
        var delta = Math.Abs(facing - target);
        delta = Math.Min(delta, 360 - delta);
        return delta <= sectorSize / 2.0;
    }

    private static double GetLegacyDegrees(long sourceX, long sourceY, long targetX, long targetY)
    {
        var degrees = Math.Atan2(targetY - sourceY, targetX - sourceX) * 180.0 / Math.PI;
        return degrees < 0 ? degrees + 360.0 : degrees;
    }
}
