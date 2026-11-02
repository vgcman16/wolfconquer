using System.Buffers;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicFuseHandlerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-relic-fuse-handler-" + Guid.NewGuid());
    private readonly string _usersRoot;
    private readonly string _itemsRoot;

    public LegacyRelicFuseHandlerTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
    }

    // This historical fixture has handoff/file state but no registered native
    // owner. Valid accepted behavior is covered by LegacyOwnedRelicFuseTests.
    [Theory]
    [InlineData("compose")]
    [InlineData("return")]
    [InlineData("missing-return")]
    public async Task DetachedFixtureCannotAllocateOrPublishAnOwnedFuseMutation(string operation)
    {
        const uint first = 6_140_001, second = first + 1, third = first + 2;
        WriteSnapshot();
        WriteItems(operation == "compose"
            ? new[] { PlayerItemSnapshotLoaderTests.DbItem(first, 4_100_001, position: 0),
                PlayerItemSnapshotLoaderTests.DbItem(second, 4_100_002, position: 0) }
            : new[] { PlayerItemSnapshotLoaderTests.DbItem(first, 4_100_001, position: 213),
                PlayerItemSnapshotLoaderTests.DbItem(second, 4_100_002, position: 214),
                PlayerItemSnapshotLoaderTests.DbItem(third, 4_100_001, position: 215) });
        using var logBuffer = new StringWriter();
        using var log = TextWriter.Synchronized(logBuffer);
        var rows = Enumerable.Range(1, 5).Select(index => LegacyFullAccountItemMutationFixture.ItemType(
            (uint)(4_100_000 + index), "DetachedFuse" + index, durability: 10_000)).ToArray();
        var caps = Enumerable.Range(1, 13).Select(type => XuanbaoParser.ParseComposeAttributeLimitRow(
            "fixture://detached-fuse", type, FormattableString.Invariant($"{type}@@0@@4@@400@@"))).ToArray();
        var world = LegacyItemWorldDataFixture.Create(rows, [], xuanbaoComposeAttributeLimits: caps);
        var (handler, connection, sent) = await EnterWorldAsync(log, world);
        var errors = new List<Exception>();
        try
        {
            var before = GetSession(handler, connection);
            var saved = File.ReadAllBytes(Path.Combine(_itemsRoot, "1293600.bin"));
            var visible = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
            int count = sent.Count;
            var request = new LegacyRelicFusePacket(operation == "compose" ? 0U : 1U,
                operation == "missing-return" ? 999U : first, second, third);
            await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));
            Assert.Equal(count, sent.Count); Assert.Same(before, GetSession(handler, connection));
            Assert.Equal(visible, PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems));
            Assert.Equal(saved, File.ReadAllBytes(Path.Combine(_itemsRoot, "1293600.bin")));
        }
        catch (Exception error) { errors.Add(error); }
        try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
        try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)> EnterWorldAsync(
        TextWriter log,
        WorldDataCatalog? worldData = null)
    {
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = worldData is null
            ? new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, log)
            : new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private void WriteItems(params PlayerItemSnapshotLoaderTests.TestDbItem[] items)
    {
        File.WriteAllBytes(
            Path.Combine(_itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(items, []));
    }

    private void WriteSnapshot()
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, "1293600.ini"),
            """
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=123
            BoundConquerPoints=0
            Money=50000
            PkPoints=0
            Online=7

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent) =>
        new(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        { sent.Add(frame.ToArray()); return ValueTask.CompletedTask; });

    private static WorldSession GetSession(WorldServerFrameHandler handler, ConquerTcpConnection connection)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var sessions = Assert.IsType<WorldSessionStore>(field.GetValue(handler));
        Assert.True(sessions.TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
