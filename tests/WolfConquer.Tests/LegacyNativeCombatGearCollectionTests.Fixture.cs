using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using CollectionTable = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearCollectionTests
{
    // Only genuine component caches are replaced; no loader, player, host or packet is involved.
    private sealed class Scope : IDisposable
    {
        private readonly bool load = GearTable.m_Load;
        private readonly Dictionary<uint, Dictionary<uint, GearTable.CQ>>? gear = GearTable.Info;
        private readonly Dictionary<uint, JadeTable.CQ>? jade = JadeTable.Info;
        private readonly Dictionary<uint, CollectionTable.CQ>? collection = CollectionTable.Info;
        internal Gear User { get; } = new();

        internal Scope()
        {
            GearTable.m_Load = true;
            GearTable.Info = new();
            JadeTable.Info = new();
            CollectionTable.Info = new();
        }

        internal static GearTable.CQ GearRow(uint id, uint level, uint score)
        {
            var row = new GearTable.CQ { ID = id + 1000, Level = level + 100, Score = score };
            if (!GearTable.Info.TryGetValue(id, out var levels)) GearTable.Info[id] = levels = new();
            levels[level] = row; // The lookup keys, not row ID/Level, select the native metadata.
            return row;
        }

        internal Gear.Weapon Weapon(uint key, uint id, uint level, uint score, uint mastery = 0)
        {
            GearRow(id, level, score);
            var value = new Gear.Weapon { ItemID = (Gear.TypeID)id, Level = level, MasteryScore = mastery };
            User.Items[(Gear.TypeID)key] = value;
            return value;
        }

        internal static JadeTable.CQ JadeRow(uint id, uint family, uint score, uint rune = 0)
        {
            var row = new JadeTable.CQ { ID = id + 1000, TypeCombatGear = family, score = score, TypeRune = rune };
            JadeTable.Info[id] = row;
            return row;
        }

        internal Gear.BlackJade Jade(uint key, uint type)
        {
            var value = new Gear.BlackJade(type, 77);
            User.BlackJadeBag[key] = value;
            return value;
        }

        internal static CollectionTable.CQ Book(uint key, uint family, uint min, uint max)
        {
            var row = new CollectionTable.CQ { Id = key + 1000, Type = family, Min = min, Mix = max,
                attr_type1 = (JadeTable.AttrType)0xDEADBEEF, attr_value1 = 41, value2 = 43 };
            CollectionTable.Info[key] = row;
            return row;
        }

        public void Dispose()
        {
            GearTable.m_Load = load;
            GearTable.Info = gear!;
            JadeTable.Info = jade!;
            CollectionTable.Info = collection!;
        }
    }
}
