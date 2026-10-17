using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class ServerStartupModeTests
{
    private static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(30);
    private const string HandoffSigningSecret = "test-handoff-signing-secret";

    public static TheoryData<string, string, int, string> ServerApps => new()
    {
        {
            ConquerRuntimeDefaults.AccountServer.Role,
            ConquerRuntimeDefaults.AccountServer.Address,
            ConquerRuntimeDefaults.AccountServer.Port,
            Path.Combine("src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj")
        },
        {
            ConquerRuntimeDefaults.Gateway.Role,
            ConquerRuntimeDefaults.Gateway.Address,
            ConquerRuntimeDefaults.Gateway.Port,
            Path.Combine("src", "WolfConquer.Gateway", "WolfConquer.Gateway.csproj")
        },
        {
            ConquerRuntimeDefaults.WorldServer.Role,
            ConquerRuntimeDefaults.WorldServer.Address,
            ConquerRuntimeDefaults.WorldServer.Port,
            Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj")
        },
    };

    [Theory]
    [MemberData(nameof(ServerApps))]
    public async Task DefaultStartupModePrintsPlannedEndpointAndExits(
        string role,
        string address,
        int port,
        string projectRelativePath)
    {
        ConsoleRunResult result = await RunServerAppAsync(projectRelativePath);

        Assert.Equal(0, result.ExitCode);
        string[] outputLines = SplitLines(result.StandardOutput);
        Assert.Contains($"{role} for {ConquerRuntimeDefaults.ServerName}", outputLines);
        Assert.Contains($"Planned listen endpoint: {address}:{port}", outputLines);
        Assert.Contains($"Frame size limit: {ConquerRuntimeDefaults.ReceiveSize} bytes", outputLines);
        Assert.DoesNotContain("World forward endpoint:", result.StandardOutput);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    [Fact]
    public void StartupModeDefaultsRemainRayzoWithKnownEndpointPorts()
    {
        Assert.Equal("TestSrv", ConquerRuntimeDefaults.ServerName);
        Assert.Equal("127.0.0.1", ConquerRuntimeDefaults.ServerAddress);
        Assert.Equal(9960, ConquerRuntimeDefaults.AccountServer.Port);
        Assert.Equal(2021, ConquerRuntimeDefaults.Gateway.Port);
        Assert.Equal(5818, ConquerRuntimeDefaults.WorldServer.Port);
    }

    [Fact]
    public async Task WorldServerStartupCanSelectLegacyGameWireMode()
    {
        ConsoleRunResult result = await RunServerAppAsync(
            Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj"),
            arguments: ["--legacy-game-wire"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Wire mode: LegacyGameServer", result.StandardOutput);
        Assert.Contains("Pass --listen to start accepting TCP clients.", result.StandardOutput);
    }

    [Fact]
    public async Task WorldServerStartupReportsUnavailableAccountEmailAuthorityWhenUnset()
    {
        ConsoleRunResult result = await RunServerAppAsync(
            Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "Account email verifier: unavailable; Security Anima email checks will fail closed.",
            result.StandardOutput);
    }

    [Fact]
    public async Task WorldServerStartupAcceptsDedicatedAccountEmailAuthority()
    {
        const string emailConnection =
            "Server=127.0.0.1;Port=65535;Database=email-fixture;User ID=test;Password=not-used;";
        ConsoleRunResult result = await RunServerAppAsync(
            Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj"),
            new Dictionary<string, string?>
            {
                ["WOLFCONQUER_ACCOUNT_EMAIL_MYSQL"] = emailConnection,
            });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Account email verifier: MySQL authority configured.", result.StandardOutput);
        Assert.DoesNotContain(emailConnection, result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountServerStartupCanSelectLegacyAccountWireMode()
    {
        ConsoleRunResult result = await RunServerAppAsync(
            Path.Combine("src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj"),
            arguments: ["--legacy-account-wire"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Wire mode: LegacyAccountServer", result.StandardOutput);
        Assert.Contains("Pass --listen to start accepting TCP clients.", result.StandardOutput);
    }

    [Fact]
    public async Task AccountServerStartupCanUseLegacyAccServerLogRepositoryWhenMysqlIsUnset()
    {
        var logPath = Path.Combine(Path.GetTempPath(), "wolf-acc-log-" + Guid.NewGuid() + ".txt");
        try
        {
            await File.WriteAllTextAsync(
                logPath,
                "UserName[Acc] |user02| UIDAccount |1296473 | IPAccount | 192.168.84.1 |  PasswordAccount  [fixture-pw] [The Clock] 19:36:16");

            ConsoleRunResult result = await RunServerAppAsync(
                Path.Combine("src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj"),
                new Dictionary<string, string?>
                {
                    ["WOLFCONQUER_ACCOUNT_LOG_PATHS"] = logPath,
                },
                arguments: ["--legacy-account-wire"]);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Account repository: using legacy AccServer logs (1 files).", result.StandardOutput);
            Assert.Contains("Wire mode: LegacyAccountServer", result.StandardOutput);
            Assert.Contains("Pass --listen to start accepting TCP clients.", result.StandardOutput);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        }
        finally
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }

    [Fact]
    public async Task AccountServerLegacyCaptureOverrideRequiresExplicitOptIn()
    {
        ConsoleRunResult result = await RunServerAppAsync(
            Path.Combine("src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj"),
            new Dictionary<string, string?>
            {
                ["WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER"] = "1293600",
            },
            arguments: ["--legacy-account-wire"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER requires explicit opt-in", result.StandardError);
        Assert.Contains("WOLFCONQUER_ALLOW_LEGACY_ACCOUNT_CAPTURE_OVERRIDE=1, true, or yes", result.StandardError);
        Assert.DoesNotContain("Legacy account capture override: Ready identifier", result.StandardOutput);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("yes")]
    public async Task AccountServerLegacyCaptureOverrideAllowsExplicitOptIn(string allowValue)
    {
        ConsoleRunResult result = await RunServerAppAsync(
            Path.Combine("src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj"),
            new Dictionary<string, string?>
            {
                ["WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER"] = "1293600",
                ["WOLFCONQUER_ALLOW_LEGACY_ACCOUNT_CAPTURE_OVERRIDE"] = allowValue,
            },
            arguments: ["--legacy-account-wire"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Legacy account capture override: Ready identifier 1293600", result.StandardOutput);
        Assert.Contains("Pass --listen to start accepting TCP clients.", result.StandardOutput);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    [Fact]
    public async Task GatewayStartupCanSelectGuardShieldWireMode()
    {
        ConsoleRunResult result = await RunServerAppAsync(
            Path.Combine("src", "WolfConquer.Gateway", "WolfConquer.Gateway.csproj"),
            arguments: ["--guard-shield-wire"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Wire mode: LegacyGuardShield", result.StandardOutput);
        Assert.Contains("Pass --listen to start accepting TCP clients.", result.StandardOutput);
    }

    [Fact]
    public async Task GatewayGuardShieldStartupReportsBanPolicyFromConfiguredIni()
    {
        var path = Path.Combine(Path.GetTempPath(), "wolf-guards-startup-" + Guid.NewGuid() + ".ini");
        try
        {
            File.WriteAllText(
                path,
                """
                [LoaderSystem]
                ServerIP=127.0.0.1

                [HashSystem]
                ConquerEXEHash=exe-md5
                MagicEffectHash=effect-md5
                MagicTypeHash=type-md5
                StrResHash=strres-md5

                [BanHourSystem]
                UseSpeed=6
                OpenProgram=4
                AutoClicker=2
                """);

            ConsoleRunResult result = await RunServerAppAsync(
                Path.Combine("src", "WolfConquer.Gateway", "WolfConquer.Gateway.csproj"),
                new Dictionary<string, string?>
                {
                    [LegacyGuardShieldTextProtocolOptions.IniPathEnvironmentVariable] = path,
                },
                arguments: ["--guard-shield-wire"]);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"GuardShield INI: {Path.GetFullPath(path)}", result.StandardOutput);
            Assert.Contains("GuardShield ban policy: UseSpeed=6h OpenProgram=4h AutoClicker=2h", result.StandardOutput);
            Assert.Contains("GuardShield effective ban hours: Clicker=2h Program=2h Speed=2h", result.StandardOutput);
            Assert.Contains("Pass --listen to start accepting TCP clients.", result.StandardOutput);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task AccountServerStartupAcceptsWorldEndpointOverrideWithoutListening()
    {
        ConsoleRunResult result = await RunServerAppAsync(
            Path.Combine("src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj"),
            arguments: ["--world-endpoint", "127.0.0.1:15818"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("World forward endpoint: 127.0.0.1:15818", result.StandardOutput);
        Assert.Contains(
            $"Planned listen endpoint: {ConquerRuntimeDefaults.AccountServer.Address}:{ConquerRuntimeDefaults.AccountServer.Port}",
            result.StandardOutput);
        Assert.Contains("Pass --listen to start accepting TCP clients.", result.StandardOutput);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    [Fact]
    public void ExplicitListenModeIsSpecifiedForFutureBoundedServerStartupTests()
    {
        string[] commandLines =
        [
            FutureStartupCommand("WolfConquer.AccountServer"),
            FutureStartupCommand("WolfConquer.Gateway"),
            FutureStartupCommand("WolfConquer.WorldServer"),
        ];

        Assert.Equal(
            [
                "dotnet run --project src/WolfConquer.AccountServer/WolfConquer.AccountServer.csproj -- --listen --address 127.0.0.1 --port 0",
                "dotnet run --project src/WolfConquer.Gateway/WolfConquer.Gateway.csproj -- --listen --address 127.0.0.1 --port 0",
                "dotnet run --project src/WolfConquer.WorldServer/WolfConquer.WorldServer.csproj -- --listen --address 127.0.0.1 --port 0",
            ],
            commandLines);
    }

    [Theory]
    [MemberData(nameof(ServerApps))]
    public async Task ListenModeLogsAcceptedClient(
        string role,
        string _,
        int defaultPort,
        string projectRelativePath)
    {
        Assert.InRange(defaultPort, 1, ushort.MaxValue);
        await using var process = await StartListeningServerAppAsync(projectRelativePath);
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", process.Port);

        var acceptedLine = await process.ReadUntilAsync(
            line => line.Contains($"{role} accepted client", StringComparison.Ordinal),
            DefaultStartupTimeout);

        Assert.Contains("accepted client", acceptedLine);
        Assert.Contains("127.0.0.1", acceptedLine);
    }

    [Theory]
    [MemberData(nameof(ServerApps))]
    public async Task ListenModeAcceptsPacketAndLogsOpcode(
        string role,
        string _,
        int defaultPort,
        string projectRelativePath)
    {
        Assert.InRange(defaultPort, 1, ushort.MaxValue);
        await using var process = await StartListeningServerAppAsync(projectRelativePath);
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", process.Port);
        var packet = new PacketWriter(1010, payloadLength: 2);
        packet.WriteUInt16(0, 0x1234);

        await client.GetStream().WriteAsync(packet.ToArray());

        var expectedOutput = role switch
        {
            var world when string.Equals(world, ConquerRuntimeDefaults.WorldServer.Role, StringComparison.Ordinal) =>
                "PARITY-GAP unknown world opcode",
            var account when string.Equals(account, ConquerRuntimeDefaults.AccountServer.Role, StringComparison.Ordinal) =>
                "PARITY-GAP unknown account opcode",
            _ => $"{role} received opcode 1010",
        };
        var receivedLine = await process.ReadUntilAsync(
            line => line.Contains(expectedOutput, StringComparison.Ordinal) &&
                line.Contains("1010", StringComparison.Ordinal),
            DefaultStartupTimeout);

        Assert.Contains(expectedOutput, receivedLine);
        Assert.Contains("1010", receivedLine);
    }

    [Fact]
    public async Task AccountServerListenModeSendsLegacyPasswordSeed()
    {
        await using var process = await StartListeningServerAppAsync(
            Path.Combine("src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj"));
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", process.Port);

        var payload = await ReadServerFirstPayloadAsync(client, 8);

        Assert.True(LegacyAccountPasswordSeedPacket.TryParse(ReadFrame(payload), out _));
    }

    [Fact]
    public async Task WorldServerListenModeSendsCapturedServerFirstLength()
    {
        await using var process = await StartListeningServerAppAsync(
            Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj"));
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", process.Port);

        var payload = await ReadServerFirstPayloadAsync(client, 256);

        Assert.Equal(256, payload.Length);
    }

    [Fact]
    public async Task GatewayListenModeRemainsQuietOnPassiveConnect()
    {
        await using var process = await StartListeningServerAppAsync(
            Path.Combine("src", "WolfConquer.Gateway", "WolfConquer.Gateway.csproj"));
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", process.Port);

        var buffer = new byte[1];
        var readTask = client.GetStream().ReadAsync(buffer).AsTask();
        var completed = await Task.WhenAny(readTask, Task.Delay(250));

        Assert.NotSame(readTask, completed);
    }

    [Fact]
    public async Task GatewayGuardShieldListenModeLogsIgnoredCommandsAsParityGap()
    {
        await using var process = await StartListeningServerAppAsync(
            Path.Combine("src", "WolfConquer.Gateway", "WolfConquer.Gateway.csproj"),
            arguments: ["--guard-shield-wire"]);
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", process.Port);

        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes("Mystery:Value\r\n"));

        var parityLine = await process.ReadUntilAsync(
            line => line.Contains("PARITY-GAP category=unsupported-packet", StringComparison.Ordinal) &&
                line.Contains("gap=guard-shield-command", StringComparison.Ordinal),
            DefaultStartupTimeout);

        Assert.Contains("dedupe=unsupported-packet:guard-shield-command:command=Mystery:status=Ignored", parityLine);
        Assert.Contains("command=Mystery", parityLine);
        Assert.Contains("ignored GuardShield text command is not modeled", parityLine);
    }

    [Fact]
    public async Task AccountServerListenModeUsesAccountHandlerWhenMysqlIsUnset()
    {
        await using var process = await StartListeningServerAppAsync(
            Path.Combine("src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj"));
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", process.Port);
        await ReadServerFirstPayloadAsync(client, 8);

        var request = new InternalAccountLoginPacket("user02", "fixture-pw");
        await client.GetStream().WriteAsync(request.ToFrameBytes());

        var response = new byte[PacketHeader.Size + AccountLoginResponsePacket.PayloadLength];
        await client.GetStream().ReadExactlyAsync(response);

        Assert.True(PacketReader.TryRead(response, out var reader));
        Assert.Equal(AccountPacketOpcodes.InternalLoginResponse, reader.Header.Opcode);
        Assert.Equal(AccountLoginWireStatus.ServiceUnavailable, AccountLoginResponsePacket.ReadStatus(reader));
        Assert.Equal(0, AccountLoginResponsePacket.ReadAccountId(reader));
    }

    [Fact]
    public async Task WorldServerStartupLoadsSeedCatalogFromEnvironment()
    {
        var seedRoot = Path.Combine(Path.GetTempPath(), "wolf-world-startup-" + Guid.NewGuid());
        try
        {
            WriteMinimalSeedCatalog(seedRoot);

            ConsoleRunResult result = await RunServerAppAsync(
                Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj"),
                new Dictionary<string, string?>
                {
                    [WorldDataOptions.SeedJsonEnvironmentVariable] = seedRoot,
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"World data seed root: {Path.GetFullPath(seedRoot)}", result.StandardOutput);
            var loadLine = result.StandardOutput
                .Split(Environment.NewLine)
                .Single(line => line.StartsWith("World data loaded:", StringComparison.Ordinal));
            foreach (var fragment in new[]
            {
                "clientConfigs=1",
                "items=1",
                "maps=1",
                "mapNames=1",
                "monsters=1",
                "collectionRewardTiers=1",
                "taskRewardTypeIniEntries=1",
                "quizQuestions=1",
                "magicTypeOperations=1",
                "neiGongSecrets=1",
                "neiGongTypes=1",
                "subProfessions=1",
                "syndicateLevels=1",
                "runeLevelExperience=1",
                "runeStorageAttributes=1",
                "itemRefineEffects=1",
                "itemRefineEffectEx=1",
                "itemRefineUpgrades=1",
                "refineryMaterials=1",
                "transformInfo=1",
                "furniture=1",
                "coatStorageTypes=1",
                "coatStorageAttributes=1",
                "coatColorRules=1",
                "christmasNpcs=1",
                "beastAttributes=1",
                "fateExperience=1",
                "spiritRates=1",
                "hundredWeapons=1",
                "gouyuTypes=1",
                "gouyuImmortals=1",
                "specialEffectAttributes=1",
                "daoqiDictTypes=1",
                "syndicateFormationTypes=1",
                "syndicateSkillTypes=1",
                "disdainRules=1",
                "jiangHuAttributes=1",
                "jiangHuCultivateStatuses=1",
                "refineryBoxes=1",
                "refinerySouls=1",
                "stateCountFiles=1",
                "binarySeedFiles=1",
                "emptySeedFiles=1",
                "backupSeedFiles=1",
                "pokerTables=1",
                "rouletteTables=1",
                "npcTimeEntries=1",
                "activityTasks=1",
                "meltingTypeRules=1",
                "lotteryPrizes=1",
                "arenaRecords=1",
                "teamArenaRecords=1",
                "elitePkRecords=1",
                "teamElitePkRecords=1",
                "skillTeamPkRecords=1",
                "classPkWarRecords=1",
                "combatGearRanks=1",
                "prestigeRanks=1",
                "innerPowerRanks=1",
                "runeRankings=1",
                "staticStatues=1",
                "newPlayerRewards=1",
                "votes=1",
                "couplesPkRecords=1",
                "socialAssociations=1",
                "guildWarInfoEntries=7",
                "superGuildWarInfoEntries=7",
                "shops=1",
                "booths=1",
            })
            {
                Assert.Contains(fragment, loadLine);
            }
        }
        finally
        {
            if (Directory.Exists(seedRoot))
            {
                Directory.Delete(seedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WorldServerStartupReportsCharacterSnapshotsFromEnvironment()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-db-startup-" + Guid.NewGuid());
        var usersRoot = Path.Combine(dbRoot, "Users");
        var playerItemsRoot = Path.Combine(dbRoot, "PlayersItems");
        var playerSpellsRoot = Path.Combine(dbRoot, "PlayersSpells");
        var playerProficienciesRoot = Path.Combine(dbRoot, "PlayersProfs");
        var playersCombatGearRoot = Path.Combine(dbRoot, "PlayersCombatGear");
        var playerQuestsRoot = Path.Combine(dbRoot, "Quests");
        try
        {
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(playerItemsRoot);
            Directory.CreateDirectory(playerSpellsRoot);
            Directory.CreateDirectory(playerProficienciesRoot);
            Directory.CreateDirectory(playersCombatGearRoot);
            Directory.CreateDirectory(playerQuestsRoot);
            File.WriteAllText(Path.Combine(usersRoot, "1293600.ini"), "[Character]\r\nUID=1293600\r\n");
            File.WriteAllBytes(Path.Combine(playerItemsRoot, "1293600.bin"), []);
            File.WriteAllBytes(Path.Combine(playerSpellsRoot, "1293600.bin"), []);
            File.WriteAllBytes(Path.Combine(playerProficienciesRoot, "1293600.bin"), []);
            File.WriteAllText(Path.Combine(playersCombatGearRoot, "1293600.ini"), "");
            File.WriteAllBytes(Path.Combine(playerQuestsRoot, "1293600.bin"), []);

            ConsoleRunResult result = await RunServerAppAsync(
                Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj"),
                new Dictionary<string, string?>
                {
                    [CharacterSnapshotOptions.UsersRootEnvironmentVariable] = usersRoot,
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Character snapshot users root: {Path.GetFullPath(usersRoot)}", result.StandardOutput);
            Assert.Contains("Character snapshots available: 1", result.StandardOutput);
            Assert.Contains($"Player item snapshots root: {Path.GetFullPath(playerItemsRoot)}", result.StandardOutput);
            Assert.Contains("Player item snapshots available: 1", result.StandardOutput);
            Assert.Contains($"Player spell snapshots root: {Path.GetFullPath(playerSpellsRoot)}", result.StandardOutput);
            Assert.Contains("Player spell snapshots available: 1", result.StandardOutput);
            Assert.Contains(
                $"Player proficiency snapshots root: {Path.GetFullPath(playerProficienciesRoot)}",
                result.StandardOutput);
            Assert.Contains("Player proficiency snapshots available: 1", result.StandardOutput);
            Assert.Contains(
                $"Player combat gear snapshots root: {Path.GetFullPath(playersCombatGearRoot)}",
                result.StandardOutput);
            Assert.Contains("Player combat gear snapshots available: 1", result.StandardOutput);
            Assert.Contains($"Player quest snapshots root: {Path.GetFullPath(playerQuestsRoot)}", result.StandardOutput);
            Assert.Contains("Player quest snapshots available: 1", result.StandardOutput);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WorldServerListenModeReturnsInternalCharacterSnapshot()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-users-listen-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(usersRoot);
            WriteCharacterSnapshot(usersRoot, accountId: 1293600, uid: 1293600);

            await using var process = await StartListeningServerAppAsync(
                Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj"),
                new Dictionary<string, string?>
                {
                    [CharacterSnapshotOptions.UsersRootEnvironmentVariable] = usersRoot,
                });
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", process.Port);
            await ReadServerFirstPayloadAsync(client, 256);

            await client.GetStream().WriteAsync(new InternalWorldSnapshotRequestPacket(1293600).ToFrameBytes());

            var response = new byte[PacketHeader.Size + WorldSnapshotResponsePacket.PayloadLength];
            await client.GetStream().ReadExactlyAsync(response);

            Assert.True(PacketReader.TryRead(response, out var reader));
            Assert.Equal(WorldPacketOpcodes.InternalSnapshotResponse, reader.Header.Opcode);
            Assert.Equal(WorldSnapshotWireStatus.Success, WorldSnapshotResponsePacket.ReadStatus(reader));
            Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadAccountId(reader));
            Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadUid(reader));
            Assert.Equal(1015, WorldSnapshotResponsePacket.ReadMapId(reader));
            Assert.Equal(830, WorldSnapshotResponsePacket.ReadX(reader));
            Assert.Equal(584, WorldSnapshotResponsePacket.ReadY(reader));
            Assert.Equal(140, WorldSnapshotResponsePacket.ReadLevel(reader));
            Assert.Equal(7051, WorldSnapshotResponsePacket.ReadClass(reader));
        }
        finally
        {
            if (Directory.Exists(usersRoot))
            {
                Directory.Delete(usersRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WorldServerListenModeRedeemsFileBackedHandoffTicketAndReturnsSnapshot()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-users-handoff-listen-" + Guid.NewGuid());
        var handoffRoot = Path.Combine(Path.GetTempPath(), "wolf-handoff-listen-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(usersRoot);
            WriteCharacterSnapshot(usersRoot, accountId: 1293600, uid: 1293600);
            var handoffStore = new FileAccountHandoffTicketStore(handoffRoot, HandoffSigningSecret);
            var handoff = new AccountHandoffService(handoffStore);
            var ticket = handoff.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);

            await using var process = await StartListeningServerAppAsync(
                Path.Combine("src", "WolfConquer.WorldServer", "WolfConquer.WorldServer.csproj"),
                new Dictionary<string, string?>
                {
                    [CharacterSnapshotOptions.UsersRootEnvironmentVariable] = usersRoot,
                    [AccountHandoffOptions.StoreRootEnvironmentVariable] = handoffRoot,
                    [AccountHandoffOptions.SigningSecretEnvironmentVariable] = HandoffSigningSecret,
                });
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", process.Port);
            await ReadServerFirstPayloadAsync(client, 256);

            await client.GetStream().WriteAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes());

            var response = new byte[PacketHeader.Size + WorldSnapshotResponsePacket.PayloadLength];
            await client.GetStream().ReadExactlyAsync(response);

            Assert.True(PacketReader.TryRead(response, out var reader));
            Assert.Equal(WorldPacketOpcodes.InternalSnapshotResponse, reader.Header.Opcode);
            Assert.Equal(WorldSnapshotWireStatus.Success, WorldSnapshotResponsePacket.ReadStatus(reader));
            Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadAccountId(reader));
            Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadUid(reader));
            Assert.Equal(1015, WorldSnapshotResponsePacket.ReadMapId(reader));
            Assert.Equal(830, WorldSnapshotResponsePacket.ReadX(reader));
            Assert.Equal(584, WorldSnapshotResponsePacket.ReadY(reader));
            Assert.Equal(140, WorldSnapshotResponsePacket.ReadLevel(reader));
            Assert.Equal(7051, WorldSnapshotResponsePacket.ReadClass(reader));
            Assert.True(handoffStore.TryGet(ticket.Token, out var redeemed));
            Assert.True(redeemed.IsRedeemed);
        }
        finally
        {
            if (Directory.Exists(usersRoot))
            {
                Directory.Delete(usersRoot, recursive: true);
            }

            if (Directory.Exists(handoffRoot))
            {
                Directory.Delete(handoffRoot, recursive: true);
            }
        }
    }

    private static async Task<ConsoleRunResult> RunServerAppAsync(
        string projectRelativePath,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyList<string>? arguments = null)
    {
        string repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.CreateFromProjectPath(repositoryRoot, projectRelativePath);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_MYSQL"] = "";
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_EMAIL_MYSQL"] = "";
        process.StartInfo.Environment[ConquerProtocolProfileConfiguration.EnvironmentVariable] =
            nameof(ConquerProtocolProfile.RootD11_7899);
        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                process.StartInfo.Environment[pair.Key] = pair.Value;
            }
        }

        if (arguments is not null)
        {
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
        }

        Assert.True(process.Start(), $"Could not start {projectRelativePath}.");

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(DefaultStartupTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();

            Assert.Fail(
                $"{projectRelativePath} default startup mode did not exit within {DefaultStartupTimeout}. " +
                $"stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static async Task<ListeningServerProcess> StartListeningServerAppAsync(
        string projectRelativePath,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyList<string>? arguments = null)
    {
        string repositoryRoot = FindRepositoryRoot();
        var process = TestProjectProcess.CreateFromProjectPath(repositoryRoot, projectRelativePath, enableRaisingEvents: true);
        process.StartInfo.ArgumentList.Add("--listen");
        process.StartInfo.ArgumentList.Add("--address");
        process.StartInfo.ArgumentList.Add("127.0.0.1");
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add("0");
        if (arguments is not null)
        {
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
        }

        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_MYSQL"] = "";
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_EMAIL_MYSQL"] = "";
        process.StartInfo.Environment[ConquerProtocolProfileConfiguration.EnvironmentVariable] =
            nameof(ConquerProtocolProfile.RootD11_7899);
        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                process.StartInfo.Environment[pair.Key] = pair.Value;
            }
        }

        var ownedSeedRoot =
            string.Equals(Path.GetFileNameWithoutExtension(projectRelativePath), "WolfConquer.WorldServer", StringComparison.Ordinal) &&
            environment?.ContainsKey(WorldDataOptions.SeedJsonEnvironmentVariable) is not true
                ? Path.Combine(Path.GetTempPath(), "wolf-listen-seed-" + Guid.NewGuid())
                : null;
        ListeningServerProcess? server = null;
        try
        {
            if (ownedSeedRoot is not null)
            {
                WriteMinimalSeedCatalog(ownedSeedRoot);
                process.StartInfo.Environment[WorldDataOptions.SeedJsonEnvironmentVariable] = ownedSeedRoot;
            }

            Assert.True(process.Start(), $"Could not start {projectRelativePath}.");
            server = new ListeningServerProcess(process, ownedSeedRoot);
            var listeningLine = await server.ReadUntilAsync(
                line => line.StartsWith("Listening on ", StringComparison.Ordinal),
                DefaultStartupTimeout);
            var match = Regex.Match(listeningLine, @":(?<port>\d+)$");
            Assert.True(match.Success, $"Could not parse listening line: {listeningLine}");
            server.Port = int.Parse(match.Groups["port"].Value);
            return server;
        }
        catch
        {
            if (server is not null)
            {
                await server.DisposeAsync();
            }
            else
            {
                process.Dispose();
                if (ownedSeedRoot is not null && Directory.Exists(ownedSeedRoot))
                {
                    Directory.Delete(ownedSeedRoot, recursive: true);
                }
            }

            throw;
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private static async Task<byte[]> ReadServerFirstPayloadAsync(TcpClient client, int length)
    {
        var payload = new byte[length];
        using var timeout = new CancellationTokenSource(DefaultStartupTimeout);
        await client.GetStream().ReadExactlyAsync(payload, timeout.Token);
        return payload;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static void WriteMinimalSeedCatalog(string seedRoot)
    {
        Directory.CreateDirectory(seedRoot);
        var counts = new Dictionary<string, int>
        {
            ["clientConfigs"] = 1,
            ["items"] = 1,
            ["itemAddBonuses"] = 1,
            ["levelExperience"] = 1,
            ["classLevelStats"] = 1,
            ["professionLevelUps"] = 1,
            ["professionLevelBenefits"] = 1,
            ["professionTitleBenefits"] = 1,
            ["questInfo"] = 1,
            ["questTaskTypes"] = 1,
            ["questMissions"] = 1,
            ["quizQuestions"] = 1,
            ["magic"] = 1,
            ["magicEffects"] = 1,
            ["magicTypeOperations"] = 1,
            ["titleTypes"] = 1,
            ["combatGear"] = 1,
            ["traps"] = 1,
            ["npcs"] = 1,
            ["sobNpcs"] = 1,
            ["portals"] = 1,
            ["maps"] = 1,
            ["mapNames"] = 1,
            ["monsters"] = 1,
            ["monsterSpawns"] = 1,
            ["lotteryDictionary"] = 1,
            ["collectionRewardTiers"] = 1,
            ["taskRewardTypes"] = 1,
            ["taskRewardTypeIniEntries"] = 1,
            ["neiGongHeader"] = 1,
            ["neiGongSecrets"] = 1,
            ["neiGongTypes"] = 1,
            ["subProfessions"] = 1,
            ["syndicateLevels"] = 1,
            ["runeLevelExperience"] = 1,
            ["runeStorageAttributes"] = 1,
            ["itemRefineEffects"] = 1,
            ["itemRefineEffectEx"] = 1,
            ["itemRefineUpgrades"] = 1,
            ["refineryMaterials"] = 1,
            ["transformInfo"] = 1,
            ["furniture"] = 1,
            ["hairColorTypes"] = 1,
            ["hairFaceStorageTypes"] = 1,
            ["coatStorageTypes"] = 1,
            ["coatStorageAttributes"] = 1,
            ["coatColorRules"] = 1,
            ["xuanbaoAdditionAttributes"] = 1,
            ["xuanbaoComposeAttributeLimits"] = 1,
            ["xuanbaoRandomAttributes"] = 1,
            ["christmasNpcs"] = 1,
            ["tutorTypes"] = 1,
            ["tutorBattleLimitTypes"] = 1,
            ["beastAttributes"] = 1,
            ["fateExperience"] = 1,
            ["spiritRates"] = 1,
            ["hundredWeapons"] = 1,
            ["gouyuTypes"] = 1,
            ["gouyuImmortals"] = 1,
            ["specialEffectAttributes"] = 1,
            ["daoqiDictTypes"] = 1,
            ["syndicateFormationTypes"] = 1,
            ["syndicateSkillTypes"] = 1,
            ["disdainRules"] = 1,
            ["jiangHuAttributes"] = 1,
            ["jiangHuCultivateStatuses"] = 1,
            ["refineryBoxes"] = 1,
            ["refinerySouls"] = 1,
            ["stateCountFiles"] = 1,
            ["binarySeedFiles"] = 1,
            ["emptySeedFiles"] = 1,
            ["backupSeedFiles"] = 1,
            ["pokerTables"] = 1,
            ["rouletteTables"] = 1,
            ["npcTimeEntries"] = 1,
            ["activityTasks"] = 1,
            ["meltingTypeRules"] = 1,
            ["lotteryPrizes"] = 1,
            ["arenaRecords"] = 1,
            ["teamArenaRecords"] = 1,
            ["elitePkRecords"] = 1,
            ["teamElitePkRecords"] = 1,
            ["skillTeamPkRecords"] = 1,
            ["classPkWarRecords"] = 1,
            ["combatGearRanks"] = 1,
            ["prestigeRanks"] = 1,
            ["innerPowerRanks"] = 1,
            ["runeRankings"] = 1,
            ["staticStatues"] = 1,
            ["newPlayerRewards"] = 1,
            ["votes"] = 1,
            ["couplesPkRecords"] = 1,
            ["socialAssociations"] = 1,
            ["guildWarInfo"] = 1,
            ["superGuildWarInfo"] = 1,
            ["stageGoalInfo"] = 1,
            ["stageGoalStages"] = 1,
            ["stageGoalTasks"] = 1,
            ["shops"] = 1,
            ["shopItems"] = 1,
            ["exchangeShopGoods"] = 1,
            ["exchangeShopGoodsEx"] = 1,
            ["exchangeShopLevels"] = 1,
            ["honorShopItems"] = 1,
            ["honorShopRecommendations"] = 1,
            ["racePointShopItems"] = 1,
            ["racePointShopRecommendations"] = 1,
            ["goldenLeagueShopItems"] = 1,
            ["goldenLeagueShopRecommendations"] = 1,
            ["goldenLeagueShopConfig"] = 2,
            ["booths"] = 1,
            ["boothItems"] = 1,
        };

        WriteJson(seedRoot, "manifest.json", new SeedManifest(DateTimeOffset.Parse("2026-06-02T05:20:00Z"), "fixture://startup", counts));
        WriteJson(seedRoot, "clientConfigs.json", new[]
        {
            new ClientConfigRecord(
                2,
                "TestSrv",
                3935,
                "0222",
                222,
                "0214",
                214,
                "0001",
                1,
                "0000",
                0,
                1,
                3,
                "127.0.0.1",
                9220,
                "client_config.ini",
                1,
                "2 TestSrv 3935 0222 0214 0001 0000 1 3 127.0.0.1 9220",
                ["2", "TestSrv", "3935", "0222", "0214", "0001", "0000", "1", "3", "127.0.0.1", "9220"]),
        });
        WriteJson(seedRoot, "items.json", new[] { new ItemType(100000, "TestItem", ["100000", "TestItem"]) });
        WriteJson(seedRoot, "itemAddBonuses.json", new[]
        {
            new ItemAddBonus(100000, 1, [0, 0, 0, 2, 0, 1, 0, 0], SourceLine: 1, RawLine: "100000 1 0 0 0 2 0 1 0 0"),
        });
        WriteJson(seedRoot, "levelExperience.json", new[]
        {
            new LevelExperience(0, 1, 120, 2, 3, SourceLine: 1, RawLine: "0 1 120 2 3", Fields: ["0", "1", "120", "2", "3"]),
        });
        WriteJson(seedRoot, "classLevelStats.json", new[]
        {
            new ClassLevelStat(
                "WindWalker",
                Level: 1,
                Strength: 5,
                Agility: 3,
                Vitality: 2,
                Spirit: 0,
                SourceFile: "Stats.ini",
                LineNumber: 2,
                RawKey: "WindWalker[1]",
                RawValue: "5,3,2,0",
                RawLine: "WindWalker[1]=5,3,2,0",
                Fields: ["5", "3", "2", "0"]),
        });
        WriteJson(seedRoot, "professionLevelUps.json", new[]
        {
            new ProfessionLevelUp(
                1,
                1000,
                RequiredLevel: 15,
                RequiredMetempsychosis: 0,
                RequiredMoney: 800,
                RequiredProficiency: 0,
                SourceFile: "prof_lev_up.txt",
                LineNumber: 1,
                RawLine: "1@@1000@@15@@0@@800@@0@@",
                Fields: ["1", "1000", "15", "0", "800", "0", ""]),
        });
        WriteJson(seedRoot, "professionLevelBenefits.json", new[]
        {
            new ProfessionLevelBenefit(
                1,
                1000,
                Rank: 0,
                BonusValues: [0, 70050, 0, 0, 50],
                SourceFile: "prof_lev_benefit.txt",
                LineNumber: 1,
                RawLine: "1@@1000@@0@@0@@70050@@0@@0@@50@@",
                Fields: ["1", "1000", "0", "0", "70050", "0", "0", "50", ""]),
        });
        WriteJson(seedRoot, "professionTitleBenefits.json", new[]
        {
            new ProfessionTitleBenefit(
                1,
                1,
                Rank: 1,
                BonusValues: [61500, 70500, 100500, 120500, 0],
                SourceFile: "prof_title_benefit.txt",
                LineNumber: 1,
                RawLine: "1@@1@@1@@61500@@70500@@100500@@120500@@0@@",
                Fields: ["1", "1", "1", "61500", "70500", "100500", "120500", "0", ""]),
        });
        WriteJson(seedRoot, "questInfo.json", new QuestInfoMetadata(20_000_000, 300, 1));
        WriteJson(seedRoot, "questTaskTypes.json", new[]
        {
            new QuestTaskType(1, "Recruit Quest", "0xFFCFC058", 1, SourceLine: 1, RawIdKey: "TypeId1", RawNameKey: "TypeName1"),
        });
        WriteJson(seedRoot, "questMissions.json", new[]
        {
            new QuestMission(
                1,
                SourceLine: 1,
                TypeId: 1,
                MissionId: 650,
                Name: "The Armorer",
                LevelMin: 8,
                LevelMax: 50,
                Auto: 0,
                First: 1,
                PrerequisiteRaw: "0",
                PrerequisiteMissionIds: [],
                MapId: 1000,
                Profession: "0",
                Sex: 0,
                FinishTime: 0,
                ActivityBeginTime: "0",
                ActivityEndTime: "0",
                CompleteFlag: 2,
                Prize: "",
                IntentionDescription: "Speak with the Armorer.",
                IntentAmount: 1,
                Content: "Have a word with the Armorer.",
                BeginNpcAnchors: [],
                FinishNpcAnchors: [],
                Intentions: [new QuestIntention(1, null, "Intention1", "Talk.", SourceLine: 1)],
                PrizeItems: [],
                Entries: []),
        });
        WriteJson(seedRoot, "quizQuestions.json", new[]
        {
            new QuizQuestionEntry(
                1,
                "In~which~channel~can~all~the~players~in~the~same~server~see~messages?",
                ["Team", "Broadcast", "Guild", "Talk"],
                CorrectOption: 2,
                CorrectAnswer: "Broadcast",
                SourceFile: "quizquestins.ini",
                LineNumber: 1,
                RawLine: "In~which~channel~can~all~the~players~in~the~same~server~see~messages?#Team#Broadcast#Guild#Talk#2",
                Fields: ["In~which~channel~can~all~the~players~in~the~same~server~see~messages?", "Team", "Broadcast", "Guild", "Talk", "2"]),
        });
        WriteJson(seedRoot, "magic.json", new[]
        {
            new MagicType(1, 100, "Thunder", ["1", "100", "Thunder"]),
            new MagicType(2, 1350, "DivineHare", ["2", "1350", "DivineHare"]),
        });
        WriteJson(seedRoot, "magicEffects.json", new[]
        {
            new MagicEffect(
                1,
                Occurrence: 1,
                Name: "Thunder",
                Description: "Upgradable",
                DescriptionEx: "Magic~attack",
                SourceFile: "MagicEffect.ini",
                SourceLine: 1,
                SourceSection: "1",
                Entries:
                [
                    new MagicEffectEntry("Name", "Thunder", "MagicEffect.ini", 2, "Name=Thunder"),
                    new MagicEffectEntry("Desc", "Upgradable", "MagicEffect.ini", 3, "Desc=Upgradable"),
                    new MagicEffectEntry("DescEx", "Magic~attack", "MagicEffect.ini", 4, "DescEx=Magic~attack"),
                ],
                Attributes: new Dictionary<string, string>
                {
                    ["Name"] = "Thunder",
                    ["Desc"] = "Upgradable",
                    ["DescEx"] = "Magic~attack",
                }),
        });
        WriteJson(seedRoot, "magicTypeOperations.json", new[]
        {
            new MagicTypeOperationEntry(
                1,
                Mode: 1,
                Group: 1,
                Level: 1,
                Flag: 2,
                ReferencedMagicTypeIds: [100],
                Values: [1, 1, 1, 1, 2, 100, 0, 0, 0],
                SourceFile: "magictypeop.txt",
                LineNumber: 1,
                RawLine: "1,1,1,1,2,100,0,0,0",
                Fields: ["1", "1", "1", "1", "2", "100", "0", "0", "0"]),
        });
        WriteJson(seedRoot, "titleTypes.json", new[]
        {
            new TitleType(
                9,
                TypeId: 1,
                Name: "Chosen~One",
                Unknown0: 0,
                Unknown1: 0,
                Unknown2: 0,
                Unknown3: 0,
                Score: 500,
                SourceFile: "title_type.txt",
                LineNumber: 2,
                RawLine: "9@@1@@Chosen~One@@0@@0@@0@@0@@500@@",
                Fields: ["9", "1", "Chosen~One", "0", "0", "0", "0", "500", ""]),
        });
        WriteJson(seedRoot, "combatGear.json", new[]
        {
            new CombatGear(
                1,
                GearTypeId: 201,
                Level: 1,
                UpExperience: 300,
                Life: 60,
                Attack: 30,
                Defense: 0,
                UnknownStat0: 0,
                MagicDefense: 0,
                MagicTypeIds: [17640000, 0, 0, 17650000, 0, 0, 0, 0, 0],
                Score: 240,
                Unknown0: 0,
                Unknown1: 0,
                Unknown2: 0,
                SourceFile: "combat_gear.txt",
                LineNumber: 1,
                RawLine: "1@@201@@1@@300@@60@@30@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@240@@0@@0@@0@@",
                Fields: ["1", "201", "1", "300", "60", "30", "0", "0", "0", "17640000", "0", "0", "17650000", "0", "0", "0", "0", "0", "240", "0", "0", "0", ""]),
        });
        WriteJson(seedRoot, "traps.json", new[]
        {
            new Trap(
                44463,
                X: 77,
                Y: 190,
                TrapTypeId: 1618,
                OptionalFlag: 1,
                SourceFile: "Traps.txt",
                LineNumber: 1,
                RawLine: "44463,77,190,1618,1",
                Fields: ["44463", "77", "190", "1618", "1"]),
        });
        WriteJson(seedRoot, "npcs.json", new[] { new Npc(200, 2, 51700, 1000, 10, 20, null) });
        WriteJson(seedRoot, "sobNpcs.json", new[]
        {
            new SobNpc(25005, "Gate", 21, 0, 1000, 85, 70, 0, 0, 1, 0, null, 1, "25005,Gate,21,0,1000,85,70,0,0,1,0", ["25005", "Gate", "21", "0", "1000", "85", "70", "0", "0", "1", "0"]),
        });
        WriteJson(seedRoot, "portals.json", new[] { new Portal(1000, 50, 50, 1000, 60, 60) });
        WriteJson(seedRoot, "maps.json", new[]
        {
            new MapMetadata(
                1000,
                "1000.ini",
                "TwinCity",
                1000,
                "12",
                12,
                1,
                2,
                1000,
                0,
                0,
                0,
                0,
                0,
                "4294967295",
                4_294_967_295,
                new Dictionary<string, string> { ["type"] = "12" }),
        });
        WriteJson(seedRoot, "mapNames.json", new[]
        {
            new MapName(
                1000,
                "Desert",
                SourceFile: "GameMapEx.ini",
                SectionLine: 1,
                NameLine: 2,
                SourceSection: "1000",
                RawNameLine: "Name=Desert"),
        });
        WriteJson(seedRoot, "monsters.json", new[]
        {
            new Monster(10, "Pheasant", new Dictionary<string, string> { ["level"] = "7" }, []),
        });
        WriteJson(seedRoot, "monsterSpawns.json", new[]
        {
            new MonsterSpawn(
                1,
                1000,
                10,
                BoundX: 1,
                BoundY: 2,
                BoundWidth: 3,
                BoundHeight: 4,
                MaxNpc: 5,
                RestSeconds: 6,
                MaxPerGenerator: 7,
                TimerBegin: 0,
                TimerEnd: 0,
                BornX: 0,
                BornY: 0,
                SourceMapFolderId: 1000,
                SourceMonsterName: "Pheasant",
                SourceRelativePath: Path.Combine("1000", "Pheasant", "1.ini"),
                Attributes: new Dictionary<string, string>()),
        });
        WriteJson(seedRoot, "lotteryDictionary.json", new[]
        {
            new LotteryDictionaryEntry(
                1,
                Type: 2,
                SubType: 3,
                Value: 1007,
                Unknown0: 0,
                Weight: 20,
                SourceFile: "dict_lottery.txt",
                LineNumber: 1,
                RawLine: "1@@2@@3@@1007@@0@@20@@",
                Fields: ["1", "2", "3", "1007", "0", "20", ""]),
        });
        WriteJson(seedRoot, "collectionRewardTiers.json", new[]
        {
            new CollectionRewardTier(
                11,
                CollectionId: 700,
                MinInclusive: 8400,
                MaxInclusive: 2_147_483_647,
                RewardType: 1,
                RewardValue: 1500,
                RewardAmount: 600,
                SourceFile: "dict_collection.txt",
                LineNumber: 11,
                RawLine: "11@@700@@8400@@2147483647@@1@@1500@@600@@",
                Fields: ["11", "700", "8400", "2147483647", "1", "1500", "600", ""]),
        });
        WriteJson(seedRoot, "taskRewardTypes.json", new[]
        {
            new TaskRewardTypeEntry(
                755,
                Parameters: [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                RewardValues: [3301408, 3301409, 3308803, 3301410, 3301411, 3301412, 3301413, 3301414],
                RewardWeights: [3000, 3000, 3000, 600, 280, 100, 10, 10],
                Extra0: 0,
                Extra1: 0,
                SourceFile: "task_reward_type.txt",
                LineNumber: 1,
                RawLine: "755@@2@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@3301408@@3301409@@3308803@@3301410@@3301411@@3301412@@3301413@@3301414@@3000@@3000@@3000@@600@@280@@100@@10@@10@@0@@0@@",
                Fields: ["755", "2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "3301408", "3301409", "3308803", "3301410", "3301411", "3301412", "3301413", "3301414", "3000", "3000", "3000", "600", "280", "100", "10", "10", "0", "0", ""]),
        });
        WriteJson(seedRoot, "taskRewardTypeIniEntries.json", new[]
        {
            new TaskRewardTypeIniEntry(
                3651,
                Parameters: [2, 1, 2, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0],
                RewardValues: [3007285, 3007284, 3007386, 3008731, 3007311, 3008729, 3600012, 3008732],
                RewardWeights: [1000, 1000, 1000, 1000, 1500, 1500, 1500, 1500],
                Extra0: 98_579_160,
                Extra1: 98_579_161,
                SourceFile: "task_reward_type.ini",
                LineNumber: 2,
                RawLine: "3651 2 1 0002 0000 0002 0 0 0 0 0 0 0 0 3007285 3007284 3007386 3008731 3007311 3008729 3600012 3008732 1000 1000 1000 1000 1500 1500 1500 1500 98579160 98579161",
                Fields: ["3651", "2", "1", "0002", "0000", "0002", "0", "0", "0", "0", "0", "0", "0", "0", "3007285", "3007284", "3007386", "3008731", "3007311", "3008729", "3600012", "3008732", "1000", "1000", "1000", "1000", "1500", "1500", "1500", "1500", "98579160", "98579161"]),
        });
        WriteJson(seedRoot, "neiGongHeader.json", new NeiGongHeader(
            SecretCount: 1,
            AttributeTips:
            [
                new NeiGongAttributeTip(0, "Potency~Cost"),
                new NeiGongAttributeTip(14, "Anti-break"),
            ],
            SourceFile: "NeiGongInfo.ini",
            LineNumber: 1,
            RawSectionName: "NeiGong",
            Fields: new Dictionary<string, string>
            {
                ["Num"] = "1",
                ["Attri0Tip"] = "Potency~Cost",
                ["Attri14Tip"] = "Anti-break",
            }));
        WriteJson(seedRoot, "neiGongSecrets.json", new[]
        {
            new NeiGongSecret(
                1,
                Name: "Universal~Concept",
                AniSection: "NeiGong_HeartSkill14Btn",
                NeiGongCount: 1,
                AttributeTypes: [7, 9],
                AttributeValues: [100, 100],
                GetWayContent: "PS:~Claim~the~book~[Universal~Concept]~from~the~Celestial~Sage~at~(TwinCity~202,416).",
                SourceFile: "NeiGongInfo.ini",
                LineNumber: 6,
                RawSectionName: "1",
                Fields: new Dictionary<string, string>
                {
                    ["id"] = "1",
                    ["Name"] = "Universal~Concept",
                    ["AniSection"] = "NeiGong_HeartSkill14Btn",
                    ["NeiGongNum"] = "1",
                    ["AttriType"] = "7-9",
                    ["AttriValue"] = "100-100",
                    ["GetWayContent"] = "PS:~Claim~the~book~[Universal~Concept]~from~the~Celestial~Sage~at~(TwinCity~202,416).",
                }),
        });
        WriteJson(seedRoot, "neiGongTypes.json", new[]
        {
            new NeiGongType(
                SecretId: 1,
                Slot: 1,
                TypeId: 1,
                Name: "Universal~Concept~(A)",
                AniSection: "NeiGong_HeartSkill14Btn",
                MaxLevel: 5,
                AttributeTypes: [2, 4],
                AttributeValues: [100, 100],
                CultureValues: [1500, 1500, 1500, 1500, 1500],
                Condition: "<item 3005365>[Universal~Concept~(A)]</item>",
                RequiredLevel: 2015,
                RequiredNeiGongValue: 0,
                RequiredPrerequisiteNeiGong: 0,
                RequiredItemTypeId: 3_005_365,
                SourceFile: "NeiGongInfo.ini",
                LineNumber: 16,
                RawSectionName: "1-1",
                Fields: new Dictionary<string, string>
                {
                    ["Type"] = "1",
                    ["Name"] = "Universal~Concept~(A)",
                    ["AniSection"] = "NeiGong_HeartSkill14Btn",
                    ["MaxLev"] = "5",
                    ["AttriType"] = "2-4",
                    ["AttriValue"] = "100-100",
                    ["NeiGongValue"] = "1500-1500-1500-1500-1500",
                    ["Condition"] = "<item 3005365>[Universal~Concept~(A)]</item>",
                    ["ReqLev"] = "2015",
                    ["ReqNeiGongValue"] = "0",
                    ["ReqPreNeiGong"] = "0",
                    ["ReqItemType"] = "3005365",
                }),
        });
        WriteJson(seedRoot, "subProfessions.json", new[]
        {
            new SubProfessionEntry(
                Id: 11,
                Title: "Martial Artist",
                Intro: "Critical Strike +1%",
                RequiredLevel: 70,
                RequiredMetempsychosis: 0,
                RequiredProfessions: [41, 31],
                Promotion: "or 800 StudyPoints",
                Suit: 0,
                LevelLimit: 9,
                RequiredJobLevel: 1,
                EffectProportion: 1,
                UpLevelEffect: "LevelUp",
                UpStepEffect: "LevelUp",
                UpStepScreenEffect: null,
                UpEffect: null,
                SubSpecial: "None",
                SuitPower: null,
                BigClass: null,
                AccessionRoute: null,
                StepUpRoute: null,
                AnimationSection: null,
                SourceFile: "SubProfessionInfo.ini",
                LineNumber: 54,
                RawSectionName: "11",
                Entries:
                [
                    new SubProfessionRawEntry("req_joblv", "0", "SubProfessionInfo.ini", 61, "req_joblv=0"),
                    new SubProfessionRawEntry("req_joblv", "1", "SubProfessionInfo.ini", 64, "req_joblv=1"),
                ]),
        });
        WriteJson(seedRoot, "syndicateLevels.json", new[]
        {
            new SyndicateLevel(
                1,
                Parameters: [999, 2, 8, 10, 3, 3, 3, 1, 3, 5, 5],
                SourceFile: "syndicate_level.txt",
                LineNumber: 1,
                RawLine: "1@@999@@2@@8@@10@@3@@3@@3@@1@@3@@5@@5@@",
                Fields: ["1", "999", "2", "8", "10", "3", "3", "3", "1", "3", "5", "5", ""]),
        });
        WriteJson(seedRoot, "runeLevelExperience.json", new[]
        {
            new RuneLevelExperience(
                28,
                RuneType: 402,
                Level: 27,
                Experience: 341_500,
                SourceFile: "rune_levexp.txt",
                LineNumber: 28,
                RawLine: "28@@402@@27@@341500@@",
                Fields: ["28", "402", "27", "341500", ""]),
        });
        WriteJson(seedRoot, "runeStorageAttributes.json", new[]
        {
            new RuneStorageAttribute(
                21,
                RequiredStorageScore: 335_000,
                Bonuses:
                [
                    new RuneStorageAttributeBonus(11, 300),
                    new RuneStorageAttributeBonus(12, 300),
                    new RuneStorageAttributeBonus(0, 0),
                ],
                SourceFile: "rune_storage_attr.txt",
                LineNumber: 21,
                RawLine: "21@@335000@@11@@300@@12@@300@@0@@0@@",
                Fields: ["21", "335000", "11", "300", "12", "300", "0", "0", ""]),
        });
        WriteJson(seedRoot, "itemRefineEffects.json", new[]
        {
            new ItemRefineEffect(
                1,
                Values: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7001, 0, 0, 0, 11001, 0, 0, 0, 0, 0],
                SourceFile: "item_refine_effect.txt",
                LineNumber: 1,
                RawLine: "1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@7001@@0@@0@@0@@11001@@0@@0@@0@@0@@0@@",
                Fields: ["1", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "7001", "0", "0", "0", "11001", "0", "0", "0", "0", "0", ""]),
        });
        WriteJson(seedRoot, "itemRefineEffectEx.json", new[]
        {
            new ItemRefineEffectEx(
                1,
                ItemTypeId: 2_100_095,
                Values: [100, 5, 7],
                SourceFile: "item_refine_effect_ex.txt",
                LineNumber: 1,
                RawLine: "1@@2100095@@100@@5@@7@@",
                Fields: ["1", "2100095", "100", "5", "7", ""]),
        });
        WriteJson(seedRoot, "itemRefineUpgrades.json", new[]
        {
            new ItemRefineUpgrade(
                1,
                Level: 1,
                Value: 30,
                SourceFile: "item_refine_upgrade.txt",
                LineNumber: 1,
                RawLine: "1@@1@@30@@",
                Fields: ["1", "1", "30", ""]),
        });
        WriteJson(seedRoot, "transformInfo.json", new[]
        {
            new TransformInfo(
                1350,
                Level: 3,
                Name: "DivineHare",
                Lookface: 267,
                MaxLife: 3000,
                SourceFile: "TransformInfo.txt",
                LineNumber: 4,
                RawLine: "1350 3 DivineHare 267 3000",
                Fields: ["1350", "3", "DivineHare", "267", "3000"]),
        });
        WriteJson(seedRoot, "furniture.json", new[]
        {
            new Furniture(
                100,
                Kind: 1,
                Lookface: 910,
                MapId: 1000,
                X: 300,
                Y: 400,
                Name: "FixtureChair",
                ItemTypeId: 100000,
                Price: 1200,
                SourceFile: "furnitures.txt",
                LineNumber: 1,
                RawLine: "100,1,910,1000,300,400,FixtureChair,100000,1200",
                Fields: ["100", "1", "910", "1000", "300", "400", "FixtureChair", "100000", "1200"]),
        });
        WriteJson(seedRoot, "hairColorTypes.json", new[]
        {
            new HairColorType(
                78,
                ColorIndex: 5,
                UnlockType: 1,
                ItemTypeId: 3326778,
                Cost: 1,
                SourceFile: "hair_color_type.txt",
                LineNumber: 1,
                RawLine: "78@@5@@1@@3326778@@1@@",
                Fields: ["78", "5", "1", "3326778", "1", ""]),
        });
        WriteJson(seedRoot, "hairFaceStorageTypes.json", new[]
        {
            new HairFaceStorageType(
                0,
                Id: 1,
                Unknown0: 0,
                Name: "YouthSpark",
                UnlockType: 0,
                RequiredValue: 0,
                Unknown1: 0,
                Cost: 10_000,
                Unknown2: 0,
                LinkedColorIdsRaw: "",
                Sort: 0,
                SourceFile: "hairface_storage_type.txt",
                LineNumber: 1,
                RawLine: "0@@1@@0@@YouthSpark@@0@@0@@0@@10000@@0@@@@0@@",
                Fields: ["0", "1", "0", "YouthSpark", "0", "0", "0", "10000", "0", "", "0", ""]),
        });
        WriteJson(seedRoot, "coatStorageTypes.json", new[]
        {
            new CoatStorageType(
                181305,
                StorageType: 31,
                Parameters: [1, 0, 0, 0, 0, 1],
                SourceFile: "coat_storage_type.txt",
                LineNumber: 1,
                RawLine: "181305@@31@@1@@0@@0@@0@@0@@1@@",
                Fields: ["181305", "31", "1", "0", "0", "0", "0", "1", ""]),
        });
        WriteJson(seedRoot, "coatStorageAttributes.json", new[]
        {
            new CoatStorageAttribute(
                17,
                StorageType: 1_000_000_000,
                RequiredStorageScore: 30,
                Bonuses:
                [
                    new CosmeticAttributeBonus(3, 300),
                    new CosmeticAttributeBonus(5, 100),
                    new CosmeticAttributeBonus(0, 0),
                    new CosmeticAttributeBonus(0, 0),
                ],
                Reserved0: 0,
                SourceFile: "coat_storage_attr.txt",
                LineNumber: 3,
                RawLine: "17@@1000000000@@30@@3@@300@@5@@100@@0@@0@@0@@0@@0@@",
                Fields: ["17", "1000000000", "30", "3", "300", "5", "100", "0", "0", "0", "0", "0", ""]),
        });
        WriteJson(seedRoot, "coatColorRules.json", new[]
        {
            new CoatColorRule(
                200412,
                Parameters: [1, 5, 499, 3326059, 1, 3328359, 5, 300, 232503150, 232503150, 232503150, 232503150, 0, 0, 0, 0, 250200, 50],
                SourceFile: "coat_color_rule.txt",
                LineNumber: 1,
                RawLine: "200412@@1@@5@@499@@3326059@@1@@3328359@@5@@300@@232503150@@232503150@@232503150@@232503150@@0@@0@@0@@0@@250200@@50@@",
                Fields: ["200412", "1", "5", "499", "3326059", "1", "3328359", "5", "300", "232503150", "232503150", "232503150", "232503150", "0", "0", "0", "0", "250200", "50", ""]),
        });
        WriteJson(seedRoot, "xuanbaoAdditionAttributes.json", new[]
        {
            new XuanbaoAdditionAttribute(
                1,
                AttributeType: 14,
                Level: 1,
                Values: [0, 50],
                SourceFile: "xuanbao_addition_attr.txt",
                LineNumber: 1,
                RawLine: "1@@14@@1@@0@@50@@",
                Fields: ["1", "14", "1", "0", "50", ""]),
        });
        WriteJson(seedRoot, "xuanbaoComposeAttributeLimits.json", new[]
        {
            new XuanbaoComposeAttributeLimit(
                1,
                Values: [0, 4, 400],
                SourceFile: "xuanbao_compose_attr_limit.txt",
                LineNumber: 1,
                RawLine: "1@@0@@4@@400@@",
                Fields: ["1", "0", "4", "400", ""]),
        });
        WriteJson(seedRoot, "xuanbaoRandomAttributes.json", new[]
        {
            new XuanbaoRandomAttribute(
                4100001,
                AttributeType: 1,
                MinValue: 30,
                MaxValue: 101,
                SourceFile: "xuanbao_rand_attr.txt",
                LineNumber: 1,
                RawLine: "4100001@@1@@30@@101@@",
                Fields: ["4100001", "1", "30", "101", ""]),
        });
        WriteJson(seedRoot, "christmasNpcs.json", new[]
        {
            new ChristmasNpc(
                52301,
                Name: null,
                Kind: 2,
                Lookface: 15820,
                MapId: 1000,
                X: 313,
                Y: 231,
                Parameters: [0, 0, 1, 1],
                SourceFile: "christmasnpcs.txt",
                LineNumber: 1,
                RawLine: "52301,,2,15820,1000,313,231,0,0,1,1",
                Fields: ["52301", "", "2", "15820", "1000", "313", "231", "0", "0", "1", "1"]),
        });
        WriteJson(seedRoot, "tutorTypes.json", new[]
        {
            new TutorType(
                1,
                MinLevel: 70,
                MaxLevel: 109,
                Values: [2, 10],
                SourceFile: "cq_tutor_type.txt",
                LineNumber: 1,
                RawLine: "1,70,109,2,10",
                Fields: ["1", "70", "109", "2", "10"]),
        });
        WriteJson(seedRoot, "tutorBattleLimitTypes.json", new[]
        {
            new TutorBattleLimitType(
                1,
                BattlePowerLimit: 100,
                SourceFile: "cq_tutor_battle_limit_type.txt",
                LineNumber: 1,
                RawLine: "1,100",
                Fields: ["1", "100"]),
        });
        WriteJson(seedRoot, "beastAttributes.json", new[]
        {
            new BeastAttribute(
                1,
                Parameters: [2400, 200, 200, 500, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                SourceFile: "beasts_attr.txt",
                LineNumber: 1,
                RawLine: "1@@2400@@200@@200@@500@@100@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@",
                Fields: ["1", "2400", "200", "200", "500", "100", "100", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""]),
        });
        WriteJson(seedRoot, "fateExperience.json", new[]
        {
            new FateExperience(
                1,
                Type: 1,
                Level: 1,
                Experience: 500,
                Values: [60_000, 120_000, 130_000, 90_000, 0, 0, 0, 10, 0, 0, 3_300_576],
                SourceFile: "fate_exp.txt",
                LineNumber: 1,
                RawLine: "1@@1@@1@@500@@60000@@120000@@130000@@90000@@0@@0@@0@@10@@0@@0@@3300576@@",
                Fields: ["1", "1", "1", "500", "60000", "120000", "130000", "90000", "0", "0", "0", "10", "0", "0", "3300576", ""]),
        });
        WriteJson(seedRoot, "spiritRates.json", new[]
        {
            new SpiritRate(
                76,
                Type: 1,
                ItemTypeId: 4_200_001,
                Parameters: [0, 0, 5, 0, 0],
                SourceFile: "spirit_rate.txt",
                LineNumber: 1,
                RawLine: "76@@1@@4200001@@0@@0@@5@@0@@0@@",
                Fields: ["76", "1", "4200001", "0", "0", "5", "0", "0", ""]),
        });
        WriteJson(seedRoot, "hundredWeapons.json", new[]
        {
            new HundredWeapon(
                1,
                WeaponType: 410,
                Level: 1,
                Values: [0, 100, 10100, 20050, 30030, 15870, 0, 0],
                SourceFile: "hundred_weapon.txt",
                LineNumber: 1,
                RawLine: "1@@410@@1@@0@@100@@10100@@20050@@30030@@15870@@0@@0@@",
                Fields: ["1", "410", "1", "0", "100", "10100", "20050", "30030", "15870", "0", "0", ""]),
        });
        WriteJson(seedRoot, "gouyuTypes.json", new[]
        {
            new GouyuType(
                1,
                Type: 100,
                Level: 1,
                Values: [50, 10_537, 0, 16_520, 16_530, 0, 0, 0, 100, 10],
                SourceFile: "gouyu_type.txt",
                LineNumber: 1,
                RawLine: "1@@100@@1@@50@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10@@",
                Fields: ["1", "100", "1", "50", "10537", "0", "16520", "16530", "0", "0", "0", "100", "10", ""]),
        });
        WriteJson(seedRoot, "gouyuImmortals.json", new[]
        {
            new GouyuImmortal(
                8,
                Values: [8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                SourceFile: "gouyu_immortal.txt",
                LineNumber: 1,
                RawLine: "8@@8@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@",
                Fields: ["8", "8", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""]),
        });
        WriteJson(seedRoot, "specialEffectAttributes.json", new[]
        {
            new SpecialEffectAttribute(
                40_701_011,
                Type: 1,
                Values: [10, 10, 100, 10, 60, 5],
                SourceFile: "speffect_attribute.txt",
                LineNumber: 1,
                RawLine: "40701011@@1@@10@@10@@100@@10@@60@@5@@",
                Fields: ["40701011", "1", "10", "10", "100", "10", "60", "5", ""]),
        });
        WriteJson(seedRoot, "daoqiDictTypes.json", new[]
        {
            new DaoqiDictType(
                113,
                Type: 0,
                Level: 1,
                Values: [100, 0, 0, 100, 1000, 900, 113, 1, 0, 0, 0, 0],
                SourceFile: "daoqi_dict_type.txt",
                LineNumber: 1,
                RawLine: "113@@0@@1@@100@@0@@0@@100@@1000@@900@@113@@1@@0@@0@@0@@0@@",
                Fields: ["113", "0", "1", "100", "0", "0", "100", "1000", "900", "113", "1", "0", "0", "0", "0", ""]),
        });
        WriteJson(seedRoot, "syndicateFormationTypes.json", new[]
        {
            new SyndicateFormationType(
                1,
                Type: 100,
                Level: 1,
                Values: [100_000_000, 120_000_000, 700, 1000, 0],
                SourceFile: "syn_formtype.txt",
                LineNumber: 1,
                RawLine: "1@@100@@1@@100000000@@120000000@@700@@1000@@0@@",
                Fields: ["1", "100", "1", "100000000", "120000000", "700", "1000", "0", ""]),
        });
        WriteJson(seedRoot, "syndicateSkillTypes.json", new[]
        {
            new SyndicateSkillType(
                1,
                SkillType: 1,
                Level: 1,
                Values: [500, 75_050, 50_000, 50],
                SourceFile: "syn_skill_type.txt",
                LineNumber: 1,
                RawLine: "1@@1@@1@@500@@75050@@50000@@50@@",
                Fields: ["1", "1", "1", "500", "75050", "50000", "50", ""]),
        });
        WriteJson(seedRoot, "disdainRules.json", new[]
        {
            new DisdainRule(
                1,
                Values: [0, 400, 2800, 0, 60000, 60000, -90, -90, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100],
                SourceFile: "cq_disdain.txt",
                LineNumber: 1,
                RawLine: "1,0,400,2800,0,60000,60000,-90,-90,1,2000,100,1,2000,100,1,2000,100,1,2000,100",
                Fields: ["1", "0", "400", "2800", "0", "60000", "60000", "-90", "-90", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100"]),
        });
        WriteJson(seedRoot, "jiangHuAttributes.json", new[]
        {
            new JiangHuAttribute(
                1,
                AttributeType: 101,
                Level: 1,
                Value: 500,
                SourceFile: "JianghuAttributes.txt",
                LineNumber: 2,
                RawLine: "1 101 1 500",
                Fields: ["1", "101", "1", "500"]),
        });
        WriteJson(seedRoot, "jiangHuCultivateStatuses.json", new[]
        {
            new JiangHuCultivateStatus(
                1,
                DeclaredValueCount: 3,
                Values: [10, 20, 30],
                SourceFile: "JingHuCultivateStatus.txt",
                LineNumber: 2,
                RawLine: "1 3 10 20 30",
                Fields: ["1", "3", "10", "20", "30"]),
        });
        WriteJson(seedRoot, "refineryBoxes.json", new[]
        {
            new RefineryBox(
                3_300_001,
                Values: [1, 2, 3],
                SourceFile: "databaserefineryboxes.txt",
                LineNumber: 2,
                RawLine: "3300001,1,2,3",
                Fields: ["3300001", "1", "2", "3"]),
        });
        WriteJson(seedRoot, "refinerySouls.json", new[]
        {
            new RefinerySoul(
                1,
                "WeaponSoul",
                ItemType: 800001,
                SourceFile: "Souls1.ini",
                LineNumber: 2,
                RawLine: "WeaponSoul 800001",
                Fields: ["WeaponSoul", "800001"]),
        });
        WriteJson(seedRoot, "refineryMaterials.json", new[]
        {
            new RefineryMaterial(
                724_350,
                "Detoxication(Unique)Material",
                PrimaryValue: 11,
                EffectType: 7,
                Parameters: [11, 7, 0],
                Target: "Necklace",
                Description: "Level~3~Refinery~item.",
                TailValues: [7, 0, 0, 0, 13_010, 500],
                SourceFile: "Rifinery.txt",
                LineNumber: 1,
                RawLine: "724350 Detoxication(Unique)Material 11 7",
                Fields: ["724350", "Detoxication(Unique)Material", "11", "7"]),
        });
        WriteJson(seedRoot, "pokerTables.json", new[]
        {
            new PokerTable(
                1,
                Values: [13, 60, 3, 2, 1, 1000, 2000, 3000],
                SourceFile: "PokerTables.txt",
                LineNumber: 1,
                RawLine: "1 013 060 3 2 1 1000 2000 3000",
                Fields: ["1", "013", "060", "3", "2", "1", "1000", "2000", "3000"]),
        });
        WriteJson(seedRoot, "rouletteTables.json", new[]
        {
            new RouletteTable(
                1,
                Values: [3001, 3002, 3003, 1, 2, 3],
                SourceFile: "Roulettes.txt",
                LineNumber: 1,
                RawLine: "1,3001,3002,3003,1,2,3",
                Fields: ["1", "3001", "3002", "3003", "1", "2", "3"]),
        });
        WriteJson(seedRoot, "npcTimeEntries.json", new[]
        {
            new NpcTimeEntry(
                1,
                "Arena",
                Values: [1, 2, 3, 4, 5, 6, 7, 8, 9],
                SourceFile: "NpcTime.txt",
                LineNumber: 1,
                RawLine: "1,Arena,1,2,3,4,5,6,7,8,9",
                Fields: ["1", "Arena", "1", "2", "3", "4", "5", "6", "7", "8", "9"]),
        });
        WriteJson(seedRoot, "activityTasks.json", new ActivityTaskCatalog(
            new ActivityTaskMetadata(
                1,
                "ActivityTask",
                SourceLine: 1,
                Entries:
                [
                    new ActivityTaskRawEntry("Num", "1", SourceLine: 2, RawLine: "Num=1"),
                ]),
            Tasks:
            [
                new ActivityTaskDefinition(
                    7,
                    1007,
                    "Daily Gift",
                    Type: 1,
                    Progress: 3,
                    ActiveValue: 25,
                    CloseLevelRaw: "0141",
                    CloseLevel: 141,
                    Tip: "Claim it from Welfare.",
                    OpenLevelRaw: "0005",
                    OpenLevel: 5,
                    PreviewRaw: null,
                    Preview: null,
                    Intro: null,
                    AnimationSection: "Sparkle",
                    Param: "[npc,Guide,9534,1002,301,529]",
                    SourceSection: "7",
                    SourceLine: 5,
                    Entries:
                    [
                        new ActivityTaskRawEntry("CloseLev", "0141", SourceLine: 11, RawLine: "CloseLev=0141"),
                    ]),
            ],
            SourceFile: "ActivityTask.ini"));
        WriteJson(seedRoot, "meltingTypeRules.json", new[]
        {
            new MeltingTypeRule(
                1,
                700002,
                Parameters: [0, 0, 1],
                SourceFile: "MeltingType.ini",
                LineNumber: 3,
                RawLine: "1 700002 0 0 1",
                Fields: ["1", "700002", "0", "0", "1"]),
        });
        WriteJson(seedRoot, "lotteryPrizes.json", new[]
        {
            new LotteryPrize(
                1,
                70,
                "WavyHeavyRing+8",
                "151159",
                151159,
                SocketOne: 2,
                SocketTwo: 0,
                PlusLevel: 8,
                SourceFile: "lottery.ini",
                LineNumber: 2,
                RawLine: "1 70 WavyHeavyRing+8 151159 2 0 8",
                Fields: ["1", "70", "WavyHeavyRing+8", "151159", "2", "0", "8"]),
        });
        WriteJson(seedRoot, "arenaRecords.json", new[]
        {
            new ArenaRecord(
                1001,
                "ArenaOne",
                Values: [120, 8000, 100001, 4000, 0, 0, 0, 0, 0, 0, 4000, 0, 0, 1],
                SourceFile: "Arena.ini",
                LineNumber: 2,
                RawLine: "1001/ArenaOne/120/8000/100001/4000/0/0/0/0/0/0/4000/0/0/1/",
                Fields: ["1001", "ArenaOne", "120", "8000", "100001", "4000", "0", "0", "0", "0", "0", "0", "4000", "0", "0", "1", ""]),
        });
        WriteJson(seedRoot, "teamArenaRecords.json", new[]
        {
            new TeamArenaRecord(
                1002,
                "TeamArenaOne",
                Values: [130, 7001, 100002, 4000, 0, 0, 0, 0, 0, 0, 4000, 0, 0, 1],
                SourceFile: "TeamArena.ini",
                LineNumber: 2,
                RawLine: "1002/TeamArenaOne/130/7001/100002/4000/0/0/0/0/0/0/4000/0/0/1/",
                Fields: ["1002", "TeamArenaOne", "130", "7001", "100002", "4000", "0", "0", "0", "0", "0", "0", "4000", "0", "0", "1", ""]),
        });
        WriteJson(seedRoot, "elitePkRecords.json", new[]
        {
            new PkTournamentRecord(3, 7, 140, "EliteOne", 100001, 1, 7001, "ElitePk.ini", 2, "3/7/140/EliteOne/100001/1/7001/", ["3", "7", "140", "EliteOne", "100001", "1", "7001", ""]),
        });
        WriteJson(seedRoot, "teamElitePkRecords.json", new[]
        {
            new PkTournamentRecord(2, 6, 130, "TeamOne", 100003, 1, 7003, "TeamElitePK.ini", 2, "2/6/130/TeamOne/100003/1/7003/", ["2", "6", "130", "TeamOne", "100003", "1", "7003", ""]),
        });
        WriteJson(seedRoot, "skillTeamPkRecords.json", new[]
        {
            new PkTournamentRecord(1, 5, 120, "SkillOne", 100005, 1, 7005, "SkillTeamPK.ini", 2, "1/5/120/SkillOne/100005/1/7005/", ["1", "5", "120", "SkillOne", "100005", "1", "7005", ""]),
        });
        WriteJson(seedRoot, "classPkWarRecords.json", new[]
        {
            new ClassPkWarRecord(10, 3, 7001, 100, "ClassPkWar.ini", 2, "10/3/7001/100/", ["10", "3", "7001", "100", ""]),
        });
        WriteJson(seedRoot, "combatGearRanks.json", new[]
        {
            new CombatGearRankEntry(5001, "RankOne", RankingFields: ["gear", "100"], SourceFile: "CombatGearRank.txt", LineNumber: 2, RawLine: "5001/RankOne/gear/100/", Fields: ["5001", "RankOne", "gear", "100", ""]),
        });
        WriteJson(seedRoot, "prestigeRanks.json", new[]
        {
            new PrestigeRankEntry(5001, "PrestigeOne", RankingFields: ["prestige", "100"], SourceFile: "PrestigeRanking.txt", LineNumber: 2, RawLine: "5001/PrestigeOne/prestige/100/", Fields: ["5001", "PrestigeOne", "prestige", "100", ""]),
        });
        WriteJson(seedRoot, "innerPowerRanks.json", new[]
        {
            new InnerPowerRankEntry("InnerOne", 5001, RankingFields: ["inner", "100"], SourceFile: "InnerPower.txt", LineNumber: 2, RawLine: "InnerOne/5001/inner/100/", Fields: ["InnerOne", "5001", "inner", "100", ""]),
        });
        WriteJson(seedRoot, "runeRankings.json", new[]
        {
            new RuneRankingRecord("RuneOne", 314_800, 5001, "RuneRanking.txt", 2, "RuneOne/314800/5001/", ["RuneOne", "314800", "5001", ""]),
        });
        WriteJson(seedRoot, "staticStatues.json", new[]
        {
            new StaticStatueRecord(298, Values: [298, 34, 1, 179], SourceFile: "StaticStatue.txt", LineNumber: 2, RawLine: "298/34/1/179/", Fields: ["298", "34", "1", "179", ""]),
        });
        WriteJson(seedRoot, "newPlayerRewards.json", new[]
        {
            new NewPlayerRewardRecord("10.0.0.1", "device-a", "NewPlayerRewords.txt", 2, "10.0.0.1/device-a/", ["10.0.0.1", "device-a", ""]),
        });
        WriteJson(seedRoot, "votes.json", new[]
        {
            new VoteRecord(7001, "127.0.0.1", "638379291346047487", "Votes.txt", 2, "7001/127.0.0.1/638379291346047487/", ["7001", "127.0.0.1", "638379291346047487", ""]),
        });
        WriteJson(seedRoot, "stateCountFiles.json", new[]
        {
            new StateCountFileData(
                "BanIp.txt",
                DeclaredCount: 0,
                Entries: [],
                RawLines: ["Count=0"]),
        });
        WriteJson(seedRoot, "binarySeedFiles.json", new[]
        {
            new BinarySeedFile(
                "Insults.bin",
                Length: 4,
                Sha256: "df3f619804a92fdb4057192dc43dd748ea778adc52bc498ce80524c014b81119",
                Base64: "AAAAAA=="),
        });
        WriteJson(seedRoot, "emptySeedFiles.json", new[]
        {
            new EmptySeedFile(
                "HeroRewards.txt",
                Length: 0,
                Sha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                Base64: ""),
        });
        WriteJson(seedRoot, "backupSeedFiles.json", new[]
        {
            new BackupSeedFile(
                "SobNpcs.txt.bak",
                Length: 4,
                Sha256: "5abdb6175f820f0ac3d8647fbb1f7a0bcc91757a782a8a145570944ca6a00c96",
                Base64: "77u/MQ=="),
        });
        WriteJson(seedRoot, "couplesPkRecords.json", new[]
        {
            new CouplesPkRecord("PlayerOne", "PartnerOne", "CouplesPK.ini", 2, "PlayerOne/PartnerOne/", ["PlayerOne", "PartnerOne", ""]),
        });
        WriteJson(seedRoot, "socialAssociations.json", new[]
        {
            new SocialAssociationRecord(
                1293116,
                4,
                1293600,
                "Coder[GM]",
                Values: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1541008, 100, 0],
                Note: "hey",
                TailValue: 7000,
                SourceFile: "Associate.txt",
                LineNumber: 2,
                RawLine: "1293116/4/0/0/0/1293600/0/0/0/0/Coder[GM]/0/0/0/0/1541008/100/0/hey/7000/",
                Fields: ["1293116", "4", "0", "0", "0", "1293600", "0", "0", "0", "0", "Coder[GM]", "0", "0", "0", "0", "1541008", "100", "0", "hey", "7000", ""]),
        });
        WriteJson(seedRoot, "guildWarInfo.json", CreateGuildWarData("GuildWarInfo.ini", 1363, "GuildFixture", 8_000));
        WriteJson(seedRoot, "superGuildWarInfo.json", CreateGuildWarData("SuperGuildWarInfo.ini", 2000, "SuperGuildFixture", 16_000));
        WriteJson(seedRoot, "stageGoalInfo.json", new StageGoalInfo(1));
        WriteJson(seedRoot, "stageGoalStages.json", new[]
        {
            new StageGoalStage(
                1,
                "0001",
                OpenLevel: 1,
                StageAnimation: "Stage_01goalBtn",
                GoalCount: 1,
                Rewards:
                [
                    new StageGoalReward(
                        1,
                        3005107,
                        Amount: 1,
                        BoundFlag: 0,
                        Key: "Prize1",
                        RawValue: "3005107@@1@@0",
                        SourceLine: 5,
                        Fields: ["3005107", "1", "0"]),
                ],
                SourceLine: 2,
                Entries: []),
        });
        WriteJson(seedRoot, "stageGoalTasks.json", new[]
        {
            new StageGoalTask(
                1,
                1,
                "[Novice] Level-up",
                "Upgrade to Level 20+.",
                Progress: 20,
                Rewards:
                [
                    new StageGoalReward(
                        1,
                        723700,
                        Amount: 1,
                        BoundFlag: 1,
                        Key: "Prize1",
                        RawValue: "723700@@1@@1",
                        SourceLine: 12,
                        Fields: ["723700", "1", "1"]),
                ],
                TaskType: 1,
                RequireRaw: null,
                Require: null,
                SourceLine: 8,
                Entries: []),
        });
        WriteJson(seedRoot, "shops.json", new[]
        {
            new ShopDefinition(
                432,
                Name: null,
                Type: null,
                MoneyType: 1,
                SourceFile: "emoneyshop.ini",
                SourceDialect: "EmoneyShop",
                Attributes: new Dictionary<string, string>()),
        });
        WriteJson(seedRoot, "shopItems.json", new[]
        {
            new ShopItem(
                432,
                100000,
                Position: 0,
                Category: "item",
                Status: "new",
                IsBound: false,
                Price: null,
                Description: null,
                Flags: ["new"],
                SourceFile: "emoneyshop.ini",
                SourceDialect: "EmoneyShop",
                RawLine: "Item0=100000 new",
                Attributes: new Dictionary<string, string>()),
        });
        WriteJson(seedRoot, "exchangeShopGoods.json", new[]
        {
            new ExchangeShopRow("exchange_shop_goods.txt", 1, ["1", "100000", ""]),
        });
        WriteJson(seedRoot, "exchangeShopGoodsEx.json", new[]
        {
            new ExchangeShopRow("exchange_shop_goods_ex.txt", 1, ["1", "100000", ""]),
        });
        WriteJson(seedRoot, "exchangeShopLevels.json", new[]
        {
            new ExchangeShopRow("exchange_shop_lev.txt", 1, ["1", "131", "0", "15", "0", ""]),
        });
        WriteJson(seedRoot, "honorShopItems.json", new[]
        {
            new HonorShopItem(
                3303085,
                Cost: 3000,
                Unknown0: 0,
                Category: "item",
                IsRecommended: false,
                Status: "new",
                Position: 0,
                SourceFile: "HonorShop.ini",
                RawLine: "3303085,3000,0,item,,new",
                Fields: ["3303085", "3000", "0", "item", "", "new"]),
        });
        WriteJson(seedRoot, "honorShopRecommendations.json", new[]
        {
            new HonorShopRecommendation(
                3303085,
                "Open~to~receive~a~Class~6~House~Pack.",
                Position: 0,
                SourceFile: "HonorShop.ini",
                RawLine: "3303085=Open~to~receive~a~Class~6~House~Pack."),
        });
        WriteJson(seedRoot, "racePointShopItems.json", new[]
        {
            new PointShopItem(
                3009000,
                Cost: 20,
                ItemClass: "pills",
                Category: "item",
                Status: null,
                Section: "Normal",
                Position: 0,
                SourceFile: "RacePointShop.ini",
                RawLine: "3009000,20,pills,item",
                Fields: ["3009000", "20", "pills", "item"]),
        });
        WriteJson(seedRoot, "racePointShopRecommendations.json", new[]
        {
            new PointShopRecommendation(
                3009000,
                "Open~to~get~5~pills.",
                Section: "Recommend",
                Position: 0,
                SourceFile: "RacePointShop.ini",
                RawLine: "3009000=Open~to~get~5~pills."),
        });
        WriteJson(seedRoot, "goldenLeagueShopItems.json", new[]
        {
            new PointShopItem(
                3303526,
                Cost: 500,
                ItemClass: "rune",
                Category: "equip",
                Status: "other",
                Section: "Recommend",
                Position: 0,
                SourceFile: "GoldenLeagueShop.ini",
                RawLine: "3303526,500,rune,equip,other",
                Fields: ["3303526", "500", "rune", "equip", "other"]),
        });
        WriteJson(seedRoot, "goldenLeagueShopRecommendations.json", new[]
        {
            new PointShopRecommendation(
                3303526,
                "Open~to~select~a~red~Rune~(B).",
                Section: "Recommend",
                Position: 0,
                SourceFile: "GoldenLeagueShop.ini",
                RawLine: "3303526=Open~to~select~a~red~Rune~(B)."),
        });
        WriteJson(seedRoot, "goldenLeagueShopConfig.json", new Dictionary<string, string>
        {
            ["Color.TipColor"] = "0xffffffff",
            ["PointLimit.GoldenLeaguePointLimit"] = "304400",
        });
        WriteJson(seedRoot, "booths.json", new[]
        {
            new BoothDefinition(
                700050,
                "Promoted",
                38194,
                1004,
                Type: null,
                X: 43,
                Y: 58,
                DeclaredItemAmount: 99,
                SourceSection: "booth700050",
                SourceLine: 2,
                Position: 0,
                SourceFile: "Booths.txt",
                Attributes: new Dictionary<string, string>()),
        });
        WriteJson(seedRoot, "boothItems.json", new[]
        {
            new BoothItem(
                700050,
                721080,
                Price: 1_000,
                Unknown0: 0,
                Tail: "0@0@0@0",
                ExtraField: null,
                ItemKey: 1,
                Position: 0,
                SourceFile: "Booths.txt",
                SourceLine: 10,
                RawLine: "Item1=721080@@1000@@0@@0@0@0@0",
                Fields: ["721080", "1000", "0", "0@0@0@0"]),
        });
    }

    private static GuildWarData CreateGuildWarData(string sourceFile, int id, string name, long hitPoints)
    {
        var infoId = new GuildWarEntry("Info", "ID", id.ToString(), "ID", id.ToString(), sourceFile, 2, $"ID={id}");
        var infoName = new GuildWarEntry("Info", "Name", name, "Name", name, sourceFile, 3, $"Name={name}");
        var leaderReward = new GuildWarEntry("Info", "LeaderReward", "0", "LeaderReward", "0", sourceFile, 4, "LeaderReward=0");
        var deputyReward = new GuildWarEntry("Info", "DeputiLeaderReward", "7", "DeputiLeaderReward", "7", sourceFile, 5, "DeputiLeaderReward=7");
        var poleName = new GuildWarEntry("Pole", "Name", "AttackPole", "Name", "AttackPole", sourceFile, 7, "Name=AttackPole");
        var poleHitPoints = new GuildWarEntry("Pole", "HitPoints", hitPoints.ToString(), "HitPoints", hitPoints.ToString(), sourceFile, 8, $"HitPoints={hitPoints}");
        var conductor = new GuildWarEntry("Condutors", "GuildConductor1", "101620,307,279,1000,", "GuildConductor1", "101620,307,279,1000,", sourceFile, 10, "GuildConductor1=101620,307,279,1000,");
        var infoEntries = new[] { infoId, infoName, leaderReward, deputyReward };
        var poleEntries = new[] { poleName, poleHitPoints };
        var conductorEntries = new[] { conductor };
        var entries = infoEntries.Concat(poleEntries).Concat(conductorEntries).ToArray();
        var sections = new[]
        {
            new GuildWarSection("Info", sourceFile, 1, "[Info]", infoEntries),
            new GuildWarSection("Pole", sourceFile, 6, "[Pole]", poleEntries),
            new GuildWarSection("Condutors", sourceFile, 9, "[Condutors]", conductorEntries),
        };

        return new GuildWarData(
            sourceFile,
            sections,
            entries,
            Array.Empty<WorldDataDiagnostic>(),
            new GuildWarInfoData(
                id,
                name,
                LeaderReward: 0,
                DeputiLeaderReward: 7,
                LeaderTop: new Dictionary<int, int> { [1] = 100 },
                DeputiTop: new Dictionary<int, int> { [1] = 50 }),
            new GuildWarPoleData("AttackPole", hitPoints),
            [
                new GuildWarConductorRow(
                    "GuildConductor1",
                    ["101620", "307", "279", "1000", ""],
                    sourceFile,
                    10,
                    "GuildConductor1=101620,307,279,1000,",
                    conductor),
            ]);
    }

    private static void WriteJson<T>(string seedRoot, string fileName, T value)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        File.WriteAllText(Path.Combine(seedRoot, fileName), JsonSerializer.Serialize(value, options));
    }

    private static void WriteCharacterSnapshot(string usersRoot, long accountId, long uid)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
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
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            PkPoints=0
            Online=7

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }

    private static string FutureStartupCommand(string projectName)
    {
        return $"dotnet run --project src/{projectName}/{projectName}.csproj -- --listen --address 127.0.0.1 --port 0";
    }

    private static string[] SplitLines(string text)
    {
        return text
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd())
            .ToArray();
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class ListeningServerProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _errorTask;
        private readonly string? _ownedSeedRoot;

        public ListeningServerProcess(Process process, string? ownedSeedRoot)
        {
            _process = process;
            _errorTask = process.StandardError.ReadToEndAsync();
            _ownedSeedRoot = ownedSeedRoot;
        }

        public int Port { get; set; }

        public async Task<string> ReadUntilAsync(Func<string, bool> predicate, TimeSpan timeout)
        {
            using var stop = new CancellationTokenSource(timeout);
            while (!stop.IsCancellationRequested)
            {
                var readTask = _process.StandardOutput.ReadLineAsync(stop.Token).AsTask();
                string? line;
                try
                {
                    line = await readTask;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (line is null)
                {
                    try
                    {
                        await _process.WaitForExitAsync(stop.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    break;
                }

                if (predicate(line))
                {
                    return line;
                }
            }

            var stderr = _errorTask.IsCompleted || _process.HasExited ? await _errorTask : string.Empty;
            var reason = _process.HasExited
                ? $"Server process exited with code {_process.ExitCode} before the expected output."
                : "Timed out waiting for server process output.";
            Assert.Fail($"{reason} stderr: {stderr}");
            throw new UnreachableException();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            await _process.WaitForExitAsync();
            _process.Dispose();
            if (_ownedSeedRoot is not null && Directory.Exists(_ownedSeedRoot))
            {
                Directory.Delete(_ownedSeedRoot, recursive: true);
            }
        }
    }
}
