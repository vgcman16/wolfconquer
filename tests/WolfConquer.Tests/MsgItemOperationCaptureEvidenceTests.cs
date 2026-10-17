using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgItemOperationCaptureEvidenceTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const int PayloadHexLimit = 256;
    private const string EvidencePrefix = "WIRE-EVIDENCE MsgItemOperationRaw";

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-msg-item-operation-capture-" + Guid.NewGuid());
    private readonly string _usersRoot;
    private readonly string _itemsRoot;
    private readonly string _spellsRoot;
    private readonly string _proficienciesRoot;
    private readonly string _characterSnapshotPath;

    public MsgItemOperationCaptureEvidenceTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _itemsRoot = Path.Combine(_root, "PlayersItems");
        _spellsRoot = Path.Combine(_root, "PlayersSpells");
        _proficienciesRoot = Path.Combine(_root, "PlayersProficiencies");
        _characterSnapshotPath = Path.Combine(_usersRoot, AccountId + ".ini");

        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
        Directory.CreateDirectory(_spellsRoot);
        Directory.CreateDirectory(_proficienciesRoot);
        File.WriteAllBytes(
            _characterSnapshotPath,
            Encoding.ASCII.GetBytes("[Character]\r\nUID=1293600\r\nMoney=123456\r\n"));
        File.WriteAllBytes(Path.Combine(_itemsRoot, CharacterUid + ".bin"), [0x49, 0x54, 0x45, 0x4D]);
        File.WriteAllBytes(Path.Combine(_spellsRoot, CharacterUid + ".bin"), [0x53, 0x50, 0x45, 0x4C, 0x4C]);
        File.WriteAllBytes(Path.Combine(_proficienciesRoot, CharacterUid + ".bin"), [0x50, 0x52, 0x4F, 0x46]);
    }

    [Fact]
    public void RegistryOwnsExactActiveClientOpcode()
    {
        Assert.Equal((ushort)2427, WorldPacketOpcodes.MsgItemOperation);
        Assert.True(PacketHandlers.TryGetHandler(WorldPacketOpcodes.MsgItemOperation, out var handler));
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task ParsedUnknownActionRemainsReadOnlyAndEmitsRawEvidence()
    {
        byte[] payload = [0x08, 0x01, 0x18, 0x00, 0x30, 0x63, 0x38, 0x01];
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(CreateFrame(payload)));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var sessionAfter));
        Assert.Same(session, sessionAfter);
        var evidence = Assert.Single(EvidenceLines(log.ToString()));
        Assert.Contains(
            "active 7899 CMsgItemPB action 99 is not modeled",
            evidence,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistryDispatchCapturesExactPayloadAndPreservesSessionAndPersistence()
    {
        byte[] payload = [0x08, 0x45, 0x10, 0x82, 0x01, 0x1A, 0x03, 0xA1, 0xB2, 0xC3];
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var persistenceCalls = 0;
        var persistenceArmed = false;
        var sessions = new WorldSessionStore((_, _, _) =>
        {
            persistenceCalls++;
            if (persistenceArmed)
            {
                throw new InvalidOperationException("Capture handler attempted session persistence.");
            }
        });
        var session = CreateSession();
        sessions.Set(connection, session);
        persistenceArmed = true;
        var persistenceCallsBefore = persistenceCalls;
        var filesBefore = SnapshotPersistenceFiles();

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: _spellsRoot,
                PlayerItemsRoot: _itemsRoot,
                PlayerProficienciesRoot: _proficienciesRoot),
            ReadFrame(CreateFrame(payload)));

        Assert.Empty(sent);
        Assert.Equal(persistenceCallsBefore, persistenceCalls);
        Assert.True(sessions.TryGet(connection, out var sessionAfter));
        Assert.Same(session, sessionAfter);
        Assert.Same(session.Character, sessionAfter!.Character);
        Assert.Same(session.PlayerItems, sessionAfter.PlayerItems);
        Assert.Same(session.PlayerSpells, sessionAfter.PlayerSpells);
        Assert.Same(session.PlayerProficiencies, sessionAfter.PlayerProficiencies);
        AssertPersistenceFilesUnchanged(filesBefore, SnapshotPersistenceFiles());

        var evidence = Assert.Single(EvidenceLines(log.ToString()));
        var expected =
            $"{EvidencePrefix} opcode=2427 " +
            "session=accountId=1293600 username=evidence-tester characterUid=1293600 " +
            "characterName=EvidenceTester map=1015 x=830 y=584 hp=61258 money=123456 cp=654321 " +
            $"payloadLength={payload.Length} payloadHexBytes={payload.Length} payloadHexTruncated=false " +
            $"payloadHex={Convert.ToHexString(payload)} fullPayloadSha256={Sha256(payload)} " +
            "PARITY-GAP category=unsupported-packet severity=warning gap=unsupported-packet " +
            "dedupe=unsupported-packet:unsupported-packet:opcode=2427:packet=MsgItemOperation " +
            "opcode=2427 packet=MsgItemOperation; active 7899 CMsgItemPB payload is malformed or " +
            "missing common required fields 1,6,7";
        Assert.Equal(expected, evidence);
        Assert.Single(
            log.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task OversizedPayloadCapsHexAt256BytesAndHashesAllBytes()
    {
        var payload = Enumerable.Range(0, 512)
            .Select(index => checked((byte)((index * 37 + 11) % 256)))
            .ToArray();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(CreateFrame(payload)));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var sessionAfter));
        Assert.Same(session, sessionAfter);

        var evidence = Assert.Single(EvidenceLines(log.ToString()));
        Assert.Contains(
            $"payloadLength={payload.Length} payloadHexBytes={PayloadHexLimit} payloadHexTruncated=true",
            evidence,
            StringComparison.Ordinal);
        Assert.Contains(
            $"payloadHex={Convert.ToHexString(payload.AsSpan(0, PayloadHexLimit))}",
            evidence,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"payloadHex={Convert.ToHexString(payload)}",
            evidence,
            StringComparison.Ordinal);
        Assert.Contains($"fullPayloadSha256={Sha256(payload)}", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"fullPayloadSha256={Sha256(payload.AsSpan(0, PayloadHexLimit))}",
            evidence,
            StringComparison.Ordinal);
        Assert.Contains(
            "PARITY-GAP category=unsupported-packet severity=warning gap=unsupported-packet",
            evidence,
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "evidence-tester",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "EvidenceTester",
                Body: 1003,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 123_456,
                MapId: 1015,
                X: 830,
                Y: 584,
                PreviousMapId: 1000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 100,
                Agility: 100,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 123_456,
                ConquerPoints: 654_321,
                BoundConquerPoints: 111,
                BankConquerPoints: 222,
                PkPoints: 0,
                Online: 7,
                SourceFile: _characterSnapshotPath,
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: _usersRoot);
    }

    private IReadOnlyDictionary<string, byte[]> SnapshotPersistenceFiles()
    {
        return Directory
            .EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(_root, path),
                File.ReadAllBytes,
                StringComparer.Ordinal);
    }

    private static void AssertPersistenceFilesUnchanged(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var path in expected.Keys)
        {
            Assert.Equal(expected[path], actual[path]);
        }
    }

    private static IReadOnlyList<string> EvidenceLines(string output)
    {
        return output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith(EvidencePrefix, StringComparison.Ordinal))
            .ToArray();
    }

    private static string Sha256(ReadOnlySpan<byte> payload)
    {
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static byte[] CreateFrame(ReadOnlySpan<byte> payload)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemOperation, payload.Length);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static ConquerTcpConnection CreateConnection(ICollection<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
