using System.Buffers;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class VisibleDx8D13TrainingVitalityProtectPacketTests
{
    [Fact]
    public void ExactD13FrameParsesClientOwnedLayout()
    {
        var frame = ReadFrame("0B00DC0802000300000000");

        Assert.True(VisibleDx8D13TrainingVitalityProtectPacket.TryParse(frame, out var packet));
        Assert.Equal((ushort)2, packet.Action);
        Assert.Equal((byte)3, packet.Mode);
        Assert.Equal(0u, packet.Reserved);
        Assert.True(packet.HasKnownAction);
        Assert.Equal(Convert.FromHexString("0B00DC0802000300000000"), packet.ToFrameBytes());
    }

    [Fact]
    public void ExactD13CancelFrameParsesClientOwnedLayout()
    {
        var frame = ReadFrame("0B00DC0808000400000000");

        Assert.True(VisibleDx8D13TrainingVitalityProtectPacket.TryParse(frame, out var packet));
        Assert.Equal(
            (ushort)VisibleDx8D13TrainingVitalityProtectAction.CancelRetreat,
            packet.Action);
        Assert.Equal((byte)LegacyChiPowerType.Turtle, packet.Mode);
        Assert.Equal(0u, packet.Reserved);
        Assert.True(packet.HasKnownAction);
        Assert.Equal(Convert.FromHexString("0B00DC0808000400000000"), packet.ToFrameBytes());
    }

    [Theory]
    [InlineData("0A00DC08020003000000")]
    [InlineData("0C00DC08020003000000000000")]
    [InlineData("0B00DD0802000300000000")]
    public void WrongLengthOrOpcodeDoesNotParse(string frameHex)
    {
        Assert.False(
            VisibleDx8D13TrainingVitalityProtectPacket.TryParse(
                ReadFrame(frameHex),
                out _));
    }

    [Fact]
    public async Task VisibleDx8D13DispatchConsumesExactFrameWithoutMutation()
    {
        using var log = new StringWriter();
        var disconnectCount = 0;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: log);

        await handler.HandleFrameAsync(
            CreateConnection(() => disconnectCount++),
            ReadFrame("0B00DC0802000300000000"));

        Assert.Equal(0, disconnectCount);
        var output = log.ToString();
        Assert.Contains(
            "WIRE-EVIDENCE VisibleDx8D13 CMsgTrainingVitalityProtect",
            output);
        Assert.Contains("opcode=2268 frameLength=11 action=2 mode=3", output);
        Assert.Contains("reserved=0 knownAction=True mutation=none", output);
        Assert.DoesNotContain("unknown world opcode", output);
    }

    [Fact]
    public async Task VisibleDx8D13DispatchLogsUnknownActionWithoutApplyingOldSourceBehavior()
    {
        using var log = new StringWriter();
        var disconnectCount = 0;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: log);

        await handler.HandleFrameAsync(
            CreateConnection(() => disconnectCount++),
            ReadFrame("0B00DC0809000000000000"));

        Assert.Equal(0, disconnectCount);
        Assert.Contains(
            "action=9 mode=0 reserved=0 knownAction=False mutation=none",
            log.ToString());
    }

    [Fact]
    public async Task VisibleDx8D13RestoreWithoutSessionRemainsMutationFree()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: log);
        var packet = new VisibleDx8D13TrainingVitalityProtectPacket(
            (ushort)VisibleDx8D13TrainingVitalityProtectAction.Restore,
            Mode: (byte)LegacyChiPowerType.Dragon,
            Reserved: 0);

        await handler.HandleFrameAsync(
            CreateConnection(static () => { }),
            ReadFrame(packet.ToFrameBytes()));

        Assert.Contains(
            "action=4 mode=1 reserved=0 knownAction=True mutation=none reason=sessionMissing",
            log.ToString());
    }

    [Fact]
    public async Task VisibleDx8D13MalformedLengthIsConsumedBeforeAnyLegacyHandler()
    {
        using var log = new StringWriter();
        var disconnectCount = 0;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: log);

        await handler.HandleFrameAsync(
            CreateConnection(() => disconnectCount++),
            ReadFrame("0A00DC08020003000000"));

        Assert.Equal(0, disconnectCount);
        var output = log.ToString();
        Assert.Contains("PROTOCOL-MALFORMED consumed world packet", output);
        Assert.Contains("profile=VisibleDx8D13 session=none opcode=2268", output);
        Assert.Contains("packet=CMsgTrainingVitalityProtect action=ignore", output);
        Assert.DoesNotContain("WIRE-EVIDENCE", output);
    }

    [Theory]
    [InlineData((ushort)VisibleDx8D13TrainingVitalityProtectAction.Seal)]
    [InlineData((ushort)VisibleDx8D13TrainingVitalityProtectAction.Restore)]
    [InlineData((ushort)VisibleDx8D13TrainingVitalityProtectAction.CancelRetreat)]
    public async Task RootD11DoesNotReuseD13OpcodeOrLayout(ushort action)
    {
        using var log = new StringWriter();
        var disconnectCount = 0;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.RootD11_7899,
            log: log);

        await handler.HandleFrameAsync(
            CreateConnection(() => disconnectCount++),
            ReadFrame(
                new VisibleDx8D13TrainingVitalityProtectPacket(
                    action,
                    Mode: (byte)LegacyChiPowerType.Tiger,
                    Reserved: 0)
                .ToFrameBytes()));

        Assert.Equal(0, disconnectCount);
        var output = log.ToString();
        Assert.Contains("PARITY-GAP unknown world opcode", output);
        Assert.Contains("opcode=2268", output);
        Assert.DoesNotContain("CMsgTrainingVitalityProtect", output);
    }

    [Fact]
    public async Task VisibleDx8D13SealPersistsCurrentPowerBeforeClientAcknowledgement()
    {
        var usersRoot = Path.Combine(
            Path.GetTempPath(),
            "WolfConquer-D13ChiSeal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            const long uid = 1_293_600;
            var userPath = Path.Combine(usersRoot, uid + ".ini");
            File.WriteAllText(
                userPath,
                "[Character]\r\n" +
                $"UID={uid}\r\n" +
                "Dragon=1/100/1/10/0/2/20/1/3/30/0/4/40/1/\r\n" +
                "DragonTime=1\r\n" +
                "RetreatedDragon=9-90|10-100|11-110|12-120\r\n");

            using var log = new StringWriter();
            var sent = new List<byte[]>();
            var connection = new ConquerTcpConnection(
                localEndPoint: null,
                remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (bytes, _) =>
                {
                    sent.Add(bytes.ToArray());
                    return ValueTask.CompletedTask;
                });
            var handler = new WorldServerFrameHandler(
                snapshotLoader: null,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
                log: log);
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Dragon"] = "1/100/1/10/0/2/20/1/3/30/0/4/40/1/",
                ["DragonTime"] = "1",
                ["RetreatedDragon"] = "9-90|10-100|11-110|12-120",
            };
            GetSessions(handler).Set(
                connection,
                CreateSession(uid, usersRoot, userPath, attributes));

            await handler.HandleFrameAsync(
                connection,
                ReadFrame("0B00DC0802000100000000"));

            Assert.Single(sent);
            Assert.True(
                VisibleDx8D13TrainingVitalityProtectPacket.TryParseResponse(
                    ReadFrame(sent[0]),
                    out var acknowledgement));
            Assert.Equal(
                (ushort)VisibleDx8D13TrainingVitalityProtectAction.SealAcknowledged,
                acknowledgement.Action);
            Assert.Equal((byte)LegacyChiPowerType.Dragon, acknowledgement.Mode);
            Assert.Equal(0u, acknowledgement.Reserved);

            Assert.True(GetSessions(handler).TryGet(connection, out var updated));
            Assert.NotNull(updated);
            Assert.Equal("1-10|2-20|3-30|4-40", updated.Character.Attributes["RetreatedDragon"]);
            Assert.Contains("RetreatedDragon=1-10|2-20|3-30|4-40", File.ReadAllText(userPath));
            Assert.Contains("mutation=seal reason=sealed", log.ToString());
        }
        finally
        {
            Directory.Delete(usersRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("0B00DC0802000000000000", "modeInvalid")]
    [InlineData("0B00DC0802000101000000", "reservedNonzero")]
    public async Task VisibleDx8D13SealRejectsUnprovenFieldsWithoutMutation(
        string frameHex,
        string reason)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (bytes, _) =>
            {
                sent.Add(bytes.ToArray());
                return ValueTask.CompletedTask;
            });
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: log);

        await handler.HandleFrameAsync(connection, ReadFrame(frameHex));

        Assert.Empty(sent);
        Assert.Contains($"mutation=none reason={reason}", log.ToString());
    }

    [Theory]
    [InlineData(LegacyChiPowerType.Dragon, "DragonTime", "RetreatedDragon")]
    [InlineData(LegacyChiPowerType.Phoenix, "PhoenixTime", "RetreatedPhoenix")]
    [InlineData(LegacyChiPowerType.Tiger, "TigerTime", "RetreatedTiger")]
    [InlineData(LegacyChiPowerType.Turtle, "TurtleTime", "RetreatedTurtle")]
    public async Task VisibleDx8D13CancelClearsSelectedRetreatAndPersistsBeforeExactAcknowledgement(
        LegacyChiPowerType powerType,
        string timeKey,
        string retreatedKey)
    {
        var usersRoot = Path.Combine(
            Path.GetTempPath(),
            "WolfConquer-D13ChiCancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            const long uid = 1_293_600;
            const string savedRetreat = "9-90|10-100|11-110|12-120";
            var userPath = Path.Combine(usersRoot, uid + ".ini");
            File.WriteAllText(
                userPath,
                "[Character]\r\n" +
                $"UID={uid}\r\n" +
                $"{timeKey}=1\r\n" +
                $"{retreatedKey}={savedRetreat}\r\n");

            bool? persistedBeforeAcknowledgement = null;
            var sent = new List<byte[]>();
            var connection = new ConquerTcpConnection(
                localEndPoint: null,
                remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (bytes, _) =>
                {
                    var persisted = IniDocument.Parse(File.ReadAllText(userPath))
                        .GetSection("Character");
                    persistedBeforeAcknowledgement =
                        persisted[timeKey] == "0" && persisted[retreatedKey] == string.Empty;
                    sent.Add(bytes.ToArray());
                    return ValueTask.CompletedTask;
                });
            var handler = new WorldServerFrameHandler(
                snapshotLoader: null,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
                log: TextWriter.Null);
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [timeKey] = "1",
                [retreatedKey] = savedRetreat,
            };
            GetSessions(handler).Set(
                connection,
                CreateSession(uid, usersRoot, userPath, attributes));

            var request = new VisibleDx8D13TrainingVitalityProtectPacket(
                (ushort)VisibleDx8D13TrainingVitalityProtectAction.CancelRetreat,
                (byte)powerType,
                Reserved: 0);
            await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

            Assert.True(persistedBeforeAcknowledgement);
            var responseBytes = Assert.Single(sent);
            Assert.Equal(VisibleDx8D13TrainingVitalityProtectPacket.FrameLength, responseBytes.Length);
            Assert.Equal(request.ToResponseFrameBytes(), responseBytes);
            Assert.True(
                VisibleDx8D13TrainingVitalityProtectPacket.TryParseResponse(
                    ReadFrame(responseBytes),
                    out var acknowledgement));
            Assert.Equal(
                (ushort)VisibleDx8D13TrainingVitalityProtectAction.CancelRetreat,
                acknowledgement.Action);
            Assert.Equal((byte)powerType, acknowledgement.Mode);
            Assert.Equal(0u, acknowledgement.Reserved);

            Assert.True(GetSessions(handler).TryGet(connection, out var updated));
            Assert.NotNull(updated);
            Assert.Equal("0", updated.Character.Attributes[timeKey]);
            Assert.Equal(string.Empty, updated.Character.Attributes[retreatedKey]);

            var persistedAfterAcknowledgement = IniDocument.Parse(File.ReadAllText(userPath))
                .GetSection("Character");
            Assert.Equal("0", persistedAfterAcknowledgement[timeKey]);
            Assert.Equal(string.Empty, persistedAfterAcknowledgement[retreatedKey]);
        }
        finally
        {
            Directory.Delete(usersRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("0B00DC0808000000000000")]
    [InlineData("0B00DC0808000101000000")]
    public async Task VisibleDx8D13CancelRejectsInvalidModeOrReservedWithoutMutationOrAcknowledgement(
        string frameHex)
    {
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (bytes, _) =>
            {
                sent.Add(bytes.ToArray());
                return ValueTask.CompletedTask;
            });
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: TextWriter.Null);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DragonTime"] = "1",
            ["RetreatedDragon"] = "9-90|10-100|11-110|12-120",
        };
        var original = CreateSession(1_293_600, null, "fixture", attributes);
        GetSessions(handler).Set(connection, original);

        await handler.HandleFrameAsync(connection, ReadFrame(frameHex));

        Assert.Empty(sent);
        Assert.True(GetSessions(handler).TryGet(connection, out var unchanged));
        Assert.NotNull(unchanged);
        Assert.Same(original, unchanged);
        Assert.Equal("1", unchanged.Character.Attributes["DragonTime"]);
        Assert.Equal(
            "9-90|10-100|11-110|12-120",
            unchanged.Character.Attributes["RetreatedDragon"]);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("0", "")]
    public async Task VisibleDx8D13CancelRejectsMissingSelectedSavedRetreatWithoutMutationOrAcknowledgement(
        string? retreatTime,
        string? retreatedPower)
    {
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (bytes, _) =>
            {
                sent.Add(bytes.ToArray());
                return ValueTask.CompletedTask;
            });
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: TextWriter.Null);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (retreatTime is not null)
        {
            attributes["DragonTime"] = retreatTime;
        }
        if (retreatedPower is not null)
        {
            attributes["RetreatedDragon"] = retreatedPower;
        }

        var original = CreateSession(1_293_600, null, "fixture", attributes);
        GetSessions(handler).Set(connection, original);
        var request = new VisibleDx8D13TrainingVitalityProtectPacket(
            (ushort)VisibleDx8D13TrainingVitalityProtectAction.CancelRetreat,
            (byte)LegacyChiPowerType.Dragon,
            Reserved: 0);

        await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(GetSessions(handler).TryGet(connection, out var unchanged));
        Assert.Same(original, unchanged);
    }

    [Theory]
    [InlineData(LegacyChiPowerType.Dragon, "DragonTime", "RetreatedDragon", "Dragon")]
    [InlineData(LegacyChiPowerType.Phoenix, "PhoenixTime", "RetreatedPhoenix", "Pheonix")]
    [InlineData(LegacyChiPowerType.Tiger, "TigerTime", "RetreatedTiger", "Tiger")]
    [InlineData(LegacyChiPowerType.Turtle, "TurtleTime", "RetreatedTurtle", "Turtle")]
    public async Task VisibleDx8D13RestorePersistsThenSendsTwoExactChiRefreshesAndAcknowledgement(
        LegacyChiPowerType powerType,
        string timeKey,
        string retreatedKey,
        string powerKey)
    {
        var usersRoot = Path.Combine(
            Path.GetTempPath(),
            "WolfConquer-D13ChiRestore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            const long uid = 1_293_600;
            const string currentPower = "1/777/1/10/1/2/20/1/3/30/1/4/40/1/";
            const string restoredPower = "1/777/9/90/0/10/100/0/11/110/0/12/120/0/";
            var userPath = Path.Combine(usersRoot, uid + ".ini");
            File.WriteAllText(
                userPath,
                "[Character]\r\n" +
                $"UID={uid}\r\n" +
                "ChiPoints=153850\r\n" +
                $"{powerKey}={currentPower}\r\n" +
                $"{timeKey}=1\r\n" +
                $"{retreatedKey}=9-90|10-100|11-110|12-120\r\n");

            var sent = new List<byte[]>();
            bool? persistedBeforeFirstSend = null;
            var connection = new ConquerTcpConnection(
                localEndPoint: null,
                remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (bytes, _) =>
                {
                    if (sent.Count == 0)
                    {
                        var persisted = IniDocument.Parse(File.ReadAllText(userPath))
                            .GetSection("Character");
                        persistedBeforeFirstSend = persisted[powerKey] == restoredPower;
                    }
                    sent.Add(bytes.ToArray());
                    return ValueTask.CompletedTask;
                });
            using var log = new StringWriter();
            var handler = new WorldServerFrameHandler(
                snapshotLoader: null,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
                log: log);
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ChiPoints"] = "153850",
                [powerKey] = currentPower,
                [timeKey] = "1",
                [retreatedKey] = "9-90|10-100|11-110|12-120",
            };
            GetSessions(handler).Set(
                connection,
                CreateSession(uid, usersRoot, userPath, attributes));

            var request = new VisibleDx8D13TrainingVitalityProtectPacket(
                (ushort)VisibleDx8D13TrainingVitalityProtectAction.Restore,
                (byte)powerType,
                Reserved: 0);
            await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

            Assert.True(persistedBeforeFirstSend);
            Assert.Equal(3, sent.Count);
            Assert.True(
                VisibleDx8D13TrainingVitalityInfoPacket.TryParse(
                    ReadFrame(sent[0]),
                    out var sendInfo));
            Assert.Equal(LegacyChiInfoAction.Send, sendInfo.Action);
            var sendPower = Assert.Single(sendInfo.Powers, power => power.Type == powerType);
            Assert.Equal(
                [
                    new LegacyChiAttributeState(9, 90, Locked: false),
                    new LegacyChiAttributeState(10, 100, Locked: false),
                    new LegacyChiAttributeState(11, 110, Locked: false),
                    new LegacyChiAttributeState(12, 120, Locked: false),
                ],
                sendPower.Attributes);

            Assert.True(
                VisibleDx8D13TrainingVitalityInfoPacket.TryParse(
                    ReadFrame(sent[1]),
                    out var upgradeInfo));
            Assert.Equal(LegacyChiInfoAction.Upgrade, upgradeInfo.Action);
            var upgradePower = Assert.Single(upgradeInfo.Powers, power => power.Type == powerType);
            Assert.Equal(sendPower.Experience, upgradePower.Experience);
            Assert.Equal(sendPower.Attributes, upgradePower.Attributes);

            Assert.True(
                VisibleDx8D13TrainingVitalityProtectPacket.TryParseResponse(
                    ReadFrame(sent[2]),
                    out var acknowledgement));
            Assert.Equal(
                (ushort)VisibleDx8D13TrainingVitalityProtectAction.RestoreAcknowledged,
                acknowledgement.Action);
            Assert.Equal((byte)powerType, acknowledgement.Mode);
            Assert.Equal(0u, acknowledgement.Reserved);

            Assert.True(GetSessions(handler).TryGet(connection, out var updated));
            Assert.NotNull(updated);
            Assert.Equal(restoredPower, updated.Character.Attributes[powerKey]);
            Assert.Contains("mutation=restore reason=restored", log.ToString());
        }
        finally
        {
            Directory.Delete(usersRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("0B00DC0804000000000000", "modeInvalid")]
    [InlineData("0B00DC0804000101000000", "reservedNonzero")]
    public async Task VisibleDx8D13RestoreRejectsInvalidModeOrReservedWithoutAcknowledgement(
        string frameHex,
        string reason)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (bytes, _) =>
            {
                sent.Add(bytes.ToArray());
                return ValueTask.CompletedTask;
            });
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: log);

        await handler.HandleFrameAsync(connection, ReadFrame(frameHex));

        Assert.Empty(sent);
        Assert.Contains($"mutation=none reason={reason}", log.ToString());
    }

    private static WorldSessionStore GetSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static WorldSession CreateSession(
        long uid,
        string? usersRoot,
        string sourceFile,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new WorldSession(
            AccountId: 2,
            Username: "chi-seal",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: "ChiSeal",
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: sourceFile,
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static ConquerTcpConnection CreateConnection(Action disconnect)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            disconnectAsync: _ =>
            {
                disconnect();
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(string frameHex)
    {
        var sequence = new ReadOnlySequence<byte>(Convert.FromHexString(frameHex));
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
