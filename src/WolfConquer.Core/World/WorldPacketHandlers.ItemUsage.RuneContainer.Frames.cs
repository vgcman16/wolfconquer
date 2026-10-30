namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ItemSpellProjection PrepareOwnedRuneContainerProjection(WorldPacketHandlerContext context,
        WorldSession stable, object identity, LegacyRuneContainerKind kind, uint uid, byte? storageType)
    {
        if ((kind == LegacyRuneContainerKind.Drop) != (storageType is null) ||
            storageType is { } type && (type > 1 || (type == 0) != (kind == LegacyRuneContainerKind.Store))) throw InvalidSpellMutation();
        var evidence = LegacyRuneContainerEvidence.Capture(context, stable, kind, uid);
        var projected = ProjectRecordedRuneContainer(stable.Character, stable.PlayerItems, stable.PlayerSpells, evidence.Operation);
        var changed = stable with { Character = projected.Character, PlayerItems = projected.Items, PlayerSpells = projected.Spells };
        var actual = kind == LegacyRuneContainerKind.Drop ? changed
            : NormalizeItemSpellHitpoints(stable, ClampLegacyEquipmentRuntimeHitpoints(changed, context.WorldData));
        if (actual.Character.MinHitPoints != projected.Character.MinHitPoints || !evidence.IsCurrent()) throw InvalidSpellMutation();
        var output = new List<byte[]>();
        foreach (var item in projected.Events)
        {
            if (item.Kind != LegacyRuneOperationEventKind.RemoveSpell) throw InvalidSpellMutation();
            output.Add(new MsgActionPacket { ObjId = CheckedUInt32(stable.Character.Uid, nameof(stable.Character.Uid)),
                DwParam = item.Value, Type = (MsgActionType)109 }.ToFrameBytes());
        }
        output.Add(storageType is { } responseType
            ? new LegacyRuneStoragePacket(responseType, uid,
                LegacyRuneStoragePacket.Startup(projected.Items, context.WorldData?.RuneStorageAttributes).HPAdd).ToFrameBytes()
            : new MsgItemUsagePacket(uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes());
        // Storage's complete query runs after its response. Drop's original
        // sequence contains only the item removal, plus any newly required
        // proven temporary-spell retirement above; neither invents an equip echo.
        return new(actual, output.ToArray(), Refresh: null, UpdateRank: storageType is null && context.RuneRankings is not null,
            Rune: null, Container: evidence);
    }

    private static async ValueTask HandleOwnedRuneContainerStorageAsync(WorldPacketHandlerContext context,
        WorldSession session, LegacyRuneStoragePacket packet, CancellationToken cancellationToken)
    {
        var kind = packet.Type == 0 ? LegacyRuneContainerKind.Store : LegacyRuneContainerKind.Bag;
        if (!TryCommitLegacyItemSpell(context, session,
                (stable, identity) => PrepareOwnedRuneContainerProjection(context, stable, identity, kind, packet.ItemUID, packet.Type),
                cancellationToken, out var updated, out var frames, out var publicationIdentity, out var reason))
        {
            await context.Log.WriteLineAsync($"WorldServer held rune storage type={packet.Type} reason={reason}; no success was published.").ConfigureAwait(false);
            return;
        }
        await PublishItemSpellFramesAsync(context, updated, frames, publicationIdentity, cancellationToken).ConfigureAwait(false);
        if (context.Sessions is not { } store || !LegacyNativeClientPublication.TryCurrent(store, context.Connection,
                updated.Character.Uid, publicationIdentity, out var current) || !ReferenceEquals(current, updated)) return;
        var query = await ExecuteLegacyEquipmentQueryAsync(context with { Session = updated }, updated,
            new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.RuneStorageQuery,
                new LegacyEquipmentQueryPublicationScope(updated.Character.Uid, updated.PlayerItems)
                    { OwnerNativeClientIdentity = publicationIdentity }), cancellationToken).ConfigureAwait(false);
        if (!query.Completed) return; // The container WAL is already committed; never report a rejected mutation.
        UpdateLegacyRuneRanking(context with { Session = query.Session }, query.Session!);
        await context.Log.WriteLineAsync($"WorldServer committed LegacyRuneStorage {FormatSession(context with { Session = updated })} type={packet.Type} itemUid={packet.ItemUID}.").ConfigureAwait(false);
    }
}
