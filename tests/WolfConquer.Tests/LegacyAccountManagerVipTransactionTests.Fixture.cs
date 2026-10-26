using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Registered managed D13 entrypoints, actual Store/recovery and owned files;
// not native login, client rendering, SQL or hardware power-loss evidence.
public sealed partial class LegacyAccountManagerVipTransactionTests
{
    private const uint NpcId = 45_632;
    private const uint Uid = 1_299_651;
    private static readonly DateTime FutureExpiry = new(2099, 11, 1, 12, 34, 56, DateTimeKind.Local);

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-vip-transaction-" + Guid.NewGuid().ToString("N"));
        internal readonly ConcurrentQueue<byte[]> Sent = new();
        internal readonly StringWriter Log = new();
        internal readonly WorldSessionStore Sessions = new();
        internal readonly LegacyTradeStateStore Trades = new();
        internal readonly WorldOperationBarrier Operations = new();
        internal readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        internal readonly ControlledClock Clock = new();
        internal readonly ConquerTcpConnection Connection;
        internal readonly LegacyTradeRecoveryState Recovery;
        internal readonly WorldDataCatalog WorldData;
        private readonly Dictionary<string, byte[]> originalFiles;
        internal Action<byte[]>? BeforeSend;
        internal WorldSession Initial { get; }
        internal string UsersRoot => Path.Combine(root, "Users");
        internal string ItemsRoot => Path.Combine(root, "PlayersItems");
        internal string UserPath => Path.Combine(UsersRoot, Uid.ToString(CultureInfo.InvariantCulture) + ".ini");

