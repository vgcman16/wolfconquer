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
public sealed partial class LegacyOwnedStartupTradeRecoveryTests
{
    private static async Task WithFixture(Func<LegacyOwnedStartupTradeRecoveryFixture, Task> test, bool complete = true,
        string currency = "both", bool reciprocalItems = true, string warehouse = "canonical",
        string locked = "none", bool tradePartner = false)
    {
        LegacyOwnedStartupTradeRecoveryFixture? fixture = null;
        Exception? primary = null, cleanup = null;
        try
        {
            fixture = new();
            await fixture.InitializeAsync(complete, currency, reciprocalItems, warehouse, locked, tradePartner);
            await test(fixture);
        }
        catch (Exception error) { primary = error; }
        if (fixture is not null)
            try { await fixture.CloseAsync(); } catch (Exception error) { cleanup = error; }
        if (primary is null && cleanup is null && fixture is not null)
            try { fixture.Data.Dispose(); } catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        // Failed assertions and cleanup retain the guarded owned temp root.
    }
}

// Shared with real Program child tests. Every retained WAL comes from the real
// Frame -> Store -> trade producer; no journal or accepting owner is fabricated.
internal sealed partial class LegacyOwnedStartupTradeRecoveryFixture
{
    internal const uint LeftUid = LegacyOwnedAdmissionRuntimeFixture.Uid, RightUid = LeftUid + 1;
    internal const uint LeftItemUid = 1_000_091, RightItemUid = 1_000_092, ItemId = 1_000_000;
    internal uint LeftAccount => LeftUid;
    internal uint RightAccount => RightUid;
    internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
    private readonly List<LegacyOwnedAdmissionRuntime> runtimes = [];
    private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
    private readonly Dictionary<uint, CancellationTokenSource> lifetimes = [];
    private readonly Dictionary<uint, List<byte[]>> frames = [];
    private readonly List<IDisposable> resources = [];
    private readonly List<SocialAssociationRecord> relations = [];
    private bool complete;
    private string currency = "both";
    private bool reciprocalItems = true;
    private string lockedMode = "none";
    internal StringWriter Log { get; } = new();
    internal LegacyOwnedAdmissionRuntime Runtime { get; private set; } = null!;
    internal WorldServerFrameHandler Handler { get; private set; } = null!;
    internal WorldDataCatalog Catalog { get; private set; } = null!;
    internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
        .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
    internal Dictionary<string, byte[]> Before { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, byte[]> After { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    internal byte[] RegistryState { get; set; } = [];
    internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
    internal string CharacterPath(uint uid) => Path.Combine(Data.Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    internal string ItemsPath(uint uid) => Path.Combine(Data.Items, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    internal string StagePath(string target) => target + ".managed-trade-" +
        JsonNode.Parse(File.ReadAllBytes(PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
    internal IReadOnlyList<string> OrderedTargets { get; private set; } = [];

    internal async Task InitializeAsync(bool complete = true, string currency = "both", bool reciprocalItems = true,
        string warehouse = "canonical", string locked = "none", bool tradePartner = false)
    {
        if (currency is not ("both" or "none" or "left-only")) throw new ArgumentOutOfRangeException(nameof(currency));
        if (warehouse is not ("canonical" or "missing" or "equal-aliases" or "alias-only"))
            throw new ArgumentOutOfRangeException(nameof(warehouse));
        if (locked is not ("none" or "root" or "deposit" or "root-and-deposit"))
            throw new ArgumentOutOfRangeException(nameof(locked));
        this.complete = complete;
        this.currency = currency;
        this.reciprocalItems = reciprocalItems;
        lockedMode = locked;
        resources.Add(Log);
        resources.Add(new NativeLoginBeastMetadataScope(1000));
        var original = File.ReadAllBytes(Data.UserPath);
        if (warehouse != "canonical")
        {
            var replacement = warehouse switch
            {
                "missing" => string.Empty,
                "equal-aliases" => "WHMoney=555\r\nWarehouseMoney=555\r\n",
                "alias-only" => "WarehouseMoney=555\r\n",
                _ => throw new ArgumentOutOfRangeException(nameof(warehouse)),
            };
            original = Replace(original, new Dictionary<string, string> { ["WHMoney=555\r\n"] = replacement });
            File.WriteAllBytes(Data.UserPath, original);
        }
        File.WriteAllBytes(CharacterPath(RightUid), Replace(original, new Dictionary<string, string>
        {
            [$"UID={LeftUid}\r\n"] = $"UID={RightUid}\r\n",
            ["Name=OwnedStartup\r\n"] = "Name=OwnedStartupPeer\r\n",
            ["Money=777\r\n"] = "Money=2000\r\n",
            ["ConquerPoints=222\r\n"] = "ConquerPoints=60\r\n",
        }));
        foreach (var uid in new[] { LeftUid, RightUid })
        {
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid, Items(uid));
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(uid, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(uid, PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(uid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(uid, CombatGearSnapshot.Empty);
        }
        Data.Bootstrap();
        Runtime = Open();
        if (tradePartner) AddTradePartnerRelations();
        Catalog = CreateCatalog(relations);
        Handler = new WorldServerFrameHandler(Data.Loader, null, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
            Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
        await LoginAsync(LeftUid);
        await LoginAsync(RightUid);
        await TradeAsync(LeftUid, new(0) { UID = RightUid });
        await TradeAsync(RightUid, new(1) { UID = LeftUid });
        Assert.True(Current(LeftUid).LegacyTradeActive);
        Assert.True(Current(RightUid).LegacyTradeActive);
        await TradeAsync(LeftUid, new(6) { ItemUID = LeftItemUid });
        if (reciprocalItems) await TradeAsync(RightUid, new(6) { ItemUID = RightItemUid });
        if (currency != "none")
        {
            await TradeAsync(LeftUid, new(7) { Money = 300 });
            await TradeAsync(LeftUid, new(12) { CPS = 5 });
        }
        if (currency == "both")
        {
            await TradeAsync(RightUid, new(7) { Money = 700 });
            await TradeAsync(RightUid, new(12) { CPS = 10 });
        }
        AssertWallet(LeftUid, currency == "none" ? 777 : 477, currency == "none" ? 222 : 217, hasClaim: currency != "none");
        AssertWallet(RightUid, currency == "both" ? 1_300 : 2_000, currency == "both" ? 50 : 60, hasClaim: currency == "both");
        var leftClaim = ReadClaim(LeftUid); var rightClaim = ReadClaim(RightUid);
        if (currency != "none")
        { Assert.NotNull(leftClaim); Assert.Equal(300L, leftClaim.Money); Assert.Equal(5U, leftClaim.ConquerPoints); }
        if (currency == "both")
        {
            Assert.NotNull(rightClaim); Assert.Equal(leftClaim!.TradeId, rightClaim.TradeId);
            Assert.Equal(700L, rightClaim.Money); Assert.Equal(10U, rightClaim.ConquerPoints);
        }
        if (complete) await TradeAsync(LeftUid, new(9));
        Before = TargetFiles();
        RegistryState = File.ReadAllBytes(Data.State);
    }

    internal Task StageAsync(int boundary) => ProducePendingAsync(LegacyJournalInterruption.StopAfter(boundary), boundary);
    internal Task StopAfterStageFlushedAsync(int ordinal) =>
        ProducePendingAsync(LegacyJournalInterruption.StopAfterStageFlushed(ordinal), null);

    private async Task ProducePendingAsync(LegacyJournalInterruption interruption, int? boundary)
    {
        using (interruption)
        {
            Handler.TradeJournalInterruption = interruption;
            try { await TradeAsync(complete ? RightUid : LeftUid, new(complete ? 9U : 2U)); }
            finally { Handler.TradeJournalInterruption = null; }
            Assert.True(interruption.PendingObserved, Log.ToString());
            if (boundary is { } replaced) Assert.Equal(Enumerable.Range(0, replaced + 1), interruption.Visited);
        }
        Assert.True(Runtime.Recovery.IsBlocked);
        Assert.True(LegacyTradeJournal.HasPending(Data.Users));
        var record = JsonNode.Parse(File.ReadAllBytes(PendingPath))!.AsObject();
        Assert.Equal(1, record["Version"]!.GetValue<int>());
        var ordered = new List<string>();
        After = new(StringComparer.OrdinalIgnoreCase);
        foreach (var change in record["Changes"]!.AsArray())
        {
            var kind = (LegacyTradeJournalFileKind)change!["Kind"]!.GetValue<int>();
            var id = checked((uint)change["FileId"]!.GetValue<long>());
            var path = kind == LegacyTradeJournalFileKind.Character ? CharacterPath(id) : ItemsPath(id);
            Assert.Equal(Before[path], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.True(After.TryAdd(path, Convert.FromBase64String(change["After"]!.GetValue<string>())));
            ordered.Add(path);
        }
        Assert.Equal(4, After.Count);
        OrderedTargets = ordered;
        Assert.Equal(RegistryState, File.ReadAllBytes(Data.State));
        var expected = ExpectedWallets();
        AssertWalletImage(LeftUid, After[CharacterPath(LeftUid)], expected.LeftMoney, expected.LeftCp);
        AssertWalletImage(RightUid, After[CharacterPath(RightUid)], expected.RightMoney, expected.RightCp);
        if (boundary is { } count)
            for (var index = 0; index < ordered.Count; index++)
                Assert.Equal(index < count ? After[ordered[index]] : Before[ordered[index]], File.ReadAllBytes(ordered[index]));
    }

    private PlayerItemSnapshot Items(uint uid)
    {
        var physical = uid == LeftUid ? LeftItemUid : RightItemUid;
        var inventory = Item(physical);
        if (uid == LeftUid && (lockedMode is "root" or "root-and-deposit")) inventory = inventory.WithLocked(1);
        if (uid == LeftUid && (lockedMode is "deposit" or "root-and-deposit"))
            inventory = inventory.WithDepositedItems([Item(LeftItemUid + 200).WithLocked(1)]);
        return new([inventory], [], [], [], [])
        { WarehouseItems = [Item(physical + 100, warehouse: 1000).WithBound(1).WithLocked(1)] };
    }
    private static PlayerItemSnapshotItem Item(uint uid, uint warehouse = 0) => new()
    { Uid = uid, ItemId = ItemId, StackSize = 1, Color = 5, Durability = 10, MaximumDurability = 10, WarehouseId = warehouse };
    internal static WorldDataCatalog CreateCatalog(IReadOnlyList<SocialAssociationRecord>? socialAssociations = null) =>
        LegacyItemWorldDataFixture.Create([OwnedEquipmentBehaviorFixture.Item(ItemId, "OwnedStartupRecoveryItem",
            new Dictionary<int, string> { [ItemType.DurabilityFieldIndex] = "10" })], [], socialAssociations: socialAssociations);
    internal static byte[] Replace(byte[] image, IReadOnlyDictionary<string, string> replacements)
    {
        var text = CharacterSnapshotFileEncoding.Decode(image, out var encoding);
        foreach (var (before, after) in replacements)
        { Assert.Contains(before, text, StringComparison.Ordinal); text = text.Replace(before, after, StringComparison.Ordinal); }
        return encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
    }
}
