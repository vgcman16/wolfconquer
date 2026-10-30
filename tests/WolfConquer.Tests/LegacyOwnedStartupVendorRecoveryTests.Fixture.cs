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
public sealed partial class LegacyOwnedStartupVendorRecoveryTests
{
    private static async Task WithFixture(Func<LegacyOwnedStartupVendorRecoveryFixture, Task> test,
        bool cp = false, long? amount = null, int buyerCount = 1)
    {
        LegacyOwnedStartupVendorRecoveryFixture? fixture = null;
        var errors = new List<Exception>();
        try
        {
            fixture = new();
            await fixture.InitializeAsync(cp, amount, buyerCount);
            await test(fixture);
        }
        catch (Exception error) { errors.Add(error); }
        if (fixture is not null)
            try { await fixture.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0 && fixture is not null)
            try { fixture.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
        // Failed assertions/cleanup retain the guarded owned temporary saves.
    }
}

// The real configured Runtime/Frame/Store owns every setup and buy request.
// Captured bytes attest the recorded settlement projection; they do not
// independently authenticate a historical listing or issue a fresh purchase.
internal sealed partial class LegacyOwnedStartupVendorRecoveryFixture
{
    internal const uint SellerUid = LegacyOwnedAdmissionRuntimeFixture.Uid, BuyerUid = SellerUid + 1;
    internal const uint SoldUid = 1_000_091, BuyerItemUid = 1_000_092, SellerKeptUid = 1_000_093;
    internal const uint DepositUid = SoldUid + 200, SashType = 1_100_003, OrdinaryType = 1_000_000;
    internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
    internal readonly StringWriter Log = new();
    private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
    private readonly Dictionary<uint, CancellationTokenSource> lifetimes = [];
    private readonly Dictionary<uint, List<byte[]>> frames = [];
    private readonly List<IDisposable> resources = [];
    private readonly List<LegacyOwnedAdmissionRuntime> runtimes = [];
    internal LegacyOwnedAdmissionRuntime Runtime { get; private set; } = null!;
    internal WorldServerFrameHandler Handler { get; private set; } = null!;
    internal bool IsCp { get; private set; }
    internal long Amount { get; private set; }
    internal uint BoothUid { get; private set; }
    internal int BuyerCount { get; private set; }
    internal byte[] RegistryState { get; set; } = [];
    internal Dictionary<string, byte[]> Before { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, byte[]> After { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    internal IReadOnlyList<string> OrderedTargets { get; private set; } = [];
    internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
    internal string CharacterPath(uint uid) => Path.Combine(Data.Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    internal string ItemsPath(uint uid) => Path.Combine(Data.Items, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    internal string StagePath(string target) => target + ".managed-trade-" +
        JsonNode.Parse(File.ReadAllBytes(PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
    internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
        .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));

    internal async Task InitializeAsync(bool cp = false, long? amount = null, int buyerCount = 1)
    {
        if (buyerCount is < 1 or > 40) throw new ArgumentOutOfRangeException(nameof(buyerCount));
        IsCp = cp; Amount = amount ?? (cp ? 25 : 125); BuyerCount = buyerCount;
        if (Amount < 0 || Amount > (cp ? 60 : 2_000))
            throw new ArgumentOutOfRangeException(nameof(amount));
        var synchronized = TextWriter.Synchronized(Log);
        resources.Add(synchronized);
        resources.Add(new NativeLoginBeastMetadataScope(1000));
        var original = File.ReadAllBytes(Data.UserPath);
        File.WriteAllBytes(CharacterPath(BuyerUid), Replace(original, new Dictionary<string, string>
        {
            [$"UID={SellerUid}\r\n"] = $"UID={BuyerUid}\r\n", ["Name=OwnedStartup\r\n"] = "Name=OwnedBuyer\r\n",
            ["Money=777\r\n"] = "Money=2000\r\n", ["ConquerPoints=222\r\n"] = "ConquerPoints=60\r\n",
        }));
        var sold = Item(SoldUid, SashType).WithDepositedItems([Item(DepositUid)]);
        sold.PerfectionLevel = 1; sold.OwnerUid = SellerUid; sold.OwnerName = "RecordedOwner"; sold.Signature = "owned-vendor";
        var seller = new PlayerItemSnapshot([sold, Item(SellerKeptUid)], [], [], [], [])
        { WarehouseItems = [Item(SoldUid + 100).WithWarehouseId(1000).WithBound(1).WithLocked(1)] };
        var buyer = new PlayerItemSnapshot(Enumerable.Range(0, buyerCount).Select(index =>
            Item(index == 0 ? BuyerItemUid : checked(1_100_000U + (uint)index))).ToArray(), [], [], [], [])
        { WarehouseItems = [Item(BuyerItemUid + 100).WithWarehouseId(1000).WithBound(1).WithLocked(1)] };
        foreach (var (uid, items) in new[] { (SellerUid, seller), (BuyerUid, buyer) })
        {
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid, items);
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(uid, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(uid, PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(uid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(uid, CombatGearSnapshot.Empty);
        }
        Data.Bootstrap(); Runtime = Open();
        Handler = new WorldServerFrameHandler(Data.Loader, null, Catalog(), LegacyRuntimeUpdateLoopOptions.Disabled,
            synchronized,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
        await LoginAsync(SellerUid); await LoginAsync(BuyerUid);
        await StartAndListAsync();
        Before = TargetFiles(); RegistryState = File.ReadAllBytes(Data.State);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint type = OrdinaryType) => new()
    { Uid = uid, ItemId = type, StackSize = 1, Durability = 10, MaximumDurability = 10, Color = 5 };
    private static WorldDataCatalog Catalog() => LegacyItemWorldDataFixture.Create(
        [OwnedEquipmentBehaviorFixture.Item(SashType, "OwnedVendorSash", new Dictionary<int, string> { [ItemType.DurabilityFieldIndex] = "10" }),
         OwnedEquipmentBehaviorFixture.Item(OrdinaryType, "OwnedVendorItem", new Dictionary<int, string> { [ItemType.DurabilityFieldIndex] = "10" })], []);
    private Dictionary<string, byte[]> TargetFiles() => new[] { BuyerUid, SellerUid }
        .SelectMany(uid => new[] { CharacterPath(uid), ItemsPath(uid) })
        .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
    internal static byte[] Replace(byte[] image, IReadOnlyDictionary<string, string> replacements)
    {
        var text = CharacterSnapshotFileEncoding.Decode(image, out var encoding);
        foreach (var (before, after) in replacements)
        { Assert.Contains(before, text, StringComparison.Ordinal); text = text.Replace(before, after, StringComparison.Ordinal); }
        return encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
    }
}
