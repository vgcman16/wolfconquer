using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    private const uint PersistencePickupFloorUid = 9_202_001;
    private const long PersistencePickupAmount = 125;

    [Theory]
    [InlineData(0)] // Whole rich item stack.
    [InlineData(1)] // Silver.
    [InlineData(2)] // Conquer Points, retaining the managed update compatibility.
    public async Task PickupPersistsExactAssetsAndConsumesFloorBeforeFirstOutboundOutsideWorldLease(int disposition)
    {
        const uint uid = 1_299_951;
        Seed(uid);
        var owner = NewOwner(identity: new(42));
        var barrier = ActualOperationBarrier(owner);
        Player? player = null;
        WorldSession? before = null;
        FloorItemRuntimeEntry? entry = null;
        byte[]? characterBytes = null;
        var armed = false;
        var observed = 0;
        var leaseAvailable = false;
        Exception? callbackFailure = null;
        Task probeCleanup = Task.CompletedTask;
        player = await Login(owner, uid, _ =>
        {
            if (!armed || observed != 0) return;
            observed++;
            callbackFailure = Record.Exception(() =>
            {
                Assert.NotNull(before);
                Assert.NotNull(entry);
                Assert.NotNull(characterBytes);
                AssertPersistencePickupCommitted(owner, player!, before, entry, characterBytes,
                    NormalPersistencePickupWallet(before, disposition, PersistencePickupAmount));
            });
            using var cancellation = new CancellationTokenSource();
            var acquisition = barrier.EnterExclusiveAsync(cancellation.Token);
            leaseAvailable = acquisition.IsCompletedSuccessfully;
            if (leaseAvailable) acquisition.Result.Dispose();
            else
            {
                cancellation.Cancel();
                probeCleanup = DisposeCancelledProbe(acquisition);
            }
        });
        before = Current(owner, player);
        entry = SeedPersistencePickupFloor(owner, player, disposition);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);
        await probeCleanup.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, observed);
        Assert.Null(callbackFailure); // Dispatch must not swallow the ordering oracle.
        Assert.True(leaseAvailable, "Pickup/dispatch must release the world lease before its first socket send.");
        AssertPersistencePickupCommitted(owner, player, before, entry, characterBytes,
            NormalPersistencePickupWallet(before, disposition, PersistencePickupAmount));
        AssertPersistencePickupFrames(player.Sent.Skip(first).ToArray(), entry,
            NormalPersistencePickupWallet(before, disposition, PersistencePickupAmount));
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [WindowsNpcSaleTheory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task PickupCharacterOrItemsSharingRefusalRetainsExactFloorAndAssetsThenRetryCreditsOnce(int disposition, bool itemTarget)
    {
        const uint uid = 1_299_961;
        Seed(uid);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var entry = SeedPersistencePickupFloor(owner, player, disposition);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;

        // Read-sharing permits preimage inspection but refuses the real Windows
        // replacement/write boundary for both WAL targets, even unchanged ones.
        using (File.Open(itemTarget ? ItemPath(uid) : UserPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);
            AssertPersistencePickupDenied(owner, player, before, entry, characterBytes, itemBytes, first);
        }
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPersistencePickupCommitted(owner, player, before, entry, characterBytes,
            NormalPersistencePickupWallet(before, disposition, PersistencePickupAmount));
        AssertPersistencePickupFrames(player.Sent.Skip(first).ToArray(), entry,
            NormalPersistencePickupWallet(before, disposition, PersistencePickupAmount));
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PickupFirstSendFailureKeepsCommittedAssetsAndFreshOwnerCannotCreditConsumedFloorAgain(int disposition)
    {
        const uint uid = 1_299_971;
        Seed(uid);
        var owner = NewOwner(identity: new(42));
        Player? player = null;
        WorldSession? before = null;
        FloorItemRuntimeEntry? entry = null;
        byte[]? characterBytes = null;
        var armed = false;
        var attemptedSends = 0;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, _ =>
        {
            if (!armed) return;
            attemptedSends++;
            callbackFailure ??= Record.Exception(() =>
            {
                Assert.NotNull(before);
                Assert.NotNull(entry);
                Assert.NotNull(characterBytes);
                AssertPersistencePickupCommitted(owner, player!, before, entry, characterBytes,
                    NormalPersistencePickupWallet(before, disposition, PersistencePickupAmount));
            });
            throw new IOException("Synthetic first committed pickup send failure.");
        });
        before = Current(owner, player);
        entry = SeedPersistencePickupFloor(owner, player, disposition);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        Assert.Equal(1, attemptedSends);
        Assert.Null(callbackFailure);
        Assert.Equal(first, player.Sent.Count); // The throwing send was not queued.
        var wallet = NormalPersistencePickupWallet(before, disposition, PersistencePickupAmount);
        AssertPersistencePickupCommitted(owner, player, before, entry, characterBytes, wallet);
        armed = false;
        await AssertPersistencePickupReplayIsSilent(owner, player);
        Assert.Equal(1, attemptedSends);

        // Independent ownership models a fresh snapshot read, not process-kill,
        // durable ground objects, or atomic native SQL/quest/Inventory.ADD proof.
        var reopenedOwner = NewOwner(identity: new(42));
        var reopened = await Login(reopenedOwner, uid);
        var reloaded = Current(reopenedOwner, reopened);
        Assert.Equal(wallet.Money, reloaded.Character.Money);
        Assert.Equal(wallet.ConquerPoints, reloaded.Character.ConquerPoints);
        Assert.Equal(wallet.BankCps, reloaded.Character.BankConquerPoints);
        Assert.Equal(wallet.WarehouseMoney.ToString(CultureInfo.InvariantCulture), reloaded.Character.Attributes["WHMoney"]);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(HandlerPersistencePickupItems(before, entry)),
            PlayerItemSnapshotLoader.ToLegacyBytes(reloaded.PlayerItems));
        await AssertPersistencePickupReplayIsSilent(reopenedOwner, reopened);
    }

    [Theory]
    [InlineData("stale-items", 0)]
    [InlineData("nested-owned-floor-uid", 0)]
    [InlineData("stale-money", 1)]
    [InlineData("stale-cp", 2)]
    public async Task PickupRejectsStaleImagesGloballyOwnedItemUidAndCurrencyBaselinesBeforeConsumingFloor(string scenario, int disposition)
    {
        const uint uid = 1_299_981;
        Seed(uid);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        var current = Current(owner, player);
        if (scenario == "stale-items")
        {
            new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, current.PlayerItems with
            {
                InventoryItems = current.PlayerItems.InventoryItems.Select(item => item.Uid == KeptUid
                    ? item.WithItemId(ItemId + 1) : item).ToArray(),
            });
        }
        else if (scenario == "nested-owned-floor-uid")
        {
            var items = current.PlayerItems with
            {
                WarehouseItems = current.PlayerItems.WarehouseItems.Select(item =>
                    item.WithDepositedItems(new[] { AdmissionFloorItem() })).ToArray(),
            };
            owner.Sessions.Set(player.Connection, current with { PlayerItems = items }, out var final);
            Assert.Same(final, Current(owner, player));
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(player.Connection.NativeGameClient!.OrdinarySpellBinding);
            Assert.Same(final.PlayerItems, binding.ItemSnapshot);
            Assert.Same(final.PlayerItems, binding.Items.Image.FinalSnapshot);
            var child = Assert.Single(final.PlayerItems.WarehouseItems.SelectMany(item => item.DepositedItems));
            Assert.Equal(AdmissionPickedItemUid, child.Uid);
        }
        else ReplaceCharacterField(uid, scenario == "stale-money" ? "Money" : "ConquerPoints",
            scenario == "stale-money" ? "1001" : "51");
        var before = Current(owner, player);
        var entry = SeedPersistencePickupFloor(owner, player, disposition);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPersistencePickupDenied(owner, player, before, entry, characterBytes, itemBytes, first);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task PickupPreservesNativeSilverCapCpBankingAndUintWrapWithCoherentSavedAliases(int disposition, bool wrap)
    {
        const uint uid = 1_299_991;
        Seed(uid, money: disposition == 1 ? InventoryMoneyCap - 5 : 1_000, warehouseAlias: true);
        if (disposition == 2) ReplaceCharacterField(uid, "ConquerPoints",
            (wrap ? (long)uint.MaxValue - 5 : 2_000_000_000L - 5).ToString(CultureInfo.InvariantCulture));
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var amount = wrap ? 10 : PersistencePickupAmount;
        var entry = SeedPersistencePickupFloor(owner, player, disposition, amount);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        var expected = disposition == 1
            ? new PersistencePickupWallet(InventoryMoneyCap, 50, 170, 444)
            : new PersistencePickupWallet(1_000, wrap ? 4 : 2_000_000_000L, 50, wrap ? 444 : 564);

        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);

        AssertPersistencePickupCommitted(owner, player, before, entry, characterBytes, expected);
        var frames = player.Sent.Skip(first).ToArray();
        AssertPersistencePickupFrames(frames, entry, expected);
        var messages = frames
            .Select(bytes => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) ? message : null)
            .Where(message => message is not null).Select(message => message!).ToArray();
        var npcReplies = frames.Where(bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _)).ToArray();
        if (disposition == 1)
        {
            var overflow = Assert.Single(messages, message => message.Content.Count > 3 &&
                message.Content[3].Contains("warehouse", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("120", overflow.Content[3], StringComparison.Ordinal);
            Assert.Equal(2_600U, overflow.ChatType);
            Assert.Equal(LegacyMsgMessagePacket.YellowColor, overflow.Color);
            Assert.Empty(npcReplies);
        }
        else
        {
            // Native GameClient.CreateBoxDialog calls Dialog.CreateMessageBox,
            // then FinalizeDialog(messagebox:true): one NPCReply, no Finish or
            // MsgMessage Dialog/color. It is not guarded Player.MessageBox.
            Assert.DoesNotContain(messages, message => message.Content.Count > 3 &&
                message.Content[3].Contains("bank", StringComparison.OrdinalIgnoreCase));
            if (wrap) Assert.Empty(npcReplies);
            else
            {
                var boxFrame = Assert.Single(npcReplies);
                Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(boxFrame), out var box));
                Assert.Equal(0U, box.Unknown);
                Assert.Equal(ushort.MaxValue, box.InputMaxLength);
                Assert.Equal((byte)0, box.OptionId);
                Assert.Equal(LegacyNpcReplyInteractType.MessageBox, box.InteractType);
                Assert.Equal("120 Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.", box.Text);
                var walletFrame = Assert.Single(frames, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                    update.Updates.Any(value => value.Type == LegacyMsgUpdateDataType.ConquerPoints));
                Assert.True(Array.IndexOf(frames, boxFrame) < Array.IndexOf(frames, walletFrame));
            }
        }
        await AssertPersistencePickupReplayIsSilent(owner, player);
    }

    [Fact]
    public async Task PickupWaitsForSharedWorldOperationAndPersistsItsLatestWalletInsteadOfStaleCapturedState()
    {
        const uint uid = 1_299_999;
        Seed(uid);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        var entry = SeedPersistencePickupFloor(owner, player, disposition: 1);
        var barrier = ActualOperationBarrier(owner);
        var original = Current(owner, player);
        var originalCharacter = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        WorldSession? latest = null;
        byte[]? latestCharacter = null;
        Task? pickup = null;
        try
        {
            using (await barrier.EnterSharedAsync())
            {
                pickup = PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);
                Assert.False(pickup.IsCompleted);
                AssertPersistencePickupDenied(owner, player, original, entry, originalCharacter, itemBytes, first);
                var attributes = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["WHMoney"] = "150" };
                latest = original with { Character = original.Character with { Money = 900, Attributes = attributes } };
                owner.Sessions.Set(player.Connection, latest);
                latestCharacter = File.ReadAllBytes(UserPath(uid));
                Assert.Equal(900, CharacterSnapshotParser.ParseText(Encoding.UTF8.GetString(latestCharacter)).Money);
                Assert.False(pickup.IsCompleted);
            }
            Assert.NotNull(pickup);
            await pickup.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(latest);
            Assert.NotNull(latestCharacter);
            AssertPersistencePickupCommitted(owner, player, latest, entry, latestCharacter, new(1_025, 50, 150, 444));
            AssertPersistencePickupFrames(player.Sent.Skip(first).ToArray(), entry, new(1_025, 50, 150, 444));
            await AssertPersistencePickupReplayIsSilent(owner, player);
        }
        finally
        {
            if (pickup is { IsCompleted: false })
            {
                player.Lifetime.Cancel();
                try { await pickup.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
            }
        }
    }

    private FloorItemRuntimeEntry SeedPersistencePickupFloor(Owner owner, Player player, int disposition, long amount = PersistencePickupAmount)
    {
        var character = Current(owner, player).Character;
        var item = AdmissionFloorItem();
        var visualId = disposition switch { 1 => 1_090_000U, 2 => 1_091_020U, _ => ItemId };
        var packet = LegacyFloorItemPacket.FromDroppedItem(item.WithItemId(visualId), character, PersistencePickupFloorUid);
        var entry = disposition switch
        {
            1 => FloorItemRuntimeEntry.ForMoney(character.MapId, packet, amount),
            2 => FloorItemRuntimeEntry.ForConquerPoints(character.MapId, packet, amount),
            _ => FloorItemRuntimeEntry.ForItem(character.MapId, packet, item),
        };
        DropFloor(owner).AddVisible(entry);
        return entry;
    }

    private static PlayerItemSnapshot PersistencePickupItems(WorldSession before, FloorItemRuntimeEntry entry) =>
        entry.Kind == FloorItemRuntimePickupKind.Item
            ? before.PlayerItems with { InventoryItems = before.PlayerItems.InventoryItems.Append(entry.Item!).ToArray() }
            : before.PlayerItems;

    private static PlayerItemSnapshot HandlerPersistencePickupItems(WorldSession before, FloorItemRuntimeEntry entry) =>
        entry.Kind == FloorItemRuntimePickupKind.Item
            // Handler invokes native MsgGameItem.Send after Add selection. This
            // ordinary item has a nonzero max and only its Plus byte changes.
            // The raw append primitive/recovery oracle above remains untouched.
            ? PlayerItemSnapshotLoader.Parse(ExpectedPickupInventoryBytes(before.PlayerItems, entry.Item!,
                merge: false, stack: entry.Item!.StackSize))
            : before.PlayerItems;

    private static PersistencePickupWallet NormalPersistencePickupWallet(WorldSession before, int disposition, long amount) => new(
        before.Character.Money + (disposition == 1 ? amount : 0),
        before.Character.ConquerPoints + (disposition == 2 ? amount : 0),
        long.Parse(before.Character.Attributes["WHMoney"], CultureInfo.InvariantCulture), before.Character.BankConquerPoints);

    private void AssertPersistencePickupCommitted(Owner owner, Player player, WorldSession before,
        FloorItemRuntimeEntry entry, byte[] characterBefore, PersistencePickupWallet wallet)
    {
        var after = Current(owner, player);
        var expectedItems = HandlerPersistencePickupItems(before, entry);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems), File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems), PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
        var values = PersistencePickupWalletValues(before, entry.Kind, wallet);
        var expectedAttributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var value in values) expectedAttributes[value.Key] = value.Value;
        Assert.Equal(expectedAttributes.Count, after.Character.Attributes.Count);
        foreach (var value in expectedAttributes)
            Assert.Equal(value.Value, after.Character.Attributes[value.Key]);
        Assert.Equal(before.Character with
        {
            Money = wallet.Money, ConquerPoints = wallet.ConquerPoints, BankConquerPoints = wallet.BankCps,
            Attributes = after.Character.Attributes,
        }, after.Character);
        Assert.Equal(before with { Character = after.Character, PlayerItems = after.PlayerItems }, after);
        Assert.Equal(ExpectedPersistencePickupCharacterBytes(characterBefore, values), File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static Dictionary<string, string> PersistencePickupWalletValues(WorldSession before,
        FloorItemRuntimePickupKind kind, PersistencePickupWallet wallet)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (kind == FloorItemRuntimePickupKind.Money)
        {
            values["Money"] = wallet.Money.ToString(CultureInfo.InvariantCulture);
            values["WHMoney"] = wallet.WarehouseMoney.ToString(CultureInfo.InvariantCulture);
            if (before.Character.Attributes.ContainsKey("WarehouseMoney")) values["WarehouseMoney"] = values["WHMoney"];
        }
        else if (kind == FloorItemRuntimePickupKind.ConquerPoints)
        {
            values["ConquerPoints"] = wallet.ConquerPoints.ToString(CultureInfo.InvariantCulture);
            values["BankCps"] = wallet.BankCps.ToString(CultureInfo.InvariantCulture);
        }
        return values;
    }

    private static byte[] ExpectedPersistencePickupCharacterBytes(byte[] original, IReadOnlyDictionary<string, string> values)
    {
        var text = Encoding.UTF8.GetString(original);
        foreach (var value in values)
        {
            var expression = new Regex("^(" + Regex.Escape(value.Key) + "=)[^\\r\\n]*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.Single(expression.Matches(text));
            text = expression.Replace(text, match => match.Groups[1].Value + value.Value);
        }
        return Encoding.UTF8.GetBytes(text);
    }

    private void AssertPersistencePickupDenied(Owner owner, Player player, WorldSession before, FloorItemRuntimeEntry entry,
        byte[] characterBytes, byte[] itemBytes, int first)
    {
        Assert.Same(before, Current(owner, player));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Same(entry, Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow)));
        Assert.Empty(player.Sent.Skip(first));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static void AssertPersistencePickupFrames(IReadOnlyList<byte[]> frames, FloorItemRuntimeEntry entry, PersistencePickupWallet wallet)
    {
        Assert.Single(frames, bytes => IsAdmissionFloorRemoval(bytes, PersistencePickupFloorUid));
        var removal = Array.FindIndex(frames.ToArray(), bytes => IsAdmissionFloorRemoval(bytes, PersistencePickupFloorUid));
        var expectedText = entry.Kind switch
        {
            FloorItemRuntimePickupKind.Money => $"You have picked up a {entry.Amount} silvers.",
            FloorItemRuntimePickupKind.ConquerPoints => $"You have picked up a {entry.Amount} ConquerPoints.",
            _ => "You have picked up a OrdinaryNpcSaleFixture.",
        };
        var text = Assert.Single(frames, bytes => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
            message.Content.Count > 3 && message.Content[3] == expectedText);
        Assert.True(Array.IndexOf(frames.ToArray(), text) > removal);
        var updateFrames = frames.Select(bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) ? update : null)
            .Where(update => update is not null).Select(update => update!).ToArray();
        Assert.All(updateFrames, update => Assert.Equal(entry.Packet.ItemOwnerUid, update.Uid));
        var updates = updateFrames.SelectMany(update => update.Updates).ToArray();
        Assert.Equal(entry.Kind == FloorItemRuntimePickupKind.Money ? 2 : 0,
            updates.Count(update => update.Type == LegacyMsgUpdateDataType.Money));
        Assert.Equal(entry.Kind == FloorItemRuntimePickupKind.ConquerPoints ? 1 : 0,
            updates.Count(update => update.Type == LegacyMsgUpdateDataType.ConquerPoints));
        Assert.All(updates.Where(update => update.Type == LegacyMsgUpdateDataType.Money),
            update => Assert.Equal(checked((ulong)wallet.Money), update.Value));
        Assert.All(updates.Where(update => update.Type == LegacyMsgUpdateDataType.ConquerPoints),
            update => Assert.Equal(checked((ulong)wallet.ConquerPoints), update.Value));
        Assert.All(frames.Where(bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Updates.Any(value => value.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.ConquerPoints)),
            bytes => Assert.True(Array.IndexOf(frames.ToArray(), bytes) < removal));
        var items = frames.Where(bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _)).ToArray();
        if (entry.Kind == FloorItemRuntimePickupKind.Item)
        {
            var itemFrame = Assert.Single(items);
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(itemFrame), out var item));
            Assert.Equal(entry.Item!.Uid, item.Uid);
            Assert.Equal(entry.Item.ItemId, item.ItemId);
            Assert.Equal(entry.Item.StackSize, item.StackSize);
            Assert.True(Array.IndexOf(frames.ToArray(), itemFrame) < removal);
        }
        else Assert.Empty(items);
    }

    private async Task AssertPersistencePickupReplayIsSilent(Owner owner, Player player)
    {
        var before = Current(owner, player);
        var characterBytes = File.ReadAllBytes(UserPath(player.Uid));
        var itemBytes = File.ReadAllBytes(ItemPath(player.Uid));
        var first = player.Sent.Count;
        await PickupAdmissionFloor(owner, player, PersistencePickupFloorUid);
        Assert.Same(before, Current(owner, player));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Empty(player.Sent.Skip(first));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private readonly record struct PersistencePickupWallet(long Money, long ConquerPoints, long WarehouseMoney, long BankCps);
}
