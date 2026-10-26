using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private const uint StackExistingFirstUid = 8_000_100;
    private const uint StackSecondMailId = 24;

    private static async Task RunStackPackageAsync(Action<Fixture> configure,
        Func<Fixture, PackageJoin, Task> body, WorldDataCatalog? catalog = null)
    {
        var fixture = new Fixture();
        PackageJoin? join = null;
        var errors = new List<Exception>();
        try
        {
            fixture.SeedMailboxPackageJoin(inventoryCount: 0);
            configure(fixture); // Complete owned setup before registry birth or real Frame admission.
            join = new PackageJoin(fixture, catalog ?? StackPackageCatalog(), bindProducer: true);
            await join.StartAsync(bootstrap: true);
            await body(fixture, join);
        }
        catch (Exception error) { errors.Add(error); }
        if (join is not null)
            try { await join.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        if (join is null || join.IsClosed)
        {
            try { await fixture.CleanupAsync(errors); }
            catch (Exception error) { errors.Add(error); Unfinished.Add(fixture); }
        }
        else
        {
            Unfinished.Add(fixture);
            errors.Add(new InvalidOperationException($"Stack fixture retained real owners; closure unproven; ownedRoot={fixture.Root}."));
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Package stack action and cleanup failed.", errors);
    }

    private static PlayerItemSnapshotItem StackOwnedRow(uint uid, uint itemId, ushort amount, uint color = 5) => new()
    {
        Uid = uid, ItemId = itemId, Durability = 321, MaximumDurability = 321, StackSize = amount, Color = color,
    };

    private static PlayerItemSnapshotItem[] StackAllCompatible(ushort amount = 10) =>
        RankOnePackageRows.Select((row, index) => StackOwnedRow(StackExistingFirstUid + (uint)index, row.Id, amount)).ToArray();

    private static WorldDataCatalog StackPackageCatalog(uint splitItem = 0, ushort limit = 1000)
    {
        var items = RankOnePackageRows.Select(row =>
        {
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = row.Id.ToString(CultureInfo.InvariantCulture);
            fields[1] = "OwnedStackPackage" + row.Id;
            fields[ItemType.DurabilityFieldIndex] = "321";
            fields[ItemType.StackSizeFieldIndex] = (row.Id == splitItem ? limit : 1000).ToString(CultureInfo.InvariantCulture);
            return new ItemType(checked((int)row.Id), fields[1], fields);
        });
        return LegacyItemWorldDataFixture.Create(items.ToArray());
    }

    private sealed partial class Fixture
    {
        internal LegacyMailboxEntry StackSecondMailSeed { get; private set; } = null!;
        internal void SetStackInventory(IEnumerable<PlayerItemSnapshotItem> rows, int fillTo = 0)
        {
            var items = rows.ToList();
            for (var index = items.Count; index < fillTo; index++) items.Add(new()
            { Uid = checked((uint)(20_000 + index)), ItemId = 1_088_001, Durability = 5, MaximumDurability = 10, StackSize = 1 });
            SaveTransferSetup(Current.PlayerItems with { InventoryItems = items.ToArray() });
        }

        internal void AddStackSecondPackageMail()
        {
            var mailbox = new LegacyMailboxStore(Root);
            Assert.Equal(2, mailbox.Load(Uid).Count);
            StackSecondMailSeed = mailbox.Add(Uid, new LegacyMailboxEntry
            {
                Id = StackSecondMailId, Flag = 1, Title = "Second real stack package", SenderName = "System sentinel",
                Content = "Request order controls actual item updates.", Timer = AtomicMailSeed.Timer,
            });
            mailbox.Save(Uid);
        }
    }
}
