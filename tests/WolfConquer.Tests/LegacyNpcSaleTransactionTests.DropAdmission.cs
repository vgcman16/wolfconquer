using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Native DropItem checks IsCheckedPass before item lookup and independent Anima
// security. These are managed D13/file regressions, not full Drop or SQL proof.
public sealed partial class LegacyNpcSaleTransactionTests
{
    [Fact]
    public async Task ProtectedDropDeniesUnverifiedAndWrongPinThenSamePhysicalVerifiedPlayerConsumesWholeBoundUid()
    {
        const uint uid = 1_299_401;
        SeedPinDrop(uid, "111,0,0,");
        var owner = NewOwner();
        var player = await LoginPinDrop(owner, uid);
        var physical = player.Connection.NativeGameClient!.Player;
        Assert.False(physical.IsCheckedPass);
        await AssertPinDropDenied(owner, player);
        Assert.Equal(new LegacySecondaryPasswordPacket(6, 0, 0), Assert.Single(await VerifyDropPin(owner, player, 999)));
        Assert.False(physical.IsCheckedPass);
        await AssertPinDropDenied(owner, player);

        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0), Assert.Single(await VerifyDropPin(owner, player, 111)));
        Assert.Same(physical, player.Connection.NativeGameClient.Player);
        Assert.True(physical.IsCheckedPass);
        var before = Current(owner, player);
        Assert.Equal((ushort)3, Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid).StackSize);
        var expected = before.PlayerItems with
        { InventoryItems = before.PlayerItems.InventoryItems.Where(item => item.Uid != SoldUid).ToArray() };
        var first = player.Sent.Count;

        await DropPinItem(owner, player, SoldUid);

        var after = Current(owner, player);
        Assert.DoesNotContain(after.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        Assert.Equal(KeptUid, Assert.Single(after.PlayerItems.InventoryItems).Uid);
        Assert.Equal(before.Character.Money, after.Character.Money);
        Assert.Equal(before.Character.ConquerPoints, after.Character.ConquerPoints);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(uid)));
        Assert.True(IsRemoval(Assert.Single(player.Sent.Skip(first))));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, after));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("111,1,9223372036854775807,")]
    public async Task MalformedStoredPinStillAdmitsGameLoginButCannotDropOrVerify(string stored)
    {
        const uint uid = 1_299_411;
        SeedPinDrop(uid, stored);
        var owner = NewOwner();
        var player = await LoginPinDrop(owner, uid);
        Assert.False(player.Connection.NativeGameClient!.Player.IsCheckedPass);
        var before = File.ReadAllBytes(UserPath(uid));

        await AssertPinDropDenied(owner, player);
        Assert.DoesNotContain(await VerifyDropPin(owner, player, 111), packet => packet.Action == 5 && packet.OldPassword == 257);
        await AssertPinDropDenied(owner, player);

        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForgedRawCheckedFlagOrCopiedZeroPinAttributesCannotGrantDropAdmission(bool forgeNativeFlag)
    {
        const uint uid = 1_299_421;
        SeedPinDrop(uid, "111,0,0,");
        var owner = NewOwner();
        var player = await LoginPinDrop(owner, uid);
        var before = File.ReadAllBytes(UserPath(uid));
        if (forgeNativeFlag) player.Connection.NativeGameClient!.Player.IsCheckedPass = true;
        else
        {
            var current = Current(owner, player);
            var attrs = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["SecurityPass"] = "0,0,0", ["IsCheckedPass"] = "true" };
            owner.Sessions.Set(player.Connection, current with { Character = current.Character with { Attributes = attrs } });
        }
        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, Current(owner, player)));

        await AssertPinDropDenied(owner, player);

        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
    }

    [Fact]
    public async Task ReplacementSameUidPlayerCannotInheritDropVerificationEvenWithRawCheckedFieldTrue()
    {
        const uint uid = 1_299_431;
        SeedPinDrop(uid, "111,0,0,");
        var owner = NewOwner();
        var player = await LoginPinDrop(owner, uid);
        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0), Assert.Single(await VerifyDropPin(owner, player, 111)));
        var native = player.Connection.NativeGameClient!;
        var prior = native.Player;
        Assert.True(prior.IsCheckedPass);
        native.Player = new LegacyNativePlayerRuntime(native) { UID = uid, SecurityPassword = 111, IsCheckedPass = true };
        Assert.NotSame(prior, native.Player);
        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, Current(owner, player)));

        await AssertPinDropDenied(owner, player);

        Assert.True(native.Player.IsCheckedPass); // A raw true field alone remains insufficient.
    }

    [Fact]
    public async Task SecondaryPinDenialPrecedesIndependentAnimaFeedbackThenVerificationReachesThatExistingGate()
    {
        const uint uid = 1_299_441;
        SeedPinDrop(uid, "111,0,0,", anima: true);
        var owner = NewOwner();
        var player = await LoginPinDrop(owner, uid);
        await AssertPinDropDenied(owner, player); // In particular, no Anima popup yet.
        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0), Assert.Single(await VerifyDropPin(owner, player, 111)));
        var original = Current(owner, player);
        var userBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;

        await DropPinItem(owner, player, SoldUid);

        var frame = ReadFrame(Assert.Single(player.Sent.Skip(first)));
        Assert.Equal(LegacyNpcReplyPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("Please Active Pincode", reply.Text);
        Assert.Same(original, Current(owner, player));
        Assert.Equal(userBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task SecondaryPinDenialPrecedesMissingInventoryLookupAndVerificationAllowsThatLookup()
    {
        const uint uid = 1_299_451;
        const uint missingItem = SoldUid + 999;
        SeedPinDrop(uid, "111,0,0,");
        var owner = NewOwner();
        var player = await LoginPinDrop(owner, uid);
        var log = await AssertPinDropDenied(owner, player, missingItem);
        Assert.DoesNotContain("inventory item was not found", log, StringComparison.Ordinal);
        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0), Assert.Single(await VerifyDropPin(owner, player, 111)));

        log = await AssertPinDropDenied(owner, player, missingItem, expectedReason: "inventory item was not found");

        Assert.DoesNotContain("secondaryPasswordUnverifiedOrOwnerInvalid", log, StringComparison.Ordinal);
    }

    private void SeedPinDrop(uint uid, string securityPass, bool anima = false)
    {
        Seed(uid);
        var text = File.ReadAllText(UserPath(uid)).Replace("SecurityPass=0,0,0", "SecurityPass=" + securityPass, StringComparison.Ordinal);
        if (anima) text += "\nPinCodeAnima=abc123\nTREPIN2=false\n";
        File.WriteAllText(UserPath(uid), text);
        var loader = new PlayerItemSnapshotLoader(ItemsRoot);
        var items = loader.LoadByCharacterUid(uid);
        loader.SaveByCharacterUid(uid, items with
        {
            InventoryItems = items.InventoryItems.Select(item => item.Uid == SoldUid
                ? item.WithBound(1).WithStackSize(3).WithAnimaItemId(anima ? 4_200_001U : 0) : item).ToArray(),
        });
    }

    private async Task<Player> LoginPinDrop(Owner owner, uint uid)
    {
        var player = CreateConnection(uid);
        await SendLogin(owner, player);
        Assert.Contains(player.Sent, IsAnswerOk);
        Assert.Equal(uid, Current(owner, player).Character.Uid);
        Assert.Same(player.Connection.NativeGameClient, Current(owner, player).NativeGameClient);
        Assert.Equal(uid, player.Connection.NativeGameClient!.Player.UID);
        return player;
    }

    private async Task<LegacySecondaryPasswordPacket[]> VerifyDropPin(Owner owner, Player player, uint pin)
    {
        var first = player.Sent.Count;
        await owner.Handler.HandleFrameAsync(player.Connection,
            ReadFrame(new LegacySecondaryPasswordPacket(1, 0, pin).ToFrameBytes()), player.Lifetime.Token);
        return player.Sent.Skip(first).Select(ReadFrame).Where(frame => frame.Header.Opcode == LegacySecondaryPasswordPacket.Opcode)
            .Select(frame =>
            {
                Assert.True(LegacySecondaryPasswordPacket.TryParse(frame, out var packet));
                return packet;
            }).ToArray();
    }

    private async Task DropPinItem(Owner owner, Player player, uint itemUid) => await owner.Handler.HandleFrameAsync(
        player.Connection, ReadFrame(new MsgItemUsagePacket(itemUid, 0, MsgItemUsageAction.DropItem, 0, 0, 0, 0).ToFrameBytes()),
        player.Lifetime.Token);

    private async Task<string> AssertPinDropDenied(Owner owner, Player player, uint itemUid = SoldUid,
        string expectedReason = "secondaryPasswordUnverifiedOrOwnerInvalid")
    {
        var original = Current(owner, player);
        var characterBytes = File.ReadAllBytes(UserPath(player.Uid));
        var itemBytes = File.ReadAllBytes(ItemPath(player.Uid));
        var projectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems);
        var first = player.Sent.Count;
        var logStart = owner.Log.GetStringBuilder().Length;
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));

        await DropPinItem(owner, player, itemUid);

        Assert.Same(original, Current(owner, player));
        Assert.Equal(original.Character.Money, Current(owner, player).Character.Money);
        Assert.Equal(original.Character.ConquerPoints, Current(owner, player).Character.ConquerPoints);
        Assert.Equal(projectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, player).PlayerItems));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Empty(player.Sent.Skip(first));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        var log = owner.Log.ToString()[logStart..];
        Assert.Contains(expectedReason, log, StringComparison.Ordinal);
        return log;
    }

    private static FloorItemRuntimeStateStore DropFloor(Owner owner)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<FloorItemRuntimeStateStore>(field.GetValue(owner.Handler));
    }
}
