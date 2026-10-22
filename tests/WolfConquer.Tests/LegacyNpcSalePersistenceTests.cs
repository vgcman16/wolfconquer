using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Disposable files and interrupted replacement simulation, not a live-client/SQL/power-loss test.
public sealed partial class LegacyNpcSalePersistenceTests : IDisposable
{
    private const long Uid = 1_293_600;
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-npc-sale-" + Guid.NewGuid().ToString("N"));
    private string Users => Path.Combine(root, "Users");
    private string Items => Path.Combine(root, "PlayersItems");
    private string CharacterPath => Path.Combine(Users, Uid + ".ini");
    private string ItemPath => Path.Combine(Items, Uid + ".bin");

    [Theory]
    [InlineData("utf8", "\n")]
    [InlineData("utf8-bom", "\r\n")]
    [InlineData("windows1252", "\r")]
    public void CommitsBothImagesPreservingEncodingLineEndingsAndEveryUnrelatedField(string encodingName, string newline)
    {
        var encoding = EncodingFor(encodingName);
        var original = WriteSession(encoding: encoding, newline: newline);
        var expectedCharacter = CharacterSnapshotFileEncoding.ReadAllText(CharacterPath, out _)
            .Replace("Money=1000" + newline, "Money=1030" + newline, StringComparison.Ordinal);
        var updated = Updated(original);
        Assert.True(LegacyNpcSalePersistence.TryPersist(original, updated, Items, out var reason), reason);
        Assert.Equal(Encode(expectedCharacter, encoding), File.ReadAllBytes(CharacterPath));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems), File.ReadAllBytes(ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
        Assert.Equal(50, CharacterSnapshotParser.ParseText(expectedCharacter).ConquerPoints);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("canonical")]
    [InlineData("alias")]
    [InlineData("both")]
    public void WarehouseAliasesAreAddedOrUpdatedCoherently(string warehouseMode)
    {
        var original = WriteSession(warehouseMode: warehouseMode);
        Assert.True(LegacyNpcSalePersistence.TryPersist(original, Updated(original, warehouse: 30), Items, out var reason), reason);
        var parsed = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath));
        Assert.Equal("30", parsed.Attributes["WHMoney"]);
        Assert.Equal(warehouseMode is "alias" or "both", parsed.Attributes.ContainsKey("WarehouseMoney"));
        if (parsed.Attributes.ContainsKey("WarehouseMoney")) Assert.Equal("30", parsed.Attributes["WarehouseMoney"]);
    }

    [Fact]
    public void StaleProjectedMoneyAndCpAttributesDoNotOverrideTypedWalletBaseline()
    {
        var original = WriteSession();
        var attrs = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["Money"] = "5", ["ConquerPoints"] = "1" };
        original = original with { Character = original.Character with { Attributes = attrs } };
        Assert.True(LegacyNpcSalePersistence.TryPersist(original, Updated(original), Items, out var reason), reason);
        var parsed = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath));
        Assert.Equal(1_030, parsed.Money);
        Assert.Equal(50, parsed.ConquerPoints);
    }

    [Theory]
    [InlineData("Money=1000", "Money=999")]
    [InlineData("ConquerPoints=50", "ConquerPoints=49")]
    [InlineData("WHMoney=25", "WHMoney=26")]
    [InlineData("SecurityPass=1234,0,0", "SecurityPass=5678,0,0")]
    public void StaleWalletWarehouseOrPinRejectsBeforeJournalPublication(string from, string to)
    {
        var original = WriteSession();
        ReplaceCharacter(from, to);
        AssertRejectedUnchanged(original, Updated(original));
    }

    [Fact]
    public void FullItemPreimageMustMatchAndAllContainersHaveUniqueUids()
    {
        var original = WriteSession();
        var changed = original.PlayerItems with { RuneItems = [Item(998, position: 211)] };
        File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(changed));
        AssertRejectedUnchanged(original, Updated(original));
        var duplicated = original.PlayerItems with { WarehouseItems = [Item(10, warehouse: 1000)] };
        original = WriteSession(items: duplicated);
        AssertRejectedUnchanged(original, Updated(original));
    }

    [Theory]
    [InlineData("Money=-1")]
    [InlineData("Money=9223372036854775808")]
    [InlineData("ConquerPoints=4294967296")]
    [InlineData("WHMoney=-1")]
    [InlineData("WHMoney=9223372036854775808")]
    [InlineData("WHMoney=26")]
    [InlineData("UID=1293600")]
    [InlineData("SecurityPass=0,0,0")]
    [InlineData("[Character]\nNote=ambiguous")]
    public void DuplicateOrMalformedCharacterFieldsAreNeverCanonicalizedIntoAcceptance(string extra)
    {
        var original = WriteSession();
        ReplaceCharacter("[Unrelated]", extra + "\n[Unrelated]");
        AssertRejectedUnchanged(original, Updated(original));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("4294967296")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void InvalidOrMismatchedPhysicalUidIsRejected(string uid)
    {
        var original = WriteSession();
        ReplaceCharacter("UID=" + Uid, "UID=" + uid);
        AssertRejectedUnchanged(original, Updated(original));
    }

    [Theory]
    [InlineData("Money=1000", "Money=-1")]
    [InlineData("Money=1000", "Money=9223372036854775808")]
    [InlineData("ConquerPoints=50", "ConquerPoints=4294967296")]
    [InlineData("WHMoney=25", "WHMoney=-1")]
    [InlineData("WHMoney=25", "WHMoney=9223372036854775808")]
    public void UniqueMalformedOrOverflowingWalletFieldIsRejectedWithoutRewrite(string from, string to)
    {
        var original = WriteSession();
        ReplaceCharacter(from, to);
        AssertRejectedUnchanged(original, Updated(original));
    }

    [Fact]
    public void InvalidUpdatedWalletAndIdentityNeverReachJournalPublication()
    {
        var original = WriteSession();
        AssertRejectedUnchanged(original, Updated(original, money: -1));
        AssertRejectedUnchanged(original, Updated(original, warehouse: -1));
        AssertRejectedUnchanged(original, Updated(original) with { AccountId = Uid + 1 });
        var updated = Updated(original);
        var oversized = new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["WHMoney"] = "9223372036854775808" };
        AssertRejectedUnchanged(original, updated with { Character = updated.Character with { Attributes = oversized } });
    }

    [Fact]
    public void MaximumNonnegativeSignedWalletValuesRemainRepresentable()
    {
        var original = WriteSession();
        Assert.True(LegacyNpcSalePersistence.TryPersist(original, Updated(original, long.MaxValue, long.MaxValue), Items,
            out var reason), reason);
        var parsed = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath));
        Assert.Equal(long.MaxValue, parsed.Money);
        Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture), parsed.Attributes["WHMoney"]);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("claim")]
    [InlineData("malformed-claim")]
    public void TradingOrEscrowCannotBeSoldThroughThisPrimitive(string mode)
    {
        var extra = mode switch
        {
            "claim" => "ManagedTradeEscrowId=abcdef0123456789abcdef0123456789\nManagedTradeEscrowMoney=10\nManagedTradeEscrowCP=0",
            "malformed-claim" => "ManagedTradeEscrowId=bad",
            _ => string.Empty,
        };
        var original = WriteSession(extra: extra);
        if (mode == "active") original = original with { LegacyTradeActive = true };
        AssertRejectedUnchanged(original, Updated(original));
    }

    [Fact]
    public void ClearedEscrowAndRawPinArePreservedByteForByte()
    {
        var extra = string.Join('\n', LegacyTradeEscrowClaims.ClearValues.Select(pair => pair.Key + "=" + pair.Value));
        var original = WriteSession(extra: extra);
        Assert.True(LegacyNpcSalePersistence.TryPersist(original, Updated(original), Items, out var reason), reason);
        var fields = IniDocument.Parse(File.ReadAllText(CharacterPath)).GetSection("Character");
        foreach (var (key, value) in LegacyTradeEscrowClaims.ClearValues) Assert.Equal(value, fields[key]);
        Assert.Equal("1234,0,0", fields["SecurityPass"]);
    }

    [Fact]
    public void ExistingWarehouseAliasesMustAgreeAndTheUpdatedProjectionCannotLeaveOneStale()
    {
        var original = WriteSession(warehouseMode: "both");
        var updated = Updated(original, warehouse: 30);
        var stale = new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["WarehouseMoney"] = "25" };
        AssertRejectedUnchanged(original, updated with { Character = updated.Character with { Attributes = stale } });
        ReplaceCharacter("WarehouseMoney=25", "WarehouseMoney=26");
        AssertRejectedUnchanged(original, updated);
    }

    [Fact]
    public void NoCpOrUnrelatedCharacterMutationCanRideAlongWithASale()
    {
        var original = WriteSession();
        var updated = Updated(original);
        AssertRejectedUnchanged(original, updated with { Character = updated.Character with { ConquerPoints = 51 } });
        AssertRejectedUnchanged(original, updated with { Character = updated.Character with { Level = 99 } });
        var changedPin = new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["SecurityPass"] = "0,0,0" };
        AssertRejectedUnchanged(original, updated with { Character = updated.Character with { Attributes = changedPin } });
    }

    [Fact]
    public void MissingItemFileIsAcceptedOnlyWhenEveryExpectedContainerIsEmpty()
    {
        var original = WriteSession();
        File.Delete(ItemPath);
        AssertRejectedUnchanged(original, Updated(original));
        original = WriteSession(items: PlayerItemSnapshot.Empty);
        File.Delete(ItemPath);
        var updated = Updated(original) with { PlayerItems = PlayerItemSnapshot.Empty };
        Assert.True(LegacyNpcSalePersistence.TryPersist(original, updated, Items, out var reason), reason);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty), File.ReadAllBytes(ItemPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InterruptedPrefixesRecoverTheSameTwoImagesExactlyOnce(int replacementCount)
    {
        var original = WriteSession();
        var updated = Updated(original);
        Assert.False(LegacyNpcSalePersistence.TryPersist(original, updated, Items,
            count => count == replacementCount, out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out var recovered, out reason), reason);
        Assert.Equal(1, recovered);
        Assert.Equal(1_030, CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath)).Money);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems), File.ReadAllBytes(ItemPath));
        var finalCharacter = File.ReadAllBytes(CharacterPath);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out recovered, out reason), reason);
        Assert.Equal(0, recovered);
        Assert.Equal(finalCharacter, File.ReadAllBytes(CharacterPath));
    }

    [Fact]
    public void PendingTransactionRejectsAnotherSaleWithRecoveryPrefix()
    {
        var original = WriteSession();
        Assert.False(LegacyNpcSalePersistence.TryPersist(original, Updated(original), Items, count => count == 0, out _));
        var beforeCharacter = File.ReadAllBytes(CharacterPath);
        var beforeItems = File.ReadAllBytes(ItemPath);
        Assert.False(LegacyNpcSalePersistence.TryPersist(original, Updated(original), Items, out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.Equal(beforeCharacter, File.ReadAllBytes(CharacterPath));
        Assert.Equal(beforeItems, File.ReadAllBytes(ItemPath));
    }

    [Fact]
    public void OrdinaryOsFailureRestoresExactPreimagesThenRetryCommitsOnce()
    {
        if (!OperatingSystem.IsWindows()) return;
        var original = WriteSession();
        var beforeCharacter = File.ReadAllBytes(CharacterPath);
        var beforeItems = File.ReadAllBytes(ItemPath);
        using (new FileStream(ItemPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(LegacyNpcSalePersistence.TryPersist(original, Updated(original), Items, out var reason));
            Assert.DoesNotContain(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        }
        Assert.Equal(beforeCharacter, File.ReadAllBytes(CharacterPath));
        Assert.Equal(beforeItems, File.ReadAllBytes(ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
        Assert.True(LegacyNpcSalePersistence.TryPersist(original, Updated(original), Items, out var retryReason), retryReason);
        Assert.Equal(1_030, CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath)).Money);
    }

    [Fact]
    public void FailedRollbackRetainsRecoveryEvidenceUntilTheOsLeaseIsReleased()
    {
        if (!OperatingSystem.IsWindows()) return;
        var original = WriteSession();
        FileStream? blockRollback = null;
        try
        {
            using var itemLease = new FileStream(ItemPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Assert.False(LegacyNpcSalePersistence.TryPersist(original, Updated(original), Items, count =>
            {
                if (count == 1) blockRollback = new FileStream(CharacterPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return false;
            }, out var reason));
            Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
            Assert.True(LegacyTradeJournal.HasPending(Users));
        }
        finally { blockRollback?.Dispose(); }
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out var recovered, out var recoveryReason), recoveryReason);
        Assert.Equal(1, recovered);
        Assert.Equal(1_030, CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath)).Money);
    }

    private WorldSession WriteSession(Encoding? encoding = null, string newline = "\n", string warehouseMode = "canonical",
        string extra = "", PlayerItemSnapshot? items = null)
    {
        Directory.CreateDirectory(Users);
        Directory.CreateDirectory(Items);
        var zeroFields = new[] { "Body", "Face", "Haire", "Class", "FirstClass", "SecoundeClass", "Reborn", "Level",
            "Experience", "Map", "X", "Y", "PMap", "PMapX", "PMapY", "Strength", "Agility", "Vitaliti", "Spirit",
            "Atributes", "MinHitPoints", "MinMana", "BoundConquerPoints", "BankCps", "PkPoints", "Online" };
        var warehouse = warehouseMode switch
        {
            "alias" => "WarehouseMoney=25\n", "both" => "WHMoney=25\nWarehouseMoney=25\n",
            "none" => string.Empty, _ => "WHMoney=25\n",
        };
        var text = "[Character]\nUID=" + Uid + "\nName=Andreé\n" +
            string.Join('\n', zeroFields.Select(key => key + "=0")) + "\nMoney=1000\nConquerPoints=50\n" + warehouse +
            "SecurityPass=1234,0,0\nSentinel=preserved\n" + extra +
            "\n[Unrelated]\nMoney=do-not-touch\nNote=preserve café\n";
        text = text.Replace("\n", newline, StringComparison.Ordinal);
        File.WriteAllText(CharacterPath, text, encoding ?? new UTF8Encoding(false, true));
        items ??= new([Item(10), Item(11)], [Item(20, position: 213)], [Item(30, position: 217)],
            [Item(40, position: 4)], [Item(50, position: 211)], [Item(60, warehouse: 100)])
        { WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(71)])] };
        File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(items));
        return new(Uid, "fixture", CharacterSnapshotParser.ParseText(text, CharacterPath), items,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
    }

    private static WorldSession Updated(WorldSession original, long money = 1_030, long warehouse = 25)
    {
        var attrs = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["WHMoney"] = warehouse.ToString(CultureInfo.InvariantCulture) };
        if (attrs.ContainsKey("WarehouseMoney")) attrs["WarehouseMoney"] = warehouse.ToString(CultureInfo.InvariantCulture);
        return original with
        {
            Character = original.Character with { Money = money, Attributes = attrs },
            PlayerItems = original.PlayerItems with { InventoryItems = original.PlayerItems.InventoryItems.Where(item => item.Uid != 10).ToArray() },
        };
    }

    private static PlayerItemSnapshotItem Item(uint uid, ushort position = 0, uint warehouse = 0) => new()
    {
        Uid = uid, ItemId = 1_000_000 + uid, Position = position, WarehouseId = warehouse,
        Durability = 100, MaximumDurability = 100, StackSize = 1, OwnerName = "keeper", Signature = "retain",
    };

    private void AssertRejectedUnchanged(WorldSession original, WorldSession updated)
    {
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.Exists(ItemPath) ? File.ReadAllBytes(ItemPath) : null;
        Assert.False(LegacyNpcSalePersistence.TryPersist(original, updated, Items, out var reason));
        Assert.NotEmpty(reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.Exists(ItemPath) ? File.ReadAllBytes(ItemPath) : null);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    private void ReplaceCharacter(string before, string after) => File.WriteAllText(CharacterPath,
        File.ReadAllText(CharacterPath).Replace(before, after, StringComparison.Ordinal));

    private static Encoding EncodingFor(string name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return name switch { "windows1252" => Encoding.GetEncoding(1252), "utf8-bom" => new UTF8Encoding(true, true), _ => new UTF8Encoding(false, true) };
    }

    private static byte[] Encode(string text, Encoding encoding) => encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
