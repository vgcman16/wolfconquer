using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    private sealed record MailboxJade(BlackJade Owner, uint Type, uint Data)
    {
        internal static MailboxJade Capture(BlackJade row) => row is null ? throw MailboxRewardUnavailable() : new(row, row.Type, row.Data);
        internal bool IsCurrent(BlackJade row) => ReferenceEquals(Owner, row) && row is not null && row.Type == Type && row.Data == Data;
    }

    private sealed record MailboxBagJade(uint Key, MailboxJade Jade)
    {
        internal static MailboxBagJade Capture(uint key, BlackJade row)
        {
            var image = MailboxJade.Capture(row);
            if (key != image.Type) throw MailboxRewardUnavailable();
            return new(key, image);
        }
        internal bool IsCurrent(BlackJade row) => Jade.IsCurrent(row);
    }

    private sealed class MailboxGearItem
    {
        internal readonly TypeID Key;
        private readonly Weapon owner;
        private readonly TypeID id;
        private readonly uint level, exp, equip, transform, mastery;
        private readonly uint[][] animas;
        private readonly uint[][] animaOwners;
        private readonly uint[][] animaRowOwners;
        private readonly BlackJade[] jades;
        private readonly MailboxJade[] jadeRows;

        private MailboxGearItem(TypeID key, Weapon row, uint[][] animaOwners, uint[][] animaRowOwners, uint[][] values,
            BlackJade[] jades, MailboxJade[] jadeRows)
        {
            Key = key; owner = row; id = row.ItemID; level = row.Level; exp = row.Exp; equip = row.Equip;
            transform = row.Transform; mastery = row.MasteryScore; this.animaOwners = animaOwners;
            this.animaRowOwners = animaRowOwners;
            animas = values; this.jades = jades; this.jadeRows = jadeRows;
        }

        internal static MailboxGearItem Capture(TypeID key, Weapon row)
        {
            if (row is null || row.ItemID != key) throw MailboxRewardUnavailable();
            var values = row.TypeAnima; var jades = row.BlackJades;
            if (values is null || values.Length != 2 || values[0] is null || values[1] is null ||
                values[0].Length != 7 || values[1].Length != 7 || jades is null || jades.Length != 6)
                throw MailboxRewardUnavailable();
            var rowOwners = values.ToArray();
            var result = new MailboxGearItem(key, row, values, rowOwners, [rowOwners[0].ToArray(), rowOwners[1].ToArray()],
                jades, jades.Select(MailboxJade.Capture).ToArray());
            if (!result.IsCurrent(row)) throw MailboxRewardUnavailable();
            return result;
        }

        internal bool IsCurrent(Weapon row)
        {
            if (!ReferenceEquals(owner, row) || row is null || row.ItemID != id || row.Level != level || row.Exp != exp ||
                row.Equip != equip || row.Transform != transform || row.MasteryScore != mastery ||
                !ReferenceEquals(row.TypeAnima, animaOwners) || !ReferenceEquals(row.BlackJades, jades)) return false;
            for (int index = 0; index < 2; index++)
                if (!ReferenceEquals(animaOwners[index], animaRowOwners[index]) || animaOwners[index] is null ||
                    !animas[index].AsSpan().SequenceEqual(animaOwners[index])) return false;
            for (int index = 0; index < jadeRows.Length; index++)
                if (!jadeRows[index].IsCurrent(jades[index])) return false;
            return true;
        }

        internal CombatGearSnapshotItem Snapshot() => new((uint)id, level, exp, equip, transform, mastery,
            [animas[0].ToArray(), animas[1].ToArray()], jadeRows.Select(row => row.Type).ToArray());
    }
}
