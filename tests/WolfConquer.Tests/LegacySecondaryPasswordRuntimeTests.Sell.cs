using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Native MsgItemUsuagePacket.SellItem checks physical IsCheckedPass before any sale.
// Disposable managed D13 login/packet/file proof, not a live game-client UI test.
public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    private const uint PinSaleNpcId = 20_499;
    private const uint PinSaleItemId = 50_000; // Ordinary item, not Anima or a no-sell id.
    private const uint PinSaleItemUid = 601_000_001;
    private const long PinSaleValue = 300; // GoldWorth900 /3, full10/10 durability.

    [Fact]
    public async Task ProtectedNpcSaleStaysUnchangedUntilCorrectSecondaryPasswordThenPersistsValueAndRemoval()
    {
        const uint uid = 1_298_001;
        SeedPinSale(uid, "111,0,0");
        var owner = NewOwner(worldData: CreatePinSaleWorldData());
        var player = await Login(owner, uid);
        Assert.False(player.NativeClient.Player.IsCheckedPass);

        await AssertPinSaleDenied(owner, player);
        AssertReply(await Password(owner, player, 1, newPin: 999), 6, 0);
        await AssertPinSaleDenied(owner, player);
        AssertReply(await Password(owner, player, 1, newPin: 111), 5, 257);
        Assert.True(player.NativeClient.Player.IsCheckedPass);

        await AssertPinSaleSucceeded(owner, player);
        Assert.Equal((111U, 0U, 0L), ReadCredential(uid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbsentOrZeroSecondaryPasswordInitializesPhysicalOwnerAndAllowsNpcSale(bool absent)
    {
        const uint uid = 1_298_011;
        SeedPinSale(uid, "0,0,0");
        if (absent)
            File.WriteAllLines(UserPath(uid), File.ReadAllLines(UserPath(uid)).Where(line =>
                !line.StartsWith("SecurityPass=", StringComparison.OrdinalIgnoreCase)));
        var owner = NewOwner(worldData: CreatePinSaleWorldData());
        var player = await Login(owner, uid);
        Assert.True(player.NativeClient.Player.IsCheckedPass);
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, Current(owner, player)));

        await AssertPinSaleSucceeded(owner, player);

        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid)));
        Assert.Equal(!absent, saved.Attributes.ContainsKey("SecurityPass"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForgedCurrentNativeCheckedFlagOrSnapshotZeroPasswordCannotAuthorizeNpcSale(bool forgeNativeFlag)
    {
        const uint uid = 1_298_021;
        SeedPinSale(uid, "111,0,0");
        var owner = NewOwner(worldData: CreatePinSaleWorldData());
        var player = await Login(owner, uid);
        var userBefore = File.ReadAllBytes(UserPath(uid));
        if (forgeNativeFlag) player.NativeClient.Player.IsCheckedPass = true;
        else
        {
            var current = Current(owner, player);
            var attrs = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["SecurityPass"] = "0,0,0" };
            owner.Sessions.Set(player.Connection, current with { Character = current.Character with { Attributes = attrs } });
        }

        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, Current(owner, player)));
        await AssertPinSaleDenied(owner, player);
        AssertNoSuccess(await Password(owner, player, 1, newPin: 111));
        Assert.Equal(userBefore, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal((111U, 0U, 0L), ReadCredential(uid));
    }

    [Theory]
    [InlineData(0)] // Fresh real managed login on another connection.
    [InlineData(1)] // Replacement current physical Player with forged public scalars.
    [InlineData(2)] // Foreign connection copying the old client and session.
    public async Task FreshConnectionReplacementPlayerOrCopiedOwnerCannotInheritNpcSaleVerification(int scenario)
    {
        const uint uid = 1_298_031;
        SeedPinSale(uid, "111,0,0");
        var owner = NewOwner(worldData: CreatePinSaleWorldData());
        var verified = await Login(owner, uid);
        AssertReply(await Password(owner, verified, 1, newPin: 111), 5, 257);
        Assert.True(verified.NativeClient.Player.IsCheckedPass);
        var priorPlayer = verified.NativeClient.Player;
        Player attempted;
        if (scenario == 0)
        {
            // Native duplicate admission rejects this attempt after retiring the old
            // connection. A subsequent login on the same world owner is the retry.
            var duplicate = CreateConnection(uid);
            await owner.Handler.HandleFrameAsync(duplicate.Connection,
                ReadFrame(new LegacyMsgLoginGamePacket(uid).ToFrameBytes()), duplicate.Lifetime.Token);
            Assert.DoesNotContain(duplicate.Sent, IsAnswerOk);
            Assert.False(owner.Sessions.TryGet(verified.Connection, out _));
            Assert.False(owner.Sessions.TryGet(duplicate.Connection, out _));
            attempted = await Login(owner, uid);
            Assert.NotSame(verified.NativeClient, attempted.NativeClient);
            Assert.NotSame(priorPlayer, attempted.NativeClient.Player);
            Assert.False(attempted.NativeClient.Player.IsCheckedPass);
        }
        else if (scenario == 1)
        {
            verified.NativeClient.Player = new LegacyNativePlayerRuntime(verified.NativeClient)
            { UID = uid, SecurityPassword = 111, IsCheckedPass = true };
            Assert.NotSame(priorPlayer, verified.NativeClient.Player);
            attempted = verified;
        }
        else
        {
            attempted = CreateConnection(uid, verified.NativeClient);
            var original = Current(owner, verified);
            byte[] character = File.ReadAllBytes(UserPath(uid));
            byte[] items = File.ReadAllBytes(ItemPath(uid));
            var refusal = Assert.Throws<InvalidOperationException>(() => owner.Sessions.Set(attempted.Connection, original with { }));
            Assert.Contains("ordinarySpellPublicationRequiresCurrentCompleteOwner", refusal.Message);
            Assert.False(owner.Sessions.TryGet(attempted.Connection, out _));
            Assert.Same(priorPlayer, attempted.NativeClient.Player);
            Assert.Same(original, Current(owner, verified));
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(verified.Connection, original));
            Assert.True(priorPlayer.IsCheckedPass);
            int first = attempted.Sent.Count;
            await SellPinFixtureItem(owner, attempted);
            Assert.DoesNotContain(attempted.Sent.Skip(first), bytes =>
                MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Action == MsgItemUsageAction.RemoveInventory ||
                LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Money));
            AssertNoSuccess(await Password(owner, attempted, 1, newPin: 111));
            Assert.False(owner.Sessions.TryGet(attempted.Connection, out _));
            Assert.Equal(character, File.ReadAllBytes(UserPath(uid)));
            Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(verified.Connection, Current(owner, verified)));

            var freshOwner = NewOwner(worldData: CreatePinSaleWorldData());
            var fresh = await Login(freshOwner, uid);
            Assert.NotSame(verified.NativeClient, fresh.NativeClient);
            Assert.NotSame(priorPlayer, fresh.NativeClient.Player);
            Assert.False(fresh.NativeClient.Player.IsCheckedPass);
            await AssertPinSaleDenied(freshOwner, fresh);
            AssertNoSuccess(await Password(freshOwner, fresh, 1, newPin: 222));
            AssertReply(await Password(freshOwner, fresh, 1, newPin: 111), 5, 257);
            await AssertPinSaleSucceeded(freshOwner, fresh);
            return;
        }

        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(attempted.Connection, Current(owner, attempted)));
        await AssertPinSaleDenied(owner, attempted);
        Assert.Equal((111U, 0U, 0L), ReadCredential(uid));
        if (scenario == 0)
        {
            AssertReply(await Password(owner, attempted, 1, newPin: 111), 5, 257);
            await AssertPinSaleSucceeded(owner, attempted);
        }
        else AssertNoSuccess(await Password(owner, attempted, 1, newPin: 111));
    }

    private void SeedPinSale(uint uid, string securityPass)
    {
        Seed(uid, securityPass, PinSaleItemUid);
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, new PlayerItemSnapshot(
            [new PlayerItemSnapshotItem
            {
                Uid = PinSaleItemUid, ItemId = PinSaleItemId, Durability = 10, MaximumDurability = 10,
                Position = 0, StackSize = 1, Bound = 0, Color = 5, AnimaItemId = 0,
            }], [], [], [], []));
    }

    private async Task SellPinFixtureItem(Owner owner, Player player) => await owner.Handler.HandleFrameAsync(
        player.Connection, ReadFrame(new MsgItemUsagePacket(PinSaleNpcId, PinSaleItemUid,
            MsgItemUsageAction.SellItem, Timestamp: 0, DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes()),
        player.Lifetime.Token);

    private async Task AssertPinSaleDenied(Owner owner, Player player)
    {
        var before = Current(owner, player);
        var userBytes = File.ReadAllBytes(UserPath(player.Uid));
        var itemBytes = File.ReadAllBytes(ItemPath(player.Uid));
        var sessionItems = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
        var first = player.Sent.Count;
        Assert.Equal(PinSaleItemUid, Assert.Single(before.PlayerItems.InventoryItems).Uid);

        await SellPinFixtureItem(owner, player);

        var after = Current(owner, player);
        Assert.Same(before, after);
        Assert.Equal(before.Character.Money, after.Character.Money);
        Assert.Equal(before.Character.ConquerPoints, after.Character.ConquerPoints);
        Assert.Equal(sessionItems, PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
        Assert.Equal(userBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.DoesNotContain(player.Sent.Skip(first), bytes =>
            MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Action == MsgItemUsageAction.RemoveInventory);
    }

    private async Task AssertPinSaleSucceeded(Owner owner, Player player)
    {
        var before = Current(owner, player);
        var first = player.Sent.Count;
        var item = Assert.Single(before.PlayerItems.InventoryItems);
        Assert.Equal(PinSaleItemUid, item.Uid);
        Assert.Equal((ushort)10, item.Durability);
        Assert.Equal((ushort)10, item.MaximumDurability);

        await SellPinFixtureItem(owner, player);

        var after = Current(owner, player);
        Assert.Empty(after.PlayerItems.InventoryItems);
        Assert.Equal(before.Character.Money + PinSaleValue, after.Character.Money);
        Assert.Equal(before.Character.ConquerPoints, after.Character.ConquerPoints);
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(player.Uid)));
        Assert.Equal(after.Character.Money, saved.Money);
        Assert.Equal(before.Character.ConquerPoints, saved.ConquerPoints);
        Assert.Empty(new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(player.Uid).InventoryItems);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems), File.ReadAllBytes(ItemPath(player.Uid)));
        var removals = player.Sent.Skip(first).Select(ReadFrame).Where(frame =>
            MsgItemUsagePacket.TryParse(frame, out var packet) && packet.Action == MsgItemUsageAction.RemoveInventory).ToArray();
        Assert.True(MsgItemUsagePacket.TryParse(Assert.Single(removals), out var removal));
        Assert.Equal(PinSaleItemUid, removal.Id);
        Assert.Contains(player.Sent.Skip(first), bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Uid == player.Uid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Money &&
                entry.Value == (ulong)after.Character.Money));
    }

    private static WorldDataCatalog CreatePinSaleWorldData()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = PinSaleItemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "SecondaryPinOrdinarySaleItem";
        fields[ItemType.GoldWorthFieldIndex] = "900";
        fields[ItemType.DurabilityFieldIndex] = "10";
        return LegacyItemWorldDataFixture.Create([new ItemType((int)PinSaleItemId, fields[1], fields)], [],
            npcs: [new Npc((int)PinSaleNpcId, Kind: 0, Lookface: 0, MapId: 1002, X: 430, Y: 378, OptionalFlag: null)]);
    }
}
