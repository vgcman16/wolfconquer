using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Native FloorMap item branch (MsgItemPacket.cs:187..190) calls this after
    // item/floor-removal/pickup text. Quests.IncreaseQuestObjectives:102..174
    // uses the accepted, non-Kingdom row for 1311, expanding only when needed.
    private static async ValueTask IncreaseLegacyFloorPickupQuest1311Async(WorldPacketHandlerContext context,
        WorldSession admitted, LegacyNativePlayerRuntime? pickupPlayer, CancellationToken cancellationToken)
    {
        var reason = "sessionStoreUnavailable";
        WorldSession? committed = null;
        byte[]? objectiveFrame = null;
        var hasQuestRoot = !string.IsNullOrWhiteSpace(context.PlayerQuestsRoot);
        if (context.Sessions is { } sessions)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!sessions.TryGet(context.Connection, out var current) || current is null)
                { reason = "currentSessionMissing"; break; }
                if (!MatchesLegacyFloorPickupQuestOwner(context, admitted, pickupPlayer, current))
                { reason = "pickupOwnerChanged"; break; }
                var quests = current.Character.PlayerQuests;
                var rows = quests.Quests.Where(quest => quest.Uid == 1_311).Take(2).ToArray();
                if (rows.Length > 1)
                { reason = "questRowAmbiguous"; break; }
                if (rows.Length == 0 || rows[0].Status != PlayerQuestStatus.Accepted)
                { reason = "questNotAccepted"; break; }
                var quest = rows[0];
                var intentions = quest.Intentions.Count == 0 ? new uint[1] : quest.Intentions.ToArray();
                intentions[0] = unchecked(intentions[0] + 1u);
                var increased = quest with { Intentions = intentions };
                var updated = WithPlayerQuests(current, new PlayerQuestSnapshot(
                    quests.Quests.Select(row => row.Uid == 1_311 ? increased : row).ToArray()));
                byte[] frame;
                try { frame = MsgQuestDataPacket.CreateObjectiveFrame(increased); }
                catch (Exception ex) when (ex is ArgumentException or OverflowException)
                { reason = "questRepresentationInvalid:" + ex.GetType().Name; break; }

                var failure = string.Empty;
                if (!sessions.TryCommitSession(context.Connection, current, updated, () =>
                {
                    if (!MatchesLegacyFloorPickupQuestOwner(context, admitted, pickupPlayer, current))
                    { failure = "pickupOwnerChanged"; return false; }
                    return !hasQuestRoot || TryPersistLegacyFloorPickupQuest1311(context.PlayerQuestsRoot!,
                        current.Character.Uid, quests, updated.Character.PlayerQuests, out failure);
                }))
                {
                    // Only a pre-callback CAS race is retried, against its latest
                    // wallet/items/quest state. Never retry an ambiguous save here.
                    if (failure.Length != 0) { reason = failure; break; }
                    reason = "sessionChanged";
                    continue;
                }
                committed = updated;
                objectiveFrame = frame;
                break;
            }
        }
        if (committed is null)
        {
            await context.Log.WriteLineAsync($"WorldServer skipped LegacyFloorItem quest1311 {FormatSession(context)}; {reason}.")
                .ConfigureAwait(false);
            return;
        }

        // TryCommitSession has released the connection and file gates. It does
        // not invoke ordinary autosave; configured quest persistence already ran.
        await context.Connection.SendAsync(objectiveFrame!, cancellationToken).ConfigureAwait(false);
        if (!hasQuestRoot)
            await context.Log.WriteLineAsync($"PARITY-GAP LegacyFloorItem quest1311 {FormatSession(context with { Session = committed })}; " +
                "quest update is session-only because no PlayerQuestsRoot is available.").ConfigureAwait(false);
        else
            await context.Log.WriteLineAsync($"WorldServer handled LegacyFloorItem quest1311 {FormatSession(context with { Session = committed })}; " +
                "persistence=quest-file-before-publication.").ConfigureAwait(false);
    }

    private static bool MatchesLegacyFloorPickupQuestOwner(WorldPacketHandlerContext context,
        WorldSession admitted, LegacyNativePlayerRuntime? pickupPlayer, WorldSession current)
    {
        if (current.AccountId <= 0 || current.AccountId != admitted.AccountId ||
            current.Character.Uid is <= 0 or > uint.MaxValue || current.Character.Uid != admitted.Character.Uid ||
            !ReferenceEquals(current.NativeGameClient, admitted.NativeGameClient) ||
            !ReferenceEquals(context.Connection.NativeGameClient, admitted.NativeGameClient)) return false;
        if (pickupPlayer is null) return current.NativeGameClient is null;
        return ReferenceEquals(current.NativePlayer, pickupPlayer) &&
            ReferenceEquals(pickupPlayer.Owner, current.NativeGameClient) && pickupPlayer.UID == current.Character.Uid;
    }
}
