using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real disposable files and journal interruption hooks; not live-client/SQL/power-loss proof.
public sealed class LegacyItemRemovalPersistenceTests : IDisposable
{
    private const long Uid = 1_293_600;
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-item-removal-" + Guid.NewGuid().ToString("N"));
    private string Users => Path.Combine(root, "Users");
    private string Items => Path.Combine(root, "PlayersItems");
    private string CharacterPath => Path.Combine(Users, Uid + ".ini");
    private string ItemPath => Path.Combine(Items, Uid + ".bin");

    [Theory]
    [InlineData("utf8", "\n", "none")]
    [InlineData("utf8-bom", "\r\n", "both")]
    [InlineData("windows1252", "\r", "alias")]
    public void WholeInventoryUidRemovalPreservesExactCharacterAndEveryRemainingItemImage(
        string encodingName, string newline, string warehouseMode)
    {
        var original = WriteSession(encodingName, newline, warehouseMode);
        var beforeCharacter = File.ReadAllBytes(CharacterPath);
        var updated = Removed(original);
        var expectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems);

        Assert.True(LegacyNpcSalePersistence.TryPersistItemRemoval(original, updated, Items, out var reason), reason);

        Assert.Equal(beforeCharacter, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath));
        Assert.Equal((ushort)4, original.PlayerItems.InventoryItems[0].StackSize);
        var saved = PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(ItemPath));
        Assert.DoesNotContain(saved.InventoryItems, item => item.Uid == 10);
        var remaining = Assert.Single(saved.InventoryItems, item => item.Uid == 11);
        Assert.Equal((ushort)4, remaining.StackSize);
        Assert.Equal("keeper11", remaining.OwnerName);
        Assert.Equal("signature11", remaining.Signature);
        Assert.Equal(23u, remaining.PerfectionLevel);
        Assert.Equal(995u, remaining.Attribute5);
        var fields = IniDocument.Parse(CharacterSnapshotFileEncoding.ReadAllText(CharacterPath, out _)).GetSection("Character");
        Assert.Equal("001000", fields["Money"]);
        Assert.Equal("00050", fields["ConquerPoints"]);
        Assert.Equal(warehouseMode == "both", fields.ContainsKey("WHMoney"));
        Assert.Equal("1234,0,0,", fields["SecurityPass"]);
        foreach (var (key, value) in LegacyTradeEscrowClaims.ClearValues) Assert.Equal(value, fields[key]);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Fact]
    public void IdenticalAttributeCopyIsAllowedWithoutAddingAbsentWarehouseFields()
    {
        var original = WriteSession(warehouseMode: "none");
        var character = File.ReadAllBytes(CharacterPath);
        var updated = Removed(original) with
        {
            Character = original.Character with
            {
                Attributes = new Dictionary<string, string>(original.Character.Attributes, StringComparer.Ordinal),
            },
        };
        Assert.True(LegacyNpcSalePersistence.TryPersistItemRemoval(original, updated, Items, out var reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.DoesNotContain("WHMoney=", File.ReadAllText(CharacterPath), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Money=001000", "Money=999")]
    [InlineData("ConquerPoints=00050", "ConquerPoints=49")]
    [InlineData("WHMoney=25", "WHMoney=26")]
    [InlineData("SecurityPass=1234,0,0,", "SecurityPass=5678,0,0,")]
    [InlineData("UID=1293600", "UID=1293601")]
    [InlineData("UID=1293600", "UID=1293600\nuid=1293600")]
    [InlineData("[Unrelated]", "[Character]\nSentinel=ambiguous\n[Unrelated]")]
    [InlineData("Money=001000", "Money=-1")]
    [InlineData("ConquerPoints=00050", "ConquerPoints=4294967296")]
    [InlineData("SecurityPass=1234,0,0,", "SecurityPass=malformed")]
    public void StaleMalformedOrAmbiguousCharacterBaselineNeverRewritesEitherImage(string before, string after)
    {
        var original = WriteSession();
        ReplaceCharacter(before, after);
        AssertRejectedUnchanged(original, Removed(original));
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("nested")]
    [InlineData("missing")]
    public void StaleOrMissingFullItemPreimageCannotBeOverwritten(string mode)
    {
        var original = WriteSession();
        if (mode == "missing") File.Delete(ItemPath);
        else
        {
            var stale = mode == "metadata"
                ? original.PlayerItems with { EquipmentItems = [Item(40, position: 4).WithDurability(1)] }
                : original.PlayerItems with
                {
                    WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(71).WithStackSize(9)])],
                };
            File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(stale));
        }
        AssertRejectedUnchanged(original, Removed(original));
    }

    [Theory]
    [InlineData("no-removal")]
    [InlineData("two-removals")]
    [InlineData("stack-only")]
    [InlineData("added-item")]
    [InlineData("remaining-metadata")]
    [InlineData("other-container")]
    [InlineData("collection-boundary")]
    [InlineData("inventory-order")]
    public void OnlyOneWholeInventoryUidAndNoOtherItemChangeCanBeCommitted(string mode)
    {
        var original = WriteSession();
        var updated = Removed(original);
        var items = mode switch
        {
            "no-removal" => original.PlayerItems,
            "two-removals" => updated.PlayerItems with { InventoryItems = [original.PlayerItems.InventoryItems[2]] },
            "stack-only" => original.PlayerItems with
            {
                InventoryItems = original.PlayerItems.InventoryItems.Select(item => item.Uid == 10 ? item.WithStackSize(3) : item).ToArray(),
            },
            "added-item" => updated.PlayerItems with { InventoryItems = [.. updated.PlayerItems.InventoryItems, Item(999)] },
            "remaining-metadata" => updated.PlayerItems with
            {
                InventoryItems = updated.PlayerItems.InventoryItems.Select(item => item.Uid == 11 ? item.WithDurability(1) : item).ToArray(),
            },
            "other-container" => updated.PlayerItems with { WarehouseItems = [] },
            // Serialized top-level order is unchanged, but moving a UID between
            // snapshot collections is still not a lawful inventory-only removal.
            "collection-boundary" => updated.PlayerItems with
            {
                RelicFuseItems = [],
                MythsoulBagItems = [.. original.PlayerItems.RelicFuseItems, .. original.PlayerItems.MythsoulBagItems],
            },
            _ => updated.PlayerItems with { InventoryItems = updated.PlayerItems.InventoryItems.Reverse().ToArray() },
        };
        AssertRejectedUnchanged(original, updated with { PlayerItems = items });
    }

    [Theory]
    [InlineData("money")]
    [InlineData("cp")]
    [InlineData("level")]
    [InlineData("attribute")]
    [InlineData("warehouse-insertion")]
    [InlineData("account")]
    [InlineData("native-owner")]
    [InlineData("anima-state")]
    public void NoCharacterAttributeWalletOrSessionOwnerChangeMayRideAlong(string mode)
    {
        var original = WriteSession(warehouseMode: "none");
        var updated = Removed(original);
        updated = mode switch
        {
            "money" => updated with { Character = updated.Character with { Money = 1001 } },
            "cp" => updated with { Character = updated.Character with { ConquerPoints = 51 } },
            "level" => updated with { Character = updated.Character with { Level = 99 } },
            "attribute" => WithAttribute(updated, "SecurityPass", "0,0,0"),
            "warehouse-insertion" => WithAttribute(updated, "WHMoney", "0"),
            "account" => updated with { AccountId = Uid + 1 },
            "native-owner" => updated with { NativeGameClient = new LegacyNativeGameClientRuntime() },
            _ => updated with { LegacyAnimaSecurityOldPinVerified = true },
        };
        AssertRejectedUnchanged(original, updated);
    }

    [Theory]
    [InlineData("inventory")]
    [InlineData("equipment")]
    [InlineData("deposited")]
    [InlineData("updated")]
    public void GlobalDuplicateUidsAreRejectedEvenOutsideTheRemovedItem(string mode)
    {
        var original = WriteSession();
        var duplicate = mode switch
        {
            "inventory" => original.PlayerItems with { InventoryItems = [.. original.PlayerItems.InventoryItems, Item(11)] },
            "equipment" => original.PlayerItems with { EquipmentItems = [Item(11, position: 4)] },
            _ => original.PlayerItems with { WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(11)])] },
        };
        if (mode != "updated") original = WriteSession(items: duplicate);
        var updated = Removed(original);
        if (mode == "updated") updated = updated with
        {
            PlayerItems = updated.PlayerItems with { EquipmentItems = [Item(11, position: 4)] },
        };
        AssertRejectedUnchanged(original, updated);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("claim")]
    [InlineData("partial-claim")]
    public void ActiveTradeAndDurableEscrowCannotBeConsumedByItemRemoval(string mode)
    {
        var claimFields = mode switch
        {
            "claim" => "ManagedTradeEscrowId=abcdef0123456789abcdef0123456789\nManagedTradeEscrowMoney=10\nManagedTradeEscrowCP=0",
            "partial-claim" => "ManagedTradeEscrowMoney=10",
            _ => null,
        };
        var original = WriteSession(claimFields: claimFields);
        if (mode == "active") original = original with { LegacyTradeActive = true };
        AssertRejectedUnchanged(original, Removed(original));
    }

    [Fact]
    public void OrdinaryWindowsReplacementFailureRestoresExactPreimagesAndRetryCommits()
    {
        if (!OperatingSystem.IsWindows()) return;
        var original = WriteSession(warehouseMode: "none");
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        using (new FileStream(ItemPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(LegacyNpcSalePersistence.TryPersistItemRemoval(original, Removed(original), Items, out var reason));
            Assert.DoesNotContain(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        }
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
        var updated = Removed(original);
        Assert.True(LegacyNpcSalePersistence.TryPersistItemRemoval(original, updated, Items, out var retryReason), retryReason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems), File.ReadAllBytes(ItemPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InterruptedPrefixesReplayItemRemovalOnceWithoutChangingCharacterBytes(int replacementCount)
    {
        var original = WriteSession(warehouseMode: "none");
        var character = File.ReadAllBytes(CharacterPath);
        var beforeItems = File.ReadAllBytes(ItemPath);
        var updated = Removed(original);
        var afterItems = PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems);
        Assert.False(LegacyNpcSalePersistence.TryPersistItemRemoval(original, updated, Items,
            count => count == replacementCount, out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.True(LegacyTradeJournal.HasPending(Users));
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(replacementCount == 2 ? afterItems : beforeItems, File.ReadAllBytes(ItemPath));

        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out var recovered, out reason), reason);
        Assert.Equal(1, recovered);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(afterItems, File.ReadAllBytes(ItemPath));
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out recovered, out reason), reason);
        Assert.Equal(0, recovered);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(afterItems, File.ReadAllBytes(ItemPath));
    }

    [Fact]
    public void PendingSingleOwnerJournalFencesAnotherRemovalBeforeAnyWrite()
    {
        var original = WriteSession();
        var updated = Removed(original);
        Assert.False(LegacyNpcSalePersistence.TryPersistItemRemoval(original, updated, Items, count => count == 0, out _));
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        Assert.False(LegacyNpcSalePersistence.TryPersistItemRemoval(original, updated, Items, out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
    }

    private WorldSession WriteSession(string encodingName = "utf8", string newline = "\n", string warehouseMode = "both",
        PlayerItemSnapshot? items = null, string? claimFields = null)
    {
        Directory.CreateDirectory(Users);
        Directory.CreateDirectory(Items);
        var zeroFields = new[] { "Body", "Face", "Haire", "Class", "FirstClass", "SecoundeClass", "Reborn", "Level",
            "Experience", "Map", "X", "Y", "PMap", "PMapX", "PMapY", "Strength", "Agility", "Vitaliti", "Spirit",
            "Atributes", "MinHitPoints", "MinMana", "BoundConquerPoints", "BankCps", "PkPoints", "Online" };
        var warehouse = warehouseMode switch
        {
            "alias" => "WarehouseMoney=25\n", "both" => "WHMoney=25\nWarehouseMoney=25\n", _ => string.Empty,
        };
        claimFields ??= string.Join('\n', LegacyTradeEscrowClaims.ClearValues.Select(pair => pair.Key + "=" + pair.Value));
        var text = ("; café\n[Character]\nUID=" + Uid + "\nName=Andreé\n" +
            string.Join('\n', zeroFields.Select(key => key + "=0")) + "\nMoney=001000\nConquerPoints=00050\n" + warehouse +
            "SecurityPass=1234,0,0,\nSentinel=preserved\n" + claimFields +
            "\n[Unrelated]\nMoney=do-not-touch\nNote=preserve café\n").Replace("\n", newline, StringComparison.Ordinal);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = encodingName switch
        {
            "windows1252" => Encoding.GetEncoding(1252),
            "utf8-bom" => new UTF8Encoding(true, true),
            _ => new UTF8Encoding(false, true),
        };
        File.WriteAllText(CharacterPath, text, encoding);
        items ??= new([Item(10), Item(11), Item(12)], [Item(20, position: 213)], [Item(30, position: 217)],
            [Item(40, position: 4)], [Item(50, position: 211)], [Item(60, warehouse: 100)])
        { WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(71)])] };
        File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(items));
        return new(Uid, "fixture", CharacterSnapshotParser.ParseText(text, CharacterPath), items,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
    }

    private static WorldSession Removed(WorldSession original) => original with
    {
        PlayerItems = original.PlayerItems with
        {
            InventoryItems = original.PlayerItems.InventoryItems.Where(item => item.Uid != 10).ToArray(),
        },
    };

    private static WorldSession WithAttribute(WorldSession session, string key, string value) => session with
    {
        Character = session.Character with
        {
            Attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.Ordinal) { [key] = value },
        },
    };

    private static PlayerItemSnapshotItem Item(uint uid, ushort position = 0, uint warehouse = 0) => new()
    {
        Uid = uid, ItemId = 1_000_000 + uid, Position = position, WarehouseId = warehouse,
        Durability = 73, MaximumDurability = 100, StackSize = 4, Attribute5 = 995,
        PerfectionLevel = 23, PerfectionProgress = 998, OwnerUid = 999,
        OwnerName = "keeper" + uid, Signature = "signature" + uid,
    };

    private void AssertRejectedUnchanged(WorldSession original, WorldSession updated)
    {
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.Exists(ItemPath) ? File.ReadAllBytes(ItemPath) : null;
        Assert.False(LegacyNpcSalePersistence.TryPersistItemRemoval(original, updated, Items, out var reason));
        Assert.NotEmpty(reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.Exists(ItemPath) ? File.ReadAllBytes(ItemPath) : null);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    private void ReplaceCharacter(string before, string after) => File.WriteAllText(CharacterPath,
        File.ReadAllText(CharacterPath).Replace(before, after, StringComparison.Ordinal));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
