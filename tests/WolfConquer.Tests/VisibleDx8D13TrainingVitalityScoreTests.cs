using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class VisibleDx8D13TrainingVitalityScoreTests
{
    private const long SelfUid = 1_293_600;
    private const long ObserverUid = SelfUid + 1;
    private const string CharacterName = "ScoreStudy";
    private const string DefaultDragonPower =
        "1/0/10/123/0/11/124/0/12/125/0/13/126/0/";

    private static readonly Lazy<Task<WorldDataCatalog>> SeedWorldData = new(LoadSeedWorldDataAsync);

    [Fact]
    public void ExactClientProvenWireRoundTripsAtFixedOffsets()
    {
        const uint encodedAttribute = (13 * 10_000) + 300;
        var packet = new VisibleDx8D13TrainingVitalityScorePacket(
            (byte)LegacyChiPowerType.Turtle,
            encodedAttribute,
            CharacterName);

        var bytes = packet.ToFrameBytes();

        Assert.Equal(41, bytes.Length);
        Assert.Equal(41, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal(0x08E4, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.Equal((byte)LegacyChiPowerType.Turtle, bytes[4]);
        Assert.Equal(encodedAttribute, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(5, 4)));
        Assert.Equal(Encoding.UTF8.GetBytes(CharacterName), bytes.AsSpan(9, CharacterName.Length).ToArray());
        Assert.All(bytes[19..], value => Assert.Equal(0, value));

        Assert.True(VisibleDx8D13TrainingVitalityScorePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(byte.MaxValue)]
    public void PowerSelectorIsBoundedToClientValuesOneThroughFour(byte powerType)
    {
        var packet = new VisibleDx8D13TrainingVitalityScorePacket(powerType, 10_200, CharacterName);

        Assert.Throws<ArgumentOutOfRangeException>(() => packet.ToFrameBytes());

        var bytes = new VisibleDx8D13TrainingVitalityScorePacket(
                (byte)LegacyChiPowerType.Dragon,
                10_200,
                CharacterName)
            .ToFrameBytes();
        bytes[4] = powerType;
        Assert.False(VisibleDx8D13TrainingVitalityScorePacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public void ParserRejectsWrongOpcodeAndEveryNon41ByteFrame()
    {
        var exact = new VisibleDx8D13TrainingVitalityScorePacket(
                (byte)LegacyChiPowerType.Dragon,
                10_200,
                CharacterName)
            .ToFrameBytes();
        var wrongOpcode = exact.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(wrongOpcode.AsSpan(2, 2), 2275);

        Assert.False(VisibleDx8D13TrainingVitalityScorePacket.TryParse(ReadFrame(wrongOpcode), out _));
        Assert.False(
            VisibleDx8D13TrainingVitalityScorePacket.TryParse(
                ReadFrame(ResizeFrame(exact, 40)),
                out _));
        Assert.False(
            VisibleDx8D13TrainingVitalityScorePacket.TryParse(
                ReadFrame(ResizeFrame(exact, 42)),
                out _));
    }

    [Fact]
    public void Utf8NameIsTruncatedOnACodePointBoundaryAndZeroTerminatedWithinName32()
    {
        var sourceName = string.Concat(Enumerable.Repeat("\u754C", 11));
        var expectedName = string.Concat(Enumerable.Repeat("\u754C", 10));

        var bytes = new VisibleDx8D13TrainingVitalityScorePacket(
                (byte)LegacyChiPowerType.Phoenix,
                20_200,
                sourceName)
            .ToFrameBytes();
        var name32 = bytes.AsSpan(9, 32);

        Assert.Equal(30, name32.IndexOf((byte)0));
        Assert.All(name32[30..].ToArray(), value => Assert.Equal(0, value));
        Assert.True(VisibleDx8D13TrainingVitalityScorePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(expectedName, parsed.CharacterName);
        Assert.DoesNotContain('\uFFFD', parsed.CharacterName);
    }

    [Fact]
    public void StudyMutationExposesOneEncodedScoreForEachUnlockedRerolledSlot()
    {
        var values = new Queue<int>([13, 1, 13, 2]);

        var studied = LegacyChiStudyState.TryStudy(
            CreateCharacter(SelfUid, CharacterName),
            LegacyChiPowerType.Dragon,
            lockedFlags: 0x05,
            FateForDragon(),
            (minimum, maximum) =>
            {
                var value = values.Dequeue();
                Assert.InRange(value, minimum, maximum);
                return value;
            },
            out var mutation,
            out var reason);

        Assert.True(studied, reason);
        Assert.Empty(values);
        Assert.Collection(
            mutation.ScoreNotifications,
            notification =>
            {
                Assert.Equal(LegacyChiPowerType.Dragon, notification.PowerType);
                Assert.Equal(10_200U, notification.AttributeScore);
                Assert.Equal(CharacterName, notification.CharacterName);
            },
            notification =>
            {
                Assert.Equal(LegacyChiPowerType.Dragon, notification.PowerType);
                Assert.Equal(20_200U, notification.AttributeScore);
                Assert.Equal(CharacterName, notification.CharacterName);
            });
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerPersistsBeforeBroadcastingEveryUnlockedScoreThenSendsRequesterChiInfo()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-d13-chi-score-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            var snapshotPath = Path.Combine(usersRoot, SelfUid.ToString(CultureInfo.InvariantCulture) + ".ini");
            File.WriteAllText(snapshotPath, BuildSnapshot(SelfUid, CharacterName));
            var loader = new CharacterSnapshotLoader(usersRoot);
            var character = loader.LoadByAccountId(SelfUid);
            Assert.NotNull(character);

            using var log = new StringWriter();
            var handler = CreateHandler(log, await SeedWorldData.Value, loader);
            var sessions = GetSessions(handler);
            var requesterSent = new List<byte[]>();
            var observerSent = new List<byte[]>();
            var firstSendObserved = 0;
            var persistenceObservedBeforeFirstScore = false;
            ConquerTcpConnection? requester = null;

            void BeforeAnySend(ReadOnlyMemory<byte> frame)
            {
                if (Interlocked.Exchange(ref firstSendObserved, 1) != 0)
                {
                    return;
                }

                Assert.True(
                    VisibleDx8D13TrainingVitalityScorePacket.TryParse(
                        ReadFrame(frame.ToArray()),
                        out _));
                Assert.True(sessions.TryGet(requester!, out var stored));
                Assert.NotNull(stored);
                Assert.NotSame(character, stored.Character);
                var persisted = loader.LoadByAccountId(SelfUid);
                Assert.NotNull(persisted);
                Assert.Equal(stored.Character.Attributes["ChiPoints"], persisted.Attributes["ChiPoints"]);
                Assert.Equal(stored.Character.Attributes["Dragon"], persisted.Attributes["Dragon"]);
                persistenceObservedBeforeFirstScore = true;
            }

            requester = CreateConnection(requesterSent, BeforeAnySend);
            var observer = CreateConnection(observerSent, BeforeAnySend);
            sessions.Set(requester, CreateSession(character, usersRoot));
            sessions.Set(observer, CreateSession(CreateCharacter(ObserverUid, "ScoreObserver")));

            await handler.HandleFrameAsync(
                requester,
                StudyPacket(SelfUid, lockedFlags: 0x05));

            Assert.True(persistenceObservedBeforeFirstScore);
            Assert.Equal(4, requesterSent.Count);
            Assert.Equal(2, observerSent.Count);
            Assert.True(requesterSent[0].SequenceEqual(observerSent[0]));
            Assert.True(requesterSent[1].SequenceEqual(observerSent[1]));

            Assert.True(sessions.TryGet(requester, out var updated));
            Assert.NotNull(updated);
            var power = ReadDragonPower(updated.Character);
            var expectedAttributes = new[] { power.Attributes[1], power.Attributes[3] };
            for (var index = 0; index < expectedAttributes.Length; index++)
            {
                Assert.True(
                    VisibleDx8D13TrainingVitalityScorePacket.TryParse(
                        ReadFrame(requesterSent[index]),
                        out var score));
                Assert.Equal((byte)LegacyChiPowerType.Dragon, score.PowerType);
                Assert.Equal(
                    checked((uint)((expectedAttributes[index].Type * 10_000) + expectedAttributes[index].Value)),
                    score.AttributeScore);
                Assert.Equal(CharacterName, score.CharacterName);
            }

            Assert.True(
                VisibleDx8D13TrainingVitalityInfoPacket.TryParse(
                    ReadFrame(requesterSent[2]),
                    out var sendInfo));
            Assert.True(
                VisibleDx8D13TrainingVitalityInfoPacket.TryParse(
                    ReadFrame(requesterSent[3]),
                    out var upgradeInfo));
            Assert.Equal(LegacyChiInfoAction.Send, sendInfo.Action);
            Assert.Equal(LegacyChiInfoAction.Upgrade, upgradeInfo.Action);
            Assert.All(
                observerSent,
                bytes => Assert.True(
                    VisibleDx8D13TrainingVitalityScorePacket.TryParse(ReadFrame(bytes), out _)));
        }
        finally
        {
            Directory.Delete(usersRoot, recursive: true);
        }
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task FullyLockedStudyEmitsNoScoreFrameToRequesterOrObserver()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(log, await SeedWorldData.Value);
        var sessions = GetSessions(handler);
        var requesterSent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        var requester = CreateConnection(requesterSent);
        var observer = CreateConnection(observerSent);
        sessions.Set(requester, CreateSession(CreateCharacter(SelfUid, CharacterName)));
        sessions.Set(observer, CreateSession(CreateCharacter(ObserverUid, "ScoreObserver")));

        await handler.HandleFrameAsync(requester, StudyPacket(SelfUid, lockedFlags: 0x0F));

        Assert.Equal(2, requesterSent.Count);
        Assert.Empty(observerSent);
        Assert.DoesNotContain(
            requesterSent,
            bytes => VisibleDx8D13TrainingVitalityScorePacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(
            VisibleDx8D13TrainingVitalityInfoPacket.TryParse(
                ReadFrame(requesterSent[0]),
                out var sendInfo));
        Assert.True(
            VisibleDx8D13TrainingVitalityInfoPacket.TryParse(
                ReadFrame(requesterSent[1]),
                out var upgradeInfo));
        Assert.Equal(LegacyChiInfoAction.Send, sendInfo.Action);
        Assert.Equal(LegacyChiInfoAction.Upgrade, upgradeInfo.Action);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task CompareAndSetFailureSendsNoScoreOrChiInfoToAnyActiveSession()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(log, await SeedWorldData.Value);
        var sessions = GetSessions(handler);
        var requesterSent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        ConquerTcpConnection? requester = null;
        WorldSession? conflict = null;
        var callbackCount = 0;
        var character = CreateCharacter(SelfUid, CharacterName);
        var callbackAttributes = new CallbackReadOnlyDictionary(
            character.Attributes,
            "Dragon",
            () =>
            {
                if (Interlocked.Exchange(ref callbackCount, 1) == 0)
                {
                    sessions.Set(requester!, conflict!);
                }
            });
        var original = CreateSession(character with { Attributes = callbackAttributes });
        conflict = original with { Username = "score-conflict-winner" };
        requester = CreateConnection(requesterSent);
        var observer = CreateConnection(observerSent);
        sessions.Set(requester, original);
        sessions.Set(observer, CreateSession(CreateCharacter(ObserverUid, "ScoreObserver")));

        await handler.HandleFrameAsync(requester, StudyPacket(SelfUid, lockedFlags: 0x00));

        Assert.Equal(1, callbackCount);
        Assert.Empty(requesterSent);
        Assert.Empty(observerSent);
        Assert.True(sessions.TryGet(requester, out var stored));
        Assert.Same(conflict, stored);
        Assert.Contains("mutation=none reason=sessionConflict", log.ToString());
    }

    [Fact]
    public async Task RootD11DoesNotDispatchD13StudyOrLeakScoreBroadcasts()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(log, profile: ConquerProtocolProfile.RootD11_7899);
        var sessions = GetSessions(handler);
        var requesterSent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        var requester = CreateConnection(requesterSent);
        var observer = CreateConnection(observerSent);
        var requesterSession = CreateSession(CreateCharacter(SelfUid, CharacterName));
        var observerSession = CreateSession(CreateCharacter(ObserverUid, "ScoreObserver"));
        sessions.Set(requester, requesterSession);
        sessions.Set(observer, observerSession);

        await handler.HandleFrameAsync(requester, StudyPacket(SelfUid, lockedFlags: 0x00));

        Assert.Empty(requesterSent);
        Assert.Empty(observerSent);
        Assert.True(sessions.TryGet(requester, out var storedRequester));
        Assert.Same(requesterSession, storedRequester);
        Assert.True(sessions.TryGet(observer, out var storedObserver));
        Assert.Same(observerSession, storedObserver);
        Assert.Contains("PARITY-GAP unknown world opcode", log.ToString());
        Assert.Contains("opcode=2377", log.ToString());
    }

    private static WorldServerFrameHandler CreateHandler(
        TextWriter log,
        WorldDataCatalog? worldData = null,
        CharacterSnapshotLoader? loader = null,
        ConquerProtocolProfile profile = ConquerProtocolProfile.VisibleDx8D13)
    {
        return new WorldServerFrameHandler(
            snapshotLoader: loader,
            handoffService: null,
            worldData,
            LegacyRuntimeUpdateLoopOptions.Disabled,
            log,
            protocolProfile: profile);
    }

    private static PacketFrame StudyPacket(long uid, byte lockedFlags)
    {
        return ReadFrame(new VisibleDx8D13TrainingVitalityPacket(
            checked((uint)uid),
            (ushort)VisibleDx8D13TrainingVitalityAction.Study,
            (byte)LegacyChiPowerType.Dragon,
            lockedFlags,
            Reserved0: 0,
            Reserved1: 0,
            Reserved2: 0).ToFrameBytes());
    }

    private static CharacterSnapshot CreateCharacter(long uid, string name)
    {
        return CharacterSnapshotParser.ParseText(BuildSnapshot(uid, name), "fixture");
    }

    private static WorldSession CreateSession(CharacterSnapshot character, string? usersRoot = null)
    {
        return new WorldSession(
            AccountId: character.Uid,
            Username: character.Name.ToLowerInvariant(),
            Character: character,
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static LegacyChiPowerState ReadDragonPower(CharacterSnapshot character)
    {
        return Assert.Single(
            LegacyChiState.FromSnapshot(character).Powers,
            power => power.Type == LegacyChiPowerType.Dragon);
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<FateExperience>> FateForDragon()
    {
        IReadOnlyList<FateExperience> rows =
        [
            FateRow(level: 1, experience: 0),
            FateRow(level: 98, experience: 98_000),
        ];
        return new Dictionary<int, IReadOnlyList<FateExperience>>
        {
            [(int)LegacyChiPowerType.Dragon] = rows,
        };
    }

    private static FateExperience FateRow(int level, long experience)
    {
        return new FateExperience(
            Id: level,
            Type: (int)LegacyChiPowerType.Dragon,
            Level: level,
            Experience: experience,
            Values: [0, 0, 0, 0, 0, 0, 0],
            SourceFile: "fixture",
            LineNumber: level,
            RawLine: string.Empty,
            Fields: []);
    }

    private static WorldSessionStore GetSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static ConquerTcpConnection CreateConnection(
        List<byte[]> sent,
        Action<ReadOnlyMemory<byte>>? beforeSend = null)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9_958),
            sendAsync: (frame, _) =>
            {
                beforeSend?.Invoke(frame);
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            },
            disconnectAsync: _ => ValueTask.CompletedTask);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private static byte[] ResizeFrame(byte[] source, int length)
    {
        var resized = new byte[length];
        source.AsSpan(0, Math.Min(source.Length, resized.Length)).CopyTo(resized);
        BinaryPrimitives.WriteUInt16LittleEndian(resized.AsSpan(0, 2), checked((ushort)length));
        return resized;
    }

    private static async Task<WorldDataCatalog> LoadSeedWorldDataAsync()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        Assert.True(
            File.Exists(Path.Combine(seedPath, "manifest.json")),
            $"Expected seed world data at {seedPath}.");
        return await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync().ConfigureAwait(false);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var startPaths = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.GetDirectoryName(sourceFilePath) ?? string.Empty,
        };
        foreach (var startPath in startPaths.Where(path => path.Length > 0).Distinct())
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln.");
    }

    private static string BuildSnapshot(long uid, string name)
    {
        return $$"""
            [Character]
            UID={{uid}}
            Name={{name}}
            Body=1003
            Face=154
            Haire=27
            Class=15
            FirstClass=15
            SecoundeClass=15
            Reborn=1
            Level=140
            Experience=1000
            Map=1002
            X=430
            Y=377
            PMap=1002
            PMapX=430
            PMapY=377
            Strength=10
            Agility=20
            Vitaliti=30
            Spirit=40
            Atributes=5
            MinHitPoints=1000
            MinMana=500
            MaxMana=500
            Money=10000
            ConquerPoints=100
            BoundConquerPoints=50
            BankCps=25
            PkPoints=0
            Online=1
            ExpBallUsed=0
            Dragon={{DefaultDragonPower}}
            Pheonix=0/
            Tiger=0/
            Turtle=0/
            ChiPoints=1000
            ChargeChi=0
            """;
    }

    private sealed class CallbackReadOnlyDictionary(
        IReadOnlyDictionary<string, string> inner,
        string triggerKey,
        Action callback) : IReadOnlyDictionary<string, string>
    {
        public string this[string key] => inner[key];

        public IEnumerable<string> Keys => inner.Keys;

        public IEnumerable<string> Values => inner.Values;

        public int Count => inner.Count;

        public bool ContainsKey(string key) => inner.ContainsKey(key);

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => inner.GetEnumerator();

        public bool TryGetValue(string key, out string value)
        {
            if (string.Equals(key, triggerKey, StringComparison.OrdinalIgnoreCase))
            {
                callback();
            }

            return inner.TryGetValue(key, out value!);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
