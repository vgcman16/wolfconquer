using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRelicFuseSource = "RelicFuse.Process";
    private const ushort LegacyRelicFuseSourceRelic1Position = 213;
    private const ushort LegacyRelicFuseSourceRelic2Position = 214;
    private const ushort LegacyRelicFuseResultPosition = 215;

    [PacketHandler(WorldPacketOpcodes.LegacyRelicFuse)]
    private static async ValueTask HandleLegacyRelicFuseAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyRelicFusePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyRelicFuse packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyRelicFuse " +
                    $"{FormatSession(context)} type={packet.Type}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        switch (packet.Type)
        {
            case LegacyRelicFusePacket.ComposeType:
                await HandleLegacyRelicFuseComposeAsync(context, session, packet, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyRelicFusePacket.ReturnType:
                await HandleLegacyRelicFuseReturnAsync(context, session, packet, cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyRelicFuse unknown type " +
                        $"{FormatSession(context)} source={LegacyRelicFuseSource} type={packet.Type} " +
                        $"relic1={packet.RelicID1} relic2={packet.RelicID2} relic3={packet.RelicID3}.")
                    .ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask HandleLegacyRelicFuseComposeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyRelicFusePacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryComposeLegacyRelicFuse(
                context,
                session.PlayerItems,
                packet,
                out var updatedItems,
                out var generatedRelic,
                out var response,
                out var skipReason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyRelicFuse compose " +
                    $"{FormatSession(context)} source={LegacyRelicFuseSource} " +
                    $"relic1={packet.RelicID1} relic2={packet.RelicID2} reason={skipReason}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "relic fuse compose",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(generatedRelic).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyRelicFuse compose " +
                $"{FormatSession(updatedContext)} source={LegacyRelicFuseSource} " +
                $"relic1={response.RelicID1} relic2={response.RelicID2} relic3={response.RelicID3} " +
                $"itemId={generatedRelic.ItemId} durability={generatedRelic.Durability}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyRelicFuseReturnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyRelicFusePacket packet,
        CancellationToken cancellationToken)
    {
        var changed = TryReturnLegacyRelicFuseItem(
            session.PlayerItems,
            packet.RelicID1,
            out var updatedItems,
            out var returned);
        var updatedSession = session;

        if (changed)
        {
            updatedSession = session with { PlayerItems = updatedItems };
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };

            await TryPersistLegacyItemUsageMutationAsync(
                    updatedContext,
                    updatedSession,
                    "relic fuse return",
                    characterValues: null,
                    persistItems: true)
                .ConfigureAwait(false);

            await context.Connection.SendAsync(
                    LegacyGameItemPacket
                        .FromSnapshotItem(returned, mode: LegacyGameItemPacket.UpdateItemMode)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyRelicFuse return " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyRelicFuseSource} " +
                $"relic1={packet.RelicID1} relic2={packet.RelicID2} relic3={packet.RelicID3} changed={changed}.")
            .ConfigureAwait(false);
    }

    private static bool TryReturnLegacyRelicFuseItem(
        PlayerItemSnapshot items,
        uint relicUid,
        out PlayerItemSnapshot updatedItems,
        out PlayerItemSnapshotItem returned)
    {
        updatedItems = items;
        returned = null!;

        var fuseItem = items.RelicFuseItems.FirstOrDefault(item => item.Uid == relicUid);
        if (fuseItem is null)
        {
            return false;
        }

        returned = fuseItem.WithPosition(0);
        updatedItems = items with
        {
            InventoryItems = items.InventoryItems.Concat([returned]).ToArray(),
            RelicFuseItems = [],
        };
        return true;
    }

    private static bool TryComposeLegacyRelicFuse(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot items,
        LegacyRelicFusePacket packet,
        out PlayerItemSnapshot updatedItems,
        out PlayerItemSnapshotItem generatedRelic,
        out LegacyRelicFusePacket response,
        out string skipReason)
    {
        updatedItems = items;
        generatedRelic = null!;
        response = packet;
        skipReason = "";

        var relic1 = items.InventoryItems.FirstOrDefault(item => item.Uid == packet.RelicID1);
        if (relic1 is null)
        {
            skipReason = "first inventory relic was not found";
            return false;
        }

        var relic2 = items.InventoryItems.FirstOrDefault(item => item.Uid == packet.RelicID2);
        if (relic2 is null)
        {
            skipReason = "second inventory relic was not found";
            return false;
        }

        var durability = checked((ushort)(Random.Shared.Next(30, 101) * 100));
        generatedRelic = new PlayerItemSnapshotItem
        {
            Uid = GetNextSessionItemUid(items),
            ItemId = relic1.ItemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = LegacyRelicFuseResultPosition,
            StackSize = 1,
            Attribute1 = CreateLegacyRelicFuseAttribute(context.WorldData, relic1, relic2, attributeIndex: 0),
            Attribute2 = CreateLegacyRelicFuseAttribute(context.WorldData, relic1, relic2, attributeIndex: 1),
            Attribute3 = CreateLegacyRelicFuseAttribute(context.WorldData, relic1, relic2, attributeIndex: 2),
            Attribute4 = CreateLegacyRelicFuseAttribute(context.WorldData, relic1, relic2, attributeIndex: 3),
            Attribute5 = CreateLegacyRelicFuseAttribute(context.WorldData, relic1, relic2, attributeIndex: 4),
        };

        var fuseRelic1 = relic1.WithPosition(LegacyRelicFuseSourceRelic1Position).WithStackSize(1);
        var fuseRelic2 = relic2.WithPosition(LegacyRelicFuseSourceRelic2Position).WithStackSize(1);
        updatedItems = items with
        {
            InventoryItems = items.InventoryItems
                .Where(item => item.Uid != relic1.Uid && item.Uid != relic2.Uid)
                .ToArray(),
            RelicFuseItems = items.RelicFuseItems.Concat([fuseRelic1, fuseRelic2, generatedRelic]).ToArray(),
        };
        response = packet with
        {
            RelicID1 = relic1.Uid,
            RelicID2 = relic2.Uid,
            RelicID3 = generatedRelic.Uid,
        };
        return true;
    }

    private static uint CreateLegacyRelicFuseAttribute(
        WorldDataCatalog? worldData,
        PlayerItemSnapshotItem relic1,
        PlayerItemSnapshotItem relic2,
        int attributeIndex)
    {
        var relic1Attribute = GetLegacyRelicAttribute(relic1, attributeIndex);
        var relic2Attribute = GetLegacyRelicAttribute(relic2, attributeIndex);
        var type = IsAllowedLegacyRelicAttribute(relic1.ItemId, relic2Attribute.Type)
            ? relic1Attribute.Type
            : LegacyChiAttributeType.None;

        if (Random.Shared.Next(0, 100) < 30)
        {
            type = GetRandomLegacyRelicAttribute(relic1.ItemId);
        }

        if (type == LegacyChiAttributeType.None)
        {
            type = GetRandomLegacyRelicAttribute(relic1.ItemId);
        }

        var value = 0;
        var epic = false;
        if (TryGetLegacyRelicComposeLimit(worldData, type, out var limit))
        {
            value = type == relic1Attribute.Type
                ? Random.Shared.Next(Math.Min(relic1Attribute.Value, limit), checked(limit + 1))
                : Random.Shared.Next(30, checked(limit + 1));
            epic = value == limit;
        }

        if (type == LegacyChiAttributeType.LukyStrike)
        {
            value = 100;
            epic = true;
        }

        return EncodeLegacyRelicAttribute(type, checked((ushort)value), epic);
    }

    private static LegacyRelicFuseAttribute GetLegacyRelicAttribute(
        PlayerItemSnapshotItem item,
        int attributeIndex)
    {
        var encoded = attributeIndex switch
        {
            0 => item.Attribute1,
            1 => item.Attribute2,
            2 => item.Attribute3,
            3 => item.Attribute4,
            4 => item.Attribute5,
            _ => 0U,
        };

        return DecodeLegacyRelicAttribute(encoded);
    }

    private static bool TryGetLegacyRelicComposeLimit(
        WorldDataCatalog? worldData,
        LegacyChiAttributeType type,
        out int limit)
    {
        limit = 0;
        if (worldData is null ||
            !worldData.XuanbaoComposeAttributeLimitsById.TryGetValue((int)type, out var row) ||
            row.Values.Count == 0)
        {
            return false;
        }

        limit = row.Values[^1];
        return limit >= 30;
    }

    private static LegacyChiAttributeType GetRandomLegacyRelicAttribute(uint itemId)
    {
        while (true)
        {
            var type = (LegacyChiAttributeType)Random.Shared.Next(1, 15);
            if (IsAllowedLegacyRelicAttribute(itemId, type))
            {
                return type;
            }
        }
    }

    private static bool IsAllowedLegacyRelicAttribute(uint itemId, LegacyChiAttributeType type)
    {
        return itemId switch
        {
            4_100_001 => type is not
                LegacyChiAttributeType.SkillCriticalStrike and not
                LegacyChiAttributeType.CriticalStrike and not
                LegacyChiAttributeType.HPAdd and not
                LegacyChiAttributeType.AddAttack,
            4_100_002 => type is not
                LegacyChiAttributeType.SkillCriticalStrike and not
                LegacyChiAttributeType.CriticalStrike and not
                LegacyChiAttributeType.AddMagicAttack and not
                LegacyChiAttributeType.AddAttack,
            4_100_003 => type is not
                LegacyChiAttributeType.HPAdd and not
                LegacyChiAttributeType.CriticalStrike and not
                LegacyChiAttributeType.AddMagicAttack and not
                LegacyChiAttributeType.AddAttack,
            4_100_004 => type is not
                LegacyChiAttributeType.HPAdd and not
                LegacyChiAttributeType.CriticalStrike and not
                LegacyChiAttributeType.AddMagicAttack and not
                LegacyChiAttributeType.SkillCriticalStrike,
            4_100_005 => type is not
                LegacyChiAttributeType.HPAdd and not
                LegacyChiAttributeType.AddAttack and not
                LegacyChiAttributeType.AddMagicAttack and not
                LegacyChiAttributeType.SkillCriticalStrike,
            _ => true,
        };
    }

    private static LegacyRelicFuseAttribute DecodeLegacyRelicAttribute(uint encoded)
    {
        return new LegacyRelicFuseAttribute(
            (LegacyChiAttributeType)(encoded % 100),
            checked((ushort)(encoded / 1_000)),
            encoded % 1_000 - encoded % 100 == 100);
    }

    private static uint EncodeLegacyRelicAttribute(
        LegacyChiAttributeType type,
        ushort value,
        bool epic)
    {
        return checked(((uint)value * 1_000) + (byte)type + (epic ? 100U : 0U));
    }

    private readonly record struct LegacyRelicFuseAttribute(
        LegacyChiAttributeType Type,
        ushort Value,
        bool Epic);
}
