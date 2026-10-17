using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRealmTransferServicesFootballInventoryFullMessage =
        "Please make 1 more space in your inventory.";

    private static async ValueTask ApplyLegacyRealmTransferServicesFootballRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        CancellationToken cancellationToken)
    {
        const string sourceLines = "25936-25991";
        var (cost, rewardItemId, rewardName) = optionId switch
        {
            3 => (30U, 4_200_009U, "EuroSilveTrophy"),
            4 => (60U, 4_200_010U, "EuroGoldTrophy"),
            _ => throw new ArgumentOutOfRangeException(nameof(optionId)),
        };

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox(LegacyRealmTransferServicesFootballInventoryFullMessage)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RealmTransferServices Football reward " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=inventory-full; source bag check precedes Football points.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyRealmTransferServicesFootballPoints(
                session.Character.Attributes,
                out var footballPoints))
        {
            await LogLegacyRealmTransferServicesFootballRewardFenceAsync(
                    context,
                    source,
                    sourceLines,
                    optionId,
                    rewardItemId,
                    "football-points-invalid")
                .ConfigureAwait(false);
            return;
        }

        if (footballPoints < cost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["Sorry, you do not have enough Football points."],
                    [new LegacyNpcDialogOption("I understand.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyRealmTransferServicesFootballRewardFenceAsync(
                    context,
                    source,
                    sourceLines,
                    optionId,
                    rewardItemId,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, rewardItemId, out var rewardItemType))
        {
            await LogLegacyRealmTransferServicesFootballRewardFenceAsync(
                    context,
                    source,
                    sourceLines,
                    optionId,
                    rewardItemId,
                    context.WorldData is null ? "world-data-missing" : "itemtype-missing")
                .ConfigureAwait(false);
            return;
        }

        if (rewardItemType.Id != rewardItemId || rewardItemType.LegacyDurability > ushort.MaxValue)
        {
            await LogLegacyRealmTransferServicesFootballRewardFenceAsync(
                    context,
                    source,
                    sourceLines,
                    optionId,
                    rewardItemId,
                    "itemtype-invalid")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem grantedItem;
        try
        {
            grantedItem = CreatePurchasedItems(
                    session.PlayerItems,
                    rewardItemType,
                    count: 1,
                    isBound: false)[0]
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
        }
        catch (OverflowException)
        {
            await LogLegacyRealmTransferServicesFootballRewardFenceAsync(
                    context,
                    source,
                    sourceLines,
                    optionId,
                    rewardItemId,
                    "item-uid-exhausted")
                .ConfigureAwait(false);
            return;
        }

        var updatedFootballPoints = checked(footballPoints - cost);
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            "MyFootBallPoints",
            updatedFootballPoints);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems
                .Concat([grantedItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MyFootBallPoints"] = updatedFootballPoints.ToString(CultureInfo.InvariantCulture),
        };
        var itemFrame = LegacyGameItemPacket
            .FromSnapshotItem(grantedItem, mode: LegacyGameItemPacket.AddItemMode)
            .ToFrameBytes();
        var talkFrame = LegacyMsgMessagePacket
            .SystemTalk(
                updatedCharacter.Name + " has claimed " + rewardName +
                " from Football Tournament npc")
            .ToFrameBytes();

        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: true,
                out var persistenceReason))
        {
            await LogLegacyRealmTransferServicesFootballRewardFenceAsync(
                    context,
                    source,
                    sourceLines,
                    optionId,
                    rewardItemId,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var activeSessions = sessions.Snapshot();

        await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);
        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(talkFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices Football reward " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                $"footballPoints={footballPoints}->{updatedFootballPoints} rewardItemId={rewardItemId} " +
                $"itemUid={grantedItem.Uid} bound=false broadcastRecipients={activeSessions.Count} " +
                "packetOrder=GameItemAdd,GlobalTalk.")
            .ConfigureAwait(false);
    }

    private static bool TryReadLegacyRealmTransferServicesFootballPoints(
        IReadOnlyDictionary<string, string> attributes,
        out uint footballPoints)
    {
        if (attributes.TryGetValue("MyFootBallPoints", out var raw))
        {
            return uint.TryParse(
                raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out footballPoints);
        }

        if (attributes.TryGetValue("FootballPoints", out raw))
        {
            return uint.TryParse(
                raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out footballPoints);
        }

        footballPoints = 0;
        return true;
    }

    private static Task LogLegacyRealmTransferServicesFootballRewardFenceAsync(
        WorldPacketHandlerContext context,
        string source,
        string sourceLines,
        byte optionId,
        uint rewardItemId,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc RealmTransferServices Football reward " +
            $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
            $"rewardItemId={rewardItemId} reason={reason}; " +
            "no Football points, items, session state, or packets were committed.");
    }
}