        internal Fixture(ulong points = 150, DateTime? expiry = null, bool withItemsAuthority = true,
            LegacyNativeGameClientRuntime? native = null)
        {
            Directory.CreateDirectory(UsersRoot);
            Directory.CreateDirectory(ItemsRoot);
            var text = string.Join("\r\n", new[]
            {
                "; Preserve this complete image and its CRLF/BOM.", "[Character]", $"UID={Uid}",
                "Name=VipTransaction", "Body=1008", "Face=154", "Haire=27", "Class=10", "FirstClass=0",
                "SecoundeClass=0", "Reborn=0", "Level=100", "Experience=0", "Map=1002", "X=430", "Y=380",
                "PMap=1002", "PMapX=430", "PMapY=380", "Strength=10", "Agility=10", "Vitaliti=10",
                "Spirit=10", "Atributes=0", "MinHitPoints=1000", "MinMana=100", "Money=777",
                "ConquerPoints=222", "BoundConquerPoints=333", "BankCps=444", "PkPoints=0", "Online=1",
                "SecurityPass=0,0,0", "WHMoney=555", $"DonatePoints={points}", "VipLevel=0",
                $"VipTime={(expiry ?? FutureExpiry).ToBinary().ToString(CultureInfo.InvariantCulture)}",
                "KeepBytes=outside-the-three-vip-fields", "", "[FixtureOtherSection]", "Sentinel=keep-this-too", ""
            });
            File.WriteAllText(UserPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var items = new PlayerItemSnapshot(
                [new PlayerItemSnapshotItem { Uid = 801_299_651, ItemId = 1_000_000, StackSize = 1, Color = 5 }], [], [], [], []);
            new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(Uid, items);
            var character = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath), Path.GetFileName(UserPath));
            Initial = new WorldSession(Uid, "vip-fixture", character, items,
                PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: UsersRoot, ActiveNpcId: NpcId);
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, _) =>
            {
                var copy = bytes.ToArray();
                BeforeSend?.Invoke(copy);
                Sent.Enqueue(copy);
                return ValueTask.CompletedTask;
            }, nativeGameClient: native);
            Sessions.Set(Connection, Initial);
            Recovery = new LegacyTradeRecoveryState(UsersRoot, withItemsAuthority ? ItemsRoot : null);
            Assert.True(Recovery.TryEnsureInitialized(out var reason), reason);
            WorldData = LegacyItemWorldDataFixture.Create([], [],
                npcs: [new Npc((int)NpcId, 0, 0, 1002, 430, 378, null)]);
            originalFiles = Files();
        }

        internal WorldSession Current => Sessions.TryGet(Connection, out var value)
            ? Assert.IsType<WorldSession>(value) : throw new InvalidOperationException("Owned session is absent.");

        internal WorldPacketHandlerContext Context => new(Connection, Log, Session: Current, Sessions: Sessions,
            WorldData: WorldData, PlayerItemsRoot: ItemsRoot, TimeProvider: Clock,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
        { Trades = Trades, TradeOperations = Operations, TradeRecovery = Recovery };

        internal Task Purchase(byte option, WorldPacketHandlerContext? context = null, CancellationToken cancellation = default) =>
            Registry.HandleAsync(context ?? Context, ReadFrame(new LegacyNpcDialogRequestPacket(
                NpcId, 0, option, 1, 0, "").ToFrameBytes()), cancellation).AsTask();

        internal Dictionary<string, byte[]> Files() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);

        internal void AssertFiles(Dictionary<string, byte[]> expected)
        {
            var actual = Files();
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
            foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
        }

        internal void AssertUnchanged(WorldSession before, Dictionary<string, byte[]> files, int first)
        {
            Assert.Same(before, Current);
            AssertFiles(files);
            Assert.DoesNotContain(Sent.Skip(first), IsSuccess);
        }

        internal void AssertCommitted(WorldSession before, ulong points, ulong level, DateTime expiry, byte[] beforeBytes)
        {
            var current = Current;
            Assert.Equal(points.ToString(CultureInfo.InvariantCulture), current.Character.Attributes["DonatePoints"]);
            Assert.Equal(level.ToString(CultureInfo.InvariantCulture), current.Character.Attributes["VipLevel"]);
            Assert.Equal(expiry.ToBinary().ToString(CultureInfo.InvariantCulture), current.Character.Attributes["VipTime"]);
            Assert.Equal(before.Character.Money, current.Character.Money);
            Assert.Equal(before.Character.ConquerPoints, current.Character.ConquerPoints);
            Assert.Equal(before.Character.BoundConquerPoints, current.Character.BoundConquerPoints);
            Assert.Equal(before.Character.BankConquerPoints, current.Character.BankConquerPoints);
            Assert.Same(before.PlayerItems, current.PlayerItems);
            AssertDurable(before, points, level, expiry, beforeBytes);
        }

        internal void AssertDurable(WorldSession before, ulong points, ulong level, DateTime expiry, byte[] beforeBytes)
        {
            var expectedFiles = new Dictionary<string, byte[]>(originalFiles, StringComparer.Ordinal)
            {
                [Path.GetRelativePath(root, UserPath)] = ExpectedImage(before, beforeBytes, points, level, expiry)
            };
            AssertFiles(expectedFiles);
        }

        internal static byte[] ExpectedImage(WorldSession before, byte[] bytes, ulong points, ulong level, DateTime expiry)
        {
            var text = Encoding.UTF8.GetString(bytes);
            foreach (var (key, value) in new[]
            {
                ("DonatePoints", points.ToString(CultureInfo.InvariantCulture)),
                ("VipLevel", level.ToString(CultureInfo.InvariantCulture)),
                ("VipTime", expiry.ToBinary().ToString(CultureInfo.InvariantCulture))
            }) text = text.Replace(key + "=" + before.Character.Attributes[key] + "\r\n", key + "=" + value + "\r\n", StringComparison.Ordinal);
            return Encoding.UTF8.GetBytes(text);
        }

        public void Dispose()
        {
            Log.Dispose();
            var target = Path.GetFullPath(root);
            var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            var name = Path.GetFileName(target);
            if (!string.Equals(Path.GetDirectoryName(target), parent,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                !name.StartsWith("wolf-vip-transaction-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(name["wolf-vip-transaction-".Length..], "N", out _))
                throw new InvalidOperationException("Owned VIP fixture cleanup target is invalid.");
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
    }

    private sealed class ControlledClock : TimeProvider
    {
        internal Action? BeforeRead;
        internal static readonly DateTimeOffset Utc = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        internal static readonly DateTime LocalNow = DateTime.SpecifyKind(Utc.DateTime, DateTimeKind.Local);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow()
        {
            BeforeRead?.Invoke();
            return Utc;
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool IsSuccess(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.VIPLevel) ||
        LegacyVipStatusPacket.TryParse(ReadFrame(bytes), out _) ||
        LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) && reply.InteractType == LegacyNpcReplyInteractType.MessageBox &&
            reply.Text.StartsWith("You have received VIP", StringComparison.Ordinal);

    private static bool IsVipUpdate(byte[] bytes) => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
        update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.VIPLevel);
}
