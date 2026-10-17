using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyDailySignInPackItemId = 3_100_011;

    private const uint LegacyHouseMaterialPackItemId = 3_008_101;

    private const uint LegacyExpBallItemId = 723_700;

    private const uint LegacyDailySignInPackRewardItemId = 3_600_023;

    private const ushort LegacyDailySignInPackStudyPoints = 300;

    private const int LegacyDailySignInPackChiPoints = 200;

    private const uint LegacyHouseMaterialPackRewardItemId = 3_008_100;

    private const ulong LegacyHouseMaterialPackDailyCap = 3;

    private const long LegacyExpBallDailyCap = 10;

    private static async ValueTask HandleLegacyDailySignInPackUseAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            PlayerItemSnapshotItem item,
            CancellationToken cancellationToken)
    {
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Please make 1 more space in inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage daily sign-in pack " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, item, out var consumeFrame);
        var rewardRoll = Random.Shared.Next(0, 7);
        var updatedCharacter = session.Character;
        string reward;
        byte[]? rewardFrame = null;
        byte[]? rewardUpdateFrame = null;
        string message;

        switch (rewardRoll)
        {
            case 0:
            case 1:
                updatedCharacter = AddLegacyStudyPoints(
                    updatedCharacter,
                    LegacyDailySignInPackStudyPoints);
                reward = $"{LegacyDailySignInPackStudyPoints} StudyPoints";
                message = "You received 300 StudyPoints.";
                break;

            case 2:
                updatedCharacter = AddLegacyDailySignInChiPoints(
                    updatedCharacter,
                    LegacyDailySignInPackChiPoints);
                reward = $"{LegacyDailySignInPackChiPoints} ChiPoints";
                message = "You received 200 ChiPoints.";
                rewardUpdateFrame = ChiInfoPacketProfileSelector.CreateFrame(
                    context.ProtocolProfile,
                    updatedCharacter,
                    LegacyChiInfoAction.Upgrade);
                break;

            default:
                var rewardItem = CreateLegacyDailySignInPackRewardItem(context, updatedItems);
                updatedItems = updatedItems with
                {
                    InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).ToArray(),
                };
                reward = $"bound item {LegacyDailySignInPackRewardItemId}";
                message = "You received EminentExploitPack.";
                rewardFrame = LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes();
                break;
        }

        var updated = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updated);

        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        if (rewardFrame is not null)
        {
            await context.Connection.SendAsync(rewardFrame, cancellationToken).ConfigureAwait(false);
        }

        if (rewardUpdateFrame is not null)
        {
            await context.Connection.SendAsync(rewardUpdateFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage daily sign-in pack " +
                $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                $"source roll={rewardRoll} reward={reward}.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshotItem CreateLegacyDailySignInPackRewardItem(
            WorldPacketHandlerContext context,
            PlayerItemSnapshot items)
    {
        var durability = TryReadItemType(context.WorldData, LegacyDailySignInPackRewardItemId, out var itemType)
            ? checked((ushort)itemType.LegacyDurability)
            : (ushort)0;
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = LegacyDailySignInPackRewardItemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = 1,
            Bound = 1,
        };
    }

    private static async ValueTask HandleLegacyHouseMaterialPackUseAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            PlayerItemSnapshotItem item,
            CancellationToken cancellationToken)
    {
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage house material pack " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        var openedToday = ReadAttributeUInt64(session.Character.Attributes, "OpenHousePack");
        if (openedToday >= LegacyHouseMaterialPackDailyCap)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("You've already opened such a pack for 3 times today. Please retry tomorrow.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage house material pack " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                    $"daily OpenHousePack cap reached opened={openedToday} cap={LegacyHouseMaterialPackDailyCap}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, item, out var consumeFrame);
        var rewardItem = CreateLegacyHouseMaterialPackRewardItem(context, updatedItems);
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        var updatedOpenedToday = checked(openedToday + 1);
        var updated = session with
        {
            Character = WithCharacterAttribute(session.Character, "OpenHousePack", updatedOpenedToday),
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updated);

        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You received a Class 6 House Pack!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage house material pack " +
                $"{FormatSession(context with { Session = updated })} itemUid={item.Uid} itemId={item.ItemId}; " +
                $"OpenHousePack={openedToday}->{updatedOpenedToday} rewardItemId={LegacyHouseMaterialPackRewardItemId}.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshotItem CreateLegacyHouseMaterialPackRewardItem(
            WorldPacketHandlerContext context,
            PlayerItemSnapshot items)
    {
        var durability = TryReadItemType(context.WorldData, LegacyHouseMaterialPackRewardItemId, out var itemType)
            ? checked((ushort)itemType.LegacyDurability)
            : (ushort)0;
        return new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = LegacyHouseMaterialPackRewardItemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = 1,
        };
    }

    private static async ValueTask HandleLegacyExpBallUseAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            PlayerItemSnapshotItem item,
            CancellationToken cancellationToken)
    {
        if (session.Character.Level >= LegacyMaxPlayerLevel)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage exp ball item " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                    "legacy UseItem only consumes item 723700 while the character level is below 140.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.ExpBallUsed >= LegacyExpBallDailyCap)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("You can use only ten exp balls a day. Try tomorrow.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage exp ball item " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                    $"daily ExpBallUsed cap reached used={session.Character.ExpBallUsed} cap={LegacyExpBallDailyCap}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, item, out var consumeFrame);
        var updatedExpBallUsed = checked(session.Character.ExpBallUsed + 1);
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            "ExpBallUsed",
            checked((ulong)updatedExpBallUsed)) with
        {
            ExpBallUsed = updatedExpBallUsed,
        };
        var modeledExperience = TryCalculateLegacyExpBallUse(
            context.WorldData,
            session.Character,
            LegacyExpBallAmount,
            out var updatedLevel,
            out var updatedExperience);
        if (modeledExperience)
        {
            updatedCharacter = updatedCharacter with
            {
                Level = updatedLevel,
                Experience = updatedExperience,
            };
        }

        var updated = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updated);

        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        if (modeledExperience)
        {
            var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
            if (updatedCharacter.Level != session.Character.Level)
            {
                await context.Connection.SendAsync(
                        LegacyMsgUpdatePacket
                            .CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)updatedCharacter.Level))
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeExperienceUpdate(uid, checked((ulong)updatedCharacter.Experience))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var experienceEvidence = modeledExperience
            ? $"experience={session.Character.Experience}->{updatedCharacter.Experience} level={session.Character.Level}->{updatedCharacter.Level}"
            : "level experience data is not loaded, so EXP mutation is not modeled";
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage exp ball item " +
                $"{FormatSession(context with { Session = updated })} itemUid={item.Uid} itemId={item.ItemId} " +
                $"amount={LegacyExpBallAmount.ToString(CultureInfo.InvariantCulture)} {experienceEvidence}; " +
                $"ExpBallUsed={session.Character.ExpBallUsed}->{updatedCharacter.ExpBallUsed} cap={LegacyExpBallDailyCap}.")
            .ConfigureAwait(false);
    }
}
