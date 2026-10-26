using System.Buffers.Binary;
using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageStackPlan
{
    private static bool CompatibleOwnedRow(PlayerItemSnapshotItem item, LegacyMailboxPackageItemRecipe grant)
    {
        if (item.Uid == 0 || item.StackSize == 0 || item.Color > 8 || item.Locked != 0 || item.Suspicious != 0 ||
            item.UnlockTimer != 0 || item.Activate != 0 || item.TimeLeftInMinutes != 0 || item.Expiration != 0 ||
            item.OwnerUid != 0 || item.DepositedItems.Count != 0) return false;
        // Exact complete save-codec comparison rejects locked/suspicious rows,
        // pending unlock, every enhancement/owner signature and deposited child.
        // UID, current positive quantity and established color alone may differ.
        var expected = CreateTemplate(grant, item.Uid, item.StackSize, item.Color);
        var actualBytes = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([item], [], [], [], [], []));
        var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([expected], [], [], [], [], []));
        return actualBytes.AsSpan().SequenceEqual(expectedBytes);
    }

    private static PlayerItemSnapshotItem CreateTemplate(LegacyMailboxPackageItemRecipe grant,
        uint uid, ushort quantity, uint color) => new()
    {
        Uid = uid, ItemId = grant.ItemId, Durability = grant.Durability, MaximumDurability = grant.Durability,
        Plus = grant.Plus, Bound = grant.Bound, SocketProgress = grant.SocketProgress, StackSize = quantity, Color = color,
    };

    private static uint ChooseColor(byte[] seed, int allocationIndex)
    {
        Span<byte> input = stackalloc byte[36];
        seed.CopyTo(input);
        BinaryPrimitives.WriteInt32LittleEndian(input[32..], allocationIndex);
        return 3u + SHA256.HashData(input)[0] % 6u;
    }
}
