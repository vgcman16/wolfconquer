using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMonsterDeathMapSideEffectsAsync(
        WorldPacketHandlerContext context,
        WorldSession killerSession,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken,
        bool family8418RewardAlreadyApplied = false)
    {
        var updatedSession = killerSession;
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacySkyTowerMonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        if (monster.MapId == 44_461 &&
            monsterFamily.Id == 452_373)
        {
            const string sourceBranch = "MonsterRole.cs killer.Player.Map == 44461 && Family.ID == 452373";
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(
                            "Duke of Hell has been enraged and revealed his true appearance. " +
                            "Hurry and defeat him to prevent the imperial mausoleum from destruction.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            var dynamicId = Interlocked.Increment(ref s_legacyMonsterDeathDynamicId);
            var targetX = updatedSession.Character.X;
            var targetY = updatedSession.Character.Y;
            updatedSession = await SendLegacyMonsterDeathDynamicTeleportAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    mapId: 44_462,
                    x: targetX,
                    y: targetY,
                    dynamicId,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    spawnedMonsterId: 452_374,
                    mapId: 44_462,
                    x: 336,
                    y: 193,
                    dynamicId,
                    sourceBranch: "MonsterRole.cs Map.AddMapMonster monsterId=452374 moveback",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (monster.MapId == 44_462)
        {
            const string sourceBranch = "MonsterRole.cs killer.Player.Map == 44462";
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(
                            "You`ve finally knocked back the Duke of Hell. However, the fierce fights have also " +
                            "triggered the emergency switch and the imperial mausoleum will soon collapse. Run!")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            var dynamicId = Interlocked.Increment(ref s_legacyMonsterDeathDynamicId);
            updatedSession = await SendLegacyMonsterDeathDynamicTeleportAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    mapId: 44_463,
                    x: updatedSession.Character.X,
                    y: updatedSession.Character.Y,
                    dynamicId,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "QuestGUI.SendAutoPatcher",
                        "imperial-mausoleum-collapse-autopatcher",
                        reason: "source MessageBox accept callback sends QuestGUI.SendAutoPatcher(10090, 58, 164, 0); modern runtime models the source message and dynamic teleport, but the optional client auto-route prompt is not modeled yet",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        ]))
                .ConfigureAwait(false);
        }
        else if (monster.MapId == 44_456)
        {
            const string sourceBranch = "MonsterRole.cs killer.Player.Map == 44456";
            if (monsterFamily.Id == 657_218)
            {
                await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                        updatedContext,
                        updatedSession,
                        monster,
                        monsterFamily,
                        spawnedMonsterId: 657_219,
                        mapId: 44_456,
                        x: CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X)),
                        y: CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y)),
                        dynamicId: checked((long)ReadCharacterDynamicId(updatedSession.Character)),
                        sourceBranch: "MonsterRole.cs Map.AddMapMonster monsterId=657219 movego",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (ShouldApplyLegacyPursuingDevilsQuestClear(updatedContext, updatedSession))
            {
                updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                        updatedContext,
                        updatedSession,
                        questId: 3_799,
                        amount: 1,
                        sourceBranch,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedContext = updatedContext with { Session = updatedSession };

                await LogParityGapAsync(
                        updatedContext,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            "QuestGUI.SendAutoPatcher",
                            "pursuing-devils-autopatcher",
                            reason: "source sends QuestGUI.SendAutoPatcher(\"You`ve successfully driven off the pursuing devils...\", 10088, 123, 119, 200058); modern runtime updates the quest objective, but the client auto-route prompt packet is not modeled yet",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("questId", 3_799),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("questId", 3_799),
                            ]))
                    .ConfigureAwait(false);
            }
        }
        else if (monster.MapId == 1_791 &&
            monsterFamily.Id == 2_172)
        {
            const string sourceBranch = "MonsterRole.cs killer.Player.Map == 1791 && Family.ID == 2172";
            var dynamicId = checked((long)ReadCharacterDynamicId(updatedSession.Character));
            var x = CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X));
            var y = CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y));
            for (var range = 7; range >= 3; range--)
            {
                await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                        updatedContext,
                        updatedSession,
                        monster,
                        monsterFamily,
                        spawnedMonsterId: 2_173,
                        mapId: 1_791,
                        x,
                        y,
                        dynamicId,
                        sourceBranch: FormattableString.Invariant($"MonsterRole.cs Map.AddMapMonster monsterId=2173 movego range={range}"),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Some Lyra Ghosts are coming! Watch out!").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy monster death Lyra Ghost warning " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"map={monster.MapId} spawnedMonsterId=2173 count=5 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (monsterFamily.Id == 3_319)
        {
            updatedSession = await TryApplyLegacyFamily3319MonsterDeathRewardAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        if (!family8418RewardAlreadyApplied &&
            monsterFamily.Id == 8_418 &&
            monster.MapId == 10_342)
        {
            updatedSession = await TryApplyLegacyFamily8418Map10342MonsterDeathRewardAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        updatedSession = await TryApplyLegacyHouseDemonMonsterDeathRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacySpiritBeadQuest2375MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyFamily4212Quest6126MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3071Family2700MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3071Family7022MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3935StarStonePackMonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3935SeniorDragonSoulMonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3935BeastQuest35025MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3935BeastSpawnLoopAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3935Quest35028MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3935Quest35007MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3998Family40903Quest3634MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3998Family40900Quest3636MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3998Family40902Quest3638MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3998Family40904Quest3641MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3998Family40908Quest3644MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3998Family40907Quest3648MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3998Family40905Quest3645MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap3998Family40906Quest3646MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1002Family1PheasantQuestMonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1002Family2TurtledoveQuest1007MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1002Family3RobinQuestMonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1002Family4ApparitionQuestMonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1013DynamicFamily14333Quest1748MonsterDeathAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        await TryApplyLegacyMap1780Family12314463BezoarDropAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        await TryApplyLegacyMap1780Family12314462KidSiezerSpawnAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        await TryApplyLegacyMap1780KidHunterSpawnAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyMap1015MonsterDeathSideEffectsAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family2163Quest525HandkerchiefAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family2179Quest525CapriceLeaderSpawnAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family55434417Quest1498AngryBladeGhostAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family76BladeGhostBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family17BladeGhostBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family75RockMonsterBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family51RockMonsterBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family74HillMonsterBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family14SandMonsterBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family15HillMonsterBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family73SandMonsterBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family72SnakemanBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family13SnakemanBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family71ThunderApeBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family12ThunderApeBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family14336Quest1356ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family11GiantApeBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family70GiantApeL58Quest1753SmellAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family14334Quest1749ApeLeaderSpawnAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family10BranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family8305Quest1305MonkeyKingTailAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family69Quest1304ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family11223VultureBranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1011Family106RockMonsterQuest1753SmellAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1011Family11007BanditQuest1103BranchesAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1011Family7BanditQuest1114GrainAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1011Family11008BanditLeaderQuest1118Async(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        if (monster.MapId == 1_001)
        {
            updatedSession = await TryApplyLegacyMap1001MonsterDeathSideEffectsAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        updatedSession = await TryApplyLegacyEpicTrojanMonsterDeathMapSideEffectsAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        if (monster.MapId == 2_353)
        {
            updatedSession = await TryApplyLegacySaturnMonsterDeathAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        if (monster.MapId == 10_250)
        {
            updatedSession = await TryApplyLegacyDeityLandQuest3944MonsterDeathAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        if (monster.MapId == 2_022)
        {
            var current = ReadAttributeUInt64(updatedSession.Character.Attributes, "KillersDisCity");
            var updatedCharacter = WithCharacterAttribute(updatedSession.Character, "KillersDisCity", checked(current + 1));
            updatedSession = updatedSession with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = updatedContext with { Session = updatedSession };

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy monster death map side effect " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId} " +
                    $"attribute=KillersDisCity value={current + 1} branch=\"MonsterRole.cs Map == 2022\".")
                .ConfigureAwait(false);
        }

        if (monster.MapId == 2_024 &&
            monsterFamily.Id == 66_432)
        {
            await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    790_001U,
                    count: 1,
                    scatterRange: 3,
                    sourceBranch: "MonsterRole.cs Map == 2024 && Family.ID == 66432",
                    cancellationToken)
                .ConfigureAwait(false);

            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.MonsterDrop(
                        monster.MonsterId,
                        itemId: 790_001,
                        mapId: TryGetLegacyParityMapId(monster.MapId),
                        dropKind: "item",
                        resource: "MsgSchedules.DisCity",
                        source: "MonsterRole.cs",
                        reason: "source calls MsgSchedules.DisCity.KillTheUltimatePluto(killer) after the UltimatePluto item drop; event scheduler side effect is not modeled yet"))
                .ConfigureAwait(false);
        }

        if (TryGetLegacyBattleFieldMonsterDeathPoints(monster.MapId, out var battlePoints))
        {
            updatedSession = await TryApplyLegacyBattleFieldMonsterDeathMapSideEffectsAsync(
                    context,
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    battlePoints,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        if (monster.MapId == 10_428 &&
            monsterFamily.Id == 2_984)
        {
            updatedSession = await TryApplyLegacyDragonFurnaceFusionAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            updatedSession = await SendLegacyMonsterDeathTeleportAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    mapId: 1_002,
                    x: 351,
                    y: 422,
                    sourceBranch: "MonsterRole.cs Map == 10428 && Family.ID == 2984",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return updatedSession;
    }
}
