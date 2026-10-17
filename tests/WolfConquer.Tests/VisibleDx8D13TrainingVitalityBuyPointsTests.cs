using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class VisibleDx8D13TrainingVitalityBuyPointsTests
{
    private const uint SelfUid = 1_293_600;
    private const string DragonPower = "1/0/10/123/0/11/124/0/12/125/0/13/126/0/";

    private static readonly Lazy<Task<WorldDataCatalog>> SeedWorldData = new(LoadSeedWorldDataAsync);

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task CurrentD13ActivityCatalogDoesNotDefineOlderBuyPointsTask37()
    {
        var worldData = await SeedWorldData.Value;

        Assert.False(worldData.ActivityTasksBySectionId.ContainsKey(37));
    }

    [Fact]
    public void ExactD13BuyPointsRequestKeepsClientOwned24ByteLayout()
    {
        const string exactFrameHex =
            "1800490920BD130003000000000000000000000000000000";
        var packet = new VisibleDx8D13TrainingVitalityPacket(
            SelfUid,
            (ushort)VisibleDx8D13TrainingVitalityAction.BuyPoints,
            Power: 0,
            LockedFlags: 0,
            Reserved0: 0,
            Reserved1: 0,
            Reserved2: 0);

        var bytes = packet.ToFrameBytes();

        Assert.Equal(24, bytes.Length);
        Assert.Equal(24, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal(2377, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.Equal(exactFrameHex, Convert.ToHexString(bytes));
        Assert.True(VisibleDx8D13TrainingVitalityPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal(SelfUid, parsed.Uid);
        Assert.Equal(3, parsed.Action);
        Assert.Equal(0, parsed.Power);
        Assert.Equal(0, parsed.LockedFlags);
        Assert.True(parsed.HasZeroReserved);
    }

    [Fact]
    public void MaximumAddsCurrentFateValue9ForEveryUnlockedPower()
    {
        var powerAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dragon"] = PowerLine(experience: 1_000),
            ["Pheonix"] = PowerLine(experience: 2_500),
            ["Tiger"] = "0/",
            ["Turtle"] = PowerLine(experience: 10),
        };
        var character = CreateCharacter(
            chiPoints: 4_500,
            conquerPoints: 500,
            powerAttributes);
        var fate = FateCatalog(
            FateRow(LegacyChiPowerType.Dragon, level: 1, experience: 0, maximumBonus: 100),
            FateRow(LegacyChiPowerType.Dragon, level: 2, experience: 500, maximumBonus: 200),
            FateRow(LegacyChiPowerType.Dragon, level: 3, experience: 1_500, maximumBonus: 900),
            FateRow(LegacyChiPowerType.Phoenix, level: 1, experience: 0, maximumBonus: 300),
            FateRow(LegacyChiPowerType.Phoenix, level: 2, experience: 2_000, maximumBonus: 400),
            FateRow(LegacyChiPowerType.Turtle, level: 1, experience: 0, maximumBonus: 500));

        var bought = LegacyChiBuyPointsState.TryBuyToMaximum(
            character,
            fate,
            legacyTradeActive: false,
            out var mutation,
            out var requiredConquerPoints,
            out var reason);

        Assert.True(bought, reason);
        Assert.Equal("boughtPoints", reason);
        Assert.Equal(5_100U, mutation.MaximumChiPoints);
        Assert.Equal(600, mutation.PointsPurchased);
        Assert.Equal(150U, requiredConquerPoints);
        Assert.Equal(requiredConquerPoints, mutation.ConquerPointsSpent);
        Assert.Equal(350, mutation.Character.ConquerPoints);
        Assert.Equal(5_100, ReadChiPoints(mutation.Character));
        Assert.Equal(500, character.ConquerPoints);
        Assert.Equal(4_500, ReadChiPoints(character));
        Assert.Same(character.PlayerQuests, mutation.Character.PlayerQuests);
        foreach (var power in powerAttributes)
        {
            Assert.Equal(power.Value, mutation.Character.Attributes[power.Key]);
        }
    }

    [Theory]
    [InlineData(49, 0u)]
    [InlineData(50, 13u)]
    [InlineData(99, 13u)]
    [InlineData(100, 25u)]
    [InlineData(150, 38u)]
    public void CostFloorsAmountOver50BeforeMultiplyingBy12Point5ThenCeils(
        int pointsToBuy,
        uint expectedCost)
    {
        var character = CreateCharacter(
            chiPoints: 4_000 - pointsToBuy,
            conquerPoints: 100,
            powerAttributes: new Dictionary<string, string>());

        var bought = LegacyChiBuyPointsState.TryBuyToMaximum(
            character,
            EmptyFateCatalog(),
            legacyTradeActive: false,
            out var mutation,
            out var requiredConquerPoints,
            out var reason);

        Assert.True(bought, reason);
        Assert.Equal(pointsToBuy, mutation.PointsPurchased);
        Assert.Equal(4_000U, mutation.MaximumChiPoints);
        Assert.Equal(expectedCost, requiredConquerPoints);
        Assert.Equal(expectedCost, mutation.ConquerPointsSpent);
        Assert.Equal(100L - expectedCost, mutation.Character.ConquerPoints);
        Assert.Equal(4_000, ReadChiPoints(mutation.Character));
    }

    [Theory]
    [InlineData(4_000)]
    [InlineData(4_001)]
    public void FullOrOverfullChiDoesNotMutate(int chiPoints)
    {
        var character = CreateCharacter(
            chiPoints,
            conquerPoints: 1_000,
            powerAttributes: new Dictionary<string, string>());

        var bought = LegacyChiBuyPointsState.TryBuyToMaximum(
            character,
            EmptyFateCatalog(),
            legacyTradeActive: false,
            out var mutation,
            out var requiredConquerPoints,
            out var reason);

        Assert.False(bought);
        Assert.Equal("chiPointsFull", reason);
        Assert.Same(character, mutation.Character);
        Assert.Equal(0, mutation.PointsPurchased);
        Assert.Equal(0U, mutation.ConquerPointsSpent);
        Assert.Equal(4_000U, mutation.MaximumChiPoints);
        Assert.Equal(0U, requiredConquerPoints);
        Assert.Equal(chiPoints, ReadChiPoints(character));
        Assert.Equal(1_000, character.ConquerPoints);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingFateCatalogOrUnlockedPowerRowsDoesNotMutate(bool nullCatalog)
    {
        var character = CreateCharacter(chiPoints: 1_000, conquerPoints: 1_000);
        IReadOnlyDictionary<int, IReadOnlyList<FateExperience>>? fate = nullCatalog
            ? null
            : EmptyFateCatalog();

        var bought = LegacyChiBuyPointsState.TryBuyToMaximum(
            character,
            fate,
            legacyTradeActive: false,
            out var mutation,
            out var requiredConquerPoints,
            out var reason);

        Assert.False(bought);
        Assert.Equal("fateExperienceMissing", reason);
        Assert.Same(character, mutation.Character);
        Assert.Equal(0, mutation.PointsPurchased);
        Assert.Equal(0U, mutation.ConquerPointsSpent);
        Assert.Equal(0U, requiredConquerPoints);
        Assert.Equal(1_000, ReadChiPoints(character));
        Assert.Equal(1_000, character.ConquerPoints);
    }

    public static TheoryData<uint, byte, byte, uint, uint, uint, string> InvalidHandlerRequests => new()
    {
        { SelfUid + 1, 0, 0, 0, 0, 0, "uidMismatch" },
        { SelfUid, 1, 0, 0, 0, 0, "auxiliaryFieldsNonzero" },
        { SelfUid, 0, 1, 0, 0, 0, "auxiliaryFieldsNonzero" },
        { SelfUid, 0, 0, 1, 0, 0, "auxiliaryFieldsNonzero" },
        { SelfUid, 0, 0, 0, 1, 0, "auxiliaryFieldsNonzero" },
        { SelfUid, 0, 0, 0, 0, 1, "auxiliaryFieldsNonzero" },
    };

    [Theory]
    [MemberData(nameof(InvalidHandlerRequests))]
    public async Task HandlerRejectsInvalidUidOrAuxiliaryFieldsWithoutResponseOrMutation(
        uint uid,
        byte power,
        byte lockedFlags,
        uint reserved0,
        uint reserved1,
        uint reserved2,
        string expectedReason)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = CreateHandler(log);
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCharacter(chiPoints: 1_000, conquerPoints: 1_000));
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(
            connection,
            BuyPointsPacket(uid, power, lockedFlags, reserved0, reserved1, reserved2));

        Assert.Empty(sent);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains($"mutation=none reason={expectedReason}", log.ToString());
    }

    [Fact]
    public async Task HandlerRequiresAuthenticatedSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = CreateHandler(log);

        await handler.HandleFrameAsync(CreateConnection(sent), BuyPointsPacket(SelfUid));

        Assert.Empty(sent);
        Assert.Contains("mutation=none reason=sessionMissing", log.ToString());
    }

    [Fact]
    public async Task HandlerMissingFateDataSendsNothingAndDoesNotMutate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = CreateHandler(log);
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCharacter(chiPoints: 1_000, conquerPoints: 1_000));
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(connection, BuyPointsPacket(SelfUid));

        Assert.Empty(sent);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains(
            "mutation=none reason=fateExperienceMissing:requiredConquerPoints=0",
            log.ToString());
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task InsufficientConquerPointsSendsExactSystemTalkBeforeTradeGateWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = CreateHandler(log, await SeedWorldData.Value);
        var connection = CreateConnection(sent);
        var session = CreateSession(
            CreateCharacter(chiPoints: 1_000, conquerPoints: 749),
            legacyTradeActive: true);
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(connection, BuyPointsPacket(SelfUid));

        var response = Assert.Single(sent);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(response), out var talk));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, talk.ChatType);
        Assert.Equal("Sorry, you need 750 ConquerPoints", talk.Content[3]);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains(
            "mutation=none reason=conquerPointsInsufficient:requiredConquerPoints=750",
            log.ToString());
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task EnoughConquerPointsWhileInTradeSilentlyNoOps()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = CreateHandler(log, await SeedWorldData.Value);
        var connection = CreateConnection(sent);
        var session = CreateSession(
            CreateCharacter(chiPoints: 1_000, conquerPoints: 750),
            legacyTradeActive: true);
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(connection, BuyPointsPacket(SelfUid));

        Assert.Empty(sent);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains(
            "mutation=none reason=tradeActive:requiredConquerPoints=750",
            log.ToString());
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerPersistsCompareAndSetBeforeExactlyOneD13UpgradeResponse()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-d13-chi-buy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            var snapshotPath = Path.Combine(usersRoot, SelfUid.ToString(CultureInfo.InvariantCulture) + ".ini");
            File.WriteAllText(snapshotPath, BuildSnapshot());
            var loader = new CharacterSnapshotLoader(usersRoot);
            var character = loader.LoadByAccountId(SelfUid);
            Assert.NotNull(character);
            var worldData = await SeedWorldData.Value;
            Assert.True(
                LegacyChiBuyPointsState.TryBuyToMaximum(
                    character,
                    worldData.FateExperienceByType,
                    legacyTradeActive: false,
                    out var expected,
                    out var expectedCost,
                    out var expectedReason),
                expectedReason);
            Assert.Equal(4_000U, expected.MaximumChiPoints);
            Assert.Equal(3_000, expected.PointsPurchased);
            Assert.Equal(750U, expectedCost);

            var original = CreateSession(character, usersRoot: usersRoot);
            using var log = new StringWriter();
            var handler = CreateHandler(log, worldData, loader);
            var sent = new List<byte[]>();
            var persistenceObservedBeforeSend = false;
            ConquerTcpConnection? connection = null;
            connection = CreateConnection(
                sent,
                beforeSend: _ =>
                {
                    Assert.Empty(sent);
                    Assert.True(GetSessions(handler).TryGet(connection!, out var stored));
                    Assert.NotNull(stored);
                    Assert.NotSame(original, stored);
                    Assert.Equal(expected.Character.ConquerPoints, stored.Character.ConquerPoints);
                    Assert.Equal(expected.MaximumChiPoints, (uint)ReadChiPoints(stored.Character));
                    Assert.Same(original.Character.PlayerQuests, stored.Character.PlayerQuests);

                    var persisted = loader.LoadByAccountId(SelfUid);
                    Assert.NotNull(persisted);
                    Assert.Equal(stored.Character.ConquerPoints, persisted.ConquerPoints);
                    Assert.Equal(
                        stored.Character.Attributes["ChiPoints"],
                        persisted.Attributes["ChiPoints"]);
                    persistenceObservedBeforeSend = true;
                });
            GetSessions(handler).Set(connection, original);

            await handler.HandleFrameAsync(connection, BuyPointsPacket(SelfUid));

            Assert.True(persistenceObservedBeforeSend);
            var response = Assert.Single(sent);
            var frame = ReadFrame(response);
            Assert.Equal(2299, frame.Header.Opcode);
            Assert.True(VisibleDx8D13TrainingVitalityInfoPacket.TryParse(frame, out var info));
            Assert.Equal(LegacyChiInfoAction.Upgrade, info.Action);
            Assert.Equal(SelfUid, info.Uid);
            Assert.Equal(4_000, info.ChiPoints);
            Assert.True(GetSessions(handler).TryGet(connection, out var updated));
            Assert.NotNull(updated);
            Assert.Equal(250, updated.Character.ConquerPoints);
            Assert.Equal(4_000, ReadChiPoints(updated.Character));
            Assert.Contains(
                "mutation=buyPoints reason=boughtPoints:pointsPurchased=3000:" +
                "conquerPointsSpent=750:maximumChiPoints=4000",
                log.ToString());
        }
        finally
        {
            Directory.Delete(usersRoot, recursive: true);
        }
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerSessionConflictSendsNothingAndLeavesWinningSessionUnchanged()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(log, await SeedWorldData.Value);
        var sent = new List<byte[]>();
        var sessions = GetSessions(handler);
        ConquerTcpConnection? connection = null;
        WorldSession? conflict = null;
        var callbackCount = 0;
        var baseCharacter = CreateCharacter(chiPoints: 1_000, conquerPoints: 750);
        var attributes = new CallbackReadOnlyDictionary(
            baseCharacter.Attributes,
            triggerKey: "Dragon",
            callback: () =>
            {
                if (Interlocked.Exchange(ref callbackCount, 1) == 0)
                {
                    sessions.Set(connection!, conflict!);
                }
            });
        var original = CreateSession(baseCharacter with { Attributes = attributes });
        conflict = original with { Username = "conflict-winner" };
        connection = CreateConnection(sent);
        sessions.Set(connection, original);

        await handler.HandleFrameAsync(connection, BuyPointsPacket(SelfUid));

        Assert.Equal(1, callbackCount);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var actual));
        Assert.Same(conflict, actual);
        Assert.Equal(750, original.Character.ConquerPoints);
        Assert.Equal(1_000, ReadChiPoints(original.Character));
        Assert.Contains("mutation=none reason=sessionConflict", log.ToString());
    }

    [Fact]
    public async Task RootD11DoesNotDispatchD13BuyPointsOpcodeOrMutateSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = CreateHandler(log, profile: ConquerProtocolProfile.RootD11_7899);
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCharacter(chiPoints: 1_000, conquerPoints: 1_000));
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(connection, BuyPointsPacket(SelfUid));

        Assert.Empty(sent);
        AssertStoredUnchanged(handler, connection, session);
        Assert.Contains("PARITY-GAP unknown world opcode", log.ToString());
        Assert.Contains("opcode=2377", log.ToString());
        Assert.DoesNotContain("CMsgTrainingVitality ", log.ToString());
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

    private static PacketFrame BuyPointsPacket(
        uint uid,
        byte power = 0,
        byte lockedFlags = 0,
        uint reserved0 = 0,
        uint reserved1 = 0,
        uint reserved2 = 0)
    {
        return ReadFrame(new VisibleDx8D13TrainingVitalityPacket(
            uid,
            (ushort)VisibleDx8D13TrainingVitalityAction.BuyPoints,
            power,
            lockedFlags,
            reserved0,
            reserved1,
            reserved2).ToFrameBytes());
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<FateExperience>> EmptyFateCatalog()
    {
        return new Dictionary<int, IReadOnlyList<FateExperience>>();
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<FateExperience>> FateCatalog(
        params FateExperience[] rows)
    {
        return rows
            .GroupBy(row => row.Type)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FateExperience>)group.ToArray());
    }

    private static FateExperience FateRow(
        LegacyChiPowerType powerType,
        int level,
        long experience,
        long maximumBonus)
    {
        return new FateExperience(
            Id: ((int)powerType * 100) + level,
            Type: (int)powerType,
            Level: level,
            Experience: experience,
            Values: [0, 0, 0, 0, 0, 0, 0, 0, 0, maximumBonus],
            SourceFile: "fixture",
            LineNumber: level,
            RawLine: string.Empty,
            Fields: []);
    }

    private static CharacterSnapshot CreateCharacter(
        int chiPoints,
        long conquerPoints,
        IReadOnlyDictionary<string, string>? powerAttributes = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ChiPoints"] = chiPoints.ToString(CultureInfo.InvariantCulture),
        };
        if (powerAttributes is null)
        {
            attributes["Dragon"] = DragonPower;
            attributes["Pheonix"] = "0/";
            attributes["Tiger"] = "0/";
            attributes["Turtle"] = "0/";
        }
        else
        {
            foreach (var power in powerAttributes)
            {
                attributes[power.Key] = power.Value;
            }
        }

        return new CharacterSnapshot(
            Uid: SelfUid,
            Name: "BuyPoints",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 10,
            FirstClass: 10,
            SecondClass: 0,
            Reborn: 1,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 430,
            Y: 378,
            PreviousMapId: 1002,
            PreviousMapX: 430,
            PreviousMapY: 378,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 1,
            MinMana: 0,
            Money: 0,
            ConquerPoints: conquerPoints,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static WorldSession CreateSession(
        CharacterSnapshot character,
        bool legacyTradeActive = false,
        string? usersRoot = null)
    {
        return new WorldSession(
            AccountId: character.Uid,
            Username: character.Name.ToLowerInvariant(),
            Character: character,
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot,
            LegacyTradeActive: legacyTradeActive);
    }

    private static string PowerLine(uint experience)
    {
        return "1/" + experience.ToString(CultureInfo.InvariantCulture) +
            "/10/123/0/11/124/0/12/125/0/13/126/0/";
    }

    private static int ReadChiPoints(CharacterSnapshot character)
    {
        return int.Parse(character.Attributes["ChiPoints"], CultureInfo.InvariantCulture);
    }

    private static WorldSessionStore GetSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static void AssertStoredUnchanged(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession expected)
    {
        Assert.True(GetSessions(handler).TryGet(connection, out var actual));
        Assert.Same(expected, actual);
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

    private static string BuildSnapshot()
    {
        return $$"""
            [Character]
            UID={{SelfUid}}
            Name=BuyPointsPersistence
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
            ConquerPoints=1000
            BoundConquerPoints=50
            BankCps=25
            PkPoints=0
            Online=1
            ExpBallUsed=0
            Dragon={{DragonPower}}
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
