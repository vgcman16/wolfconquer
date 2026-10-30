using System.Buffers.Binary;
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
public sealed partial class LegacyOwnedStartupItemSpellRecoveryTests
{
    private static async Task WithFixture(Func<LegacyOwnedStartupItemSpellRecoveryFixture, Task> test,
        ushort stack = 1, string mode = "book", bool missingSpells = false, string bookFlags = "none", string runeFlags = "none",
        bool missingSpellsDirectory = false)
    {
        LegacyOwnedStartupItemSpellRecoveryFixture? fixture = null;
        var errors = new List<Exception>();
        try { fixture = new(); await fixture.InitializeAsync(mode, stack, missingSpells, bookFlags, runeFlags, missingSpellsDirectory); await test(fixture); }
        catch (Exception error) { errors.Add(error); }
        if (fixture is not null) try { await fixture.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0 && fixture is not null) try { fixture.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
        // Preserve the guarded owned root on assertion or cleanup failure.
    }
}

// Shared with the managed Program companion. All positive journals originate
// from the real admitted native Frame/Store item/spell producer.
internal sealed partial class LegacyOwnedStartupItemSpellRecoveryFixture
{
    internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
    internal const uint ItemUid = 1_000_091, KeptUid = ItemUid + 1, WarehouseUid = ItemUid + 100, DepositUid = ItemUid + 200;
    internal const uint BookType = 725_016, OrdinaryType = 1_000_000, SashType = 1_100_003;
    internal const uint RuneType = 4_020_517, KeeperRuneType = 4_020_515;
    internal const ushort LearnedSpell = 1360, RuneSpell = 14160;
    internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
    internal readonly StringWriter Log = new();
    private readonly List<IDisposable> resources = [];
    private readonly List<LegacyOwnedAdmissionRuntime> runtimes = [];
    private ConquerTcpConnection? connection;
    private CancellationTokenSource? lifetime;
    private readonly List<byte[]> sent = [];
    private string mode = "book";
    internal ushort Stack { get; private set; }
    internal LegacyOwnedAdmissionRuntime Runtime { get; private set; } = null!;
    internal WorldServerFrameHandler Handler { get; private set; } = null!;
    internal WorldDataCatalog Catalog { get; private set; } = null!;
    internal byte[] RegistryState { get; set; } = [];
    internal Dictionary<string, byte[]?> Before { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, byte[]> After { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    internal IReadOnlyList<string> OrderedTargets { get; private set; } = [];
    internal string CharacterPath => Data.UserPath;
    internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    internal string SpellsPath => Path.Combine(Data.Spells, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
    internal string StagePath(string target) => target + ".managed-trade-" +
        JsonNode.Parse(File.ReadAllBytes(PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
    internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
        .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
    private WorldSession Current
    {
        get
        {
            var found = Store.TryGet(connection!, out var current);
            Assert.True(found && current is not null, "Actual Rune/book Frame admission has no current Store record. " + Log);
            return Assert.IsType<WorldSession>(current);
        }
    }

    internal async Task InitializeAsync(string mode = "book", ushort stack = 1, bool missingSpells = false, string bookFlags = "none", string runeFlags = "none",
        bool missingSpellsDirectory = false)
    {
        if (missingSpellsDirectory) missingSpells = true;
        if (mode is not ("book" or "rune-identity" or "rune-equip" or "rune-noop" or "rune-displace") || stack is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (bookFlags is not ("none" or "bound" or "inscribed")) throw new ArgumentOutOfRangeException(nameof(bookFlags));
        if (runeFlags is not ("none" or "raw-zero" or "bound" or "inscribed")) throw new ArgumentOutOfRangeException(nameof(runeFlags));
        this.mode = mode; Stack = stack;
        var synchronized = TextWriter.Synchronized(Log); resources.Add(synchronized);
        resources.Add(new NativeLoginBeastMetadataScope(1000));
        var character = File.ReadAllBytes(CharacterPath);
        var text = CharacterSnapshotFileEncoding.Decode(character, out var encoding);
        text = text.Replace("MinHitPoints=330\r\n", "MinHitPoints=1\r\n", StringComparison.Ordinal);
        if (mode != "book") text = text.Replace("Class=7051\r\n", "Class=6000\r\n", StringComparison.Ordinal);
        File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        var held = Item(WarehouseUid, SashType).WithWarehouseId(1000).WithBound(1).WithLocked(1)
            .WithDepositedItems([Item(DepositUid)]);
        held.PerfectionLevel = 1; held.OwnerUid = Uid; held.OwnerName = "RecordedOwner"; held.Signature = "item-spell";
        var items = mode == "book"
            ? new PlayerItemSnapshot([Item(ItemUid, BookType, bound: bookFlags == "bound" ? (byte)1 : (byte)0,
                inscribed: bookFlags == "inscribed" ? (ushort)1 : (ushort)0).WithStackSize(stack), Item(KeptUid)], [], [], [], [])
            : new PlayerItemSnapshot([Item(KeptUid)], [], [], [],
                [Item(ItemUid, RuneType, bound: runeFlags == "bound" ? (byte)1 : (byte)0,
                    inscribed: runeFlags == "inscribed" ? (ushort)1 : (ushort)0)
                    .WithStackSize(runeFlags == "raw-zero" ? (ushort)0 : (ushort)1)
                    .WithPosition(mode is "rune-equip" or "rune-noop" or "rune-displace" ? (ushort)212 : (ushort)102),
                 Item(ItemUid + 2, KeeperRuneType).WithPosition(212)]);
        if (mode == "rune-displace") items = items with { RuneItems =
            [items.RuneItems[0], Item(ItemUid + 2, RuneType).WithPosition(122), Item(ItemUid + 3, KeeperRuneType).WithPosition(102)] };
        items = items with { WarehouseItems = [held] };
        new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, items);
        PlayerSpellSnapshotEntry[] spells = [new(6001, 4, 1111, 2, 3, 2)];
        var spellImage = PlayerSpellSnapshotLoader.ToLegacyBytes(spells); spellImage[^1] = 0xA5;
        if (missingSpells) File.Delete(SpellsPath);
        else File.WriteAllBytes(SpellsPath, spellImage);
        if (missingSpellsDirectory) { Assert.Empty(Directory.GetFileSystemEntries(Data.Spells)); Directory.Delete(Data.Spells); }
        Data.Bootstrap(); Runtime = Open(); Catalog = BuildCatalog();
        Handler = new WorldServerFrameHandler(Data.Loader, null, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
            synchronized, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
        await LoginAsync(); AssertNativeMatchesCurrent();
        Assert.True(Runtime.Recovery.IsReadyForCharacterItemsSpells(Data.Users, Data.Items, Data.Spells));
        Before = new[] { CharacterPath, ItemsPath, SpellsPath }.ToDictionary(path => path,
            path => File.Exists(path) ? File.ReadAllBytes(path) : null, StringComparer.OrdinalIgnoreCase);
        RegistryState = File.ReadAllBytes(Data.State);
        if (missingSpells) { Assert.Null(Before[SpellsPath]); Assert.Empty(Current.PlayerSpells.Spells); Assert.False(File.Exists(SpellsPath)); }
        else Assert.Equal(0xA5, Before[SpellsPath]![^1]);
        if (missingSpellsDirectory) Assert.False(Directory.Exists(Data.Spells));
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint type = OrdinaryType, byte bound = 0, ushort inscribed = 0) => new()
    { Uid = uid, ItemId = type, Durability = 10, MaximumDurability = 10, StackSize = 1, Color = 5, Bound = bound, Inscribed = inscribed };
    internal static WorldDataCatalog BuildCatalog()
    {
        ItemType Metadata(uint id, string name, IReadOnlyDictionary<int, string>? extra = null) =>
            OwnedEquipmentBehaviorFixture.Item(id, name, extra is null
                ? new Dictionary<int, string> { [ItemType.DurabilityFieldIndex] = "10" }
                : new Dictionary<int, string>(extra) { [ItemType.DurabilityFieldIndex] = "10" });
        return NativeCallerWorldDataFixture.Create(
            new ItemType[] { Metadata(BookType, "OwnedBook", new Dictionary<int, string> { [ItemType.StackSizeFieldIndex] = "10" }),
             Metadata(OrdinaryType, "OwnedKeptItem"), Metadata(SashType, "OwnedHeldSash"),
             Metadata(RuneType, "OwnedRune", new Dictionary<int, string>
                { [ItemType.RequiredClassFieldIndex] = "6000", [ItemType.RuneSpellIdFieldIndex] = "1416016" }),
             Metadata(KeeperRuneType, "OwnedRuneKeeper", new Dictionary<int, string>
                { [ItemType.RequiredClassFieldIndex] = "6000", [ItemType.RuneSpellIdFieldIndex] = "1416014" }) }, [],
            magic: [new MagicType(1360, 1360, "OwnedBookSpell", [])],
            magicEffects: MagicEffectParser.Parse("[1416000]\r\nIsRuneSkill=1\r\n", "owned-startup-rune-MagicEffect.ini"));
    }
}
