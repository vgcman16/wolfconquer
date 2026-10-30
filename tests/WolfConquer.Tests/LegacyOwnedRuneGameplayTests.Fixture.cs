using System.Buffers;
using System.Globalization;
using System.Net;
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
public sealed partial class LegacyOwnedRuneGameplayTests
{
    private static async Task Run(Func<LegacyOwnedRuneGameplayFixture, Task> body, bool legacy = false,
        byte extraSlots = 0, uint flags = 0, bool sameId = false, string classification = "1", bool passiveFirst = false,
        string legacyPassiveInput = "")
    {
        var fixture = new LegacyOwnedRuneGameplayFixture(); var failures = new List<Exception>();
        try { await fixture.InitializeAsync(legacy, extraSlots, flags, sameId, classification, passiveFirst, legacyPassiveInput); await body(fixture); }
        catch (Exception error) { failures.Add(error); }
        try { await fixture.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 0) try { fixture.Data.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException(failures);
    }
}

// All claims and positive journals originate from actual admitted Rune requests.
internal sealed partial class LegacyOwnedRuneGameplayFixture
{
    internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
    internal const uint First = 1_010_001, Shared = First + 1, Other = First + 2, Yellow = First + 3;
    internal const uint FirstType = 4_020_517, SharedType = 4_020_518, OtherType = 4_022_001, YellowType = 4_030_101;
    internal const uint PassiveType = 4_030_102;
    internal const ushort FirstSpell = 14160, OtherSpell = 15470;
    internal const string Marker = "ManagedRuneSpellOrigins";
    internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
    internal readonly List<byte[]> Sent = [];
    internal readonly StringWriter Log = new();
    private readonly List<LegacyOwnedAdmissionRuntime> runtimes = [];
    private Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute>? priorBeasts;
    private ConquerTcpConnection? connection;
    private CancellationTokenSource? lifetime;
    private Exception? observationFailure;
    internal Action<byte[]>? BeforeSend;
    internal LegacyOwnedAdmissionRuntime Runtime = null!;
    internal WorldServerFrameHandler Handler = null!;
    internal WorldDataCatalog Catalog = null!;
    internal byte[] Registry = [], OriginalSpells = [];
    internal Dictionary<string, byte[]?> Before = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, byte[]> After = new(StringComparer.OrdinalIgnoreCase);
    internal string CharacterPath => Data.UserPath;
    internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    internal string SpellsPath => Path.Combine(Data.Spells, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
    internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
        .GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Handler));
    internal WorldSession Current
    {
        get { Assert.True(Store.TryGet(connection!, out var found), "Owned native Rune admission/session unavailable:\n" + Log);
            return Assert.IsType<WorldSession>(found); }
    }
    internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;
    internal ConquerTcpConnection Connection => connection!;
    internal WorldPacketHandlerContext Context => new(Connection, Log, Current, WorldData: Catalog, Sessions: Store,
        PlayerItemsRoot: Data.Items, PlayerSpellsRoot: Data.Spells, PlayerProficienciesRoot: Data.Proficiencies,
        ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
    { Trades = Assert.IsType<LegacyTradeStateStore>(typeof(WorldServerFrameHandler).GetField("_tradeStates", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Handler)),
      TradeRecovery = Runtime.Recovery };

    internal async Task InitializeAsync(bool legacy, byte extraSlots, uint flags, bool sameId, string classification = "1", bool passiveFirst = false,
        string legacyPassiveInput = "")
    {
        if (legacyPassiveInput.Length != 0 && (!passiveFirst || legacy))
            throw new ArgumentException("Declared legacy passive input requires the actual passive item and independent retained spell baseline.");
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding)
            .Replace("Class=7051\r\n", "Class=6000\r\n", StringComparison.Ordinal)
            .Replace("MinHitPoints=330\r\n", "MinHitPoints=1\r\n", StringComparison.Ordinal)
            .Replace("KeepBytes=startup-owned-value\r\n", "KeepBytes=startup-owned-value\r\nTailedBeastActivated=true\r\n" +
                "TailedBeastTotalPoints=0\r\nStamina=100\r\nTailedBeastFlag=" + flags.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
        File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        var firstType = passiveFirst ? PassiveType : FirstType;
        var rows = new[] { Item(First, firstType), Item(Shared, sameId ? firstType : SharedType), Item(Other, OtherType), Item(Yellow, YellowType) };
        new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, new([], [], [], [], rows));
        PlayerSpellSnapshotEntry[] spells = legacy
            ? [new(FirstSpell, 7, 1234, 2, 3, 1), new(6001, 4, 1111, 2, 3, 2)] : [new(6001, 4, 1111, 2, 3, 2)];
        OriginalSpells = PlayerSpellSnapshotLoader.ToLegacyBytes(spells); OriginalSpells[^1] = 0xA5;
        File.WriteAllBytes(SpellsPath, OriginalSpells);
        if (legacyPassiveInput.Length != 0) PrepareLegacyPassiveSavedInput(legacyPassiveInput);
        var fields = Enumerable.Repeat("0", 19).ToArray(); fields[0] = "1"; fields[1] = "1000";
        fields[16] = extraSlots.ToString(CultureInfo.InvariantCulture); fields[^1] = "";
        var beast = BeastParser.ParseRow("owned-rune-gameplay-beast", 1, string.Join("@@", fields));
        priorBeasts = LegacyNativeBeastsAtrribute.Attributes; LegacyNativeBeastsAtrribute.Attributes = [];
        LegacyNativeBeastsCatalogStartup.AddRows([beast]);
        ItemType Metadata(uint id, uint encoded, uint requiredClass = 6000) => OwnedEquipmentBehaviorFixture.Item(id, "OwnedRuneGameplay",
            new Dictionary<int, string> { [ItemType.DurabilityFieldIndex] = "10", [ItemType.RequiredClassFieldIndex] = requiredClass.ToString(CultureInfo.InvariantCulture),
                [ItemType.RuneSpellIdFieldIndex] = encoded.ToString(CultureInfo.InvariantCulture) });
        var effects = MagicEffectParser.Parse((classification == "missing" ? "" : "[1416000]\r\nIsRuneSkill=" + classification + "\r\n") +
            "[1547000]\r\nIsRuneSkill=1\r\n", "owned-MagicEffect.ini");
        Catalog = NativeCallerWorldDataFixture.Create(new ItemType[] { Metadata(firstType, passiveFirst ? PassiveType : 1416016, passiveFirst ? 0u : 6000u), Metadata(SharedType, 1416017),
            Metadata(OtherType, 1547000), Metadata(YellowType, YellowType, requiredClass: 0) }, [], beastAttributes: new BeastAttribute[] { beast }, magicEffects: effects,
            magic: Enumerable.Range(0, 18).Select(level => Magic(checked((byte)level))).ToArray());
        Data.Bootstrap(); Runtime = Open();
        Handler = new WorldServerFrameHandler(Data.Loader, null, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
            Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
        await LoginAsync(); Registry = File.ReadAllBytes(Data.State); AssertConverged();
        Assert.Same(Native, Native.Beasts!.Owner); Assert.True(Native.Beasts.Activated); Assert.Equal((byte)1, Native.Beasts.Level);
        Assert.Equal((byte)(5 + extraSlots + CountFlags(flags)), Native.Beasts.YellowRuneSlots());
    }

    internal static PlayerItemSnapshotItem Item(uint uid, uint type) => new()
    { Uid = uid, ItemId = type, Position = 212, Durability = 10, MaximumDurability = 10, StackSize = 1, Color = 5 };
    private static int CountFlags(uint flags) => Enumerable.Range(0, 5).Count(bit => (flags & (1U << bit)) != 0);
    internal static MsgItemUsagePacket Equip(uint uid = First, ushort slot = 102) => new(uid, slot, MsgItemUsageAction.EquipRune, 112233, 44, 55, 66);
    internal static MsgItemUsagePacket Unequip(uint uid = First) => new(uid, 102, MsgItemUsageAction.UnequipRune, 112244, 77, 88, 99);
    internal Task DispatchAsync(MsgItemUsagePacket request) => SendAsync(request.ToFrameBytes());
    internal Task CastAsync() => SendAsync(new MsgAttackPacket(new MsgAttackQuery(Uid, Uid,
        checked((ushort)Current.Character.X), checked((ushort)Current.Character.Y), MsgAttackType.Magic,
        0, 0, 0, MsgAttackEffect.None, 0, SpellId: FirstSpell)).ToFrameBytes());

    private static MagicType Magic(byte level)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = (1_416_000 + level).ToString(CultureInfo.InvariantCulture); fields[1] = "14160";
        fields[2] = "6"; fields[3] = "OwnedIronShield"; fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = "10"; fields[10] = "100"; fields[13] = "30"; fields[14] = "1"; fields[15] = "18";
        fields[18] = "100000"; fields[20] = "1"; fields[29] = "5"; fields[33] = "500";
        fields[44] = "100"; fields[47] = "1000";
        return new MagicType(1_416_000 + level, FirstSpell, fields[3], fields);
    }

