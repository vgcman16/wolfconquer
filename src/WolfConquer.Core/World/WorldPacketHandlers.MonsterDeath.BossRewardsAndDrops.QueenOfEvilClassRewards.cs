using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyQueenOfEvilClassRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monsterFamily.Id < 0 ||
            !LegacyBossSpecialDeathDropPolicy.IsQueenOfEvilSpecialBranch(
                checked((uint)monsterFamily.Id),
                monsterFamily.Name))
        {
            return session;
        }

        var grants = new List<LegacyQueenOfEvilClassRewardItem>();
        uint ninjaPoints = 0;
        if (IsLegacyWarriorClass(session.Character.Class))
        {
            grants.Add(new LegacyQueenOfEvilClassRewardItem(3_333_403, StackSize: 5));
        }
        else if (IsLegacyNinjaClass(session.Character.Class))
        {
            ninjaPoints = 50;
        }
        else if (IsLegacyArcherClass(session.Character.Class))
        {
            grants.Add(new LegacyQueenOfEvilClassRewardItem(3_335_128, StackSize: 10));
        }
        else if (IsLegacyTrojanClass(session.Character.Class))
        {
            grants.Add(new LegacyQueenOfEvilClassRewardItem(3_321_098, StackSize: 200));
            grants.Add(new LegacyQueenOfEvilClassRewardItem(3_321_107, StackSize: 200));
        }

        if (grants.Count == 0 && ninjaPoints == 0)
        {
            return session;
        }

        if (grants.Count > 0 && !HasLegacyInventorySpace(session.PlayerItems, grants.Count))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy QueenofEvil class reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"class={session.Character.Class} reason=inventory-full requiredSlots={grants.Count}.")
                .ConfigureAwait(false);
            return session;
        }

        var rewardItems = CreateLegacyQueenOfEvilClassRewardItems(
            session.PlayerItems,
            grants,
            context.WorldData);
        var updatedItems = rewardItems.Count == 0
            ? session.PlayerItems
            : session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems.Concat(rewardItems).ToArray(),
            };

        var updatedCharacter = session.Character;
        if (ninjaPoints != 0)
        {
            var currentNinjaPoints = ReadAttributeUInt64(updatedCharacter.Attributes, "NinjaPoints", "NinjaPoint");
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                "NinjaPoints",
                checked(currentNinjaPoints + ninjaPoints));
        }

        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        foreach (var item in rewardItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy QueenofEvil class reward " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"class={session.Character.Class} itemGrants={grants.Count} ninjaPoints={ninjaPoints}; " +
                "source=MonsterRole.cs Name == \"QueenofEvil\" || Family.ID == 3970.")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyQueenOfEvilClassRewardItems(
        PlayerItemSnapshot items,
        IReadOnlyList<LegacyQueenOfEvilClassRewardItem> grants,
        WorldDataCatalog? worldData)
    {
        var nextUid = GetNextSessionItemUid(items);
        var result = new PlayerItemSnapshotItem[grants.Count];
        for (var index = 0; index < grants.Count; index++)
        {
            var grant = grants[index];
            var durability = TryReadItemType(worldData, grant.ItemId, out var itemType)
                ? checked((ushort)itemType.LegacyDurability)
                : (ushort)0;
            result[index] = new PlayerItemSnapshotItem
            {
                Uid = checked(nextUid + (uint)index),
                ItemId = grant.ItemId,
                Durability = durability,
                MaximumDurability = durability,
                Position = 0,
                StackSize = grant.StackSize,
            };
        }

        return result;
    }
}
