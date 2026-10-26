using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    private readonly record struct MailboxGearScalars(uint Mastery, uint Reiki, uint PirateExp, uint Lottery,
        uint StarFlow, uint Archshadow, uint ArrowBlades, uint Job)
    {
        internal static MailboxGearScalars Read(LegacyNativeMyCombatGear gear) => new(gear.MasteryPoints,
            gear.TaoistReiki, gear.PirateExp, gear.Lottery, gear.StarFlow, gear.Archshadow, gear.ArrowBlades, gear.GetClass());
    }

    private sealed record MailboxAir(Powers Owner, uint Id, uint SenseLevel, uint SensePower, uint ArmedLevel,
        uint ArmedPower, uint OverloadLevel, uint OverloadPower, uint Points)
    {
        internal static MailboxAir Read(Powers owner) => new(owner, owner.Id, owner.SenseLevel, owner.SenseAipPower,
            owner.ArmedLevel, owner.ArmedAipPower, owner.OverLoadLevel, owner.OverLoadAipPower, owner.Points);
        internal bool IsCurrent(Powers owner, uint points) => ReferenceEquals(Owner, owner) && owner is not null &&
            owner.Id == Id && owner.SenseLevel == SenseLevel && owner.SenseAipPower == SensePower &&
            owner.ArmedLevel == ArmedLevel && owner.ArmedAipPower == ArmedPower && owner.OverLoadLevel == OverloadLevel &&
            owner.OverLoadAipPower == OverloadPower && owner.Points == points;
        internal CombatGearAirPowersSnapshot Snapshot() =>
            new(SenseLevel, ArmedLevel, OverloadLevel, Points, SensePower, ArmedPower, OverloadPower);
    }

    private sealed class MailboxGearState
    {
        internal readonly MailboxGearScalars Scalars;
        internal readonly MailboxAir Air;
        private readonly ConcurrentDictionary<TypeID, Weapon> items;
        private readonly MailboxGearItem[] itemRows;
        private readonly ConcurrentDictionary<uint, BlackJade> bag;
        private readonly MailboxBagJade[] bagRows;
        private readonly ConcurrentDictionary<string, double[]> info;
        private readonly MailboxInfoRow[] infoRows;

        private MailboxGearState(MailboxGearScalars scalars, MailboxAir air, ConcurrentDictionary<TypeID, Weapon> items,
            MailboxGearItem[] itemRows, ConcurrentDictionary<uint, BlackJade> bag, MailboxBagJade[] bagRows,
            ConcurrentDictionary<string, double[]> info, MailboxInfoRow[] infoRows)
        {
            Scalars = scalars; Air = air;
            this.items = items; this.itemRows = itemRows; this.bag = bag; this.bagRows = bagRows;
            this.info = info; this.infoRows = infoRows;
        }

        internal static MailboxGearState Capture(LegacyNativeMyCombatGear gear, int expectedItems, int expectedJades)
        {
            var items = gear.Items; var bag = gear.BlackJadeBag; var info = gear.Info;
            var powers = gear.AirPowers;
            if (items is null || bag is null || info is null || powers is null ||
                items.Count != expectedItems || bag.Count != expectedJades) throw MailboxRewardUnavailable();
            var scalars = MailboxGearScalars.Read(gear);
            var air = MailboxAir.Read(powers);
            var itemRows = items.ToArray().OrderBy(row => (uint)row.Key)
                .Select(row => MailboxGearItem.Capture(row.Key, row.Value)).ToArray();
            var bagRows = bag.ToArray().OrderBy(row => row.Key).Select(row => MailboxBagJade.Capture(row.Key, row.Value)).ToArray();
            var infoRows = CaptureMailboxInfo(info);
            if (itemRows.Length != expectedItems || bagRows.Length != expectedJades ||
                itemRows.Select(row => row.Key).Distinct().Count() != itemRows.Length ||
                bagRows.Select(row => row.Key).Distinct().Count() != bagRows.Length) throw MailboxRewardUnavailable();
            var result = new MailboxGearState(scalars, air, items, itemRows, bag, bagRows, info, infoRows);
            if (!result.IsCurrent(gear, result.Scalars.PirateExp, result.Air.Points)) throw MailboxRewardUnavailable();
            return result;
        }

        internal bool IsCurrent(LegacyNativeMyCombatGear gear, uint pirateExp, uint points)
        {
            if (!ReferenceEquals(gear.Items, items) || !ReferenceEquals(gear.BlackJadeBag, bag) ||
                !ReferenceEquals(gear.Info, info) || !Air.IsCurrent(gear.AirPowers, points) ||
                MailboxGearScalars.Read(gear) != (Scalars with { PirateExp = pirateExp })) return false;
            var currentItems = items.ToArray().OrderBy(row => (uint)row.Key).ToArray();
            if (currentItems.Length != itemRows.Length) return false;
            for (int index = 0; index < itemRows.Length; index++)
                if (currentItems[index].Key != itemRows[index].Key || !itemRows[index].IsCurrent(currentItems[index].Value)) return false;
            var currentBag = bag.ToArray().OrderBy(row => row.Key).ToArray();
            if (currentBag.Length != bagRows.Length) return false;
            for (int index = 0; index < bagRows.Length; index++)
                if (currentBag[index].Key != bagRows[index].Key || !bagRows[index].IsCurrent(currentBag[index].Value)) return false;
            return MailboxInfoCurrent(info, infoRows);
        }

        internal CombatGearSnapshot ToSnapshot(CombatGearSnapshot sourceOrder)
        {
            var byId = itemRows.ToDictionary(row => (uint)row.Key);
            var projected = new CombatGearSnapshotItem[sourceOrder.Items.Count];
            for (int index = 0; index < projected.Length; index++)
            {
                if (!byId.TryGetValue(sourceOrder.Items[index].ItemID, out var row)) throw MailboxRewardUnavailable();
                projected[index] = row.Snapshot();
            }
            var bagKeys = bagRows.Select(row => row.Key).ToHashSet();
            if (sourceOrder.BlackJadeBag.BlackJades.Any(type => !bagKeys.Contains(type))) throw MailboxRewardUnavailable();
            return new(Scalars.Mastery, Scalars.Reiki, Scalars.PirateExp, Scalars.Lottery, projected.Length,
                Air.Snapshot(), projected, new(bagRows.Length, sourceOrder.BlackJadeBag.BlackJades.ToArray()));
        }
    }
}