    private async Task LoginAsync()
    {
        var cancellation = new CancellationTokenSource(); lifetime = cancellation;
        var native = new LegacyNativeGameClientRuntime();
        connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
        {
            token.ThrowIfCancellationRequested(); var bytes = memory.ToArray();
            try { BeforeSend?.Invoke(bytes); } catch (Exception error) { observationFailure ??= error; throw; }
            Sent.Add(bytes); return ValueTask.CompletedTask;
        }, _ => { cancellation.Cancel(); return ValueTask.CompletedTask; }, native);
        await SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes());
        Assert.Equal(Uid, Current.Character.Uid); Assert.Same(native, Current.NativeGameClient);
        Assert.NotNull(native.OrdinarySpellBinding); Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, Current));
    }
    internal async Task SendAsync(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        await Handler.HandleFrameAsync(connection!, frame, lifetime!.Token);
        if (observationFailure is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    internal LegacyOwnedAdmissionRuntime Open()
    {
        bool success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason);
        if (runtime is not null) runtimes.Add(runtime);
        Assert.True(success, reason); return Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime);
    }
    internal async Task CloseAsync()
    {
        BeforeSend = null; var errors = new List<Exception>();
        if (connection is not null)
        {
            try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
            try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
        }
        connection = null;
        foreach (var runtime in runtimes.AsEnumerable().Reverse()) try { await runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        runtimes.Clear(); lifetime?.Dispose(); lifetime = null;
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }
    internal async Task DisposeAsync()
    {
        try { await CloseAsync(); }
        finally
        {
            if (priorBeasts is not null) { LegacyNativeBeastsAtrribute.Attributes = priorBeasts; priorBeasts = null; }
            Log.Dispose();
        }
    }
}
