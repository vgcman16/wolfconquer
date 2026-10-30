using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed record OperatorMailRequest(LegacyOperatorGearMailRequest Request,
        LegacyMailboxPackageCatalog[] Catalogs, LegacyOwnedNativeItemImage.CatalogEvidence NativeCatalog, string Digest);

    private bool TryPrepareOperatorMailRequest(LegacyOperatorGearMailRequest supplied,
        out OperatorMailRequest? prepared, out string reason)
    {
        prepared = null;
        reason = "operatorMailRequestInvalid";
        if (supplied is null || supplied.OperationId == Guid.Empty || supplied.ExpectedDomain == Guid.Empty ||
            supplied.AccountId == 0 || supplied.CharacterUid == 0 || supplied.Items is null ||
            !(supplied.Items is LegacyOperatorGearMailItem[] || supplied.Items.GetType() == typeof(List<LegacyOperatorGearMailItem>)) ||
            supplied.Items.Count is < 1 or > 32 || !OperatorMailText(supplied.Title, 64) ||
            !OperatorMailText(supplied.SenderName, 128) || !OperatorMailText(supplied.Content, 512)) return false;
        var count = supplied.Items.Count;
        var items = new LegacyOperatorGearMailItem[count];
        var catalogs = new LegacyMailboxPackageCatalog[count];
        for (var i = 0; i < count; i++)
        {
            var item = supplied.Items[i];
            if (item is null || item.ItemId == 0 || item.Plus > 12 || item.SecondSocket && !item.FirstSocket ||
                !LegacyMailboxPackageCatalog.TryCapture(_worldData!, item.ItemId, out var catalog, out reason) ||
                catalog!.Durability == 0 || item.StackSize == 0 || item.StackSize > catalog.StackLimit ||
                !catalog.CanStack && item.StackSize != 1) return false;
            var plus = item.Plus;
            if (item.ItemId is >= 730001 and <= 730012)
            {
                var stone = checked((byte)(item.ItemId - 730000));
                if (plus != 0 && plus != stone) return false;
                plus = stone;
            }
            var equipment = ItemType.GetLegacyEquipmentPosition(item.ItemId) != 0;
            if ((!equipment && (item.FirstSocket || item.SecondSocket)) ||
                !equipment && plus != 0 && item.ItemId is not (>= 730001 and <= 730012)) return false;
            items[i] = item with { Plus = plus };
            catalogs[i] = catalog;
        }
        if (supplied.Items.Count != count || catalogs.Any(catalog => !catalog.IsCurrent())) return false;
        LegacyOwnedNativeItemImage.CatalogEvidence nativeCatalog;
        try
        {
            // Reuse the actual native constructor's required Rune evidence.
            // No item UID, fake item or accepting metadata resolver is issued.
            nativeCatalog = LegacyOwnedNativeItemImage.ValidateItemIds(_worldData, items.Select(item => item.ItemId), _protocolProfile);
        }
        catch (Exception error) when (IsOperatorMailPreparationFailure(error))
        { reason = "operatorMailNativeCatalogInvalid"; return false; }
        var frozen = supplied with { Items = items };
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, new UTF8Encoding(false, true), leaveOpen: true))
        {
            writer.Write(LegacyOwnedAdmissionRuntime.OperatorGearMailWriter);
            writer.Write(frozen.ExpectedDomain.ToByteArray()); writer.Write(frozen.AccountId); writer.Write(frozen.CharacterUid);
            writer.Write(frozen.Title); writer.Write(frozen.SenderName); writer.Write(frozen.Content); writer.Write(items.Length);
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                writer.Write(item.ItemId); writer.Write(item.StackSize); writer.Write(item.Plus); writer.Write(item.Bound);
                writer.Write(item.FirstSocket); writer.Write(item.SecondSocket); writer.Write(catalogs[i].Digest);
            }
        }
        if (catalogs.Any(catalog => !catalog.IsCurrent()) || !nativeCatalog.IsCurrentPlain())
            return false;
        prepared = new(frozen, catalogs, nativeCatalog, Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant());
        reason = string.Empty;
        return true;
    }

    // Current mailbox store is ASCII. Refuse lossy/truncated names rather than
    // discovering a roundtrip failure after UID reservation; no credential text.
    private static bool OperatorMailText(string text, int maximum) => text is not null &&
        text.Length is > 0 && text.Length <= maximum && text.All(c => c is >= ' ' and <= '~' or '\r' or '\n');
}
