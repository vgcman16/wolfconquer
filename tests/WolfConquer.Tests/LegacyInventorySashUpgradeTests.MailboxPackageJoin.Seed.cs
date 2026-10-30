using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private const uint PackageHistoricalFloor = 9_000_000;
    private const uint PackageClassExperience = 31;
    private static readonly (uint Id, ushort Quantity)[] RankOnePackageRows =
    [
        (3_324_226, 1), (4_050_001, 200), (4_040_001, 200),
        (3_009_003, 5), (3_316_793, 10), (3_330_770, 10),
    ];

    private sealed partial class Fixture
    {
        internal string PackageGearRoot => Path.Combine(Root, "PlayersCombatGear");
        internal string PackageGearPath => Path.Combine(PackageGearRoot, Uid + ".ini");
        internal string PackageMailboxRoot => Path.GetDirectoryName(AtomicMailPath)!;
        internal string PackageUidState => Path.Combine(Items, ".managed-item-uids", "state.json");
        internal string PackageUidLease => Path.Combine(Items, ".managed-item-uids", "owner.lock");

        internal void SeedMailboxPackageJoin(long job = 10, uint flag = 1, int inventoryCount = 1)
        {
            Seed(frameLogin: true);
            var seededCharacter = Encoding.UTF8.GetString(File.ReadAllBytes(CharacterPath));
            Assert.False(seededCharacter.Contains("ClassExperience=", StringComparison.Ordinal));
            seededCharacter = seededCharacter.Replace("[Unrelated]", "ClassExperience=" +
                PackageClassExperience.ToString(CultureInfo.InvariantCulture) + Environment.NewLine + "[Unrelated]", StringComparison.Ordinal);
            File.WriteAllBytes(CharacterPath, new UTF8Encoding(false).GetBytes(seededCharacter));
            File.WriteAllBytes(CharacterPath, ProjectSashFields(File.ReadAllBytes(CharacterPath),
                new Dictionary<string, string>
                {
                    ["Class"] = job.ToString(CultureInfo.InvariantCulture),
                    ["ClassExperience"] = PackageClassExperience.ToString(CultureInfo.InvariantCulture),
                }));
            var inventory = Enumerable.Range(0, inventoryCount).Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(10_000 + index)), ItemId = 1_088_001,
                Durability = 5, MaximumDurability = 10, StackSize = 1,
            }).ToArray();
            var attachment = new PlayerItemSnapshotItem
            {
                Uid = AtomicMailItemUid, ItemId = 1_088_001, Durability = 5, MaximumDurability = 10,
                WarehouseId = 1000, StackSize = 3, Bound = 1, Plus = 2, Bless = 1,
                SocketProgress = 17, Color = 3, PerfectionLevel = 2, PerfectionProgress = 19,
                OwnerUid = 77, OwnerName = "MailOwner", Signature = "mail sentinel",
            };
            // This fixture reconnects through genuine native wardrobe admission.
            // Keep the retained UID/location sentinel, using an actual garment
            // rather than the generic fixture's meteor in a WH100 slot.
            var wardrobe = Current.PlayerItems.WardrobeItems.Select(item =>
                item.Uid == 9005 ? item.WithItemId(181305) : item).ToArray();
            SaveTransferSetup(Current.PlayerItems with
            {
                InventoryItems = inventory,
                WardrobeItems = wardrobe,
                WarehouseItems = Current.PlayerItems.WarehouseItems.Append(attachment)
                    .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
            });
            AtomicMailSeed = new LegacyMailboxEntry
            {
                Id = AtomicMailId, Title = "Atomic package", SenderName = "System sentinel",
                Content = "All real owners must agree before reward publication.",
                ItemId = AtomicMailItemUid, Money = AtomicMailMoney, EMoney = AtomicMailCp,
                EmoneyRecordType = 19, Flag = flag, Timer = new DateTime(2099, 1, 1).ToBinary(),
            };
            var mailbox = new LegacyMailboxStore(Root);
            mailbox.Add(Uid, AtomicMailSeed);
            mailbox.Add(Uid, new LegacyMailboxEntry
            {
                Id = 22, Title = "Unselected sentinel", SenderName = "Other sender", Content = "Keep me intact.",
                Money = 37, EMoney = 11, EmoneyRecordType = 5, Timer = AtomicMailSeed.Timer,
            });
            mailbox.Save(Uid);
            new CombatGearSnapshotLoader(PackageGearRoot).SaveByCharacterUid(Uid, PackageJoinGear());
        }

        internal LegacyItemUidRegistryOptions PackageRegistryOptions() => new(Users, Items,
            PackageMailboxRoot, Path.Combine(Root, "RedeemItems.bin"), Path.Combine(Root, "ClaimItems.bin"),
            PackageHistoricalFloor, "owned synthetic historical high-water fixture; no live namespace claim");

        internal Dictionary<string, byte[]> PackageFiles()
        {
            Assert.True(File.Exists(PackageUidLease));
            Assert.Equal(0, new FileInfo(PackageUidLease).Length);
            Assert.Equal((FileAttributes)0, File.GetAttributes(PackageUidLease) & FileAttributes.ReparsePoint);
            // The real process lease is FileShare.None. Observe its ordinary empty
            // file metadata; read every other owned file in the complete tree.
            return Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(path, PackageUidLease, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(path => Path.GetRelativePath(Root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static CombatGearSnapshot PackageJoinGear() => CombatGearSnapshot.Empty with
    {
        MasteryPoints = 23, PirateExp = 17, TaoistReiki = 11,
        AirPowers = CombatGearAirPowersSnapshot.Empty with { Points = 19 },
    };

    private static WorldDataCatalog PackageJoinCatalog(string? fault = null)
    {
        var rows = RankOnePackageRows.Select(row =>
        {
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = row.Id.ToString(CultureInfo.InvariantCulture);
            var name = "OwnedPackage" + row.Id;
            fields[1] = name;
            if (fault == "raw-id" && row.Id == 3_330_770) fields[0] = "3330771";
            if (fault == "raw-name" && row.Id == 3_330_770) fields[1] = "ForeignRawName";
            fields[ItemType.DurabilityFieldIndex] = fault == "durability" && row.Id == 3_330_770 ? "invalid" : "321";
            fields[ItemType.StackSizeFieldIndex] = fault == "stack" && row.Id == 4_050_001 ? "1" : "1000";
            if (fault == "timed" && row.Id == 3_330_770) fields[ItemType.TimeFieldIndex] = "60";
            return new ItemType(checked((int)row.Id), name, fields);
        });
        if (fault == "missing") rows = rows.Where(row => row.Id != 3_330_770);
        return LegacyItemWorldDataFixture.Create(rows.ToArray());
    }
}
