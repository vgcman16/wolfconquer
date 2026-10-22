using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearPersistenceTests
{
    private sealed class PersistenceFixture : IDisposable
    {
        private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
        private readonly bool priorReady = LegacyNativeCombatGearTable.m_Load;
        private readonly Dictionary<uint, Dictionary<uint, LegacyNativeCombatGearTable.CQ>> priorGear = LegacyNativeCombatGearTable.Info;
        private readonly Dictionary<uint, LegacyNativeDaoqiDictTypeTable.CQ> priorJade = LegacyNativeDaoqiDictTypeTable.Info;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-gear-" + Guid.NewGuid().ToString("N"));
        internal string Database => Path.Combine(Root, "db");
        internal string GearDirectory => Path.Combine(Database, "PlayersCombatGear");
        internal string PathFor(uint uid) => Path.Combine(GearDirectory, uid.ToString(CultureInfo.CurrentCulture) + ".ini");
        internal LegacyNativeItemCounterShellIni Ini(uint uid) => new(PathFor(uid), overridePath: true);

        internal PersistenceFixture()
        {
            Directory.CreateDirectory(GearDirectory);
            LegacyNativeServerConfig.DbLocation = Database + Path.DirectorySeparatorChar;
            LegacyNativeCombatGearTable.m_Load = true;
            LegacyNativeCombatGearTable.Info = new();
            LegacyNativeDaoqiDictTypeTable.Info = new();
        }

        internal void Row(uint id, uint lookupLevel)
        {
            if (!LegacyNativeCombatGearTable.Info.TryGetValue(id, out var rows))
                LegacyNativeCombatGearTable.Info[id] = rows = new();
            rows[lookupLevel] = new() { ID = id, Level = lookupLevel };
        }

        internal void Jade(uint id, uint family) => LegacyNativeDaoqiDictTypeTable.Info[id] = new() { ID = id, TypeRune = family };

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = priorLocation;
            LegacyNativeCombatGearTable.m_Load = priorReady;
            LegacyNativeCombatGearTable.Info = priorGear;
            LegacyNativeDaoqiDictTypeTable.Info = priorJade;
            string full = Path.GetFullPath(Root);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), temp) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-gear-[0-9a-f]{32}$"))
                throw new InvalidOperationException("Gear fixture cleanup containment failed.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }

    private static Gear.Weapon Weapon(uint id, uint level, uint basis = 100)
    {
        var item = new Gear.Weapon
        {
            ItemID = (Gear.TypeID)id, Level = level, Exp = basis + 1, Equip = basis + 2,
            Transform = basis + 3, MasteryScore = basis + 4
        };
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 7; j++) item.TypeAnima[i][j] = basis + (uint)(i * 7 + j + 10);
        for (int i = 0; i < 6; i++) item.BlackJades[i] = new(basis + (uint)i + 1000, 999);
        return item;
    }

    private static void AssertWeapon(Gear.Weapon expected, Gear.Weapon actual)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.ItemID, actual.ItemID);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Exp, actual.Exp);
        Assert.Equal(expected.Equip, actual.Equip);
        Assert.Equal(expected.Transform, actual.Transform);
        Assert.Equal(expected.MasteryScore, actual.MasteryScore);
        for (int i = 0; i < 2; i++) Assert.Equal(expected.TypeAnima[i], actual.TypeAnima[i]);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(expected.BlackJades[i].Type, actual.BlackJades[i].Type);
            Assert.Equal(0u, actual.BlackJades[i].Data); // Native Save persists Type only.
        }
    }
}
