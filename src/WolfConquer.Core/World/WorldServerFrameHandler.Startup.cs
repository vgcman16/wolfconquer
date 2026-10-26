using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private static readonly HashSet<uint> OldClientMissingItemTypeIds =
    [
        729_464,
        3_316_793,
    ];

    private async ValueTask SendLegacyWorldEntryStartupAsync(
        ConquerTcpConnection connection,
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells,
        CombatGearSnapshot combatGear,
        CancellationToken cancellationToken,
        LegacyNativeGameClientRuntime? nativeLoadedClient = null,
        bool preserveMailboxRecoveryState = false,
        LegacySpellPublicationOwner? loginSpells = null)
    {
        PrepareLegacyLocalServerIdentity(connection, snapshot.Uid);
        await SendLegacyWorldEntryStartupPrefixAsync(connection, cancellationToken).ConfigureAwait(false);
        await _log.WriteLineAsync(
                $"WorldServer legacy startup prefix complete uid={snapshot.Uid} mode={_worldEntryStartupProbe.PrefixMode}.")
            .ConfigureAwait(false);

        if (LegacyCollectionStoragePacket.TryCreateFromSnapshot(snapshot, out var collectionStorage))
        {
            await SendLegacyWorldEntryStartupFrameAsync(
                    connection,
                    snapshot,
                    "collection-storage",
                    collectionStorage.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyWorldEntryStartupFrameAsync(
                connection,
                snapshot,
                "loader-hero",
                LegacyLoaderHeroInfoPacket.FromSnapshot(snapshot,
                    nativePlayer: nativeLoadedClient?.Player).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyWorldEntryStartupFrameAsync(
                connection,
                snapshot,
                "proto-hero",
                LegacyProtoHeroInfoPacket.FromSnapshot(snapshot, usersRoot: _snapshotLoader?.UsersRoot,
                    nativePlayer: nativeLoadedClient?.Player).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        var chiState = LegacyChiState.FromSnapshot(snapshot);
        foreach (var ranking in LegacyGenericRankingPacket.CreateChiQueryCountStartup(snapshot.Uid, snapshot.Name, chiState))
        {
            await connection.SendAsync(ranking.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await connection.SendAsync(
                ChiInfoPacketProfileSelector.CreateStartupFrame(_protocolProfile, snapshot.Uid, chiState),
                cancellationToken)
            .ConfigureAwait(false);
        if (_protocolProfile == ConquerProtocolProfile.VisibleDx8D13 &&
            VisibleDx8D13TrainingVitalityExpiryNotifyPacket.TryCreateFromSnapshot(
                snapshot,
                (_runtimeUpdateLoop.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime,
                out var expiryNotify,
                out _))
        {
            await connection.SendAsync(expiryNotify.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacySecondaryPasswordPacket.TryCreateStartup(snapshot, out var secondaryPassword))
        {
            await connection.SendAsync(secondaryPassword.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await connection.SendAsync(LegacyAchievementPacket.FromSnapshot(snapshot).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(LegacyNobilityIconPacket.FromSnapshot(snapshot).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyGameItemsAsync(connection, playerItems.InventoryItems, "inventory", cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyGameItemsAsync(connection, playerItems.RelicFuseItems, "relic-fuse", cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyGameItemsAsync(connection, playerItems.MythsoulBagItems, "mythsoul-bag", cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyGameItemsAsync(connection, playerItems.EquipmentItems, "equipment", cancellationToken)
            .ConfigureAwait(false);
        if (!preserveMailboxRecoveryState)
        {
            if (!await SendLegacyEquipmentStartupSideEffectsAsync(
                    connection,
                    snapshot,
                    playerItems,
                    playerSpells,
                    combatGear,
                    cancellationToken).ConfigureAwait(false) ||
                !TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, snapshot.Uid, out var afterEquipmentQuery)) return;
            snapshot = afterEquipmentQuery.Character;
            playerItems = afterEquipmentQuery.PlayerItems;
            playerSpells = afterEquipmentQuery.PlayerSpells;
            combatGear = afterEquipmentQuery.CombatGear ?? CombatGearSnapshot.Empty;
        }
        await SendLegacyGameItemsAsync(connection, playerItems.RuneItems, "rune", cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(LegacyAutoHuntPacket.Startup.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        var activeness = LegacyActivenessState.FromSnapshot(snapshot);
        await connection.SendAsync(LegacyActivityTasksPacket.FromState(activeness).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(LegacyActivityRewardsInfoPacket.FromState(activeness).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(LegacyActivityClaimsPacket.FromState(activeness).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        if (LegacyFlowerPacket.TryCreateLoginFreeFlowers(snapshot, out var freeFlowers))
        {
            await connection.SendAsync(freeFlowers.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacyClanPacket.TryCreateStartup(snapshot, _snapshotLoader?.UsersRoot, out var clan))
        {
            await connection.SendAsync(clan.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacyJiangHuInfoPacket.TryCreateStartup(snapshot, out var jiangHu))
        {
            await connection.SendAsync(jiangHu.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyInnerPowerPotencyStartupUpdate(snapshot, _snapshotLoader?.UsersRoot)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyVipLevelStartupUpdate(snapshot).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(LegacyVipStatusPacket.FromSnapshot(snapshot).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyMerchantStartupUpdate(snapshot).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                MsgActionPacket.CreateLegacyMerchantStartup(CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var titleStorage in LegacyTitleStoragePacket.CreateLegacyStartupCheckUpRemovals(
                     snapshot,
                     _snapshotLoader?.UsersRoot,
                     playerItems,
                     _worldData?.CoatStorageTypesByItemTypeId))
        {
            await connection.SendAsync(titleStorage.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var coatStorage in LegacyCoatStoragePacket.CreateLegacyStartup(playerItems))
        {
            await connection.SendAsync(coatStorage.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var titleStorage in LegacyTitleStoragePacket.CreateLegacyStartupFullLoad(
                     snapshot,
                     _snapshotLoader?.UsersRoot,
                     playerItems,
                     _worldData?.CoatStorageTypesByItemTypeId))
        {
            await connection.SendAsync(titleStorage.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacyStartupWelcomePackets.TryCreatePrivilegedBroadcast(snapshot, out var privilegedBroadcast))
        {
            await connection.SendAsync(privilegedBroadcast, cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacyWeatherPacket.TryCreateStartup(snapshot, out var weather))
        {
            await connection.SendAsync(weather.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var welcomeFrame in LegacyStartupWelcomePackets.CreateEnglishStartup(ConquerRuntimeDefaults.ServerName))
        {
            await connection.SendAsync(welcomeFrame, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyInventorySashStartupUpdates(snapshot))
        {
            await connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        // MsgLoginHandler392-394: Add(1110) sends one new-grant2101 here.
        // Existing saved spells belong to client Action78, not this startup slot.
        if (loginSpells is not null && !await loginSpells.PublishAsync(null, cancellationToken).ConfigureAwait(false)) return;

        if (LegacySameGroupServerListPacket.TryCreateFromClientConfigs(_worldData?.ClientConfigs, out var groupServers))
        {
            await connection.SendAsync(groupServers.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await connection.SendAsync(
                LegacyRuneRankMessage.CreateLiveStartup(snapshot, playerItems, _runeRankings).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await connection.SendAsync(
                LegacyRuneStoragePacket.Startup(playerItems, _worldData?.RuneStorageAttributes).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyThunderStrikerUndyingImprintingStartupAsync(
                connection,
                snapshot,
                playerSpells,
                cancellationToken)
            .ConfigureAwait(false);
        if (LegacyOverheadLeagueInfoPacket.TryCreateStartup(snapshot, out var overheadLeagueInfo))
        {
            await connection.SendAsync(overheadLeagueInfo.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyPKPointsStartupUpdate(snapshot).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyClassExperienceStartupUpdate(snapshot,
                    nativePlayer: nativeLoadedClient?.Player).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        if (LegacyCombatGearPacket.TryCreateStartup(snapshot, combatGear, out var combatGearPacket))
        {
            await connection.SendAsync(combatGearPacket.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            foreach (var spell in LegacyCombatGearPacket.CreatePirateStartupSpells(
                         snapshot,
                         combatGear,
                         _worldData?.CombatGearByGearTypeAndLevel,
                         _worldData?.DaoqiDictTypesById))
            {
                await connection.SendAsync(spell.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (_clientCompatibility.EnableNewerClientRevelatorStartupEffect &&
                LegacyCombatGearStartupEffectPacket.TryCreatePirateRevelatorStartupFrames(
                    snapshot,
                    playerSpells,
                    out var startupEffectFrames))
            {
                foreach (var startupEffectFrame in startupEffectFrames)
                {
                    await connection.SendAsync(startupEffectFrame, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (!preserveMailboxRecoveryState)
                {
                    if (!await SendLegacyEquipmentStartupSideEffectsAsync(
                            connection,
                            snapshot,
                            playerItems,
                            playerSpells,
                            combatGear,
                            cancellationToken).ConfigureAwait(false) ||
                        !TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, snapshot.Uid, out var afterRevelatorQuery)) return;
                    snapshot = afterRevelatorQuery.Character;
                    playerItems = afterRevelatorQuery.PlayerItems;
                    playerSpells = afterRevelatorQuery.PlayerSpells;
                    combatGear = afterRevelatorQuery.CombatGear ?? CombatGearSnapshot.Empty;
                }
            }

            await connection.SendAsync(LegacyAirPowerUpPacket.CreateStartup(snapshot, combatGear).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        var hasMail = _mailboxes is not null &&
            _mailboxes.List(CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid))).Count > 0;
        await connection.SendAsync(LegacyMailNotifyPacket.Startup(hasMail).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(LegacyDragonSkinPacket.CreateStartup(snapshot).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyAssociationStartupPacketsAsync(connection, snapshot, cancellationToken)
            .ConfigureAwait(false);

        if (LegacyBeastsInfoPacket.TryCreateStartup(snapshot, playerItems, out var beastsInfo))
        {
            await connection.SendAsync(beastsInfo.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var donationUpdate in LegacyMsgUpdatePacket.CreateLegacyDonationStartupUpdates(snapshot))
        {
            await connection.SendAsync(donationUpdate.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask SendLegacyWorldEntryStartupFrameAsync(
        ConquerTcpConnection connection,
        CharacterSnapshot snapshot,
        string stage,
        ReadOnlyMemory<byte> frame,
        CancellationToken cancellationToken)
    {
        var opcode = 0;
        var payloadLength = frame.Length >= PacketHeader.Size ? frame.Length - PacketHeader.Size : 0;
        if (PacketHeader.TryRead(frame.Span, out var header))
        {
            opcode = header.Opcode;
            payloadLength = Math.Max(0, header.Length - PacketHeader.Size);
        }

        await _log.WriteLineAsync(
                "WorldServer legacy startup send attempt " +
                $"stage={stage} uid={snapshot.Uid} opcode={opcode} payloadLength={payloadLength}.")
            .ConfigureAwait(false);

        try
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (NetworkExceptionClassifier.IsPeerDisconnect(ex))
        {
            await _log.WriteLineAsync(
                    "WorldServer legacy startup send peer-disconnect " +
                    $"stage={stage} uid={snapshot.Uid} opcode={opcode} payloadLength={payloadLength} " +
                    $"exception={ex.GetType().Name}: {ex.Message}")
                .ConfigureAwait(false);
            throw;
        }

        await _log.WriteLineAsync(
                "WorldServer legacy startup send returned " +
                $"stage={stage} uid={snapshot.Uid} opcode={opcode} payloadLength={payloadLength}.")
            .ConfigureAwait(false);
    }

    private async ValueTask SendLegacyWorldEntryStartupPrefixAsync(
        ConquerTcpConnection connection,
        CancellationToken cancellationToken)
    {
        await connection.SendAsync(new LegacyServerVersionPacket().ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        switch (_worldEntryStartupProbe.PrefixMode)
        {
            case LegacyWorldEntryStartupPrefixProbeMode.DecompiledSourceCompatible:
                await connection.SendAsync(LegacyServerTimerPacket.FromDateTime(DateTime.Now).ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                await connection.SendAsync(new LegacyServerConfigPacket().ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                break;
            case LegacyWorldEntryStartupPrefixProbeMode.ConfigBeforeTimer:
                await connection.SendAsync(new LegacyServerConfigPacket().ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                await connection.SendAsync(LegacyServerTimerPacket.FromDateTime(DateTime.Now).ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                break;
            case LegacyWorldEntryStartupPrefixProbeMode.SkipTimer:
                await connection.SendAsync(new LegacyServerConfigPacket().ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported world-entry startup prefix probe mode: {_worldEntryStartupProbe.PrefixMode}.");
        }
    }

    private static bool HasLegacyRevelatorStartupEffect(
        CharacterSnapshot snapshot,
        PlayerSpellSnapshot playerSpells)
    {
        if (snapshot.Class / 1000 != 7)
        {
            return false;
        }

        foreach (var spell in playerSpells.Spells)
        {
            if (spell.Id == LegacyCombatGearStartupEffectPacket.RevelatorSpellId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLegacyOverwhelmRune(
        PlayerItemSnapshot playerItems,
        WorldDataCatalog? worldData)
    {
        if (worldData is null)
        {
            return false;
        }

        foreach (var item in playerItems.RuneItems)
        {
            if (item.ItemId > int.MaxValue ||
                !worldData.ItemsById.TryGetValue((int)item.ItemId, out var itemTypes))
            {
                continue;
            }

            foreach (var itemType in itemTypes)
            {
                if (itemType.Name.Equals("Overwhelm", StringComparison.OrdinalIgnoreCase) ||
                    itemType.Name.StartsWith("Overwhelm(", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async ValueTask<bool> SendLegacyEquipmentStartupSideEffectsAsync(
        ConquerTcpConnection connection,
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells,
        CombatGearSnapshot combatGear,
        CancellationToken cancellationToken)
    {
        if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, snapshot.Uid, out var current)) return false;
        var context = CreateMonsterRuntimeContext(new WorldActiveSession(connection, current));
        var query = await WorldPacketHandlers.ExecuteLegacyEquipmentQueryAsync(context, current,
            new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.InitialWorldEntry,
                new LegacyEquipmentQueryPublicationScope(snapshot.Uid)),
            cancellationToken).ConfigureAwait(false);
        return query.Completed;
    }

    private static async ValueTask SendLegacyMsgUpdatePacketsAsync(
        ConquerTcpConnection connection,
        IReadOnlyList<LegacyMsgUpdatePacket> packets,
        CancellationToken cancellationToken)
    {
        foreach (var packet in packets)
        {
            await connection.SendAsync(packet.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<LegacySpellPacket> CreateLegacyEquipmentStartupSpells(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells)
    {
        var spells = new List<LegacySpellPacket>();
        if (TryFindSpell(playerSpells, 21_930, out var pirateRankingSpell))
        {
            spells.Add(LegacySpellPacket.FromSnapshot(pirateRankingSpell));
        }
        else if (IsLegacyPirateClass(snapshot.Class))
        {
            spells.Add(new LegacySpellPacket(
                Experience: 0,
                Id: 21_930,
                Unknown: 0,
                Level: 10,
                Type: 0,
                UseSoul: 0,
                Soul: 0,
                HashLevelSoul: 0));
        }

        if (TryFindSpell(playerSpells, 20_000, out var venomSpell))
        {
            spells.Add(LegacySpellPacket.FromSnapshot(venomSpell));
        }
        else
        {
            var venomLevel = ComputeLegacyVenomLevel(playerItems);
            if (venomLevel > 0)
            {
                spells.Add(new LegacySpellPacket(
                    Experience: 0,
                    Id: 20_000,
                    Unknown: 0,
                    Level: venomLevel,
                    Type: 0,
                    UseSoul: 0,
                    Soul: 0,
                    HashLevelSoul: 0));
            }
        }

        return spells;
    }

    private static bool TryFindSpell(
        PlayerSpellSnapshot playerSpells,
        ushort spellId,
        out PlayerSpellSnapshotEntry spell)
    {
        foreach (var current in playerSpells.Spells)
        {
            if (current.Id == spellId)
            {
                spell = current;
                return true;
            }
        }

        spell = default!;
        return false;
    }

    private static bool IsLegacyPirateClass(long classCode)
    {
        return classCode / 1000 == 7;
    }

    private static ushort ComputeLegacyVenomLevel(PlayerItemSnapshot playerItems)
    {
        var level = 0u;
        foreach (var item in playerItems.EquipmentItems)
        {
            if (item.MythSoul == 0)
            {
                continue;
            }

            var current = item.MythSoul % 10;
            if ((item.MythSoul % 1000) - current == 10)
            {
                level += current;
            }
        }

        return checked((ushort)Math.Min(level, ushort.MaxValue));
    }

    private static uint ComputeLegacyStartupMaxHitpoints(
        CharacterSnapshot snapshot,
        uint hitpoints,
        PlayerItemSnapshot playerItems,
        CombatGearSnapshot combatGear,
        WorldDataCatalog? worldData)
    {
        return LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
            snapshot,
            hitpoints,
            playerItems,
            combatGear,
            worldData);
    }

    private static uint ReadOptionalUInt32(CharacterSnapshot snapshot, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (snapshot.Attributes.TryGetValue(key, out var raw) &&
                uint.TryParse(raw, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private async ValueTask SendLegacyAssociationStartupPacketsAsync(
        ConquerTcpConnection connection,
        CharacterSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (_worldData is null ||
            !TryGetSocialAssociations(snapshot, out var records))
        {
            return;
        }

        var onlineTargetIds = _sessions
            .Snapshot()
            .Where(active => active.Session.Character.Uid != snapshot.Uid)
            .Select(active => CheckedUInt32(active.Session.Character.Uid, nameof(active.Session.Character.Uid)))
            .ToHashSet();

        var ownerId = CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        foreach (var record in records)
        {
            if (record.OwnerId < 0 || (uint)record.OwnerId != ownerId || record.TargetId < 0)
            {
                continue;
            }

            var targetOnline = onlineTargetIds.Contains((uint)record.TargetId);
            if (LegacyAssociationStartupPackets.TryCreateKnownPerson(record, targetOnline, out var knownPerson))
            {
                await connection.SendAsync(knownPerson.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (LegacyTradePartnerStartupPackets.TryCreateTradePartner(record, targetOnline, out var tradePartner))
            {
                await connection.SendAsync(tradePartner.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (!targetOnline &&
                LegacyApprenticeInformationStartupPackets.TryCreateOfflineApprentice(record, out var apprentice))
            {
                await connection.SendAsync(apprentice.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private bool TryGetSocialAssociations(
        CharacterSnapshot snapshot,
        out IReadOnlyList<SocialAssociationRecord> records)
    {
        records = [];
        if (snapshot.Uid < int.MinValue || snapshot.Uid > int.MaxValue)
        {
            return false;
        }

        if (_worldData is null ||
            !_worldData.SocialAssociationsByOwnerId.TryGetValue((int)snapshot.Uid, out var found) ||
            found is null)
        {
            return false;
        }

        records = found;
        return true;
    }

    private async ValueTask SendLegacyGameItemsAsync(
        ConquerTcpConnection connection,
        IReadOnlyList<PlayerItemSnapshotItem> items,
        string category,
        CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            if (ShouldSkipOldClientCatalogItem(item, category))
            {
                continue;
            }

            await connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(
                            item,
                            LegacyGameItemPacket.CreateRuneSpellOverride(item, _worldData?.ItemsById))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (LegacyItemExtraPacket.TryCreate(item, DateTime.Now, out var itemExtra))
            {
                await connection.SendAsync(itemExtra.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (LegacyItemLockPacket.TryCreateUnlockDate(item, DateTime.Now, out var itemLock))
            {
                await connection.SendAsync(itemLock.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private bool ShouldSkipOldClientCatalogItem(PlayerItemSnapshotItem item, string category)
    {
        if (_clientCompatibility.EnableNewerClientCatalogItems)
        {
            return false;
        }

        if (OldClientMissingItemTypeIds.Contains(item.ItemId))
        {
            LogSkippedOldClientCatalogItem(item, category, "incompatible with old-client itemtype catalog");
            return true;
        }

        if (_worldData?.Items.Count is not > 0)
        {
            return false;
        }

        if (item.ItemId <= int.MaxValue &&
            _worldData.ItemsById.ContainsKey((int)item.ItemId))
        {
            return false;
        }

        LogSkippedOldClientCatalogItem(item, category, "missing itemtype catalog");
        return true;
    }

    private void LogSkippedOldClientCatalogItem(PlayerItemSnapshotItem item, string category, string reason)
    {
        _log.WriteLine(
            $"WorldServer skipped old-client startup item {reason} " +
            $"category={category} uid={item.Uid} itemId={item.ItemId} position={item.Position} stack={item.StackSize} " +
            "nextSeam=legacy-client-itemtype-catalog-map.");
    }
}
