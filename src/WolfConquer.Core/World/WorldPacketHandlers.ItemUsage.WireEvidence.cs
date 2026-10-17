using System.Buffers;
using System.Security.Cryptography;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int ItemUsageWireEvidencePayloadHexLimit = 256;

    private static async ValueTask LogUnsupportedItemUsageWireEvidenceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame)
    {
        var payload = frame.Payload.ToArray();
        var payloadHexLength = Math.Min(payload.Length, ItemUsageWireEvidencePayloadHexLimit);
        var payloadHex = Convert.ToHexString(payload.AsSpan(0, payloadHexLength));
        var payloadSha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var payloadHexTruncated = payload.Length > payloadHexLength ? "true" : "false";
        var candidateUidMatches = FormatItemUsageWireEvidenceUidMatches(session.PlayerItems, packet);

        var evidence = string.Join(
            ' ',
            [
                "WIRE-EVIDENCE MsgItemUsageFixedLayout",
                FormattableString.Invariant($"opcode={frame.Header.Opcode} actionId={(ushort)packet.Action}"),
                FormattableString.Invariant($"id={packet.Id} dwParam={packet.DwParam} timestamp={packet.Timestamp}"),
                FormattableString.Invariant($"dwParam2={packet.DwParam2} dwParam3={packet.DwParam3} dwParam4={packet.DwParam4}"),
                FormattableString.Invariant($"activeNpcId={session.ActiveNpcId.GetValueOrDefault()}"),
                FormattableString.Invariant($"payloadLength={payload.Length} payloadHexBytes={payloadHexLength}"),
                $"payloadHexTruncated={payloadHexTruncated} payloadHex={payloadHex}",
                $"fullPayloadSha256={payloadSha256} candidateUidMatches={candidateUidMatches}.",
            ]);

        await context.Log.WriteLineAsync(evidence)
            .ConfigureAwait(false);
    }

    private static string FormatItemUsageWireEvidenceUidMatches(
        PlayerItemSnapshot items,
        MsgItemUsagePacket packet)
    {
        var candidates = new List<(string Field, uint Uid)>
        {
            ("id", packet.Id),
        };

        var dwParamLow32 = unchecked((uint)packet.DwParam);
        var dwParamHigh32 = unchecked((uint)(packet.DwParam >> 32));
        AddItemUsageWireEvidenceUidCandidate(candidates, "dwParamLow32", dwParamLow32);
        AddItemUsageWireEvidenceUidCandidate(candidates, "dwParamHigh32", dwParamHigh32);
        AddItemUsageWireEvidenceUidCandidate(candidates, "dwParam2", packet.DwParam2);
        AddItemUsageWireEvidenceUidCandidate(candidates, "dwParam3", packet.DwParam3);
        AddItemUsageWireEvidenceUidCandidate(candidates, "dwParam4", packet.DwParam4);

        var matches = new List<string>();
        foreach (var candidate in candidates)
        {
            AddItemUsageWireEvidenceCollectionMatches(
                matches,
                candidate.Field,
                candidate.Uid,
                "inventory",
                items.InventoryItems);
            AddItemUsageWireEvidenceCollectionMatches(
                matches,
                candidate.Field,
                candidate.Uid,
                "relicFuse",
                items.RelicFuseItems);
            AddItemUsageWireEvidenceCollectionMatches(
                matches,
                candidate.Field,
                candidate.Uid,
                "mythsoulBag",
                items.MythsoulBagItems);
            AddItemUsageWireEvidenceCollectionMatches(
                matches,
                candidate.Field,
                candidate.Uid,
                "equipment",
                items.EquipmentItems);
            AddItemUsageWireEvidenceCollectionMatches(
                matches,
                candidate.Field,
                candidate.Uid,
                "rune",
                items.RuneItems);
            AddItemUsageWireEvidenceCollectionMatches(
                matches,
                candidate.Field,
                candidate.Uid,
                "wardrobe",
                items.WardrobeItems);
            AddItemUsageWireEvidenceCollectionMatches(
                matches,
                candidate.Field,
                candidate.Uid,
                "warehouse",
                items.WarehouseItems);
        }

        return matches.Count == 0
            ? "none"
            : "[" + string.Join('|', matches) + "]";
    }

    private static void AddItemUsageWireEvidenceUidCandidate(
        ICollection<(string Field, uint Uid)> candidates,
        string field,
        uint uid)
    {
        if (uid != 0)
        {
            candidates.Add((field, uid));
        }
    }

    private static void AddItemUsageWireEvidenceCollectionMatches(
        ICollection<string> matches,
        string field,
        uint uid,
        string collection,
        IReadOnlyList<PlayerItemSnapshotItem> items)
    {
        foreach (var item in items)
        {
            if (item.Uid == uid)
            {
                matches.Add(FormatItemUsageWireEvidenceMatch(field, uid, collection, item));
            }

            foreach (var depositedItem in item.DepositedItems)
            {
                if (depositedItem.Uid == uid)
                {
                    matches.Add(FormatItemUsageWireEvidenceMatch(
                        field,
                        uid,
                        collection + ".deposited",
                        depositedItem));
                }
            }
        }
    }

    private static string FormatItemUsageWireEvidenceMatch(
        string field,
        uint uid,
        string collection,
        PlayerItemSnapshotItem item)
    {
        return FormattableString.Invariant(
            $"{field}={uid}->{collection}(uid={item.Uid},itemId={item.ItemId},animaItemId={item.AnimaItemId},position={item.Position},warehouseId={item.WarehouseId})");
    }
}
