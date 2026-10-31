using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedRuneDecomposeTests
{
    private static async Task Run(Func<DecomposeFixture, Task> body, string mode = "active", string fault = "",
        Action<DecomposeFixture>? beforeOpen = null)
    {
        var fixture = new DecomposeFixture(); var errors = new List<Exception>();
        try { await fixture.InitializeAsync(mode, fault, beforeOpen); await body(fixture); } catch (Exception error) { errors.Add(error); }
        try { await fixture.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { fixture.RestoreMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { fixture.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    // Every saved input precedes bootstrap. Frame Equip creates positive claims;
    // the real server alone reserves new UIDs, chooses Color and writes the WAL.
    private sealed partial class DecomposeFixture
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
        internal const uint Target = 1_035_001, Shared = Target + 1, Fragment = Target + 2, Kept = Target + 3;
        internal const uint TargetType = 4_020_517, EssenceType = 4_040_001, FragmentType = 3_306_368;
        internal const string Marker = "ManagedRuneSpellOrigins";
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly List<byte[]> Sent = [];
        internal readonly StringWriter Log = new();
        internal WorldDataCatalog Catalog = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal byte[] Ledger = [], OriginalSpells = [];
        internal string Mode = "";
        internal Action<byte[]>? BeforeSend;
        private readonly List<LegacyOwnedAdmissionRuntime> owners = [];
        private Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute>? priorBeasts;
        private ConquerTcpConnection? connection;
        private CancellationTokenSource? lifetime;
        private Exception? observerFailure;
        internal string CharacterPath => Data.UserPath;
        internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string SpellsPath => Path.Combine(Data.Spells, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Handler));
        internal WorldSession Current
        {
            get { Assert.True(Store.TryGet(connection!, out var current), "Owned Decompose admission/session unavailable:\n" + Log);
                return Assert.IsType<WorldSession>(current); }
        }
        internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;
        internal ConquerTcpConnection Connection => connection!;
        internal string? Origins => Current.Character.Attributes.GetValueOrDefault(Marker);
        internal uint HighWater => JsonNode.Parse(File.ReadAllBytes(Data.State))!["HighWater"]!.GetValue<uint>();
        internal LegacyRuneRankingState Rankings => Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler)
            .GetField("_runeRankings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Handler));

        internal async Task InitializeAsync(string mode, string fault, Action<DecomposeFixture>? beforeOpen)
        {
            Mode = mode;
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding)
                .Replace("Class=7051\r\n", mode.EndsWith("-worn", StringComparison.Ordinal) || mode.EndsWith("-alternate", StringComparison.Ordinal)
                    ? "Class=4000\r\n" : "Class=6000\r\n", StringComparison.Ordinal)
                .Replace("MinHitPoints=330\r\n", "MinHitPoints=1\r\n", StringComparison.Ordinal)
                .Replace("KeepBytes=startup-owned-value\r\n", "KeepBytes=startup-owned-value\r\nTailedBeastActivated=true\r\nTailedBeastTotalPoints=0\r\n", StringComparison.Ordinal);
            File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            bool opaqueStored = mode.StartsWith("ambiguous", StringComparison.Ordinal) || mode.StartsWith("absent", StringComparison.Ordinal);
            uint type = opaqueStored ? 4_028_001U : mode == "split" ? 4_020_527U : mode == "red" ? 4_010_101U : mode == "yellow" ? 4_030_101U : TargetType;
            ushort position = mode.EndsWith("-bag", StringComparison.Ordinal) ? (ushort)211 : mode.EndsWith("-worn", StringComparison.Ordinal)
                ? (ushort)102 : mode.EndsWith("-alternate", StringComparison.Ordinal) ? (ushort)122 : (ushort)212;
            var target = Item(Target, type, position).WithBound(mode == "bound" ? (byte)1 : (byte)0);
            if (fault == "locked") target = target.WithLocked(1);
            if (fault == "stacked") target = target.WithStackSize(2);
            if (mode == "raw-target") target = target.WithStackSize(0);
            if (fault == "child") target = target.WithDepositedItems([Item(Target + 99, 1_000_000, 0)]);
            var inventory = new List<PlayerItemSnapshotItem> { Item(Kept, 1_000_000, 0) };
            int inventoryCount = mode is "capacity-new" or "capacity-merge" ? 39 : fault == "inventory-full" ? 40 : 1;
            for (int index = 1; index < inventoryCount; index++) inventory.Add(Item(3_000_000U + (uint)index, 1_000_000, 0));
            if (mode is "merge" or "zero-merge" or "near-full" or "capacity-merge")
            {
                ushort stack = mode == "zero-merge" ? (ushort)0 : mode == "near-full" ? (ushort)9995 : (ushort)9990;
                inventory.Add(new() { Uid = Fragment, ItemId = FragmentType, Position = 0, StackSize = stack,
                    Bound = 1, Locked = 1, Color = 6, Durability = 1, MaximumDurability = 1 });
            }
            var runes = new List<PlayerItemSnapshotItem> { target };
            if (mode is "shared" or "stored-sibling") runes.Add(Item(Shared, 4_020_518, 212));
            int bagCount = fault == "bag-full" ? 100 : mode is "bag-boundary" or "stored-bag" ? 99 : 0;
            for (int index = 0; index < bagCount; index++) runes.Add(Item(2_000_000U + (uint)index, EssenceType, 211));
            // Admission publishes Rune buckets in physical position order.
            // Seed that order before bootstrap, including non-equipped cases
            // which have no setup transaction to rewrite the saved image.
            var orderedRunes = runes.OrderBy(row => row.Position).ThenBy(row => row.Uid).ToArray();
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, new(inventory, [], [], [], orderedRunes));
            PlayerSpellSnapshotEntry[] spells = opaqueStored
                ? [new(6001, 4, 1111, 2, 3, 2), new(8140, 7, 1234, 2, 3, 1)] : [new(6001, 4, 1111, 2, 3, 2)];
            OriginalSpells = PlayerSpellSnapshotLoader.ToLegacyBytes(spells); OriginalSpells[15] = 0xA5;
            if (opaqueStored) OriginalSpells[27] = 0xC7;
            File.WriteAllBytes(SpellsPath, OriginalSpells);
            Catalog = BuildCatalog(mode, fault);
            Data.Bootstrap(); beforeOpen?.Invoke(this); await ReopenFrameAsync();
            Assert.True(Native.Beasts!.Activated); Assert.Equal((byte)1, Native.Beasts.Level);
            bool equip = !opaqueStored && mode is not ("stored" or "stored-bag" or "red" or "yellow" or "bag-boundary") && fault is not ("locked" or "stacked" or "child");
            if (equip) await DispatchAsync(new(Target, 102, MsgItemUsageAction.EquipRune, 112233, 44, 55, 66));
            if (mode == "shared") await DispatchAsync(new(Shared, 122, MsgItemUsageAction.EquipRune, 112244, 77, 88, 99));
            if (equip) Assert.Equal("14160:" + (mode == "split" ? "26" : "16"), Origins);
            else Assert.Null(Origins);
            Ledger = File.ReadAllBytes(Data.State); AssertConverged(); Sent.Clear();
        }

        private WorldDataCatalog BuildCatalog(string mode, string fault)
        {
            var beastFields = Enumerable.Repeat("0", 19).ToArray(); beastFields[0] = "1"; beastFields[1] = "1000"; beastFields[^1] = "";
            var beast = BeastParser.ParseRow("owned-decompose-beast", 1, string.Join("@@", beastFields));
            priorBeasts = LegacyNativeBeastsAtrribute.Attributes; LegacyNativeBeastsAtrribute.Attributes = [];
            LegacyNativeBeastsCatalogStartup.AddRows([beast]);
            ItemType Metadata(uint id, uint spell, uint job = 6000, ushort cap = 0) => OwnedEquipmentBehaviorFixture.Item(id, "OwnedDecompose" + id,
                new Dictionary<int, string> { [ItemType.DurabilityFieldIndex] = "1", [ItemType.RequiredClassFieldIndex] = job.ToString(CultureInfo.InvariantCulture),
                    [ItemType.RuneSpellIdFieldIndex] = spell.ToString(CultureInfo.InvariantCulture), [ItemType.StackSizeFieldIndex] = cap.ToString(CultureInfo.InvariantCulture),
                    [ItemType.TimeFieldIndex] = "0" });
            var rows = new List<ItemType> { Metadata(TargetType, 1416016), Metadata(4_020_518, 1416017), Metadata(4_020_527, 1416026),
                Metadata(4_028_001, 814000, 4000), Metadata(4_010_101, 1427000, 2000), Metadata(4_030_101, 4_030_101, 0), Metadata(1_000_000, 0, 0, 1) };
            rows.AddRange(new uint[] { 4_040_001, 4_050_001, 4_060_001, 3_306_366, 3_306_367, 3_306_368, 3_306_369, 3_306_370, 3_306_371 }.Select(id => Metadata(id, 0, 0, 10000)));
            if (fault == "missing-cap") rows.RemoveAll(row => row.Id == FragmentType);
            if (fault == "malformed-cap") ((string[])rows.Single(row => row.Id == FragmentType).Fields)[ItemType.StackSizeFieldIndex] = "invalid";
            var progress = Curves();
            if (fault == "missing-progress") progress = progress.Where(row => !(row.RuneType == 402 && row.Level == 17)).ToArray();
            string effects = "[1416000]\r\nIsRuneSkill=1\r\n[1427000]\r\nIsRuneSkill=1\r\n";
            if (mode.StartsWith("ambiguous", StringComparison.Ordinal)) effects += "[814000]\r\nName=Legacy\r\n[814000]\r\nIsRuneSkill=1\r\n";
            return NativeCallerWorldDataFixture.Create(rows.ToArray(), [], beastAttributes: new BeastAttribute[] { beast }, runeLevelExperience: progress,
                magicEffects: MagicEffectParser.Parse(effects, "owned-decompose-MagicEffect.ini"), magic: Enumerable.Range(0, 27).Select(Magic).ToArray());
        }

        private static RuneLevelExperience[] Curves()
        {
            uint[] blue = [100, 500, 1300, 2500, 4300, 7000, 10700, 15200, 20500, 26500, 33200, 40700, 49000, 58000,
                68500, 80500, 94000, 109000, 131500, 154000, 176500, 199000, 221500, 251500, 281500, 311500, 341500];
            uint[] yellow = [100, 600, 1600, 4600, 12600, 27600, 47600, 72600, 104600];
            var data = new[] { (Family: 401U, Values: new uint[] { 100 }), (Family: 402U, Values: blue), (Family: 403U, Values: yellow) };
            var result = new List<RuneLevelExperience>();
            foreach (var group in data) for (int index = 0; index < group.Values.Length; index++)
            {
                string row = FormattableString.Invariant($"{result.Count + 1}@@{group.Family}@@{index + 1}@@{group.Values[index]}@@");
                result.Add(RuneParser.ParseLevelExperienceRow("owned-actual-rune_levexp.txt", result.Count + 1, row));
            }
            return result.ToArray();
        }
        private static MagicType Magic(int level)
        {
            var fields = Enumerable.Repeat("0", 53).ToArray(); fields[0] = (1_416_000 + level).ToString(CultureInfo.InvariantCulture);
            fields[1] = "14160"; fields[2] = "6"; fields[3] = "IronShield"; fields[8] = level.ToString(CultureInfo.InvariantCulture);
            fields[14] = "1"; fields[15] = "18"; fields[18] = "100000"; return new(1_416_000 + level, 14160, fields[3], fields);
        }
        internal static PlayerItemSnapshotItem Item(uint uid, uint type, ushort position) => new()
        { Uid = uid, ItemId = type, Position = position, StackSize = 1, Durability = 1, MaximumDurability = 1, Color = 5 };
        internal static byte[] Request(uint uid = Target, uint count = 1)
        {
            var writer = new PacketWriter(WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + sizeof(uint));
            writer.WriteUInt32(0, 9_999_999); writer.WriteUInt64(4, 998877); writer.WriteUInt16(12, 65);
            writer.WriteUInt32(14, 511043495); writer.WriteUInt32(18, count); writer.WriteUInt32(22, 112244); writer.WriteUInt32(26, 55);
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength, uid); return writer.ToArray();
        }
    }
}
