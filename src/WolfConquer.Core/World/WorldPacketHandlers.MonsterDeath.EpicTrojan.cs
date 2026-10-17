using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap3831EpicTrojanGeneralPakHandoffAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monsterFamily.Id != 7_484)
        {
            return session;
        }

        const uint finishQuestId = 3_272;
        const uint acceptQuestId = 3_273;
        const uint miseryNpcId = 10_583;
        const uint generalPakNpcId = 10_600;
        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 3831 && Family.ID == 7484";

        var finishedQuest = session.Character.PlayerQuests.TryGetQuest(finishQuestId, out var existingFinishQuest)
            ? existingFinishQuest with { Status = PlayerQuestStatus.Finished }
            : new PlayerQuestSnapshotEntry(finishQuestId, PlayerQuestStatus.Finished, Time: 0, Intentions: []);
        var acceptIntentions = session.Character.PlayerQuests.TryGetQuest(acceptQuestId, out var existingAcceptQuest)
            ? existingAcceptQuest.Intentions
            : new uint[TryGetLegacyQuestMission(context.WorldData, acceptQuestId, out var acceptMission)
                ? Math.Max(0, GetLegacyQuestIntentionCount(acceptMission))
                : 0];
        var acceptedQuest = new PlayerQuestSnapshotEntry(
            acceptQuestId,
            PlayerQuestStatus.Accepted,
            Time: 0,
            acceptIntentions);
        var updatedQuests = session.Character.PlayerQuests
            .WithQuest(finishedQuest)
            .WithQuest(acceptedQuest);
        var updatedCharacter = WithCharacterAttribute(
            session.Character with { PlayerQuests = updatedQuests },
            "StageEpicTrojanQuest",
            40UL);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await PersistLegacyPlayerQuestsAsync(
                updatedContext,
                updatedSession,
                sourceBranch,
                $"epic-trojan-general-pak-handoff finishQuestId={finishQuestId} acceptQuestId={acceptQuestId}",
                finishQuestId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyQuestListPacket.CreateSingleQuestFrame(
                    LegacyQuestListMode.FinishQuest,
                    ToLegacyQuestListItem(finishedQuest)),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyQuestListPacket.CreateSingleQuestFrame(
                    LegacyQuestListMode.AcceptQuest,
                    ToLegacyQuestListItem(acceptedQuest)),
                cancellationToken)
            .ConfigureAwait(false);

        await LogParityGapAsync(
                updatedContext,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "QuestGUI.SendAutoPatcher",
                    "general-pak-autopatcher",
                    reason: "source GetFinishQuest(10583,0,3272)/GetFinishQuest(10600,0,3273) then sends QuestGUI.SendAutoPatcher(\"The~Evil~Monk~Misery`s~death~laugh~sounds~weird.~Something~may~happen~to~the~Flame~Devastator.~Go~report~to~General~Pak!\", 3831, 154, 130, 10600); modern runtime updates quests and stage, but the optional client auto-route prompt packet is not modeled yet",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("finishQuestId", finishQuestId),
                        LegacyParityGapDiagnostics.Fact("acceptQuestId", acceptQuestId),
                        LegacyParityGapDiagnostics.Fact("finishNpcId", miseryNpcId),
                        LegacyParityGapDiagnostics.Fact("acceptNpcId", generalPakNpcId),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("acceptQuestId", acceptQuestId),
                    ]))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3831 Epic Trojan General Pak handoff " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"finishQuestId={finishQuestId} acceptQuestId={acceptQuestId} " +
                $"finishNpcId={miseryNpcId} acceptNpcId={generalPakNpcId} stage=40 " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3834EpicTrojanGhostReaverAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monsterFamily.Id != 7_483)
        {
            return session;
        }

        const uint finishQuestId = 3_271;
        const uint acceptQuestId = 3_277;
        const uint generalPakRouteQuestId = 10_581;
        const uint effectItemId = 1_182;
        const byte blackColor = 2;
        const int sourceEffectLifetimeSeconds = 4;
        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 3834 && Family.ID == 7483";

        var progressBefore = ReadAttributeUInt64(session.Character.Attributes, "EpicTrojanMrMirrorPrograss");
        var progressAfter = checked(progressBefore + 1);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(session.Character, "StageEpicTrojanQuest", 30UL),
                "EpicTrijanKillGhostReaver",
                1UL),
            "EpicTrojanMrMirrorPrograss",
            progressAfter);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        var floorUid = NextLegacyFloorItemUid();
        var floorItem = new LegacyFloorItemPacket(
            floorUid,
            effectItemId,
            X: 163,
            Y: 228,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            blackColor,
            CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 0,
            OwnerY: 0,
            Name: "");
        await SendFloorItemVisibleAsync(updatedContext, updatedCharacter, floorItem, cancellationToken)
            .ConfigureAwait(false);
        var storedFloorEffect = false;
        if (updatedContext.FloorItems is { } floorItems)
        {
            var now = DateTimeOffset.UtcNow;
            var createdAt = now - FloorItemRuntimeStateStore.LegacyLifetime + TimeSpan.FromSeconds(sourceEffectLifetimeSeconds);
            floorItems.AddVisible(FloorItemRuntimeEntry.ForItem(monster.MapId, floorItem), createdAt);
            storedFloorEffect = true;
        }

        var completed = progressAfter >= 18;
        if (completed)
        {
            var finishedQuest = updatedSession.Character.PlayerQuests.TryGetQuest(finishQuestId, out var existingFinishQuest)
                ? existingFinishQuest with { Status = PlayerQuestStatus.Finished }
                : new PlayerQuestSnapshotEntry(finishQuestId, PlayerQuestStatus.Finished, Time: 0, Intentions: []);
            var acceptIntentions = updatedSession.Character.PlayerQuests.TryGetQuest(acceptQuestId, out var existingAcceptQuest)
                ? existingAcceptQuest.Intentions
                : new uint[TryGetLegacyQuestMission(context.WorldData, acceptQuestId, out var acceptMission)
                    ? Math.Max(0, GetLegacyQuestIntentionCount(acceptMission))
                    : 0];
            var acceptedQuest = new PlayerQuestSnapshotEntry(
                acceptQuestId,
                PlayerQuestStatus.Accepted,
                Time: 0,
                acceptIntentions);
            var updatedQuests = updatedSession.Character.PlayerQuests
                .WithQuest(finishedQuest)
                .WithQuest(acceptedQuest);

            updatedSession = updatedSession with
            {
                Character = updatedSession.Character with { PlayerQuests = updatedQuests },
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = updatedContext with { Session = updatedSession };
            await PersistLegacyPlayerQuestsAsync(
                    updatedContext,
                    updatedSession,
                    sourceBranch,
                    $"epic-trojan-ghost-reaver-complete finishQuestId={finishQuestId} acceptQuestId={acceptQuestId}",
                    finishQuestId,
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Connection.SendAsync(
                    LegacyQuestListPacket.CreateSingleQuestFrame(
                        LegacyQuestListMode.FinishQuest,
                        ToLegacyQuestListItem(finishedQuest)),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyQuestListPacket.CreateSingleQuestFrame(
                        LegacyQuestListMode.AcceptQuest,
                        ToLegacyQuestListItem(acceptedQuest)),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox(
                            "All~the~18~Ghost~Reavers~in~the~Flame~Devastator`s~army~have~been~eliminated.~Report~back~to~General~Pak!")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            updatedSession = await SendLegacyMonsterDeathTeleportAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    mapId: 3_830,
                    x: 162,
                    y: 218,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "QuestGUI.SendAutoPatcher",
                        "general-pak-return-autopatcher",
                        reason: "source MessageBox accept callback teleports to (3830,162,218) and sends QuestGUI.SendAutoPatcher(3830,154,130,10581); modern runtime models the source message and teleport, but the optional client auto-route prompt packet is not modeled yet",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("finishQuestId", finishQuestId),
                            LegacyParityGapDiagnostics.Fact("acceptQuestId", acceptQuestId),
                            LegacyParityGapDiagnostics.Fact("routeQuestId", generalPakRouteQuestId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("routeQuestId", generalPakRouteQuestId),
                        ]))
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3834 Epic Trojan Ghost Reaver death side effect " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"progressBefore={progressBefore} progressAfter={progressAfter} completed={completed} " +
                $"floorUid={floorUid} effectItemId={effectItemId} effectColor={blackColor} storedFloorEffect={storedFloorEffect} " +
                $"sourceEffectLifetimeSeconds={sourceEffectLifetimeSeconds} " +
                $"finishQuestId={finishQuestId} acceptQuestId={acceptQuestId} routeQuestId={generalPakRouteQuestId} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3833EpicTrojanFlameDevastatorAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monsterFamily.Id == 7_485)
        {
            const string sourceBranch = "MonsterRole.cs killer.Player.Map == 3833 && Family.ID == 7485";
            const int spawnedMonsterId = 7_486;
            var updatedCharacter = WithCharacterAttribute(session.Character, "StageEpicTrojanQuest", 51UL);
            var updatedSession = session with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };
            var dynamicId = ReadCharacterDynamicId(updatedCharacter);
            var spawnedFollowup = false;

            if (!HasVisibleLegacyDynamicMonster(context.MonsterStates, 3_833, dynamicId, spawnedMonsterId))
            {
                spawnedFollowup = await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                        updatedContext,
                        updatedSession,
                        monster,
                        monsterFamily,
                        spawnedMonsterId,
                        mapId: 3_833,
                        x: CheckedUInt16(updatedCharacter.X, nameof(updatedCharacter.X)),
                        y: CheckedUInt16(updatedCharacter.Y, nameof(updatedCharacter.Y)),
                        dynamicId: checked((long)dynamicId),
                        sourceBranch: "MonsterRole.cs Map.AddMapMonster monsterId=7486 ride_screen",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("The~earth~is~shaking,~and~the~Flame~Devastator~revived!")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 3833 Epic Trojan Flame Devastator death side effect " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"stage=51 spawnedMonsterId={spawnedMonsterId} spawnedFollowup={spawnedFollowup} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);

            return updatedSession;
        }

        if (monsterFamily.Id == 7_486)
        {
            const string family7486SourceBranch = "MonsterRole.cs killer.Player.Map == 3833 && Family.ID == 7486";
            const int family7486SpawnedMonsterId = 7_487;
            var family7486Character = WithCharacterAttribute(session.Character, "StageEpicTrojanQuest", 52UL);
            var family7486Session = session with { Character = family7486Character };
            context.Sessions?.Set(context.Connection, family7486Session);
            var family7486Context = context with { Session = family7486Session };
            var family7486DynamicId = ReadCharacterDynamicId(family7486Character);
            var family7486SpawnedFollowup = false;

            await SendLegacyLocationEffectToVisiblePlayersAsync(
                    family7486Context,
                    family7486Session,
                    x: 45,
                    y: 45,
                    effectName: "xidag_bafi",
                    cancellationToken)
                .ConfigureAwait(false);

            if (!HasVisibleLegacyDynamicMonster(
                    context.MonsterStates,
                    3_833,
                    family7486DynamicId,
                    family7486SpawnedMonsterId))
            {
                family7486SpawnedFollowup = await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                        family7486Context,
                        family7486Session,
                        monster,
                        monsterFamily,
                        family7486SpawnedMonsterId,
                        mapId: 3_833,
                        x: CheckedUInt16(family7486Character.X, nameof(family7486Character.X)),
                        y: CheckedUInt16(family7486Character.Y, nameof(family7486Character.Y)),
                        dynamicId: checked((long)family7486DynamicId),
                        sourceBranch: "MonsterRole.cs Map.AddMapMonster monsterId=7487 xidag_bafi",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 3833 Epic Trojan Flame Devastator death side effect " +
                    $"{FormatSession(family7486Context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"stage=52 effect=xidag_bafi spawnedMonsterId={family7486SpawnedMonsterId} " +
                    $"spawnedFollowup={family7486SpawnedFollowup} branch=\"{family7486SourceBranch}\".")
                .ConfigureAwait(false);

            return family7486Session;
        }

        if (monsterFamily.Id != 7_487)
        {
            return session;
        }

        const string family7487SourceBranch = "MonsterRole.cs killer.Player.Map == 3833 && Family.ID == 7487";
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket
                    .ForMessageBox(
                        "Holding~the~Epic~Weapon~infused~with~the~blade~spirit,~you~beheaded~the~Flame~Devastator~and~burned~it~into~ashes.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        var family7487Session = await SendLegacyMonsterDeathTeleportAsync(
                context,
                session,
                monster,
                monsterFamily,
                mapId: 3_832,
                x: 154,
                y: 132,
                family7487SourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        var family7487Context = context with { Session = family7487Session };

        await context.Connection.SendAsync(
                LegacyStringPacket
                    .PlayerEffect(CheckedUInt32(family7487Session.Character.Uid, nameof(family7487Session.Character.Uid)), "role-select1")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var weaponConverted = false;
        var convertedFromInventory = false;
        var convertedWeaponUid = 0U;
        var convertedWeaponItemId = 0U;
        var globalMessageCount = 0;
        var bladeSpiritItemConsumed = false;
        if (TryConvertLegacyEpicTrojanEligibleWeapon(
                family7487Session.PlayerItems,
                out var itemsAfterWeaponConversion,
                out var convertedWeapon,
                out convertedFromInventory))
        {
            weaponConverted = true;
            convertedWeaponUid = convertedWeapon.Uid;
            convertedWeaponItemId = convertedWeapon.ItemId;

            await context.Connection.SendAsync(
                    LegacyGameItemPacket
                        .FromSnapshotItem(convertedWeapon, mode: LegacyGameItemPacket.UpdateItemMode)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            family7487Session = family7487Session with
            {
                Character = ResetLegacyEpicTrojanState(family7487Session.Character),
                PlayerItems = itemsAfterWeaponConversion,
            };
            context.Sessions?.Set(context.Connection, family7487Session);
            family7487Context = context with { Session = family7487Session };

            globalMessageCount = await BroadcastLegacyMap3833EpicTrojanSuccessMessageAsync(
                    family7487Context,
                    family7487Session,
                    cancellationToken)
                .ConfigureAwait(false);

            var bladeSpiritItem = family7487Session.PlayerItems.InventoryItems
                .FirstOrDefault(item => item.ItemId == LegacyEpicTrojanBladeSpiritItemId);
            if (bladeSpiritItem is not null)
            {
                var itemsAfterBladeSpirit = ConsumeSingleLegacyInventoryItem(
                    family7487Session.PlayerItems,
                    bladeSpiritItem,
                    out var bladeSpiritFrame);
                await context.Connection.SendAsync(bladeSpiritFrame, cancellationToken).ConfigureAwait(false);
                family7487Session = family7487Session with { PlayerItems = itemsAfterBladeSpirit };
                context.Sessions?.Set(context.Connection, family7487Session);
                family7487Context = context with { Session = family7487Session };
                bladeSpiritItemConsumed = true;
            }
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3833 Epic Trojan Flame Devastator completion side effect " +
                $"{FormatSession(family7487Context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"map=3832 x=154 y=132 effect=role-select1 weaponConverted={weaponConverted} " +
                $"convertedFrom={(convertedFromInventory ? "inventory" : weaponConverted ? "equipment" : "none")} " +
                $"convertedWeaponUid={convertedWeaponUid} convertedWeaponItemId={convertedWeaponItemId} " +
                $"resetEpicTrojan={weaponConverted} globalMessageCount={globalMessageCount} " +
                $"bladeSpiritItemConsumed={bladeSpiritItemConsumed} " +
                $"branch=\"{family7487SourceBranch}\".")
            .ConfigureAwait(false);

        return family7487Session;
    }
}
