using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyFullAccountItemMutationFixture;

namespace WolfConquer.Tests;

// Independent existing-row metadata proof. No fresh item is created or assigned.
public sealed class LegacyFullAccountRedBodhiBeanLockMetadataTests : IDisposable
{
    private readonly Fixture fixture = new("red-bodhi-lock-metadata", 1_293_850);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ReceiptPreservesExistingLockTimerAndEveryUnchangedSavedAndWireByte(byte locked)
    {
        var item = new PlayerItemSnapshotItem
        {
            Uid = 5, ItemId = 3_009_104, Durability = 1, MaximumDurability = 1,
            StackSize = 0, Locked = locked, UnlockTimer = 123_456_789, Color = 7,
            Plus = 3, Bless = 2, Enchant = 4, SocketOne = 11, SocketTwo = 12,
            SocketProgress = 112_233, PlusProgress = 223_344, Suspicious = 1,
            PerfectionLevel = 2, PerfectionProgress = 333, OwnerUid = 777,
            OwnerName = "Keeper", Signature = "Protected", Inscribed = 1, ItemPoints = 999,
        };
        var original = Fixture.Items(item);
        var result = await fixture.InvokeAsync(16, original,
            LegacyItemWorldDataFixture.Create(Fixture.ItemType(3_009_104, "RedBodhiBean", 0, 10_000)),
            observerCount: 1);
        Assert.True(result.Handled);
        var received = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(5U, received.Uid);
        Assert.Equal(locked, received.Locked);
        Assert.Equal(123_456_789, received.UnlockTimer);
        Assert.Equal((ushort)10_000, received.StackSize);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));

        // The real saved layout's only changed field is first-row StackSize
        // (4-byte count + DB offset52). Every other byte, including Locked at33,
        // UnlockTimer at64 and the complete perfection record, must survive.
        var expectedFile = (byte[])result.OriginalItemBytes.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(expectedFile.AsSpan(4 + 52), 10_000);
        Assert.Equal(expectedFile, File.ReadAllBytes(result.PlayerItemsPath));
        Assert.Equal(expectedFile, PlayerItemSnapshotLoader.ToLegacyBytes(result.PersistedItems));
        Assert.Equal(expectedFile, PlayerItemSnapshotLoader.ToLegacyBytes(result.PersistedAtCommit!.Items));
        Assert.Equal(result.OriginalItemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(original));
        Assert.Equal((ushort)0, item.StackSize);
        Assert.Equal(locked, item.Locked);
        Assert.Equal(123_456_789, item.UnlockTimer);

        Assert.Equal(10, result.ClaimantSent.Count);
        for (var index = 0; index < result.ClaimantSent.Count; index++)
            Assert.Equal(UpdateGolden(locked, checked((ushort)((index + 1) * 1000))), result.ClaimantSent[index]);
        Assert.Equal(new[] { "commit" }.Concat(Enumerable.Repeat("claimant:2116", 10)), result.ExposureOrder);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    private static byte[] UpdateGolden(byte locked, ushort stack)
    {
        // Confirmed current2116 layout. No production item/frame constructor is
        // used for the expected image; unspecified/reserved bytes remain zero.
        var bytes = new byte[206];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 206);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2116);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 3_009_104);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(19), 112_233);
        bytes[23] = 11; bytes[24] = 12;
        bytes[30] = 3; bytes[31] = 2; bytes[33] = 4;
        bytes[38] = 1; bytes[40] = locked;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(42), 223_344);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), stack);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 333);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 777);
        "Keeper"u8.CopyTo(bytes.AsSpan(68));
        "Protected"u8.CopyTo(bytes.AsSpan(100));
        return bytes;
    }

    public void Dispose() => fixture.Dispose();
}
