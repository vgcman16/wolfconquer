using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyEquipmentDurabilityTests
{
    private static async Task Run(Func<EquipmentFixture, Task> test, bool equipped = false,
        int inventoryFillers = 1, bool twoHandReplacement = false)
    {
        EquipmentFixture? fixture = null;
        Exception? primary = null;
        try
        {
            fixture = new EquipmentFixture();
            fixture.Initialize(equipped, inventoryFillers, twoHandReplacement);
            await fixture.Login();
            await test(fixture);
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (fixture is not null)
            try { fixture.RetainEvidence = primary is not null; await fixture.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed class EquipmentFixture : IAsyncDisposable
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid, Account = Uid;
        internal const uint GearUid = 1_000_092, RightUid = 1_000_093, LeftUid = 1_000_094;
        internal const uint Necklace = 120003, TwoHand = 560003, Right = 410003, Shield = 900003, Filler = 1_000_000;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        private readonly NativeLoginBeastMetadataScope beasts = new(1000);
        internal readonly LegacyNativeGameClientRuntime Native = new();
        internal readonly ConcurrentQueue<byte[]> Sent = new();
        internal readonly StringWriter Log = new();
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal WorldDataCatalog Catalog = null!;
        internal ConquerTcpConnection Connection = null!;
        internal Action<byte[]>? BeforeSend;
        internal bool RetainEvidence, Disconnected;
        internal string CharacterPath => Path.Combine(Data.Users, Account + ".ini");
        internal string ItemsPath => Path.Combine(Data.Items, Uid + ".bin");
        internal WorldSessionStore Store => Actual<WorldSessionStore>("_sessions");
        internal LegacyTradeRecoveryState Recovery => Actual<LegacyTradeRecoveryState>("_tradeRecovery");
        internal WorldSession? CurrentOrNull => Store.TryGet(Connection, out var session) ? session : null;
        internal WorldSession Current => Assert.IsType<WorldSession>(CurrentOrNull);
        private T Actual<T>(string name) where T : class => Assert.IsType<T>(typeof(WorldServerFrameHandler)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));

        internal void Initialize(bool equipped, int fillers, bool twoHand)
        {
            // Existing v7 ordinary admission requires account/sidecar FileIds to
            // coincide. Do not fabricate a distinct-account bound owner here.
            Assert.Equal(Data.Users, Path.GetDirectoryName(Path.GetFullPath(CharacterPath)));
            Assert.True(File.Exists(CharacterPath));
            var inventory = Enumerable.Range(0, fillers).Select(index => Row(1_100_000u + (uint)index, Filler, 0)).ToList();
            var equipment = new List<PlayerItemSnapshotItem>();
            if (twoHand)
            {
                inventory.Add(Row(GearUid, TwoHand, 0));
                equipment.Add(Row(RightUid, Right, 4).WithBound(1).WithLocked(1));
                equipment.Add(Row(LeftUid, Shield, 5).WithBound(1).WithLocked(1));
            }
            else if (equipped) equipment.Add(Row(GearUid, Necklace, 2));
            else inventory.Add(Row(GearUid, Necklace, 0));
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(new(inventory.ToArray(), [], [], equipment.ToArray(), []));
            // Reserved DBItem bytes14..15 are not decoded/re-encoded by the DTO.
            bytes[RecordOffset(bytes, GearUid) + 14] = 0xA5;
            File.WriteAllBytes(ItemsPath, bytes);
            new LegacyMailboxStore(Data.Root).Save(Uid);
            Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime);
            Catalog = LegacyItemWorldDataFixture.Create(new[] { Item(Necklace), Item(TwoHand), Item(Right), Item(Shield), Item(Filler) }, []);
            Handler = new WorldServerFrameHandler(Data.Loader, null, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
                Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var copy = frame.ToArray();
                    BeforeSend?.Invoke(copy);
                    Sent.Enqueue(copy);
                    return ValueTask.CompletedTask;
                }, _ => { Disconnected = true; return ValueTask.CompletedTask; }, Native);
        }

        internal async Task Login()
        {
            await Handler.HandleFrameAsync(Connection, Frame(new LegacyMsgLoginGamePacket(Account).ToFrameBytes()));
            Assert.Equal(Account, Current.AccountId);
            Assert.Equal(Uid, Current.Character.Uid);
            Assert.Same(Native, Current.NativeGameClient);
            Assert.NotNull(Native.OrdinarySpellBinding);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(Connection, Current));
            Assert.True(Recovery.IsReadyFor(Data.Users, Data.Items));
            Assert.Equal((byte)0xA5, File.ReadAllBytes(ItemsPath)[RecordOffset(File.ReadAllBytes(ItemsPath), GearUid) + 14]);
            Sent.Clear();
        }

        internal Task Dispatch(bool unequip = false, uint uid = GearUid, ushort position = 2) => Handler.HandleFrameAsync(Connection,
            Frame(Request(unequip, uid, position))).AsTask();
        internal static byte[] Request(bool unequip, uint uid = GearUid, ushort position = 2) =>
            new MsgItemUsagePacket(uid, position, unequip ? MsgItemUsageAction.Unequip : MsgItemUsageAction.Equip, 0, 0, 0, 0).ToFrameBytes();
        internal PlayerItemSnapshot ReadItems() => new PlayerItemSnapshotLoader(Data.Items).LoadByCharacterUid(Uid);
        internal byte[] ExpectedPositions(params (uint Uid, ushort Position)[] moves)
        {
            var bytes = File.ReadAllBytes(ItemsPath);
            foreach (var move in moves) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(RecordOffset(bytes, move.Uid) + 12, 2), move.Position);
            return bytes;
        }
        internal static int RecordOffset(byte[] bytes, uint uid)
        {
            var count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            for (var index = 0; index < count; index++)
            {
                var offset = 4 + index * PlayerItemSnapshotLoader.LegacyDbItemSize;
                Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 136, 4)));
                if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) == uid) return offset;
            }
            throw new InvalidOperationException("Owned flat fixture item was not found.");
        }
        private static PlayerItemSnapshotItem Row(uint uid, uint id, ushort position) => new()
        { Uid = uid, ItemId = id, Position = position, StackSize = 1, Durability = 321, MaximumDurability = 321, Color = 5 };
        private static ItemType Item(uint id)
        {
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "OwnedEquip" + fields[0];
            fields[ItemType.DurabilityFieldIndex] = "321"; fields[ItemType.StackSizeFieldIndex] = "1";
            return new(checked((int)id), fields[1], fields);
        }
        public async ValueTask DisposeAsync()
        {
            BeforeSend = null;
            var errors = new List<Exception>();
            try { if (Connection is not null) { await Connection.DisconnectAsync(); await Connection.CompleteTransportCloseAsync(); } }
            catch (Exception error) { errors.Add(error); }
            try { if (Runtime is not null) await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            try { beasts.Dispose(); Log.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 0 && !RetainEvidence) Data.Dispose();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }
}
