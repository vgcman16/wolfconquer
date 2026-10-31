using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    private const uint Uid = 2_399_700, Peer = Uid + 1, Right = 1_038_001, Left = Right + 1,
        AlternateRight = Right + 2, AlternateLeft = Right + 3, BagItem = Right + 4;
    private const uint Pride = 626_003, BetterPride = 626_009, Dragon = 626_439;
    private const int Fan = 194;

    private static Task Run(Func<WindFixture, Task> body, string mode = "one", long job = 16_051,
        bool fineRain = false, string? flags = null, string? alias = null, bool peer = false)
    {
        var clock = new WindClock();
        var character = OwnedEquipmentBehaviorFixture.Character(Uid, "OwnedWind", job,
            strength: 1000, agility: 1000, mapId: 1002, x: 430, y: 380, minHitPoints: 1);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        if (fineRain) attributes["StatusFlags"] = "0/0/0/0/0/0/1048576";
        if (flags is not null) attributes["StatusFlags"] = flags;
        if (alias is not null) attributes["BitVector"] = alias;
        character = character with { Attributes = attributes };
        var equipment = new List<PlayerItemSnapshotItem> { Item(Right, Pride, 4).WithDurability(mode == "broken-right" ? (ushort)0 : (ushort)10) };
        var inventory = new List<PlayerItemSnapshotItem> { Item(BagItem, 4_020_517, 0) };
        if (mode is "pair" or "broken" or "broken-right" or "alternate" or "fallback")
            equipment.Add(Item(Left, BetterPride, 5).WithDurability(mode == "broken" ? (ushort)0 : (ushort)10));
        else inventory.Add(Item(Left, BetterPride, 0));
        if (mode is "alternate" or "fallback")
        {
            equipment.Add(Item(AlternateRight, Dragon, 54));
            if (mode == "alternate") equipment.Add(Item(AlternateLeft, Pride, 55));
        }
        var items = new PlayerItemSnapshot(inventory, [], [], equipment, []);
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10000));
        OwnedEquipmentBehaviorInput[] peers = peer
            ? [new(OwnedEquipmentBehaviorFixture.Character(Peer, "WindViewer", mapId: 1002, x: 431, y: 380,
                minHitPoints: 1), PlayerItemSnapshot.Empty)] : [];
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(new(character, items), Catalog(), options,
            async owner =>
            {
                var fixture = new WindFixture(owner, clock);
                owner.BeforeSend = fixture.Observe;
                try { await body(fixture); } finally { owner.BeforeSend = null; }
            }, peers);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint id, ushort position) => new()
    { Uid = uid, ItemId = id, Position = position, Durability = 10, MaximumDurability = 10, StackSize = 0, Color = 5 };

    private static WorldDataCatalog Catalog()
    {
        // Actual retained DBServer/itemtype.txt rows, including source class,
        // level, numeric statistics, durability and raw non-stack field46.
        string[] raw = [
            "626003@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@9@@5@@0@@51@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
            "626009@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@14@@9@@0@@63@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@10@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
            "626439@@DragonFan@@16000@@12@@140@@0@@0@@36@@0@@0@@0@@200000@@0@@4293@@2970@@0@@63@@0@@0@@0@@7099@@7099@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@100@@0@@0@@0@@0@@10@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
        ];
        var rows = raw.Select(line => line.Split("@@", StringSplitOptions.None)).Select(fields =>
            new ItemType(int.Parse(fields[0], CultureInfo.InvariantCulture), fields[1], fields)).ToList();
        rows.Add(OwnedEquipmentBehaviorFixture.Item(4_020_517, "IronShield(+17)", new Dictionary<int, string>
        { [ItemType.RequiredClassFieldIndex] = "6000", [ItemType.RuneSpellIdFieldIndex] = "1416016", [ItemType.StackSizeFieldIndex] = "0" }));
        // Explicit BCL array is required by the actual native catalog adoption.
        return NativeCallerWorldDataFixture.Create(rows.ToArray());
    }

    private sealed class WindClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero).AddTicks(ticks);
        internal void Advance(TimeSpan elapsed) => ticks = checked(ticks + elapsed.Ticks);
    }

    private sealed class WindFixture(OwnedEquipmentBehaviorFixture owner, WindClock clock)
    {
        internal OwnedEquipmentBehaviorFixture Owner => owner;
        internal WindClock Clock => clock;
        internal WorldSession Current => owner.Current(Uid);
        internal WorldSessionStore Store => owner.Store;
        internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;
        internal LegacyNativeStatusFlagsBigVector32 Vector => Native.Player!.BitVector;
        internal LegacyNativeStatusFlagsBigVector32.Flag? Row => Vector.ArrayFlags.TryGetValue(Fan, out var row) ? row : null;
        internal List<byte[]> Sent => owner.ActorFrames;
        internal readonly List<(uint Recipient, byte[] Bytes)> Order = [];
        internal Action<uint, byte[]>? BeforeSend;
        internal void Observe(uint recipient, byte[] bytes)
        { BeforeSend?.Invoke(recipient, bytes); Order.Add((recipient, bytes)); }
        internal Task QueryAsync() => owner.SendAsync(Uid, LegacyRuneStoragePacket.Startup().ToFrameBytes());
        internal Task EquipAsync() => owner.DispatchAsync(new(Left, 5, MsgItemUsageAction.Equip, 1234, 0, 0, 0), Uid);
        internal Task UnequipAsync() => owner.DispatchAsync(new(Left, 5, MsgItemUsageAction.Unequip, 1235, 0, 0, 0), Uid);
        internal Task BagAsync() => owner.DispatchAsync(new(BagItem, 0, MsgItemUsageAction.Rune, 4567, 0, 0, 0), Uid);
        internal async Task CloseAsync()
        {
            await owner.Connection(Uid).DisconnectAsync();
            await owner.Connection(Uid).CompleteTransportCloseAsync();
            await owner.Runtime.DisposeAsync();
        }
    }
}
