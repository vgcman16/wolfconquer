using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Native DropItem places these IDs after Anima security and before Locked/
// Inscribed. Its protected-item MessageBox prerequisites remain unproven;
// these regressions cover the silent asset fence, not full popup/Drop parity.
public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(2_168_735U, (byte)0)]
    [InlineData(2_168_735U, (byte)1)]
    [InlineData(2_168_725U, (byte)0)]
    [InlineData(2_168_725U, (byte)1)]
    [InlineData(2_168_715U, (byte)0)]
    [InlineData(2_168_715U, (byte)1)]
    [InlineData(2_168_705U, (byte)0)]
    [InlineData(2_168_705U, (byte)1)]
    public async Task NativeProtectedDropIdsRetainWholeRichStackWithoutFramesOrGroundLoot(uint itemId, byte bound)
    {
        const uint uid = 1_299_601;
        SeedProtectedDrop(uid, itemId, bound);
        var savedItems = File.ReadAllBytes(ItemPath(uid));
        var owner = NewOwner();
        var player = await Login(owner, uid);
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, Current(owner, player)));
        Assert.Equal(savedItems, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(savedItems, PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, player).PlayerItems));
        var selected = Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        Assert.Equal(itemId, selected.ItemId);
        Assert.Equal(bound, selected.Bound);
        Assert.Equal((ushort)3, selected.StackSize);
        Assert.Equal((byte)17, selected.Enchant);
        Assert.Equal("ProtectedDrop", selected.Signature);

        var log = await AssertPinDropDenied(owner, player, expectedReason: "nativeProtectedItemDrop");

        Assert.DoesNotContain("locked or inscribed items cannot be dropped", log, StringComparison.Ordinal);
        Assert.DoesNotContain("no valid ground tile", log, StringComparison.Ordinal);
        Assert.Equal(savedItems, File.ReadAllBytes(ItemPath(uid)));
    }

    [Fact]
    public async Task SecondaryPinPrecedesProtectedDropIdWhichThenPrecedesLockedAndInscribed()
    {
        const uint uid = 1_299_611;
        SeedProtectedDrop(uid, 2_168_735, bound: 0, securityPass: "111,0,0,", lockedAndInscribed: true);
        var owner = NewOwner();
        var player = await LoginPinDrop(owner, uid);
        var physical = player.Connection.NativeGameClient!.Player;
        Assert.False(physical.IsCheckedPass);

        var unverifiedLog = await AssertPinDropDenied(owner, player);

        Assert.DoesNotContain("nativeProtectedItemDrop", unverifiedLog, StringComparison.Ordinal);
        Assert.DoesNotContain("locked or inscribed items cannot be dropped", unverifiedLog, StringComparison.Ordinal);
        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0), Assert.Single(await VerifyDropPin(owner, player, 111)));
        Assert.Same(physical, player.Connection.NativeGameClient.Player);
        Assert.True(physical.IsCheckedPass);

        var verifiedLog = await AssertPinDropDenied(owner, player, expectedReason: "nativeProtectedItemDrop");

        Assert.DoesNotContain("secondaryPasswordUnverifiedOrOwnerInvalid", verifiedLog, StringComparison.Ordinal);
        Assert.DoesNotContain("locked or inscribed items cannot be dropped", verifiedLog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndependentAnimaFeedbackPrecedesProtectedDropIdAfterActualSecondaryPinVerification()
    {
        const uint uid = 1_299_621;
        SeedProtectedDrop(uid, 2_168_725, bound: 1, securityPass: "111,0,0,", anima: true);
        var owner = NewOwner();
        var player = await LoginPinDrop(owner, uid);
        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0), Assert.Single(await VerifyDropPin(owner, player, 111)));
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, Current(owner, player)));
        var original = Current(owner, player);
        var projectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        var logStart = owner.Log.GetStringBuilder().Length;
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));

        await DropPinItem(owner, player, SoldUid);

        var frame = ReadFrame(Assert.Single(player.Sent.Skip(first)));
        Assert.Equal(LegacyNpcReplyPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("Please Active Pincode", reply.Text);
        Assert.Same(original, Current(owner, player));
        Assert.Equal(original.Character.Money, Current(owner, player).Character.Money);
        Assert.Equal(original.Character.ConquerPoints, Current(owner, player).Character.ConquerPoints);
        Assert.Equal(projectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, player).PlayerItems));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        var log = owner.Log.ToString()[logStart..];
        Assert.DoesNotContain("nativeProtectedItemDrop", log, StringComparison.Ordinal);
        Assert.DoesNotContain("locked or inscribed items cannot be dropped", log, StringComparison.Ordinal);
    }

    private void SeedProtectedDrop(uint uid, uint itemId, byte bound, string securityPass = "0,0,0,",
        bool anima = false, bool lockedAndInscribed = false)
    {
        SeedPinDrop(uid, securityPass, anima);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var items = loader.LoadByCharacterUid(uid);
        var rich = new PlayerItemSnapshotItem
        {
            Uid = SoldUid, ItemId = itemId, Durability = 7, MaximumDurability = 11,
            StackSize = 3, Bound = bound, Color = 5, Plus = 2, Bless = 1, Enchant = 17,
            SocketOne = 13, SocketTwo = 3, SocketProgress = 2_000, PlusProgress = 1_234,
            Effect = 200, ItemPoints = 987, RuneExperience = 99,
            Attribute1 = 11, Attribute2 = 22, Attribute3 = 33, Attribute4 = 44, Attribute5 = 55,
            MythSoul = 4, MythSoulPoints = 8, PerfectionLevel = 3, PerfectionProgress = 9,
            OwnerUid = uid, OwnerName = "DropOwner", Signature = "ProtectedDrop",
            AnimaItemId = anima ? 4_200_001U : 0,
            Locked = lockedAndInscribed ? (byte)1 : (byte)0,
            Inscribed = lockedAndInscribed ? (ushort)1 : (ushort)0,
        };
        loader.SaveByCharacterUid(uid, items with
        {
            InventoryItems = items.InventoryItems.Select(item => item.Uid == SoldUid ? rich : item).ToArray(),
        });
    }
}
