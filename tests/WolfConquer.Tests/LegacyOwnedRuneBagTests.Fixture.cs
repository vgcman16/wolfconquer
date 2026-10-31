using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedRuneBagTests
{
    private static async Task Run(Func<BagFixture, Task> body, string mode = "in", string fault = "", bool missingSpells = false)
    {
        var f = new BagFixture(); var errors = new List<Exception>();
        try { await f.InitializeAsync(mode, fault, missingSpells); await body(f); } catch (Exception error) { errors.Add(error); }
        try { await f.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { f.RestoreMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { f.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    // All declared rows precede bootstrap. Every mutation, fresh root UID and
    // positive WAL comes from the actual admitted D13 Runtime/Frame/Store.
    private sealed partial class BagFixture
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
        internal const uint Target = 1_036_001, Kept = Target + 1, Child = Target + 2, Receiver = Target + 3;
        internal const uint RuneType = 4_020_517, FragmentType = 3_306_366, RichType = 1_100_006;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly List<byte[]> Sent = [];
        internal readonly StringWriter Log = new();
        internal WorldDataCatalog Catalog = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal bool IntoBag;
        internal bool StartsWithoutSpells;
        internal Action<byte[]>? BeforeSend;
        internal byte[] OriginalSpells = [];
        private readonly List<LegacyOwnedAdmissionRuntime> owners = [];
        private NativeLoginBeastMetadataScope? beasts;
        private ConquerTcpConnection? connection;
        private CancellationTokenSource? lifetime;
        private Exception? observerFailure;
        internal string CharacterPath => Data.UserPath;
        internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string SpellsPath => Path.Combine(Data.Spells, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        internal WorldSession Current
        {
            get { Assert.True(Store.TryGet(connection!, out var current), "Owned RuneBag admission unavailable:\n" + Log);
                return Assert.IsType<WorldSession>(current); }
        }
        internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;
        internal ConquerTcpConnection Connection => connection!;
        internal uint HighWater => JsonNode.Parse(File.ReadAllBytes(Data.State))!["HighWater"]!.GetValue<uint>();
        internal LegacyRuneRankingState Rankings => Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler)
            .GetField("_runeRankings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));

        internal async Task InitializeAsync(string mode, string fault, bool missingSpells = false)
        {
            StartsWithoutSpells = missingSpells;
            IntoBag = mode.StartsWith("in", StringComparison.Ordinal);
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding)
                .Replace("MinHitPoints=330\r\n", "MinHitPoints=1\r\n", StringComparison.Ordinal);
            if (fault == "reborn") text = text.Replace("Reborn=2\r\n", "Reborn=1\r\n", StringComparison.Ordinal);
            if (fault == "level") text = text.Replace("Level=140\r\n", "Level=14\r\n", StringComparison.Ordinal);
            File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            bool rich = mode.Contains("rich", StringComparison.Ordinal);
            bool merge = mode.Contains("merge", StringComparison.Ordinal);
            bool fragment = merge || mode is "in-fragment" or "out-child-receiver-full";
            uint type = rich ? RichType : fragment ? FragmentType : RuneType;
            var target = rich ? Rich(Target, IntoBag ? (ushort)0 : (ushort)211)
                : Item(Target, type, IntoBag ? (ushort)0 : (ushort)211).WithLocked(1).WithBound(1);
            if (fragment) target = target.WithStackSize(10);
            if (mode.Contains("zero", StringComparison.Ordinal)) target = target.WithStackSize(0);
            if (fault == "overcap") target = target.WithStackSize(2);
            var inventory = new List<PlayerItemSnapshotItem>();
            var runes = new List<PlayerItemSnapshotItem>();
            if (IntoBag) inventory.Add(target); else runes.Add(target);
            if (merge)
            {
                inventory.Add(target.WithUid(Receiver).WithPosition(0).WithStackSize(20));
                if (mode == "out-merge-lowest") inventory.Insert(0, target.WithUid(Receiver + 1).WithPosition(0).WithStackSize(30));
            }
            if (mode == "out-child-receiver-full")
                inventory.Add(target.WithUid(Receiver).WithPosition(0).WithStackSize(20)
                    .WithDepositedItems([Item(Child, 1_000_000, 0)]));
            bool full = mode is "out-full" or "out-rich-full" or "out-merge-full" or "out-child-receiver-full";
            int wanted = full ? 40 : Math.Max(1, inventory.Count);
            while (inventory.Count < wanted) inventory.Add(Item(checked(Kept + 100U + (uint)inventory.Count), 1_000_000, 0));
            if (fault == "bag-full") for (int i = 0; i < 100; i++) runes.Add(Item(checked(Kept + 1000U + (uint)i), FragmentType, 211));
            runes.Add(Item(Kept, RuneType, 212));
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, new(inventory, [], [], [], runes));
            OriginalSpells = PlayerSpellSnapshotLoader.ToLegacyBytes([new(6001, 4, 1111, 2, 3, 2)]);
            OriginalSpells[15] = 0xA5; File.WriteAllBytes(SpellsPath, OriginalSpells);
            if (missingSpells) File.Delete(SpellsPath); // Declared before bootstrap/admission, never after binding.
            beasts = new(1000);
            var rows = new[] { Metadata(RuneType, 1416016, 0), Metadata(FragmentType, 0, 10000),
                Metadata(RichType, 0, 0), Metadata(1_000_000, 0, 1) };
            if (fault == "malformed-cap") ((string[])rows.Single(row => row.Id == type).Fields)[ItemType.StackSizeFieldIndex] = "invalid";
            if (fault == "missing-cap") rows = rows.Where(row => row.Id != type).ToArray();
            Catalog = NativeCallerWorldDataFixture.Create(rows);
            Data.Bootstrap(); await ReopenFrameAsync();
            if (missingSpells) Assert.False(File.Exists(SpellsPath)); // Observe the real post-admission baseline.
            AssertConverged(); Sent.Clear();
            Assert.Equal(target.StackSize, Selected().StackSize);
        }

        internal PlayerItemSnapshotItem Selected() => Assert.Single(IntoBag ? Current.PlayerItems.InventoryItems : Current.PlayerItems.RuneItems,
            row => row.Uid == Target);
        internal static PlayerItemSnapshotItem Item(uint uid, uint type, ushort position) => new()
        { Uid = uid, ItemId = type, Position = position, StackSize = 1, Durability = 10, MaximumDurability = 10, Color = 5 };
        private static PlayerItemSnapshotItem Rich(uint uid, ushort position) => new()
        {
            Uid = uid, ItemId = RichType, Position = position, StackSize = 1, Durability = 7, MaximumDurability = 19,
            Bound = 1, Locked = 1, Color = 6, RuneExperience = 76543, ItemPoints = 987, Inscribed = 7, UnlockTimer = 1234,
            Attribute1 = 112001, Attribute5 = 998002, TimeLeftInMinutes = 23, PerfectionLevel = 9, PerfectionProgress = 234,
            OwnerUid = Uid, OwnerName = "OriginalOwner", Signature = "sealed", DepositedItems = [
                new() { Uid = Child, ItemId = 1_000_000, StackSize = 1, Durability = 4, MaximumDurability = 10,
                    Color = 3, Locked = 1, Bound = 1, Attribute1 = 1001, PerfectionLevel = 2, OwnerName = "ChildOwner", Signature = "child" }],
        };
        private static ItemType Metadata(uint id, uint spell, ushort cap) => OwnedEquipmentBehaviorFixture.Item(id, "OwnedRuneBag" + id,
            new Dictionary<int, string> { [ItemType.RequiredClassFieldIndex] = id == RuneType ? "6000" : "0",
                [ItemType.RequiredLevelFieldIndex] = "0", [ItemType.RuneSpellIdFieldIndex] = spell.ToString(CultureInfo.InvariantCulture),
                [ItemType.StackSizeFieldIndex] = cap.ToString(CultureInfo.InvariantCulture), [ItemType.TimeFieldIndex] = "0" });
    }
}
