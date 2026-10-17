namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    public static async ValueTask<bool> TryHandleLegacyDisCityBattlefieldServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (LegacyDisCityBattlefieldServicesBoxerSources.TryGetValue(npcId, out var boxerSource))
        {
            return await LegacyDisCityBattlefieldServicesTryHandleBoxerAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    boxerSource,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return npcId switch
        {
            LegacyDisCityBattlefieldServicesDicCityMap4NpcId => await LegacyDisCityBattlefieldServicesTryHandleDicCityMap4Async(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesDisCityMap2NpcId => await LegacyDisCityBattlefieldServicesTryHandleDisCityMap2Async(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesDisCityMap1NpcId => await LegacyDisCityBattlefieldServicesTryHandleDisCityMap1Async(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesTwinDisCityMainNpcId => await LegacyDisCityBattlefieldServicesTryHandleTwinDisCityMainAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesDisCityMainNpcId => await LegacyDisCityBattlefieldServicesTryHandleDisCityMainAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesTreasureThiefNpcId => await LegacyDisCityBattlefieldServicesTryHandleSimpleTournamentAsync(
                    context,
                    LegacyDisCityBattlefieldServicesTreasureThiefNpcId,
                    optionId,
                    new LegacyDisCityBattlefieldServicesSource("NpcHandler.ThreasureThief", "NpcHandler.cs:29229-29257"),
                    "I lost all my treasure boxes in the Frozen Grotto. Help me find them during the xx:25 event.",
                    "Treasure Thief entry is not available from this runtime yet.",
                    "Source depends on MsgSchedules.CurrentTournament.Type == TreasureThief and CurrentTournament.Join.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesFindGmNpcId => await LegacyDisCityBattlefieldServicesTryHandleSimpleTournamentAsync(
                    context,
                    LegacyDisCityBattlefieldServicesFindGmNpcId,
                    optionId,
                    new LegacyDisCityBattlefieldServicesSource("NpcHandler.FindGM", "NpcHandler.cs:29258-29287"),
                    "I get lost in the city every hour at xx:05. Find me and you may win a good prize.",
                    "Find GM entry is not available from this runtime yet.",
                    "Source depends on MsgSchedules.CurrentTournament.Type == FindGM and CurrentTournament.Join.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesDbShowerNpcId => await LegacyDisCityBattlefieldServicesTryHandleSimpleTournamentAsync(
                    context,
                    LegacyDisCityBattlefieldServicesDbShowerNpcId,
                    optionId,
                    new LegacyDisCityBattlefieldServicesSource("NpcHandler.DBShower", "NpcHandler.cs:29288-29317"),
                    "DB Shower runs every hour at xx:33. Players collect Dragon Balls there instead of fighting.",
                    "DB Shower entry and rewards are not available from this runtime yet.",
                    "Source depends on MsgSchedules.CurrentTournament.Type == DBShower, CurrentTournament.Join, and event reward drops.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesBattleFieldMainNpcId => await LegacyDisCityBattlefieldServicesTryHandleBattleFieldMainAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesBattleFieldMap1NpcId => await LegacyDisCityBattlefieldServicesTryHandleBattleFieldMapAsync(
                    context,
                    session,
                    LegacyDisCityBattlefieldServicesBattleFieldMap1NpcId,
                    optionId,
                    new LegacyDisCityBattlefieldServicesSource("NpcHandler.BattleFieldMap1", "NpcHandler.cs:29565-29613"),
                    nextMapText: "You need 2.000 Points for join in the map 2.",
                    advancePointCost: 2_000,
                    advanceDestination: new LegacyNpcTeleportDestination(1_080, 195, 208, "NpcHandler.BattleFieldMap1"),
                    insufficientPointsText: "You not have 2.000 Points.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesBattleFieldMap2NpcId => await LegacyDisCityBattlefieldServicesTryHandleBattleFieldMapAsync(
                    context,
                    session,
                    LegacyDisCityBattlefieldServicesBattleFieldMap2NpcId,
                    optionId,
                    new LegacyDisCityBattlefieldServicesSource("NpcHandler.BattleFieldMap2", "NpcHandler.cs:29614-29662"),
                    nextMapText: "You need 3500 Points for join in the map 3.",
                    advancePointCost: 3_500,
                    advanceDestination: new LegacyNpcTeleportDestination(2_060, 183, 156, "NpcHandler.BattleFieldMap2"),
                    insufficientPointsText: "You not have 3.500 Points.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesBattleFieldMap3NpcId => await LegacyDisCityBattlefieldServicesTryHandleBattleFieldMap3Async(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesTeratoTwinNpcId => await LegacyDisCityBattlefieldServicesTryHandleTeratoTwinAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesGwJailNpcId => await LegacyDisCityBattlefieldServicesTryHandleGwJailAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesPrizeOfficerNpcId => await LegacyDisCityBattlefieldServicesTryHandlePrizeOfficerAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesStartNpcId => await LegacyDisCityBattlefieldServicesTryHandleStartNpcAsync(
                    context,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyDisCityBattlefieldServicesTreasureLeaveNpcId => await LegacyDisCityBattlefieldServicesTryHandleTreasureLeaveAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }
}
