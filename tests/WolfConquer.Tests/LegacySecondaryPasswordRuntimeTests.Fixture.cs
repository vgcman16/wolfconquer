using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacySecondaryPasswordRuntimeTests : IDisposable
{
    private static readonly DateTimeOffset TestNow = new(2026, 10, 22, 12, 30, 0, TimeSpan.Zero);
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-secondary-runtime-" + Guid.NewGuid().ToString("N"));
    private readonly List<Player> players = [];
    private readonly NativeLoginBeastMetadataScope beastMetadata;

    public LegacySecondaryPasswordRuntimeTests()
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
        // Native LoadCharacter773 requires real positive contiguous Beast metadata,
        // even when the saved TotalPoints value is zero. This is fixture-owned input.
        beastMetadata = new NativeLoginBeastMetadataScope(1000);
    }

    public void Dispose()
    {
        try
        {
            foreach (var player in players) player.Lifetime.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        finally
        {
            beastMetadata.Dispose(); // Restore the exact prior cache even if file cleanup fails.
        }
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "PlayersItems");
    private string UserPath(uint uid) => Path.Combine(UsersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    private string ItemPath(uint uid) => Path.Combine(ItemsRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin");

    private Owner NewOwner(DateTimeOffset? now = null, WorldDataCatalog? worldData = null,
        LegacyLocalServerIdentity? identity = null)
    {
        var log = new StringWriter();
        var clock = new FixedTime(now ?? TestNow);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(UsersRoot, ItemsRoot),
            handoffService: null, arenaStatistics: null, teamArenaStatistics: null, worldData: worldData,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled with { TimeProvider = clock, LocalServerIdentity = identity },
            log: log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return new Owner(handler, Assert.IsType<WorldSessionStore>(field.GetValue(handler)), log, clock);
    }

    private void Seed(uint uid, string securityPass, uint itemUid = 10, long money = 1_000, long cp = 50)
    {
        File.WriteAllText(UserPath(uid), $$"""
            [Character]
            UID={{uid}}
            Name=PinTrader{{uid}}
            Body=1008
            Face=154
            Haire=27
            Class=10
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=100
            Experience=0
            Map=1002
            X=430
            Y=380
            PMap=1002
            PMapX=430
            PMapY=380
            Strength=10
            Agility=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            MinHitPoints=1000
            MinMana=100
            Money={{money}}
            ConquerPoints={{cp}}
            BoundConquerPoints=333
            BankCps=444
            PkPoints=0
            Online=1
            SecurityPass={{securityPass}}

            """);
        // Validate the fixture's complete schema before any security failure case.
        Assert.Equal(uid, CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid))).Uid);
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid,
            new PlayerItemSnapshot([new PlayerItemSnapshotItem { Uid = itemUid, ItemId = 1_088_001, StackSize = 1 }], [], [], [], []));
    }

    private Player CreateConnection(uint uid, LegacyNativeGameClientRuntime? nativeClient = null, Action<byte[]>? beforeSend = null)
    {
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, _) =>
            {
                var copy = bytes.ToArray();
                beforeSend?.Invoke(copy);
                sent.Enqueue(copy);
                return ValueTask.CompletedTask;
            },
            nativeGameClient: nativeClient ?? new LegacyNativeGameClientRuntime());
        var player = new Player(uid, connection, new CancellationTokenSource(), sent);
        players.Add(player);
        return player;
    }

    private async Task<Player> Login(Owner owner, uint uid, LegacyNativeGameClientRuntime? nativeClient = null,
        Action<byte[]>? beforeSend = null)
    {
        var player = CreateConnection(uid, nativeClient, beforeSend);
        await owner.Handler.HandleFrameAsync(player.Connection,
            ReadFrame(new LegacyMsgLoginGamePacket(uid).ToFrameBytes()), player.Lifetime.Token);
        Assert.True(player.Sent.Any(IsAnswerOk), $"Test world admission failed. Handler log:{Environment.NewLine}{owner.Log}");
        Assert.Equal(uid, Current(owner, player).Character.Uid);
        Assert.Same(player.NativeClient, Current(owner, player).NativeGameClient);
        Assert.Equal(uid, player.NativeClient.Player.UID);
        return player;
    }

    private async Task<LegacySecondaryPasswordPacket[]> Password(Owner owner, Player player, uint action, uint oldPin = 0, uint newPin = 0)
    {
        var first = player.Sent.Count;
        await owner.Handler.HandleFrameAsync(player.Connection,
            ReadFrame(new LegacySecondaryPasswordPacket(action, oldPin, newPin).ToFrameBytes()), player.Lifetime.Token);
        return PasswordReplies(player, first);
    }

    private static LegacySecondaryPasswordPacket[] PasswordReplies(Player player, int first = 0) =>
        player.Sent.Skip(first).Select(ReadFrame).Where(frame => frame.Header.Opcode == LegacySecondaryPasswordPacket.Opcode)
            .Select(frame => { Assert.True(LegacySecondaryPasswordPacket.TryParse(frame, out var packet)); return packet; }).ToArray();

    private static void AssertReply(LegacySecondaryPasswordPacket[] replies, uint action, uint oldValue, uint newValue = 0) =>
        Assert.Equal(new LegacySecondaryPasswordPacket(action, oldValue, newValue), Assert.Single(replies));

    private static void AssertNoSuccess(IEnumerable<LegacySecondaryPasswordPacket> replies) =>
        Assert.DoesNotContain(replies, packet => packet == new LegacySecondaryPasswordPacket(5, 257, 0));

    private async Task Trade(Owner owner, Player player, LegacyTradePacket packet) => await owner.Handler.HandleFrameAsync(
        player.Connection, ReadFrame(packet.ToNativeFrameBytes()), player.Lifetime.Token);

    private async Task OpenTrade(Owner owner, Player left, Player right)
    {
        await Trade(owner, left, new(0) { UID = right.Uid });
        await Trade(owner, right, new(1) { UID = left.Uid });
        Assert.True(Current(owner, left).LegacyTradeActive);
        Assert.True(Current(owner, right).LegacyTradeActive);
    }

    private async Task AssertTradeDenied(Owner owner, Player left, Player right)
    {
        var before = File.ReadAllBytes(ItemPath(left.Uid));
        var leftFrames = left.Sent.Count;
        var rightFrames = right.Sent.Count;
        await Trade(owner, left, new(0) { UID = right.Uid });
        await Trade(owner, right, new(1) { UID = left.Uid });
        await Trade(owner, left, new(6) { ItemUID = 10 });
        await Trade(owner, left, new(7) { Money = 300 });
        Assert.False(Current(owner, left).LegacyTradeActive);
        Assert.False(Current(owner, right).LegacyTradeActive);
        Assert.Equal(1_000, Current(owner, left).Character.Money);
        Assert.Equal(before, File.ReadAllBytes(ItemPath(left.Uid)));
        Assert.DoesNotContain(left.Sent.Skip(leftFrames).Concat(right.Sent.Skip(rightFrames)), bytes =>
            LegacyTradePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Action == 3);
    }

    private static WorldSession Current(Owner owner, Player player)
    {
        Assert.True(owner.Sessions.TryGet(player.Connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private (uint Pin, uint Reset, long Ticks) ReadCredential(uint uid)
    {
        var raw = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid))).Attributes["SecurityPass"];
        var fields = raw.TrimEnd(',').Split(',');
        Assert.Equal(3, fields.Length);
        return (uint.Parse(fields[0], CultureInfo.InvariantCulture), uint.Parse(fields[1], CultureInfo.InvariantCulture),
            long.Parse(fields[2], CultureInfo.InvariantCulture));
    }

    private static bool IsAnswerOk(byte[] bytes) => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Content.Count > 3 && packet.Content[3] == "ANSWER_OK";

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record Owner(WorldServerFrameHandler Handler, WorldSessionStore Sessions, StringWriter Log, FixedTime Clock);
    private sealed record Player(uint Uid, ConquerTcpConnection Connection, CancellationTokenSource Lifetime, ConcurrentQueue<byte[]> Sent)
    {
        internal LegacyNativeGameClientRuntime NativeClient => Connection.NativeGameClient!;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
