using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    internal sealed partial class MailboxPointsCapture
    {
        private readonly LegacyNativeNinja ninja;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid;
        private readonly uint job;
        private readonly ulong flag;
        private readonly uint[] scalars;
        private readonly ConcurrentDictionary<uint, Item> items;
        private readonly MailboxPointsRow[] rows;

        private readonly record struct MailboxPointsRow(uint Key, Item Item, uint Level, uint Position, uint Exp, uint UK)
        {
            internal bool IsCurrent(Item item) => ReferenceEquals(Item, item) && item is not null &&
                item.ItemID == Key && item.Level == Level && item.Position == Position && item.Exp == Exp && item.UK == UK;
        }

        private MailboxPointsCapture(LegacyNativeNinja ninja, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, ConcurrentDictionary<uint, Item> items, MailboxPointsRow[] rows)
        {
            this.ninja = ninja; this.client = client; this.player = player; this.items = items; this.rows = rows;
            uid = player.UID; job = player.Class; flag = ninja.Flag; scalars = ReadScalars(ninja);
        }

        internal static bool TryCreate(LegacyNativeNinja ninja, LegacyNativeGameClientRuntime client,
            out MailboxPointsCapture? capture)
        {
            capture = null;
            if (client?.Player is not { } player || !ReferenceEquals(player.Owner, client) ||
                player.UID == 0 || !LegacyNativeAtributesStatus.IsNinja(player.Class) ||
                !ninja.IsCombatOwnedBy(client) || ninja.Items is not { } items || items.Count > MaximumEquippedItemRows)
                return false;
            var entries = items.Take(MaximumEquippedItemRows + 1).ToArray();
            if (entries.Length > MaximumEquippedItemRows || entries.Any(row =>
                row.Value is null || row.Key == 0 || row.Key != row.Value.ItemID)) return false;
            var rows = entries.Select(row => new MailboxPointsRow(row.Key, row.Value, row.Value.Level,
                row.Value.Position, row.Value.Exp, row.Value.UK)).ToArray();
            var candidate = new MailboxPointsCapture(ninja, client, player, items, rows);
            if (!candidate.IsCurrent()) return false;
            capture = candidate;
            return true;
        }

        internal bool IsCurrent(uint? expectedExp = null)
        {
            if (!OwnerAndValuesCurrent(expectedExp)) return false;
            // Never invoke a replaceable source dictionary's custom comparer.
            // Its callback could change the owner after the earlier state check.
            var entries = items.Take(MaximumEquippedItemRows + 1).ToArray();
            if (entries.Length != rows.Length) return false;
            var current = new Dictionary<uint, Item>(entries.Length);
            foreach (var entry in entries)
                if (!current.TryAdd(entry.Key, entry.Value)) return false;
            foreach (var row in rows)
                if (!current.TryGetValue(row.Key, out var item) || !row.IsCurrent(item)) return false;
            return OwnerAndValuesCurrent(expectedExp);
        }

        private bool OwnerAndValuesCurrent(uint? expectedExp)
        {
            if (!ninja.IsCombatOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || player.UID != uid || player.Class != job ||
                ninja.Flag != flag || !ReferenceEquals(ninja.Items, items) || items.Count != rows.Length) return false;
            var current = ReadScalars(ninja);
            for (var i = 0; i < scalars.Length; i++)
                if (current[i] != (i == 0 && expectedExp.HasValue ? expectedExp.Value : scalars[i])) return false;
            return true;
        }

        internal void PublishExp(uint value) => ninja.Exp = value;

        private static uint[] ReadScalars(LegacyNativeNinja ninja) =>
        [
            ninja.Exp, ninja.Fire, ninja.Water, ninja.Earth, ninja.Wind, ninja.Lightning,
            ninja.tmp_Fire, ninja.tmp_Water, ninja.tmp_Earth, ninja.tmp_Wind, ninja.tmp_Lightning,
            ninja.FireMastery, ninja.WaterMastery, ninja.EarthMastery, ninja.WindMastery,
            ninja.LightningMastery, ninja.UnlockMastery,
        ];
    }
}
