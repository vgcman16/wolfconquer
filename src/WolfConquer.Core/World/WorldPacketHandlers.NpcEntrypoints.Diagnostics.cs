using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static string FormatLegacyNpcLookup(WorldPacketHandlerContext context, uint npcId)
    {
        if (context.Session is not { } session)
        {
            return "npcLookup=no-session";
        }

        if (context.WorldData is not { } worldData)
        {
            return "npcLookup=world-data-not-loaded";
        }

        if (npcId > int.MaxValue)
        {
            return "npcLookup=id-out-of-range";
        }

        var key = (int)npcId;
        worldData.NpcsById.TryGetValue(key, out var npcs);
        worldData.SobNpcsById.TryGetValue(key, out var sobNpcs);
        worldData.ChristmasNpcsById.TryGetValue(key, out var christmasNpcs);
        worldData.FurnitureById.TryGetValue(key, out var furniture);
        var npcCount = npcs?.Count ?? 0;
        var sobNpcCount = sobNpcs?.Count ?? 0;
        var christmasNpcCount = christmasNpcs?.Count ?? 0;
        var furnitureNpcCount = furniture is null ? 0 : 1;
        var visibleNpcCount = npcs?.Count(npc => CanSeeStaticNpc(session.Character, npc)) ?? 0;
        var visibleSobNpcCount = sobNpcs?.Count(sobNpc => CanSeeSobNpc(session.Character, sobNpc)) ?? 0;
        var visibleChristmasNpcCount = christmasNpcs?.Count(npc => CanSeeChristmasNpc(session.Character, npc)) ?? 0;
        var visibleFurnitureNpcCount = furniture is not null && CanSeeFurnitureNpc(session.Character, furniture) ? 1 : 0;
        var positionDiagnostic =
            $" playerMap={session.Character.MapId} playerX={session.Character.X} playerY={session.Character.Y}";

        if (npcCount == 0 &&
            sobNpcCount == 0 &&
            christmasNpcCount == 0 &&
            furnitureNpcCount == 0)
        {
            return "npcLookup=missing" + positionDiagnostic;
        }

        return "npcLookup=" +
            $"static={npcCount}/visible={visibleNpcCount} " +
            $"sob={sobNpcCount}/visible={visibleSobNpcCount} " +
            $"christmas={christmasNpcCount}/visible={visibleChristmasNpcCount} " +
            $"furniture={furnitureNpcCount}/visible={visibleFurnitureNpcCount}" +
            positionDiagnostic +
            FormatNearestLegacyNpcLookup(session.Character, npcs, sobNpcs, christmasNpcs, furniture);
    }

    private static string FormatNearestLegacyNpcLookup(
        CharacterSnapshot character,
        IReadOnlyList<Npc>? npcs,
        IReadOnlyList<SobNpc>? sobNpcs,
        IReadOnlyList<ChristmasNpc>? christmasNpcs,
        Furniture? furniture)
    {
        var diagnostic = string.Empty;
        if (npcs is { Count: > 0 })
        {
            var nearest = FindNearestStaticNpc(character, npcs);
            diagnostic +=
                $" staticNearestMap={nearest.MapId} staticNearestX={nearest.X} staticNearestY={nearest.Y}" +
                $" staticNearestDistance={LegacyGetDistance(character.X, character.Y, nearest.X, nearest.Y)}" +
                $" staticNearestVisible={CanSeeStaticNpc(character, nearest)}";
        }

        if (sobNpcs is { Count: > 0 })
        {
            var nearest = FindNearestSobNpc(character, sobNpcs);
            diagnostic +=
                $" sobNearestMap={nearest.MapId} sobNearestX={nearest.X} sobNearestY={nearest.Y}" +
                $" sobNearestDistance={LegacyGetDistance(character.X, character.Y, nearest.X, nearest.Y)}" +
                $" sobNearestVisible={CanSeeSobNpc(character, nearest)}";
        }

        if (christmasNpcs is { Count: > 0 })
        {
            var nearest = FindNearestChristmasNpc(character, christmasNpcs);
            diagnostic +=
                $" christmasNearestMap={nearest.MapId} christmasNearestX={nearest.X} christmasNearestY={nearest.Y}" +
                $" christmasNearestDistance={LegacyGetDistance(character.X, character.Y, nearest.X, nearest.Y)}" +
                $" christmasNearestVisible={CanSeeChristmasNpc(character, nearest)}" +
                FormatStaticRoleSourceRowDiagnostic(nearest.LineNumber, nearest.RawLine);
        }

        if (furniture is not null)
        {
            diagnostic +=
                $" furnitureMap={furniture.MapId} furnitureX={furniture.X} furnitureY={furniture.Y}" +
                $" furnitureDistance={LegacyGetDistance(character.X, character.Y, furniture.X, furniture.Y)}" +
                $" furnitureVisible={CanSeeFurnitureNpc(character, furniture)}";
        }

        return diagnostic;
    }

    private static Npc FindNearestStaticNpc(CharacterSnapshot character, IReadOnlyList<Npc> npcs)
    {
        var nearest = npcs[0];
        var nearestMapRank = nearest.MapId == character.MapId ? 0 : 1;
        var nearestDistance = LegacyGetDistance(character.X, character.Y, nearest.X, nearest.Y);
        foreach (var npc in npcs.Skip(1))
        {
            var mapRank = npc.MapId == character.MapId ? 0 : 1;
            var distance = LegacyGetDistance(character.X, character.Y, npc.X, npc.Y);
            if (mapRank > nearestMapRank ||
                (mapRank == nearestMapRank && distance >= nearestDistance))
            {
                continue;
            }

            nearest = npc;
            nearestMapRank = mapRank;
            nearestDistance = distance;
        }

        return nearest;
    }

    private static ChristmasNpc FindNearestChristmasNpc(CharacterSnapshot character, IReadOnlyList<ChristmasNpc> npcs)
    {
        var nearest = npcs[0];
        var nearestMapRank = nearest.MapId == character.MapId ? 0 : 1;
        var nearestDistance = LegacyGetDistance(character.X, character.Y, nearest.X, nearest.Y);
        foreach (var npc in npcs.Skip(1))
        {
            var mapRank = npc.MapId == character.MapId ? 0 : 1;
            var distance = LegacyGetDistance(character.X, character.Y, npc.X, npc.Y);
            if (mapRank > nearestMapRank ||
                (mapRank == nearestMapRank && distance >= nearestDistance))
            {
                continue;
            }

            nearest = npc;
            nearestMapRank = mapRank;
            nearestDistance = distance;
        }

        return nearest;
    }

    private static SobNpc FindNearestSobNpc(CharacterSnapshot character, IReadOnlyList<SobNpc> sobNpcs)
    {
        var nearest = sobNpcs[0];
        var nearestMapRank = nearest.MapId == character.MapId ? 0 : 1;
        var nearestDistance = LegacyGetDistance(character.X, character.Y, nearest.X, nearest.Y);
        foreach (var sobNpc in sobNpcs.Skip(1))
        {
            var mapRank = sobNpc.MapId == character.MapId ? 0 : 1;
            var distance = LegacyGetDistance(character.X, character.Y, sobNpc.X, sobNpc.Y);
            if (mapRank > nearestMapRank ||
                (mapRank == nearestMapRank && distance >= nearestDistance))
            {
                continue;
            }

            nearest = sobNpc;
            nearestMapRank = mapRank;
            nearestDistance = distance;
        }

        return nearest;
    }

    private static string FormatLegacyNpcScriptDiagnostic(uint npcId, byte optionId)
    {
        return $"legacyScript={GetLegacyNpcScriptSource(npcId)} option={optionId}";
    }

    private static string GetLegacyNpcScriptSource(uint npcId)
    {
        if (npcId == LegacySpringFortuneNianShouChestNpcId)
        {
            return LegacySpringFortuneNianShouChestSourceMethod;
        }

        if (LegacyNpcScriptRuntime.TryGetSourceHint(npcId, out _, out var sourceMethodName))
        {
            return sourceMethodName;
        }

        return LegacyNpcScriptNames.TryGetValue(npcId, out var scriptName)
            ? $"NpcHandler.{scriptName}"
            : "unmapped";
    }

    private static bool CanUseLegacyNpcServerClick(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint npcId,
        out string npcLookup)
    {
        npcLookup = FormatLegacyNpcLookup(context, npcId);
        if (context.WorldData is not { } worldData)
        {
            return true;
        }

        if (npcId > int.MaxValue)
        {
            return false;
        }

        var key = (int)npcId;
        if (CanUseLegacyNpcServerClickFromPosition(worldData, character, key))
        {
            return true;
        }

        if (TryGetLegacyPreviousMapPosition(character, out var previous) &&
            CanUseLegacyNpcServerClickFromPosition(worldData, previous, key))
        {
            npcLookup += " acceptedVia=previous-position";
            return true;
        }

        return false;
    }

    private static bool CanUseLegacyNpcServerClickFromPosition(
        WorldDataCatalog worldData,
        CharacterSnapshot character,
        int npcId)
    {
        var hasVisibleStaticNpc =
            worldData.NpcsById.TryGetValue(npcId, out var npcs) &&
            npcs.Any(npc => CanSeeStaticNpc(character, npc));
        var hasVisibleSobNpc =
            worldData.SobNpcsById.TryGetValue(npcId, out var sobNpcs) &&
            sobNpcs.Any(sobNpc => CanSeeSobNpc(character, sobNpc));
        var hasVisibleChristmasNpc =
            worldData.ChristmasNpcsById.TryGetValue(npcId, out var christmasNpcs) &&
            christmasNpcs.Any(npc =>
                CanSeeChristmasNpc(character, npc) &&
                LegacyChristmasNpcProjection.TryCreate(npc, out var projection) &&
                projection.MatchesRequestEntity((uint)npcId));
        var hasVisibleFurnitureNpc =
            worldData.FurnitureById.TryGetValue(npcId, out var furniture) &&
            CanSeeFurnitureNpc(character, furniture) &&
            LegacyFurnitureNpcProjection.TryCreate(furniture, out var projection) &&
            projection.MatchesRequestEntity((uint)npcId);
        return hasVisibleStaticNpc || hasVisibleSobNpc || hasVisibleChristmasNpc || hasVisibleFurnitureNpc;
    }

    private static bool TryGetLegacyPreviousMapPosition(
        CharacterSnapshot character,
        out CharacterSnapshot previous)
    {
        previous = character;
        if (character.PreviousMapId != character.MapId ||
            character.PreviousMapX <= 0 ||
            character.PreviousMapY <= 0 ||
            (character.PreviousMapX == character.X && character.PreviousMapY == character.Y))
        {
            return false;
        }

        previous = character with
        {
            X = character.PreviousMapX,
            Y = character.PreviousMapY,
        };
        return true;
    }

    private static bool IsLegacyCursorScriptNpc(LegacyNpcServerRequestPacket packet)
    {
        return packet.Action == LegacyNpcServerAction.Cursor &&
            (IsLegacyNobilityDonorNpc(packet.NpcId) || packet.NpcId == LegacyAccountManagerNpcId);
    }

    private static bool TryGetLegacyLevelHuntExitSource(uint npcId, out string sourceMethod)
    {
        sourceMethod = npcId switch
        {
            LegacyLevelHuntMap1NpcId => "NpcHandler.leveHuntMap1",
            LegacyLevelHuntMap2NpcId => "NpcHandler.leveHuntMap2",
            LegacyLevelHuntMap3NpcId => "NpcHandler.leveHuntMap3",
            LegacyLevelHuntMap4NpcId => "NpcHandler.leveHuntMap4",
            LegacyLevelHuntMap5NpcId => "NpcHandler.leveHuntMap5",
            LegacyLevelHuntMap6NpcId => "NpcHandler.leveHuntMap6",
            _ => string.Empty,
        };
        return sourceMethod.Length > 0;
    }

    private static bool TryGetLegacyArchiveTeleportDestination(
        uint npcId,
        byte optionId,
        out LegacyNpcTeleportDestination destination)
    {
        destination = default;
        if (optionId != 1)
        {
            return false;
        }

        destination = npcId switch
        {
            LegacyArchiveJoinNpcId => new LegacyNpcTeleportDestination(11_724, 50, 53, "NpcHandler.joinArchaveMap"),
            LegacyArchiveExitNpcId => new LegacyNpcTeleportDestination(1_002, 410, 355, "NpcHandler.outArchaveMap"),
            _ => default,
        };
        return !string.IsNullOrEmpty(destination.SourceMethod);
    }

    private static bool TryGetLegacyJobCenterTeleportDestination(
        long characterClass,
        out LegacyNpcTeleportDestination destination)
    {
        destination = characterClass switch
        {
            >= 4_000 and <= 4_051 => new(10_445, 82, 64, "NpcHandler.joinJopCentr"),
            >= 2_000 and <= 2_051 => new(10_446, 82, 64, "NpcHandler.joinJopCentr"),
            >= 5_000 and <= 5_051 => new(10_447, 82, 64, "NpcHandler.joinJopCentr"),
            >= 1_000 and <= 1_051 => new(10_448, 82, 64, "NpcHandler.joinJopCentr"),
            >= 6_000 and <= 6_051 => new(10_449, 82, 64, "NpcHandler.joinJopCentr"),
            >= 13_000 and <= 14_051 => new(10_450, 82, 64, "NpcHandler.joinJopCentr"),
            10_000 or 10_001 => new(10_450, 82, 64, "NpcHandler.joinJopCentr"),
            >= 7_000 and <= 7_051 => new(10_451, 82, 64, "NpcHandler.joinJopCentr"),
            >= 8_000 and <= 8_051 => new(10_452, 82, 64, "NpcHandler.joinJopCentr"),
            >= 16_000 and <= 16_051 => new(10_453, 82, 64, "NpcHandler.joinJopCentr"),
            >= 9_000 and <= 9_051 => new(10_454, 82, 64, "NpcHandler.joinJopCentr"),
            _ => default,
        };
        return !string.IsNullOrEmpty(destination.SourceMethod);
    }
}
