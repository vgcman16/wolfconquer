using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> ApplyLegacyBossHarmRankPackageClaimAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyBossHarmRankPackageClaimPlan claimPlan,
        CancellationToken cancellationToken)
    {
        var packageItems = CreateLegacyBossHarmRankPackageItems(
            session.PlayerItems,
            claimPlan.ItemGrants,
            context.WorldData);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(packageItems).ToArray(),
        };

        var classExperience = ReadAttributeUInt64(session.Character.Attributes, "ClassExperience");
        var updatedClassExperience = checked(classExperience + claimPlan.ClassExperience);
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            "ClassExperience",
            updatedClassExperience);
        if (claimPlan.NinjaPoints != 0)
        {
            var ninjaPoints = ReadAttributeUInt64(updatedCharacter.Attributes, "NinjaPoints", "NinjaPoint");
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                "NinjaPoints",
                checked(ninjaPoints + claimPlan.NinjaPoints));
        }

        var updatedCombatGear = session.CombatGear;
        if (claimPlan.PirateExperience != 0 || claimPlan.PirateAirPowerPoints != 0)
        {
            var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
            updatedCombatGear = combatGear with
            {
                PirateExp = checked(combatGear.PirateExp + claimPlan.PirateExperience),
                AirPowers = combatGear.AirPowers with
                {
                    Points = checked(combatGear.AirPowers.Points + claimPlan.PirateAirPowerPoints),
                },
            };
        }

        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
            CombatGear = updatedCombatGear,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        foreach (var item in packageItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendScalarUpdateAsync(
                context,
                updatedCharacter,
                LegacyMsgUpdateDataType.ClassExperience,
                updatedClassExperience,
                cancellationToken)
            .ConfigureAwait(false);
        if (updatedCombatGear is not null &&
            (claimPlan.PirateExperience != 0 || claimPlan.PirateAirPowerPoints != 0))
        {
            await context.Connection.SendAsync(
                    LegacyDaoQiPacket.PiratePointsUpdate(updatedCombatGear.PirateExp).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyBossHarmRankPackageItems(
        PlayerItemSnapshot items,
        IReadOnlyList<LegacyBossHarmRankPackageItem> grants,
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
                StackSize = checked((ushort)Math.Max(1U, grant.Quantity)),
                Bound = grant.Bound ? (byte)1 : (byte)0,
                Plus = checked((byte)grant.Plus),
                SocketProgress = checked((uint)Math.Max(0, grant.SocketProgress)),
            };
        }

        return result;
    }
}
