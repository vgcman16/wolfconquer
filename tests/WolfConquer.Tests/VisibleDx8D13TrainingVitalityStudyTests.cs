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

public sealed class VisibleDx8D13TrainingVitalityStudyTests
{
    private const uint SelfUid = 1_293_600;
    private const string DefaultDragonPower =
        "1/0/10/123/0/11/124/0/12/125/0/13/126/0/";

    private static readonly Lazy<Task<WorldDataCatalog>> SeedWorldData = new(LoadSeedWorldDataAsync);

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task CurrentD13ActivityCatalogDoesNotDefineOlderStudyTask37()
    {
        var worldData = await SeedWorldData.Value;

        Assert.False(worldData.ActivityTasksBySectionId.ContainsKey(37));
    }

    [Fact]
    public void ExactD13StudyRequestKeepsClientOwned24ByteLayout()
    {
        var packet = new VisibleDx8D13TrainingVitalityPacket(
            SelfUid,
            (ushort)VisibleDx8D13TrainingVitalityAction.Study,
            (byte)LegacyChiPowerType.Turtle,
            LockedFlags: 0x0B,
            Reserved0: 0,
            Reserved1: 0,
            Reserved2: 0);

        var bytes = packet.ToFrameBytes();

        Assert.Equal(24, bytes.Length);
        Assert.Equal(24, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2)));
        Assert.Equal(2377, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.Equal(SelfUid, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)));
        Assert.Equal(2, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2)));
        Assert.Equal((byte)LegacyChiPowerType.Turtle, bytes[10]);
        Assert.Equal(0x0B, bytes[11]);
        Assert.All(bytes[12..], value => Assert.Equal(0, value));

        Assert.True(VisibleDx8D13TrainingVitalityPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet, parsed);
        Assert.True(parsed.HasZeroReserved);
    }

    [Fact]
    public void StudyRejectsNonRebornCharacterBelowLevel110WithoutMutation()
    {
        var character = CreateCharacter(level: 109, reborn: 0);
        var random = new InclusiveRandom([]);

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags: 0x0F,
            FateFor(LegacyChiPowerType.Dragon),
            random.Next,
            out var mutation,
            out var reason);

        Assert.False(studied);
        Assert.Equal("levelOrRebornTooLow", reason);
        Assert.Same(character, mutation.Character);
        Assert.Equal(0, mutation.PointsSpent);
        Assert.Empty(random.Bounds);
    }

    [Fact]
    public void RebornCharacterBypassesLevel110Gate()
    {
        var character = CreateCharacter(level: 1, reborn: 1);
        var random = new InclusiveRandom([]);

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags: 0x0F,
            FateFor(LegacyChiPowerType.Dragon),
            random.Next,
            out var mutation,
            out var reason);

        Assert.True(studied, reason);
        Assert.Equal(250, mutation.PointsSpent);
        Assert.Equal(750, ReadChiPoints(mutation.Character));
        Assert.Empty(random.Bounds);
    }

    [Fact]
    public void StudyRejectsLockedPowerWithoutMutation()
    {
        var character = CreateCharacter(powerLine: "0/");

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags: 0,
            FateFor(LegacyChiPowerType.Dragon),
            (_, _) => throw new InvalidOperationException("Locked powers must not roll."),
            out var mutation,
            out var reason);

        Assert.False(studied);
        Assert.Equal("powerLockedOrMissing", reason);
        Assert.Same(character, mutation.Character);
        Assert.Equal(0, mutation.PointsSpent);
    }

    [Theory]
    [InlineData(0x00, 50)]
    [InlineData(0x01, 100)]
    [InlineData(0x03, 150)]
    [InlineData(0x07, 200)]
    [InlineData(0x0F, 250)]
    public void StudyCosts50BasePlus50PerLockedSlot(byte lockedFlags, int expectedCost)
    {
        var character = CreateCharacter(chiPoints: 1_000);
        var random = new InclusiveRandom(RerollSequence(lockedFlags));

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags,
            FateFor(LegacyChiPowerType.Dragon),
            random.Next,
            out var mutation,
            out var reason);

        Assert.True(studied, reason);
        Assert.Equal(expectedCost, mutation.PointsSpent);
        Assert.Equal(1_000 - expectedCost, ReadChiPoints(mutation.Character));
        Assert.Equal(2 * (4 - CountLockedBits(lockedFlags)), random.Bounds.Count);
        Assert.All(random.Bounds, bounds => Assert.Equal((1, 13), bounds));
        Assert.Equal(0, random.Remaining);
    }

    [Fact]
    public void FateValuesIndex6UsesHundredthsOfAPercentForCostReduction()
    {
        var character = CreateCharacter(chiPoints: 1_000);
        var random = new InclusiveRandom(RerollSequence(lockedFlags: 0x01));

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags: 0x01,
            FateFor(LegacyChiPowerType.Dragon, reductionValue: 2_500),
            random.Next,
            out var mutation,
            out var reason);

        Assert.True(studied, reason);
        Assert.Equal(75, mutation.PointsSpent);
        Assert.Equal(925, ReadChiPoints(mutation.Character));
        Assert.All(random.Bounds, bounds => Assert.Equal((1, 13), bounds));
    }

    [Fact]
    public void StudyRejectsInsufficientChiPointsBeforeRollingOrMutating()
    {
        var character = CreateCharacter(chiPoints: 149);
        var random = new InclusiveRandom([]);

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags: 0x03,
            FateFor(LegacyChiPowerType.Dragon),
            random.Next,
            out var mutation,
            out var reason);

        Assert.False(studied);
        Assert.Equal("chiPointsInsufficient", reason);
        Assert.Same(character, mutation.Character);
        Assert.Equal(0, mutation.PointsSpent);
        Assert.Empty(random.Bounds);
    }

    [Theory]
    [InlineData(1, 200)]
    [InlineData(2, 200)]
    [InlineData(3, 200)]
    [InlineData(4, 200)]
    [InlineData(5, 200)]
    [InlineData(6, 3_500)]
    [InlineData(7, 2_000)]
    [InlineData(8, 2_500)]
    [InlineData(9, 250)]
    [InlineData(10, 500)]
    [InlineData(11, 300)]
    [InlineData(12, 500)]
    [InlineData(13, 300)]
    public void RerollUsesDiscardedPreliminaryRollThenInclusiveUniqueTypeAndSourceMaximum(
        byte attributeType,
        ushort expectedMaximum)
    {
        var lockedTypes = Enumerable.Range(1, 13)
            .Where(candidate => candidate != attributeType)
            .Take(3)
            .Select(candidate => (byte)candidate)
            .ToArray();
        var powerLine = PowerLine(
            experience: 0,
            new LegacyChiAttributeState(attributeType, 1, Locked: false),
            new LegacyChiAttributeState(lockedTypes[0], 101, Locked: false),
            new LegacyChiAttributeState(lockedTypes[1], 102, Locked: false),
            new LegacyChiAttributeState(lockedTypes[2], 103, Locked: false));
        var character = CreateCharacter(powerLine: powerLine);
        var discardedRoll = attributeType == 13 ? 12 : 13;
        var random = new InclusiveRandom([discardedRoll, attributeType]);

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags: 0x0E,
            FateFor(LegacyChiPowerType.Dragon),
            random.Next,
            out var mutation,
            out var reason);

        Assert.True(studied, reason);
        var power = ReadPower(mutation.Character, LegacyChiPowerType.Dragon);
        Assert.Equal(attributeType, power.Attributes[0].Type);
        Assert.Equal(expectedMaximum, power.Attributes[0].Value);
        Assert.False(power.Attributes[0].Locked);
        Assert.Equal(4, power.Attributes.Select(attribute => attribute.Type).Distinct().Count());
        Assert.Equal([discardedRoll, attributeType], random.ReturnedValues);
        Assert.Equal([(1, 13), (1, 13)], random.Bounds);
        Assert.Equal(0, random.Remaining);
    }

    [Fact]
    public void LockedSlotsArePreservedAndMarkedLockedWhileOtherSlotsReroll()
    {
        var character = CreateCharacter(powerLine: DefaultDragonPower);
        var random = new InclusiveRandom([13, 1, 13, 2]);

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags: 0x05,
            FateFor(LegacyChiPowerType.Dragon),
            random.Next,
            out var mutation,
            out var reason);

        Assert.True(studied, reason);
        Assert.Equal(150, mutation.PointsSpent);
        var attributes = ReadPower(mutation.Character, LegacyChiPowerType.Dragon).Attributes;
        Assert.Equal(new LegacyChiAttributeState(10, 123, Locked: true), attributes[0]);
        Assert.Equal(new LegacyChiAttributeState(1, 200, Locked: false), attributes[1]);
        Assert.Equal(new LegacyChiAttributeState(12, 125, Locked: true), attributes[2]);
        Assert.Equal(new LegacyChiAttributeState(2, 200, Locked: false), attributes[3]);
        Assert.Equal([(1, 13), (1, 13), (1, 13), (1, 13)], random.Bounds);
    }

    [Fact]
    public void ChargeChiForcesPowerExperienceToLevel98Requirement()
    {
        const long level98Experience = 987_654;
        var character = CreateCharacter(
            powerLine: PowerLine(
                123,
                new LegacyChiAttributeState(10, 123, false),
                new LegacyChiAttributeState(11, 124, false),
                new LegacyChiAttributeState(12, 125, false),
                new LegacyChiAttributeState(13, 126, false)),
            additionalAttributes: new Dictionary<string, string> { ["ChargeChi"] = "1" });

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags: 0x0F,
            FateFor(LegacyChiPowerType.Dragon, level98Experience),
            (_, _) => throw new InvalidOperationException("Every slot is locked."),
            out var mutation,
            out var reason);

        Assert.True(studied, reason);
        Assert.Equal((uint)level98Experience, ReadPower(mutation.Character, LegacyChiPowerType.Dragon).Experience);
    }

    [Fact]
    public void EveryLevel98Plus100000OverflowStepReturns20000ChiPoints()
    {
        const long level98Experience = 1_000;
        var character = CreateCharacter(
            chiPoints: 1_000,
            powerLine: PowerLine(
                201_000,
                new LegacyChiAttributeState(10, 123, false),
                new LegacyChiAttributeState(11, 124, false),
                new LegacyChiAttributeState(12, 125, false),
                new LegacyChiAttributeState(13, 126, false)));

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Dragon,
            lockedFlags: 0x0F,
            FateFor(LegacyChiPowerType.Dragon, level98Experience),
            (_, _) => throw new InvalidOperationException("Every slot is locked."),
            out var mutation,
            out var reason);

        Assert.True(studied, reason);
        Assert.Equal(250, mutation.PointsSpent);
        Assert.Equal(40_750, ReadChiPoints(mutation.Character));
        Assert.Equal(1_250U, ReadPower(mutation.Character, LegacyChiPowerType.Dragon).Experience);
    }

    [Fact]
    public void PhoenixStudyPreservesLegacyPheonixStorageKey()
    {
        var character = CreateCharacter(
            powerType: LegacyChiPowerType.Phoenix,
            powerLine: DefaultDragonPower);

        var studied = LegacyChiStudyState.TryStudy(
            character,
            LegacyChiPowerType.Phoenix,
            lockedFlags: 0x0F,
            FateFor(LegacyChiPowerType.Phoenix),
            (_, _) => throw new InvalidOperationException("Every slot is locked."),
            out var mutation,
            out var reason);

        Assert.True(studied, reason);
        Assert.True(mutation.Character.Attributes.ContainsKey("Pheonix"));
        Assert.False(mutation.Character.Attributes.ContainsKey("Phoenix"));
        Assert.NotEqual(character.Attributes["Pheonix"], mutation.Character.Attributes["Pheonix"]);
    }

    public static TheoryData<uint, byte, byte, uint, uint, uint, string> InvalidHandlerRequests => new()
    {
        { SelfUid + 1, 1, 0, 0, 0, 0, "uidMismatch" },
        { SelfUid, 0, 0, 0, 0, 0, "powerInvalid" },
        { SelfUid, 5, 0, 0, 0, 0, "powerInvalid" },
        { SelfUid, 1, 0x10, 0, 0, 0, "lockedFlagsInvalid" },
        { SelfUid, 1, 0, 1, 0, 0, "reservedNonzero" },
        { SelfUid, 1, 0, 0, 1, 0, "reservedNonzero" },
        { SelfUid, 1, 0, 0, 0, 1, "reservedNonzero" },
    };

    [Theory]
    [MemberData(nameof(InvalidHandlerRequests))]
    public async Task HandlerRejectsInvalidUidPowerFlagsAndReservedWithoutResponseOrMutation(
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
        var session = CreateSession(CreateCharacter());
        GetSessions(handler).Set(connection, session);
        var packet = StudyPacket(uid, power, lockedFlags, reserved0, reserved1, reserved2);

        await handler.HandleFrameAsync(connection, packet);

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

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            StudyPacket(SelfUid, (byte)LegacyChiPowerType.Dragon, lockedFlags: 0x0F));

        Assert.Empty(sent);
        Assert.Contains("mutation=none reason=sessionMissing", log.ToString());
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerPersistsCompareAndSetBeforeExactlyD13SendThenUpgradeResponses()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-d13-chi-study-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            var snapshotPath = Path.Combine(usersRoot, SelfUid.ToString(CultureInfo.InvariantCulture) + ".ini");
            File.WriteAllText(snapshotPath, BuildSnapshot());
            var loader = new CharacterSnapshotLoader(usersRoot);
            var character = loader.LoadByAccountId(SelfUid);
            Assert.NotNull(character);
            var original = CreateSession(character, usersRoot);
            using var log = new StringWriter();
            var handler = CreateHandler(log, await SeedWorldData.Value, loader);
            var sent = new List<byte[]>();
            var persistenceObservedBeforeFirstSend = false;
            ConquerTcpConnection? connection = null;
            connection = CreateConnection(
                sent,
                beforeSend: _ =>
                {
                    if (sent.Count != 0)
                    {
                        return;
                    }

                    Assert.True(GetSessions(handler).TryGet(connection!, out var stored));
                    Assert.NotNull(stored);
                    Assert.NotSame(original, stored);
                    var persisted = loader.LoadByAccountId(SelfUid);
                    Assert.NotNull(persisted);
                    Assert.Equal(stored.Character.Attributes["ChiPoints"], persisted.Attributes["ChiPoints"]);
                    Assert.Equal(stored.Character.Attributes["Dragon"], persisted.Attributes["Dragon"]);
                    persistenceObservedBeforeFirstSend = true;
                });
            GetSessions(handler).Set(connection, original);

            await handler.HandleFrameAsync(
                connection,
                StudyPacket(SelfUid, (byte)LegacyChiPowerType.Dragon, lockedFlags: 0x0F));

            Assert.True(persistenceObservedBeforeFirstSend);
            Assert.Equal(2, sent.Count);
            var frames = sent.Select(ReadFrame).ToArray();
            Assert.All(frames, frame => Assert.Equal(2299, frame.Header.Opcode));
            Assert.True(VisibleDx8D13TrainingVitalityInfoPacket.TryParse(frames[0], out var sentInfo));
            Assert.True(VisibleDx8D13TrainingVitalityInfoPacket.TryParse(frames[1], out var upgradeInfo));
            Assert.Equal(LegacyChiInfoAction.Send, sentInfo.Action);
            Assert.Equal(LegacyChiInfoAction.Upgrade, upgradeInfo.Action);
            Assert.Equal(SelfUid, sentInfo.Uid);
            Assert.Equal(SelfUid, upgradeInfo.Uid);
            Assert.Equal(750, sentInfo.ChiPoints);
            Assert.Equal(sentInfo.ChiPoints, upgradeInfo.ChiPoints);
            Assert.Equal(250U, Assert.Single(sentInfo.Powers).Experience);
            Assert.Equal(250U, Assert.Single(upgradeInfo.Powers).Experience);
            Assert.Contains("mutation=study reason=studied:pointsSpent=250", log.ToString());
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
        var baseCharacter = CreateCharacter();
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

        await handler.HandleFrameAsync(
            connection,
            StudyPacket(SelfUid, (byte)LegacyChiPowerType.Dragon, lockedFlags: 0x0F));

        Assert.Equal(1, callbackCount);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var actual));
        Assert.Same(conflict, actual);
        Assert.Equal("1000", original.Character.Attributes["ChiPoints"]);
        Assert.Equal(DefaultDragonPower, original.Character.Attributes["Dragon"]);
        Assert.Contains("mutation=none reason=sessionConflict", log.ToString());
    }

    [Fact]
    public async Task RootD11DoesNotDispatchD13StudyOpcodeOrMutateSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = CreateHandler(log, profile: ConquerProtocolProfile.RootD11_7899);
        var connection = CreateConnection(sent);
        var session = CreateSession(CreateCharacter());
        GetSessions(handler).Set(connection, session);

        await handler.HandleFrameAsync(
            connection,
            StudyPacket(SelfUid, (byte)LegacyChiPowerType.Dragon, lockedFlags: 0x0F));

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

    private static PacketFrame StudyPacket(
        uint uid,
        byte power,
        byte lockedFlags,
        uint reserved0 = 0,
        uint reserved1 = 0,
        uint reserved2 = 0)
    {
        return ReadFrame(new VisibleDx8D13TrainingVitalityPacket(
            uid,
            (ushort)VisibleDx8D13TrainingVitalityAction.Study,
            power,
            lockedFlags,
            reserved0,
            reserved1,
            reserved2).ToFrameBytes());
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<FateExperience>> FateFor(
        LegacyChiPowerType powerType,
        long level98Experience = 98_000,
        long reductionValue = 0)
    {
        IReadOnlyList<FateExperience> rows =
        [
            FateRow(powerType, level: 1, experience: 0, reductionValue),
            FateRow(powerType, level: 98, level98Experience, reductionValue),
        ];
        return new Dictionary<int, IReadOnlyList<FateExperience>>
        {
            [(int)powerType] = rows,
        };
    }

    private static FateExperience FateRow(
        LegacyChiPowerType powerType,
        int level,
        long experience,
        long reductionValue)
    {
        return new FateExperience(
            Id: level,
            Type: (int)powerType,
            Level: level,
            Experience: experience,
            Values: [0, 0, 0, 0, 0, 0, reductionValue],
            SourceFile: "fixture",
            LineNumber: level,
            RawLine: string.Empty,
            Fields: []);
    }

    private static CharacterSnapshot CreateCharacter(
        LegacyChiPowerType powerType = LegacyChiPowerType.Dragon,
        string? powerLine = null,
        int chiPoints = 1_000,
        long level = 140,
        long reborn = 1,
        IReadOnlyDictionary<string, string>? additionalAttributes = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ChiPoints"] = chiPoints.ToString(CultureInfo.InvariantCulture),
            [PowerKey(powerType)] = powerLine ?? DefaultDragonPower,
        };
        if (additionalAttributes is not null)
        {
            foreach (var pair in additionalAttributes)
            {
                attributes[pair.Key] = pair.Value;
            }
        }

        return new CharacterSnapshot(
            Uid: SelfUid,
            Name: "Study",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 10,
            FirstClass: 10,
            SecondClass: 0,
            Reborn: reborn,
            Level: level,
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
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
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

    private static string PowerKey(LegacyChiPowerType powerType)
    {
        return powerType switch
        {
            LegacyChiPowerType.Dragon => "Dragon",
            LegacyChiPowerType.Phoenix => "Pheonix",
            LegacyChiPowerType.Tiger => "Tiger",
            LegacyChiPowerType.Turtle => "Turtle",
            _ => throw new ArgumentOutOfRangeException(nameof(powerType)),
        };
    }

    private static string PowerLine(uint experience, params LegacyChiAttributeState[] attributes)
    {
        Assert.Equal(LegacyChiPowerState.AttributeCount, attributes.Length);
        var fields = new List<string>
        {
            "1",
            experience.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var attribute in attributes)
        {
            fields.Add(attribute.Type.ToString(CultureInfo.InvariantCulture));
            fields.Add(attribute.Value.ToString(CultureInfo.InvariantCulture));
            fields.Add(attribute.Locked ? "1" : "0");
        }

        return string.Join('/', fields) + "/";
    }

    private static LegacyChiPowerState ReadPower(CharacterSnapshot character, LegacyChiPowerType powerType)
    {
        return Assert.Single(
            LegacyChiState.FromSnapshot(character).Powers,
            power => power.Type == powerType);
    }

    private static int ReadChiPoints(CharacterSnapshot character)
    {
        return int.Parse(character.Attributes["ChiPoints"], CultureInfo.InvariantCulture);
    }

    private static int[] RerollSequence(byte lockedFlags)
    {
        var sequence = new List<int>();
        for (var index = 0; index < LegacyChiPowerState.AttributeCount; index++)
        {
            if ((lockedFlags & (1 << index)) != 0)
            {
                continue;
            }

            sequence.Add(13); // Source Reroll assigns once before the do/while overwrites it.
            sequence.Add(index + 1);
        }

        return sequence.ToArray();
    }

    private static int CountLockedBits(byte lockedFlags)
    {
        var count = 0;
        for (var bit = 0; bit < LegacyChiPowerState.AttributeCount; bit++)
        {
            if ((lockedFlags & (1 << bit)) != 0)
            {
                count++;
            }
        }

        return count;
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
            Name=StudyPersistence
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

    private sealed class InclusiveRandom(IEnumerable<int> values)
    {
        private readonly Queue<int> _values = new(values);

        public List<(int Minimum, int Maximum)> Bounds { get; } = [];

        public List<int> ReturnedValues { get; } = [];

        public int Remaining => _values.Count;

        public int Next(int minimum, int maximum)
        {
            Bounds.Add((minimum, maximum));
            if (_values.Count == 0)
            {
                throw new InvalidOperationException("The deterministic random sequence was exhausted.");
            }

            var value = _values.Dequeue();
            ReturnedValues.Add(value);
            return value;
        }
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
