using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Item = WolfConquer.Core.Data.PlayerItemSnapshotItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemRepairPersistenceTests
{
    private const uint Target = 91;
    private static Item Row(uint uid, uint id = 410009, ushort durability = 250, ushort stack = 1,
        ushort position = 0, Item[]? children = null) => new()
        { Uid = uid, ItemId = id, Durability = durability, MaximumDurability = 1000, StackSize = stack,
            Position = position, DepositedItems = children ?? Array.Empty<Item>() };
    private static PlayerItemSnapshot Items(params Item[] rows) => new(rows.Where(row => row.Position == 0).ToArray(),
        Array.Empty<Item>(), Array.Empty<Item>(), rows.Where(row => row.Position != 0).ToArray(), Array.Empty<Item>());
    private static PlayerItemSnapshot Repaired(PlayerItemSnapshot before, Dictionary<uint, ushort?>? material = null)
    {
        Item[] Convert(IReadOnlyList<Item> source) => source.Where(row => material is null ||
            !material.TryGetValue(row.Uid, out var amount) || amount is not null).Select(row => row.Uid == Target ?
            row.WithDurability(row.MaximumDurability) : material is not null && material.TryGetValue(row.Uid, out var amount) ?
            row.WithStackSize(amount!.Value) : row).ToArray();
        return before with { InventoryItems = Convert(before.InventoryItems), EquipmentItems = Convert(before.EquipmentItems) };
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)4)]
    public void PaidActualJournalPreservesOpaqueRowsAndMoneyTextWhileRepairingOwnedTarget(ushort position) => Run(
        Items(Row(Target, position: position, children: new[] { Row(99) }), Row(97)), f =>
    {
        var before = f.Before; var raw = f.Raw.ToArray(); var text = File.ReadAllBytes(f.CharacterPath);
        var after = f.After(Repaired(before.PlayerItems), 990);
        var write = f.Prepare(after, LegacyItemRepairPayment.Money);
        Assert.Equal(LegacyItemRepairOutcome.Committed, write.TryCommit(out var reason)); Assert.Empty(reason);
        var expected = raw.ToArray(); int targetOffset = position == 0 ? 4 : 4 + 184;
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(targetOffset + 8), 1000);
        Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
        var actualText = File.ReadAllText(f.CharacterPath);
        Assert.Equal(Encoding.UTF8.GetString(text).Replace("Money =  1000 ", "Money =  990 ", StringComparison.Ordinal), actualText);
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
        Assert.Equal(990, CharacterSnapshotParser.ParseText(actualText).Money);
        Assert.Equal((ushort)250, before.PlayerItems.InventoryItems.Concat(before.PlayerItems.EquipmentItems).Single(x => x.Uid == Target).Durability);
    });

    [Fact]
    public void MeteorActualJournalKeepsUnchangedCharacterAndPhysicalStackDistribution() => Run(
        Items(Row(Target, durability: 0), Row(92, 1088001, stack: 2), Row(93, 1088002, stack: 4)), f =>
    {
        var after = f.After(Repaired(f.Before.PlayerItems, new() { [92] = null, [93] = 1 }));
        var character = File.ReadAllBytes(f.CharacterPath); var raw = f.Raw;
        var expected = new byte[raw.Length - 184];
        raw.AsSpan(0, 4 + 184).CopyTo(expected); raw.AsSpan(4 + 2 * 184).CopyTo(expected.AsSpan(4 + 184));
        BinaryPrimitives.WriteInt32LittleEndian(expected, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 8), 1000);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 184 + 52), 1);
        Assert.Equal(LegacyItemRepairOutcome.Committed, f.Prepare(after, LegacyItemRepairPayment.Meteors).TryCommit(out var reason));
        Assert.Empty(reason); Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(expected, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal((ushort)2, f.Before.PlayerItems.InventoryItems.Single(row => row.Uid == 92).StackSize);
    });

    [Fact]
    public void UnrelatedRuntimeMovementAndVitalsRemainRuntimeValuesInsteadOfNormalizingTheirDiskBytes() => Run(
        Items(Row(Target)), f =>
    {
        var original = File.ReadAllText(f.CharacterPath);
        f.Before = f.Before with { Character = f.Before.Character with { X = 321, MinHitPoints = 99 } };
        var after = f.After(Repaired(f.Before.PlayerItems), 990);
        Assert.Equal(LegacyItemRepairOutcome.Committed, f.Prepare(after, LegacyItemRepairPayment.Money).TryCommit(out _));
        Assert.Equal(original.Replace("Money =  1000 ", "Money =  990 ", StringComparison.Ordinal), File.ReadAllText(f.CharacterPath));
        Assert.Equal(321, after.Character.X); Assert.Equal(99, after.Character.MinHitPoints);
    });

    [Fact]
    public void PositivePaidDecreaseNearInt64LimitCannotWrapOrRequireAnInventedWalletCap() => Run(
        Items(Row(Target)), f =>
    {
        f.RewriteMoney(long.MaxValue); var after = f.After(Repaired(f.Before.PlayerItems), long.MaxValue - 1);
        Assert.Equal(LegacyItemRepairOutcome.Committed, f.Prepare(after, LegacyItemRepairPayment.Money).TryCommit(out _));
        Assert.Equal(long.MaxValue - 1, CharacterSnapshotParser.ParseText(File.ReadAllText(f.CharacterPath)).Money);
    });

    private static void Run(PlayerItemSnapshot items, Action<Fixture> action)
    {
        var f = new Fixture(items); Exception? primary = null;
        try { f.Initialize(); action(f); } catch (Exception error) { primary = error; }
        try { f.Dispose(); } catch (Exception error)
        { if (primary is not null) throw new AggregateException(primary, error); throw; }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }
    private sealed class Fixture : IDisposable
    {
        private const long Account = 730001, Uid = 730002;
        private readonly PlayerItemSnapshot input;
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-item-repair-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(root, "Users");
        internal string ItemsRoot => Path.Combine(root, "Items");
        internal string CharacterPath => Path.Combine(Users, Account.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string ItemsPath => Path.Combine(ItemsRoot, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal byte[] Raw = Array.Empty<byte>();
        internal WorldSession Before = null!;
        internal Fixture(PlayerItemSnapshot input) => this.input = input;
        internal void Initialize()
        {
            Guard(); Directory.CreateDirectory(Users); Directory.CreateDirectory(ItemsRoot);
            Raw = PlayerItemSnapshotLoader.ToLegacyBytes(input);
            // Unmodeled native padding/reserved words must survive byte-for-byte.
            int offset = 4; int roots = BinaryPrimitives.ReadInt32LittleEndian(Raw);
            for (int root = 0; root < roots; root++)
            {
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(Raw.AsSpan(offset + 136));
                for (uint row = 0; row <= count; row++)
                { Raw[offset + 14] = (byte)(0xA0 + row); Raw[offset + 72] = 0xA5; Raw[offset + 82] = 0x5A; offset += 184; }
            }
            File.WriteAllBytes(ItemsPath, Raw); WriteCharacter(1000); Reload();
        }
        private void WriteCharacter(long money)
        {
            var lines = new[] { "; keep this comment", "[Character]", "UID=730002", "Name=RepairOwner", "Body=1003", "Face=0", "Haire=0",
                "Class=1001", "FirstClass=0", "SecoundeClass=0", "Reborn=0", "Level=1", "Experience=0", "Map=1002", "X=320", "Y=320",
                "PMap=1002", "PMapX=320", "PMapY=320", "Strength=10", "Agility=10", "Vitaliti=10", "Spirit=10", "Atributes=0",
                "MinHitPoints=100", "MinMana=0", "Money =  " + money.ToString(CultureInfo.InvariantCulture) + " ", "ConquerPoints=0",
                "BoundConquerPoints=0", "BankCps=0", "PkPoints=0", "Online=0", "Opaque=do-not-touch", "[Other]", "Unknown=retained" };
            File.WriteAllText(CharacterPath, string.Join("\r\n", lines), new UTF8Encoding(false));
        }
        private void Reload() => Before = new(Account, "repair-owner", CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath),
            Path.GetFileName(CharacterPath)), PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(ItemsPath)),
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
        internal void RewriteMoney(long money) { WriteCharacter(money); Reload(); }
        internal WorldSession After(PlayerItemSnapshot items, long? money = null)
        {
            var attrs = new Dictionary<string, string>(Before.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            if (money is not null) attrs["Money"] = money.Value.ToString(CultureInfo.InvariantCulture);
            return Before with { Character = Before.Character with { Money = money ?? Before.Character.Money, Attributes = attrs }, PlayerItems = items };
        }
        internal LegacyItemRepairPersistence Prepare(WorldSession after, LegacyItemRepairPayment payment)
        {
            Assert.True(LegacyItemRepairPersistence.TryPrepare(Before, after, ItemsRoot, Target, payment, out var write, out var reason), reason);
            return Assert.IsType<LegacyItemRepairPersistence>(write);
        }
        private void Guard()
        {
            string full = Path.GetFullPath(root); string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("wolf-item-repair-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(Path.GetFileName(full)[17..], "N", out var guid) || guid == Guid.Empty)
                throw new InvalidOperationException("Unowned repair fixture path.");
        }
        public void Dispose() { Guard(); if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
