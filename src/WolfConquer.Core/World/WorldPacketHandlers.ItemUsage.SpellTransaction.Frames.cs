using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static byte[][] BuildItemSpellRefresh(WorldPacketHandlerContext context, WorldSession session, object identity)
    {
        if (context.WorldData is not { } worldData) return [];
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var hitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        var maximum = ComputeLegacyRuntimeMaxHitpoints(session.Character, hitpoints, session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty, worldData, session.AlternateEquipmentActive);
        var result = new List<byte[]>();
        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(uid, maximum, Math.Min(hitpoints, maximum)))
            result.Add(update.ToFrameBytes());
        result.Add(CreateNativeMagicCachedPlayerStatusResponse(context, session, context.Connection, identity).ToFrameBytes());
        result.Add(LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(uid, session.PlayerItems).ToFrameBytes());
        return result.ToArray();
    }

    private static WorldSession NormalizeItemSpellHitpoints(WorldSession original, WorldSession updated)
    {
        if (original.Character.MinHitPoints == updated.Character.MinHitPoints) return updated;
        var attributes = new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["MinHitPoints"] = updated.Character.MinHitPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        return updated with { Character = updated.Character with { Attributes = attributes } };
    }

    private static bool ItemSpellFramesEqual(byte[][] first, byte[][] second) =>
        first.Length == second.Length && first.Where((frame, index) => !frame.AsSpan().SequenceEqual(second[index])).Any() == false;

    private static bool ItemSpellProjectionMatches(ItemSpellProjection first, ItemSpellProjection second) =>
        first.UpdateRank == second.UpdateRank && RuneOperationsEqual(first.Rune?.Operation, second.Rune?.Operation) &&
        RuneContainerOperationsEqual(first.Container?.Operation, second.Container?.Operation) &&
        RuneComposeOperationsEqual(first.Compose?.Operation, second.Compose?.Operation) &&
        RuneChanceOperationsEqual(first.Chance?.Operation, second.Chance?.Operation) &&
        FullAccountCharacterScalarsEqual(first.Updated.Character, second.Updated.Character) &&
        FullAccountStoredFieldsMatch(first.Updated.Character.Attributes, second.Updated.Character.Attributes) &&
        FullAccountStoredFieldsMatch(second.Updated.Character.Attributes, first.Updated.Character.Attributes) &&
        PlayerItemSnapshotLoader.ToLegacyBytes(first.Updated.PlayerItems).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(second.Updated.PlayerItems)) &&
        first.Updated.PlayerSpells.Spells.SequenceEqual(second.Updated.PlayerSpells.Spells) &&
        ItemSpellFramesEqual(first.Frames, second.Frames) &&
        (first.Refresh is null ? second.Refresh is null : second.Refresh is not null && ItemSpellFramesEqual(first.Refresh, second.Refresh));

    private static async ValueTask PublishItemSpellFramesAsync(WorldPacketHandlerContext context, WorldSession final,
        byte[][] frames, object identity, CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions) return;
        foreach (var frame in frames)
        {
            if (!Current()) return;
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!Current()) return;
        }
        bool Current()
        {
            if (context.TradeRecovery is not { IsBlocked: false } || context.Trades!.HasConnectionEnded(context.Connection) ||
                !LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, final.Character.Uid, identity, out var current) ||
                !ReferenceEquals(current, final) || !ReferenceEquals(current.NativeGameClient, context.Connection.NativeGameClient)) return false;
            var client = current.NativeGameClient;
            return client is null || client.OrdinarySpellBinding is { } binding && binding.Store.IsBoundSpellOwnerCurrent(binding);
        }
    }
}
