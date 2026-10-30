using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real-file component coverage, not a live client/native SQL/power-loss proof.
public sealed class LegacyFloorPickupPersistenceTests : IDisposable
{
    private const long Uid = 1_293_600;
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-pickup-save-" + Guid.NewGuid().ToString("N"));
    private string Users => Path.Combine(root, "Users");
    private string Items => Path.Combine(root, "PlayersItems");
    private string CharacterPath => Path.Combine(Users, Uid + ".ini");
    private string ItemPath => Path.Combine(Items, Uid + ".bin");
    private sealed record Plan(WorldSession Updated, FloorItemRuntimeEntry Entry, PlayerItemSnapshotItem? Item);

    [Theory]
    [InlineData("utf8", "\n")]
    [InlineData("utf8-bom", "\r\n")]
    [InlineData("windows1252", "\r")]
    public void ItemPickupPreservesExactCharacterAndAppendsTheFullStackImage(string encodingName, string newline)
    {
        var original = WriteSession(encodingName: encodingName, newline: newline, warehouseMode: "none");
        var character = File.ReadAllBytes(CharacterPath);
        var plan = Prepare(original, FloorItemRuntimePickupKind.Item);
        Assert.True(Persist(original, plan, out var reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(plan.Updated.PlayerItems), File.ReadAllBytes(ItemPath));
        var item = Assert.Single(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(ItemPath)).InventoryItems, item => item.Uid == 900);
        Assert.Equal((ushort)4, item.StackSize);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal("keeper900", item.OwnerName);
        Assert.Equal("signature900", item.Signature);
        Assert.Equal(23u, item.PerfectionLevel);
        Assert.Equal(995u, item.Attribute5);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Theory]
    [InlineData(FloorItemRuntimePickupKind.Money, "utf8", "\n", "none")]
    [InlineData(FloorItemRuntimePickupKind.Money, "utf8-bom", "\r\n", "alias")]
    [InlineData(FloorItemRuntimePickupKind.Money, "windows1252", "\r", "both")]
    [InlineData(FloorItemRuntimePickupKind.ConquerPoints, "utf8", "\n", "none")]
    [InlineData(FloorItemRuntimePickupKind.ConquerPoints, "utf8-bom", "\r\n", "alias")]
    [InlineData(FloorItemRuntimePickupKind.ConquerPoints, "windows1252", "\r", "both")]
    public void CurrencyPickupChangesOnlyItsIntendedCharacterFields(FloorItemRuntimePickupKind kind,
        string encodingName, string newline, string warehouseMode)
    {
        var original = WriteSession(encodingName: encodingName, newline: newline, warehouseMode: warehouseMode);
        var itemBytes = File.ReadAllBytes(ItemPath);
        var expected = CharacterSnapshotFileEncoding.ReadAllText(CharacterPath, out var encoding);
        var plan = Prepare(original, kind, amount: 30);
        expected = kind == FloorItemRuntimePickupKind.Money
            ? expected.Replace("Money=1000" + newline, "Money=1030" + newline, StringComparison.Ordinal)
            : expected.Replace("ConquerPoints=50" + newline, "ConquerPoints=80" + newline, StringComparison.Ordinal);
        if (kind == FloorItemRuntimePickupKind.Money && warehouseMode == "none")
            expected = expected.Replace("[Unrelated]", "WHMoney=0" + newline + "[Unrelated]", StringComparison.Ordinal);
        if (kind == FloorItemRuntimePickupKind.Money && warehouseMode == "alias")
            expected = expected.Replace("[Unrelated]", "WHMoney=25" + newline + "[Unrelated]", StringComparison.Ordinal);
        Assert.True(Persist(original, plan, out var reason), reason);
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expected)).ToArray(), File.ReadAllBytes(CharacterPath));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath));
        Assert.Equal(kind == FloorItemRuntimePickupKind.Money || warehouseMode == "both",
            CharacterSnapshotParser.ParseText(expected).Attributes.ContainsKey("WHMoney"));
    }

    [Theory]
    [InlineData(FloorItemRuntimePickupKind.Money, 499999999999L, 1L, 500000000000L, 25L, 0L)]
    [InlineData(FloorItemRuntimePickupKind.Money, 499999999999L, 2L, 500000000000L, 26L, 1L)]
    [InlineData(FloorItemRuntimePickupKind.Money, 500000000000L, 4294967295L, 500000000000L, 4294967320L, 4294967295L)]
    [InlineData(FloorItemRuntimePickupKind.ConquerPoints, 1999999999L, 1L, 2000000000L, 10L, 0L)]
    [InlineData(FloorItemRuntimePickupKind.ConquerPoints, 2000000000L, 1L, 2000000000L, 11L, 1L)]
    [InlineData(FloorItemRuntimePickupKind.ConquerPoints, 4294967295L, 1L, 2000000000L, 2294967306L, 2294967296L)]
    [InlineData(FloorItemRuntimePickupKind.ConquerPoints, 2000000000L, 4294967295L, 2000000000L, 4294967305L, 4294967295L)]
    [InlineData(FloorItemRuntimePickupKind.ConquerPoints, 4294967295L, 4294967295L, 2000000000L, 6589934600L, 6589934590L)]
    public void CurrencyCapsAndWideCpConservationAreSharedByPlannerAndPersistence(FloorItemRuntimePickupKind kind,
        long before, long amount, long expectedWallet, long expectedStorage, long expectedTransfer)
    {
        var original = WriteSession(money: kind == FloorItemRuntimePickupKind.Money ? before : 1000,
            cps: kind == FloorItemRuntimePickupKind.ConquerPoints ? before : 50);
        var itemsBefore = File.ReadAllBytes(ItemPath);
        var expectedText = CharacterSnapshotFileEncoding.ReadAllText(CharacterPath, out var encoding);
        var beforeStorage = kind == FloorItemRuntimePickupKind.Money
            ? long.Parse(original.Character.Attributes["WHMoney"], CultureInfo.InvariantCulture)
            : original.Character.BankConquerPoints;
        Assert.True(LegacyFloorPickupWallet.TryAdd(original.Character, kind, amount, out var change, out var reason), reason);
        Assert.Equal(expectedWallet, kind == FloorItemRuntimePickupKind.Money
            ? change.UpdatedCharacter.Money : change.UpdatedCharacter.ConquerPoints);
        Assert.Equal(expectedStorage, kind == FloorItemRuntimePickupKind.Money
            ? long.Parse(change.UpdatedCharacter.Attributes["WHMoney"], CultureInfo.InvariantCulture)
            : change.UpdatedCharacter.BankConquerPoints);
        Assert.Equal(expectedTransfer, kind == FloorItemRuntimePickupKind.Money ? change.SilverTransferred : change.CpsTransferred);
        Assert.Equal(checked(before + beforeStorage + amount), checked(expectedWallet + expectedStorage));
        var beforeText = before.ToString(CultureInfo.InvariantCulture);
        var walletText = expectedWallet.ToString(CultureInfo.InvariantCulture);
        var storageText = expectedStorage.ToString(CultureInfo.InvariantCulture);
        if (kind == FloorItemRuntimePickupKind.Money)
            expectedText = expectedText.Replace("\nMoney=" + beforeText + "\n", "\nMoney=" + walletText + "\n", StringComparison.Ordinal)
                .Replace("\nWHMoney=25\n", "\nWHMoney=" + storageText + "\n", StringComparison.Ordinal)
                .Replace("\nWarehouseMoney=25\n", "\nWarehouseMoney=" + storageText + "\n", StringComparison.Ordinal);
        else
            expectedText = expectedText.Replace("\nConquerPoints=" + beforeText + "\n", "\nConquerPoints=" + walletText + "\n", StringComparison.Ordinal)
                .Replace("\nBankCps=10\n", "\nBankCps=" + storageText + "\n", StringComparison.Ordinal);
        var plan = Prepare(original, kind, amount);
        Assert.True(Persist(original, plan, out reason), reason);
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expectedText)).ToArray(), File.ReadAllBytes(CharacterPath));
        Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath));
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath));
        Assert.Equal(change.UpdatedCharacter.Money, saved.Money);
        Assert.Equal(change.UpdatedCharacter.ConquerPoints, saved.ConquerPoints);
        Assert.Equal(change.UpdatedCharacter.BankConquerPoints, saved.BankConquerPoints);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(4294967296L)]
    public void NativeUintCurrencyAmountRangeRejectsUnsupportedValuesWithoutWrites(long amount)
    {
        var original = WriteSession();
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        foreach (var kind in new[] { FloorItemRuntimePickupKind.Money, FloorItemRuntimePickupKind.ConquerPoints })
        {
            Assert.False(LegacyFloorPickupWallet.TryAdd(original.Character, kind, amount, out _, out var reason));
            Assert.NotEmpty(reason);
        }
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
    }

    [Theory]
    [InlineData("money")]
    [InlineData("warehouse")]
    [InlineData("bank")]
    public void UnsupportedSignedStorageOverflowIsRejectedNotWrapped(string mode)
    {
        var original = WriteSession(money: mode == "money" ? long.MaxValue : 500_000_000_000,
            cps: 2_000_000_000, bank: mode == "bank" ? long.MaxValue : 10,
            warehouse: mode == "warehouse" ? long.MaxValue : 25);
        var character = File.ReadAllBytes(CharacterPath);
        var kind = mode == "bank" ? FloorItemRuntimePickupKind.ConquerPoints : FloorItemRuntimePickupKind.Money;
        Assert.False(LegacyFloorPickupWallet.TryAdd(original.Character, kind, 1, out _, out var reason));
        Assert.Equal("floorPickupCurrencyOverflow", reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Theory]
    [InlineData("UID=1293600", "UID=1293601")]
    [InlineData("Money=1000", "Money=999")]
    [InlineData("ConquerPoints=50", "ConquerPoints=49")]
    [InlineData("WHMoney=25", "WHMoney=26")]
    [InlineData("SecurityPass=1234,0,0,", "SecurityPass=5678,0,0,")]
    [InlineData("BankCps=10", "BankCps=11")]
    [InlineData("BankCps=10", "BankCps=10\nbankcps=10")]
    [InlineData("[Unrelated]", "[Character]\nNote=duplicate\n[Unrelated]")]
    public void StrictPhysicalCharacterPreimagesCannotBeOverwritten(string before, string after)
    {
        var original = WriteSession();
        var plan = Prepare(original, FloorItemRuntimePickupKind.ConquerPoints);
        File.WriteAllText(CharacterPath, File.ReadAllText(CharacterPath).Replace(before, after, StringComparison.Ordinal));
        AssertRejectedUnchanged(original, plan);
    }

    [Fact]
    public void CpAndBankDiskBaselineUsesTypedWalletNotOldScalarAttributeProjections()
    {
        var original = WriteSession();
        var attrs = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["Money"] = "1", ["ConquerPoints"] = "2", ["BankCps"] = "3" };
        original = original with { Character = original.Character with { Attributes = attrs } };
        Assert.True(Persist(original, Prepare(original, FloorItemRuntimePickupKind.ConquerPoints), out var reason), reason);
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath));
        Assert.Equal(1000, saved.Money);
        Assert.Equal(80, saved.ConquerPoints);
        Assert.Equal(10, saved.BankConquerPoints);
    }

    [Theory]
    [InlineData("nested-metadata")]
    [InlineData("missing")]
    public void FullItemPreimageMustMatchEvenForCurrencyPickup(string mode)
    {
        var original = WriteSession();
        var plan = Prepare(original, FloorItemRuntimePickupKind.Money);
        if (mode == "missing") File.Delete(ItemPath);
        else File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems with
        {
            WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(71).WithStackSize(9)])],
        }));
        AssertRejectedUnchanged(original, plan);
    }

    [Theory]
    [InlineData("remaining-metadata")]
    [InlineData("forged-pickup")]
    [InlineData("no-append")]
    [InlineData("extra-append")]
    [InlineData("wallet")]
    [InlineData("quest")]
    [InlineData("native-owner")]
    public void OnlyTheDeclaredFullAppendOrExactCurrencyChangeMayCommit(string mode)
    {
        var original = WriteSession();
        var plan = Prepare(original, mode == "wallet" ? FloorItemRuntimePickupKind.Money : FloorItemRuntimePickupKind.Item);
        var updated = plan.Updated;
        switch (mode)
        {
            case "remaining-metadata":
                updated = updated with { PlayerItems = updated.PlayerItems with
                { InventoryItems = updated.PlayerItems.InventoryItems.Select(item => item.Uid == 10 ? item.WithDurability(1) : item).ToArray() } };
                break;
            case "forged-pickup":
                var forged = plan.Item!.WithDurability(1);
                plan = plan with { Item = forged };
                updated = updated with { PlayerItems = updated.PlayerItems with
                { InventoryItems = [.. original.PlayerItems.InventoryItems, forged] } };
                break;
            case "no-append": updated = updated with { PlayerItems = original.PlayerItems }; break;
            case "extra-append": updated = updated with { PlayerItems = updated.PlayerItems with
                { InventoryItems = [.. updated.PlayerItems.InventoryItems, Item(901)] } }; break;
            case "wallet": updated = updated with { Character = updated.Character with { Money = updated.Character.Money + 1 } }; break;
            case "quest": updated = updated with { Character = updated.Character with
                { PlayerQuests = new([new(1311, PlayerQuestStatus.Accepted, 0, [1])]) } }; break;
            case "native-owner": updated = updated with { NativeGameClient = new LegacyNativeGameClientRuntime() }; break;
        }
        AssertRejectedUnchanged(original, plan with { Updated = updated });
    }

    [Theory]
    [InlineData("inventory")]
    [InlineData("equipment")]
    [InlineData("deposited")]
    public void PickupUidMustBeUniqueAcrossEveryExistingContainer(string mode)
    {
        var original = WriteSession();
        var items = mode switch
        {
            "inventory" => original.PlayerItems with { InventoryItems = [.. original.PlayerItems.InventoryItems, Item(900)] },
            "equipment" => original.PlayerItems with { EquipmentItems = [Item(900, position: 4)] },
            _ => original.PlayerItems with { WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(900)])] },
        };
        original = WriteSession(items: items);
        AssertRejectedUnchanged(original, Prepare(original, FloorItemRuntimePickupKind.Item));
    }

    [Fact]
    public void ActiveTradeOrAnUnsettledClaimCannotBeCreditedThroughPickupPersistence()
    {
        var original = WriteSession();
        var plan = Prepare(original, FloorItemRuntimePickupKind.Item);
        AssertRejectedUnchanged(original with { LegacyTradeActive = true }, plan);
        original = LegacyTradeEscrowClaims.WithClaim(original, new(Guid.NewGuid(), 1, 0));
        plan = Prepare(original, FloorItemRuntimePickupKind.Item);
        AssertRejectedUnchanged(original, plan);
    }

    [Fact]
    public void MissingItemFileIsAllowedForATrulyEmptyOriginalAndGeneratedDeclaredAppend()
    {
        var original = WriteSession(items: PlayerItemSnapshot.Empty, warehouseMode: "none");
        File.Delete(ItemPath);
        var character = File.ReadAllBytes(CharacterPath);
        var plan = Prepare(original, FloorItemRuntimePickupKind.Item);
        plan = plan with { Entry = plan.Entry with { Item = null } };
        Assert.True(Persist(original, plan, out var reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(plan.Updated.PlayerItems), File.ReadAllBytes(ItemPath));
    }

    [Fact]
    public void WindowsReplacementFailureRollsBackBothImagesAndCanBeRetried()
    {
        if (!OperatingSystem.IsWindows()) return;
        var original = WriteSession();
        var plan = Prepare(original, FloorItemRuntimePickupKind.Money);
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        using (new FileStream(ItemPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(Persist(original, plan, out var reason));
            Assert.DoesNotContain(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        }
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
        Assert.True(Persist(original, plan, out var retryReason), retryReason);
        Assert.Equal(1030, CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath)).Money);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InterruptedPrefixesRecoverTheSameCurrencyAndItemImagesExactlyOnce(int replacementCount)
    {
        var original = WriteSession(cps: 2_000_000_000);
        var plan = Prepare(original, FloorItemRuntimePickupKind.ConquerPoints, amount: 30);
        Assert.False(LegacyNpcSalePersistence.TryPersistFloorPickup(original, plan.Updated, Items, plan.Entry,
            plan.Item, LegacyJournalInterruption.StopAfter(replacementCount), out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out var recovered, out reason), reason);
        Assert.Equal(1, recovered);
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath));
        Assert.Equal(2_000_000_000, saved.ConquerPoints);
        Assert.Equal(40, saved.BankConquerPoints);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems), items);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out recovered, out reason), reason);
        Assert.Equal(0, recovered);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
    }

    private WorldSession WriteSession(long money = 1000, long cps = 50, long bank = 10, long warehouse = 25,
        string encodingName = "utf8", string newline = "\n", string warehouseMode = "both", PlayerItemSnapshot? items = null)
    {
        Directory.CreateDirectory(Users);
        Directory.CreateDirectory(Items);
        var zeroFields = new[] { "Body", "Face", "Haire", "Class", "FirstClass", "SecoundeClass", "Reborn", "Level",
            "Experience", "Map", "X", "Y", "PMap", "PMapX", "PMapY", "Strength", "Agility", "Vitaliti", "Spirit",
            "Atributes", "MinHitPoints", "MinMana", "BoundConquerPoints", "PkPoints", "Online" };
        var warehouseText = warehouseMode switch
        {
            "alias" => "WarehouseMoney=" + warehouse + "\n",
            "both" => "WHMoney=" + warehouse + "\nWarehouseMoney=" + warehouse + "\n", _ => string.Empty,
        };
        var text = ("; café\n[Character]\nUID=" + Uid + "\nName=Andreé\n" +
            string.Join('\n', zeroFields.Select(key => key + "=0")) +
            "\nMoney=" + money.ToString(CultureInfo.InvariantCulture) + "\nConquerPoints=" + cps.ToString(CultureInfo.InvariantCulture) +
            "\nBankCps=" + bank.ToString(CultureInfo.InvariantCulture) + "\n" + warehouseText +
            "SecurityPass=1234,0,0,\nSentinel=preserved\n\n[Unrelated]\nMoney=do-not-touch\nNote=preserve café\n")
            .Replace("\n", newline, StringComparison.Ordinal);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = encodingName switch
        {
            "windows1252" => Encoding.GetEncoding(1252), "utf8-bom" => new UTF8Encoding(true, true), _ => new UTF8Encoding(false, true),
        };
        File.WriteAllText(CharacterPath, text, encoding);
        items ??= new([Item(10), Item(11)], [Item(20, position: 213)], [Item(30, position: 217)],
            [Item(40, position: 4)], [Item(50, position: 211)], [Item(60, warehouse: 100)])
        { WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(71)])] };
        File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(items));
        return new(Uid, "fixture", CharacterSnapshotParser.ParseText(text, CharacterPath), items,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
    }

    private static Plan Prepare(WorldSession original, FloorItemRuntimePickupKind kind, long amount = 30)
    {
        var item = kind == FloorItemRuntimePickupKind.Item ? Item(900, position: 9) : null;
        var packet = LegacyFloorItemPacket.FromDroppedItem(item ?? Item(999), original.Character, 5000);
        var entry = new FloorItemRuntimeEntry(original.Character.MapId, packet, kind, kind == FloorItemRuntimePickupKind.Item ? 0 : amount,
            item, OwnerOnly: false);
        if (item is not null)
        {
            var pickup = item.WithPosition(0);
            return new(original with { PlayerItems = original.PlayerItems with
            { InventoryItems = [.. original.PlayerItems.InventoryItems, pickup] } }, entry, pickup);
        }
        Assert.True(LegacyFloorPickupWallet.TryAdd(original.Character, kind, amount, out var change, out var reason), reason);
        return new(original with { Character = change.UpdatedCharacter }, entry, null);
    }

    private bool Persist(WorldSession original, Plan plan, out string reason) =>
        LegacyNpcSalePersistence.TryPersistFloorPickup(original, plan.Updated, Items, plan.Entry, plan.Item, out reason);

    private void AssertRejectedUnchanged(WorldSession original, Plan plan)
    {
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.Exists(ItemPath) ? File.ReadAllBytes(ItemPath) : null;
        Assert.False(Persist(original, plan, out var reason));
        Assert.NotEmpty(reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.Exists(ItemPath) ? File.ReadAllBytes(ItemPath) : null);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    private static PlayerItemSnapshotItem Item(uint uid, ushort position = 0, uint warehouse = 0) => new()
    {
        Uid = uid, ItemId = 1_000_000 + uid, Position = position, WarehouseId = warehouse,
        Durability = 73, MaximumDurability = 100, StackSize = 4, Attribute5 = 995,
        PerfectionLevel = 23, PerfectionProgress = 998, OwnerUid = 999, OwnerName = "keeper" + uid, Signature = "signature" + uid,
    };

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
