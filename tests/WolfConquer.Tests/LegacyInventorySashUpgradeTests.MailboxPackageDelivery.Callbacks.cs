using System.Collections;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed class DeliveryCatalogProbe
    {
        internal bool Armed { get; set; }
        internal bool Fired { get; private set; }
        internal int AfterCommitReads { get; private set; }
        internal Action? Mutation { get; set; }

        internal void ReadSameValue()
        {
            if (!Armed) return;
            AfterCommitReads++;
            if (Fired) return;
            Fired = true;
            Mutation?.Invoke();
        }
    }

    private sealed class DeliveryCatalogFields(string[] values, DeliveryCatalogProbe probe) : IReadOnlyList<string>
    {
        public int Count => values.Length;
        public string this[int index] { get { probe.ReadSameValue(); return values[index]; } }
        public IEnumerator<string> GetEnumerator()
        {
            for (var index = 0; index < values.Length; index++)
            { probe.ReadSameValue(); yield return values[index]; }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static WorldDataCatalog DeliveryProbeCatalog(DeliveryCatalogProbe probe)
    {
        var rows = RankOnePackageRows.Select(row =>
        {
            var fields = Enumerable.Repeat("0", 66).ToArray();
            var name = "OwnedPackage" + row.Id;
            fields[0] = row.Id.ToString(CultureInfo.InvariantCulture);
            fields[1] = name;
            fields[ItemType.DurabilityFieldIndex] = "321";
            fields[ItemType.StackSizeFieldIndex] = "1000";
            return new ItemType(checked((int)row.Id), name,
                row.Id == RankOnePackageRows[0].Id ? new DeliveryCatalogFields(fields, probe) : fields);
        }).ToArray();
        return LegacyItemWorldDataFixture.Create(rows);
    }

    private static Action MutateDeliveryNativeOwner(string kind, Fixture fixture, PackageJoin join, WorldSession committed)
    {
        var client = join.NativeClient;
        var ninja = Assert.IsType<LegacyNativeNinja>(client.MyNinja);
        return kind switch
        {
            "uid" => () => client.Player.UID = checked(Fixture.Uid + 1),
            "ninja-exp" => () => ninja.Exp = 76,
            "ninja-row" => () =>
            {
                Assert.True(ninja.Items.TryGetValue(100, out var row));
                row.Level = checked(row.Level + 1);
            },
            "retire" => () =>
            {
                Assert.True(join.SessionStore.TryRemove(join.Connection!, out var retired));
                Assert.Same(committed, retired);
            },
            "pin" => () => LegacySecondaryPasswordRuntime.Invalidate(join.Connection!, committed),
            "none" => () => { },
            _ => throw new ArgumentException("Unknown delivery fixture mutation.", nameof(kind)),
        };
    }
}
