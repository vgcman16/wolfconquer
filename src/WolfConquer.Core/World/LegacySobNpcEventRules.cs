using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacySobNpcEventRules
{
    // Mirrors legacy MrRayzo.Role.SobNpc.Die and MrRayzo.Database.NpcServer map 1039 handling.
    public const int TrainingMapId = 1_039;

    // Mirrors legacy CanAttackNpc.Verified map 3844 fast allow.
    public const int CanAttackNpcAlwaysAllowedMapId = 3_844;

    // Mirrors legacy MrRayzo.GUILDWAR.NPCID seed bindings.
    public const int GuildWarMapId = 1_038;
    public const int GuildWarPillarUid = 810;
    public const int GuildWarRightGateUid = 101_314;
    public const int GuildWarLeftGateUid = 101_315;
    public const int GuildWarTenacityPillarUid = 100_942;
    public const int GuildWarExorcimPillarUid = 101_097;
    public const int GuildWarHonorPillarUid = 100_900;
    public const int ClanWarPoleUid = 890;
    public const int TwinCityClanWarMapId = 1_505;
    public const int PhoenixCastleClanWarMapId = 1_509;
    public const int ApeCityClanWarMapId = 1_506;
    public const int DesertCityClanWarMapId = 1_508;
    public const int BirdIslandClanWarMapId = 1_507;
    public const int UnionWarMapId = 22_348;
    public const int UnionWarPoleUid = 22_348;
    public const int TwinCityClanDominationPoleUid = 1_002;
    public const int PhoenixCastleClanDominationPoleUid = 1_011;
    public const int ApeCityClanDominationPoleUid = 1_020;
    public const int DesertCityClanDominationPoleUid = 1_000;
    public const int BirdIslandClanDominationPoleUid = 1_015;
    public const int PoleDominationPoleUid = 830;
    public const int PoleDominationBirdIslandPoleUid = 831;
    public const int PoleDominationDesertCityPoleUid = 832;
    public const int PoleDominationPhoenixCastlePoleUid = 833;
    public const int NobilityPoleMapId = 11_140;
    public const int NobilityKingPoleUid = 22_340;
    public const int NobilityPrincePoleUid = 22_341;
    public const int NobilityDukePoleUid = 22_342;
    public const int NobilityEarlPoleUid = 22_343;
    public const int EliteGuildWarMapId = 6_521;
    public const int EliteGuildWarPoleUid = 6_539;
    public const int EliteGuildWarSecondPoleUid = 6_540;
    public const int EliteGuildWarThirdPoleUid = 6_541;
    public const int TrepilWarMapId = 3_838;
    public const int TrepilWarPoleUid = 65_046;
    public const int TopPoleMapId = 1_737;
    public const int TopPoleUid = 65_056;
    public const int WarOfPlayersMapId = 6_729;
    public const int WarOfPlayersPoleUid = 6_606;
    public const int ClassPoleWarMapId = 10_005;
    public const int ClassPoleWarWarriorGodPoleUid = 8_001;
    public const int ClassPoleWarArcherMasterPoleUid = 8_002;
    public const int ClassPoleWarNinjaMasterPoleUid = 9_803;
    public const int ClassPoleWarMonkMasterPoleUid = 8_004;
    public const int ClassPoleWarTrojanMasterPoleUid = 8_005;
    public const int ClassPoleWarTaoistMasterPoleUid = 8_006;
    public const int ClassPoleWarFireMasterPoleUid = 8_007;
    public const int ClassPoleWarPirateMasterPoleUid = 8_008;
    public const int ClassPoleWarKungfuKingPoleUid = 8_009;
    public const int ClassPoleWarWindWalkerPoleUid = 9_011;
    public const int ClassPoleWarClassPolaWarPoleUid = 8_012;
    public const int ClassPoleWarThnderStrikePoleUid = 90_111;
    public const int WarFightersMapId = 11_073;
    public const int WarFightersPoleUid = 9_406;
    public const int SuperGuildWarMapId = 3_868;
    public const int SuperGuildWarPoleUid = 820;
    public const int EmperorWarMapId = 10_702;
    public const int EmperorWarPoleUid = 6_505;
    public const int GuildWarScoreMapId = 6_784;
    public const int GuildWarScorePoleUid = 8_447;
    public const int ClanWarScoreMapId = 3_839;
    public const int ClanWarScorePoleUid = 8_448;
    public const int ChampionsOfWarrMapId = 6_546;
    public const int ChampionsOfWarrPoleUid = 6_547;
    public const int Guild6PoleWarMapId = 10_070;
    public const int Guild6PoleWarPoleUid = 100_731;
    public const int Guild6PoleWar2PoleUid = 100_732;
    public const int Guild6PoleWar3PoleUid = 100_733;
    public const int Guild6PoleWar4PoleUid = 100_734;
    public const int Guild6PoleWar5PoleUid = 100_735;
    public const int Guild6PoleWar6PoleUid = 100_736;

    // Mirrors legacy MrRayzo.Role.Flags.NpcType.
    public const int PoleKind = 10;
    public const int StakeKind = 21;
    public const int ScarecrowKind = 22;
    public const int FurnitureKind = 25;
    public const int GateKind = 26;
    public const int GuildWarCapturePillarKind = 64;

    // Mirrors legacy MrRayzo.GUILDWAR.StaticMesh gate mesh values.
    public const int GuildWarRightGateClose1Mesh = 0x5BB;
    public const int GuildWarRightGateClose2Mesh = 0x5F7;
    public const int GuildWarRightGateClose3Mesh = 0x633;
    public const int GuildWarRightGateClose4Mesh = 0x66F;
    public const int GuildWarLeftGateClose1Mesh = 0x5D3;
    public const int GuildWarLeftGateClose2Mesh = 0x60F;
    public const int GuildWarLeftGateClose3Mesh = 0x64B;
    public const int GuildWarLeftGateClose4Mesh = 0x687;
    public const int GuildWarLeftGateDefaultDemolitionMesh = 0x5E7;
    public const int GuildWarRightGateDefaultDemolitionMesh = 0x5CF;
    public const int GuildWarRightGateDemolition2Mesh = 0x60B;
    public const int GuildWarRightGateDemolition3Mesh = 0x647;
    public const int GuildWarRightGateDemolition4Mesh = 0x683;
    public const int GuildWarLeftGateDemolition2Mesh = 0x623;
    public const int GuildWarLeftGateDemolition3Mesh = 0x65F;
    public const int GuildWarLeftGateDemolition4Mesh = 0x69B;

    // Mirrors legacy MrRayzo.GUILDWAR.Death MsgNewSynWar Type=9 ownership broadcasts.
    public const uint GuildWarExorcimOwnershipChangeDwParam = 100_009;
    public const uint GuildWarTenacityOwnershipChangeDwParam = 100_008;

    public static bool IsTrainingSob(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == TrainingMapId &&
            sobNpc.Kind is StakeKind or ScarecrowKind;
    }

    public static bool IsEventDamageableSob(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (IsCanAttackNpcAlwaysAllowedMap(sobNpc))
        {
            return true;
        }

        if (IsTrainingSob(sobNpc))
        {
            return false;
        }

        return HasSeedHitpoints(sobNpc) &&
            (sobNpc.Kind is PoleKind or FurnitureKind or GateKind ||
                IsSourceUnionWarPoleSob(sobNpc) ||
                IsKnownGuildWarSob(sobNpc) ||
                TryGetClassPoleWarPole(sobNpc, out _, out _, out _));
    }

    public static bool IsCanAttackNpcAlwaysAllowedMap(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == CanAttackNpcAlwaysAllowedMapId;
    }

    public static bool IsStaticStatueSob(
        SobNpc sobNpc,
        IReadOnlyList<StaticStatueRecord> staticStatues)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);
        ArgumentNullException.ThrowIfNull(staticStatues);

        foreach (var staticStatue in staticStatues)
        {
            if (TryGetStaticStatueSobIdentity(
                    staticStatue,
                    out var uid,
                    out var x,
                    out var y,
                    out var mapId,
                    out var maxHitpoints,
                    out _) &&
                sobNpc.Id == uid &&
                sobNpc.X == x &&
                sobNpc.Y == y &&
                sobNpc.MapId == mapId &&
                sobNpc.MaxLife == maxHitpoints)
            {
                return true;
            }
        }

        return false;
    }

    public static LegacySobNpcDeathBehavior GetDeathBehavior(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (IsTrainingSob(sobNpc))
        {
            return LegacySobNpcDeathBehavior.ResetHitpointsAndBroadcastDeathUpdates;
        }

        if (!IsEventDamageableSob(sobNpc))
        {
            return LegacySobNpcDeathBehavior.NotDamageable;
        }

        if (TryGetDefaultDemolitionMesh(sobNpc, out _))
        {
            return LegacySobNpcDeathBehavior.GateDemolition;
        }

        return LegacySobNpcDeathBehavior.PersistentDeath;
    }

    public static LegacySobNpcDeathBehavior GetDeathBehavior(
        SobNpc sobNpc,
        IReadOnlyList<StaticStatueRecord>? staticStatues)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (staticStatues is not null &&
            IsStaticStatueSob(sobNpc, staticStatues))
        {
            return LegacySobNpcDeathBehavior.PersistentDeath;
        }

        return GetDeathBehavior(sobNpc);
    }

    public static bool IsKnownGuildWarSob(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == GuildWarMapId &&
            sobNpc.Id is
                GuildWarPillarUid or
                GuildWarRightGateUid or
                GuildWarLeftGateUid or
                GuildWarTenacityPillarUid or
                GuildWarExorcimPillarUid or
                GuildWarHonorPillarUid;
    }

    public static bool IsNewGuildWarAuthorizationSob(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == GuildWarMapId &&
            sobNpc.Id is
                GuildWarPillarUid or
                GuildWarTenacityPillarUid or
                GuildWarExorcimPillarUid or
                GuildWarHonorPillarUid;
    }

    public static bool IsClanWarPoleSob(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.Id == ClanWarPoleUid;
    }

    public static bool IsClanWarCurrentWarPole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.Id == ClanWarPoleUid &&
            TryGetClanWarCityName(sobNpc.MapId, out _);
    }

    public static bool TryGetClanWarCityName(int mapId, out string cityName)
    {
        cityName = mapId switch
        {
            TwinCityClanWarMapId => "Twin",
            PhoenixCastleClanWarMapId => "Phoenix",
            ApeCityClanWarMapId => "Ape",
            DesertCityClanWarMapId => "Desert",
            BirdIslandClanWarMapId => "Bird",
            _ => string.Empty,
        };
        return cityName.Length != 0;
    }

    public static bool IsUnionWarPoleSob(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.Id == UnionWarPoleUid;
    }

    public static bool IsSourceUnionWarPoleSob(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == UnionWarMapId &&
            sobNpc.Id == UnionWarPoleUid;
    }

    public static bool TryGetClanDominationPoleScheduleName(SobNpc sobNpc, out string scheduleName)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        scheduleName = sobNpc.Id switch
        {
            TwinCityClanDominationPoleUid => "TCClanDomination",
            PhoenixCastleClanDominationPoleUid => "PHClanDomination",
            ApeCityClanDominationPoleUid => "APClanDomination",
            DesertCityClanDominationPoleUid => "DCClanDomination",
            BirdIslandClanDominationPoleUid => "BRClanDomination",
            _ => string.Empty,
        };
        return scheduleName.Length != 0;
    }

    public static bool TryGetPoleDominationPoleScheduleName(SobNpc sobNpc, out string scheduleName)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        scheduleName = sobNpc.Id switch
        {
            PoleDominationPoleUid => "PoleDomination",
            PoleDominationBirdIslandPoleUid => "PoleDominationBI",
            PoleDominationDesertCityPoleUid => "PoleDominationDC",
            PoleDominationPhoenixCastlePoleUid => "PoleDominationPC",
            _ => string.Empty,
        };
        return scheduleName.Length != 0;
    }

    public static bool TryGetNobilityPoleSchedule(
        SobNpc sobNpc,
        out string scheduleName,
        out byte requiredRank)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        (scheduleName, requiredRank) = sobNpc.Id switch
        {
            NobilityKingPoleUid => ("MsgNobilityPole", LegacyNobilityRanking.RankKing),
            NobilityPrincePoleUid => ("MsgNobilityPole1", LegacyNobilityRanking.RankPrince),
            NobilityDukePoleUid => ("MsgNobilityPole2", LegacyNobilityRanking.RankDuke),
            NobilityEarlPoleUid => ("MsgNobilityPole3", LegacyNobilityRanking.RankEarl),
            _ => (string.Empty, (byte)0),
        };
        return scheduleName.Length != 0;
    }

    public static bool TryGetEliteGuildWarPole(
        SobNpc sobNpc,
        out string scheduleName,
        out string furnitureName)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        (scheduleName, furnitureName) = sobNpc.Id switch
        {
            EliteGuildWarPoleUid => ("EliteGuildWar", "Furnitures"),
            EliteGuildWarSecondPoleUid => ("EliteGuildWar2", "Furnitures2"),
            EliteGuildWarThirdPoleUid => ("EliteGuildWar3", "Furnitures3"),
            _ => (string.Empty, string.Empty),
        };
        return scheduleName.Length != 0;
    }

    public static bool IsTrepilWarPole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.Id == TrepilWarPoleUid;
    }

    public static bool IsTopPole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == TopPoleMapId &&
            sobNpc.Id == TopPoleUid;
    }

    public static bool IsWarOfPlayersPole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.Id == WarOfPlayersPoleUid;
    }

    public static bool IsWarFightersPole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == WarFightersMapId &&
            sobNpc.Id == WarFightersPoleUid;
    }

    public static bool IsSuperGuildWarPole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == SuperGuildWarMapId &&
            sobNpc.Id == SuperGuildWarPoleUid;
    }

    public static bool TryGetClassPoleWarPole(
        SobNpc sobNpc,
        out string scheduleName,
        out int requiredClassFamily,
        out string classGateName)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (sobNpc.MapId != ClassPoleWarMapId)
        {
            scheduleName = string.Empty;
            requiredClassFamily = 0;
            classGateName = string.Empty;
            return false;
        }

        (scheduleName, requiredClassFamily, classGateName) = sobNpc.Id switch
        {
            ClassPoleWarWarriorGodPoleUid => ("MsgWarriorGod", 2, "IsWarrior"),
            ClassPoleWarArcherMasterPoleUid => ("MsgArcherMaster", 4, "IsArcher"),
            ClassPoleWarNinjaMasterPoleUid => ("MsgNinjaMaster", 5, "IsNinja"),
            ClassPoleWarTrojanMasterPoleUid => ("MsgTrojanMaster", 1, "IsTrojan"),
            ClassPoleWarTaoistMasterPoleUid => ("MsgTaoistMaster", 13, "IsWater"),
            ClassPoleWarFireMasterPoleUid => ("MsgFireMaster", 14, "IsFire"),
            ClassPoleWarPirateMasterPoleUid => ("MsgPirateMaster", 7, "IsPirate"),
            ClassPoleWarKungfuKingPoleUid => ("MsgKungfuKing", 8, "IsLee"),
            ClassPoleWarWindWalkerPoleUid => ("MsgWindWalker", 16, "IsWindWalker"),
            ClassPoleWarThnderStrikePoleUid => ("MsgThnderStrike", 9, "IsThunderStriker"),
            ClassPoleWarClassPolaWarPoleUid => ("MsgClassPolaWar", 0, string.Empty),
            ClassPoleWarMonkMasterPoleUid => ("MsgMonkMaster", 6, "IsMonk"),
            _ => (string.Empty, 0, string.Empty),
        };
        return scheduleName.Length != 0;
    }

    public static bool IsEmperorWarPole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == EmperorWarMapId &&
            sobNpc.Id == EmperorWarPoleUid;
    }

    public static bool IsGuildWarScorePole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == GuildWarScoreMapId &&
            sobNpc.Id == GuildWarScorePoleUid;
    }

    public static bool IsClanWarScorePole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == ClanWarScoreMapId &&
            sobNpc.Id == ClanWarScorePoleUid;
    }

    public static bool IsChampionsOfWarrPole(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.Id == ChampionsOfWarrPoleUid;
    }

    public static bool TryGetGuild6PoleWarPoleSchedule(
        SobNpc sobNpc,
        out string scheduleName,
        out string displayName)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (sobNpc.MapId != Guild6PoleWarMapId)
        {
            scheduleName = string.Empty;
            displayName = string.Empty;
            return false;
        }

        (scheduleName, displayName) = sobNpc.Id switch
        {
            Guild6PoleWarPoleUid => ("Guild6PoleWar", "GuildPole[1]War"),
            Guild6PoleWar2PoleUid => ("Guild6PoleWar2", "GuildPole[2]War"),
            Guild6PoleWar3PoleUid => ("Guild6PoleWar3", "GuildPole[3]War"),
            Guild6PoleWar4PoleUid => ("Guild6PoleWar4", "GuildPole[4]War"),
            Guild6PoleWar5PoleUid => ("Guild6PoleWar5", "GuildPole[5]War"),
            Guild6PoleWar6PoleUid => ("Guild6PoleWar6", "GuildPole[6]War"),
            _ => (string.Empty, string.Empty),
        };
        return scheduleName.Length != 0;
    }

    public static bool IsGuildWarCapturePillar(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return sobNpc.MapId == GuildWarMapId &&
            sobNpc.Kind == GuildWarCapturePillarKind &&
            sobNpc.Id is GuildWarExorcimPillarUid or GuildWarTenacityPillarUid;
    }

    public static bool IsGuildWarOwnershipChangeBroadcastPillar(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return TryGetGuildWarOwnershipChangeDwParam(sobNpc, out _);
    }

    public static uint? GetGuildWarOwnershipChangeDwParam(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return TryGetGuildWarOwnershipChangeDwParam(sobNpc, out var dwParam)
            ? dwParam
            : null;
    }

    public static bool TryGetGuildWarOwnershipChangeDwParam(SobNpc sobNpc, out uint dwParam)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (!IsGuildWarCapturePillar(sobNpc))
        {
            dwParam = 0;
            return false;
        }

        if (sobNpc.Id == GuildWarExorcimPillarUid)
        {
            dwParam = GuildWarExorcimOwnershipChangeDwParam;
            return true;
        }

        if (sobNpc.Id == GuildWarTenacityPillarUid)
        {
            dwParam = GuildWarTenacityOwnershipChangeDwParam;
            return true;
        }

        dwParam = 0;
        return false;
    }

    public static int? GetDefaultDemolitionMesh(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        return TryGetDefaultDemolitionMesh(sobNpc, out var mesh) ? mesh : null;
    }

    public static int? GetGuildWarGateCloseMesh(SobNpc sobNpc, uint level)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (!TryGetGuildWarGateSide(sobNpc, out var rightGate))
        {
            return null;
        }

        return GetGuildWarGateCloseMesh(level, rightGate);
    }

    public static int? GetGuildWarGateDemolitionMesh(SobNpc sobNpc, uint level)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (!TryGetGuildWarGateSide(sobNpc, out var rightGate))
        {
            return null;
        }

        return GetGuildWarGateDemolitionMesh(level, rightGate);
    }

    public static int GetGuildWarGateCloseMesh(uint level, bool rightGate)
    {
        if (rightGate)
        {
            return level switch
            {
                <= 9U => GuildWarRightGateClose1Mesh,
                <= 14U => GuildWarRightGateClose4Mesh,
                <= 19U => GuildWarRightGateClose3Mesh,
                _ => GuildWarRightGateClose4Mesh,
            };
        }

        return level switch
        {
            <= 9U => GuildWarLeftGateClose1Mesh,
            <= 14U => GuildWarLeftGateClose2Mesh,
            <= 19U => GuildWarLeftGateClose3Mesh,
            _ => GuildWarLeftGateClose4Mesh,
        };
    }

    public static int GetGuildWarGateDemolitionMesh(uint level, bool rightGate)
    {
        if (rightGate)
        {
            return level switch
            {
                <= 9U => GuildWarRightGateDefaultDemolitionMesh,
                <= 14U => GuildWarRightGateDemolition2Mesh,
                <= 19U => GuildWarRightGateDemolition3Mesh,
                _ => GuildWarRightGateDemolition4Mesh,
            };
        }

        return level switch
        {
            <= 9U => GuildWarLeftGateDefaultDemolitionMesh,
            <= 14U => GuildWarLeftGateDemolition2Mesh,
            <= 19U => GuildWarLeftGateDemolition3Mesh,
            _ => GuildWarLeftGateDemolition4Mesh,
        };
    }

    public static bool TryGetDefaultDemolitionMesh(SobNpc sobNpc, out int mesh)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        if (sobNpc.Kind != GateKind)
        {
            mesh = 0;
            return false;
        }

        if (sobNpc.MapId == GuildWarMapId &&
            sobNpc.Id == GuildWarLeftGateUid)
        {
            mesh = GuildWarLeftGateDefaultDemolitionMesh;
            return true;
        }

        if (sobNpc.MapId == GuildWarMapId &&
            sobNpc.Id == GuildWarRightGateUid)
        {
            mesh = GuildWarRightGateDefaultDemolitionMesh;
            return true;
        }

        var name = sobNpc.Name;
        if (name.Contains("LeftGate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Left Gate", StringComparison.OrdinalIgnoreCase))
        {
            mesh = GuildWarLeftGateDefaultDemolitionMesh;
            return true;
        }

        if (name.Contains("RightGate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Right Gate", StringComparison.OrdinalIgnoreCase))
        {
            mesh = GuildWarRightGateDefaultDemolitionMesh;
            return true;
        }

        mesh = 0;
        return false;
    }

    private static bool TryGetGuildWarGateSide(SobNpc sobNpc, out bool rightGate)
    {
        if (sobNpc.MapId == GuildWarMapId &&
            sobNpc.Id == GuildWarLeftGateUid)
        {
            rightGate = false;
            return true;
        }

        if (sobNpc.MapId == GuildWarMapId &&
            sobNpc.Id == GuildWarRightGateUid)
        {
            rightGate = true;
            return true;
        }

        rightGate = false;
        return false;
    }

    private static bool HasSeedHitpoints(SobNpc sobNpc)
    {
        return sobNpc.Life > 0 || sobNpc.MaxLife > 0;
    }

    private static bool TryGetStaticStatueSobIdentity(
        StaticStatueRecord staticStatue,
        out int uid,
        out int x,
        out int y,
        out int mapId,
        out int maxHitpoints,
        out int hitpoints)
    {
        uid = 0;
        x = 0;
        y = 0;
        mapId = 0;
        maxHitpoints = 0;
        hitpoints = 0;

        var values = staticStatue.Values;
        if (values.Count < 7 ||
            values[0] != values.Count - 7)
        {
            return false;
        }

        var tailOffset = values.Count - 6;
        uid = values[tailOffset];
        x = values[tailOffset + 1];
        y = values[tailOffset + 2];
        mapId = values[tailOffset + 3];
        maxHitpoints = values[tailOffset + 4];
        hitpoints = values[tailOffset + 5];
        return true;
    }
}

public enum LegacySobNpcDeathBehavior
{
    NotDamageable = 0,
    PersistentDeath = 1,
    ResetHitpointsAndBroadcastDeathUpdates = 2,
    GateDemolition = 3,
}
