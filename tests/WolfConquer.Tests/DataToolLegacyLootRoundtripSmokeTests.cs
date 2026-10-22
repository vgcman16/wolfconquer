using System.Buffers.Binary;
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

[CollectionDefinition("DataTool legacy loot roundtrip", DisableParallelization = true)]
public sealed class DataToolLegacyLootRoundtripTestCollection { }

[Collection("DataTool legacy loot roundtrip")]
public sealed class DataToolLegacyLootRoundtripSmokeTests
{
    private const uint CharacterUid = 1_397_601;
    private const uint ItemUid = 6_020_001;
    private const uint PotionId = 1_001_000;

    [Fact]
    public async Task RealEncryptedWorldRoundtripDropsPicksPersistsAndReloadsOnSecondSocket()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        await using var world = new OwnedWorld();
        var originalItems = File.ReadAllBytes(world.ItemPath);

        var result = await RunDataToolAsync(world.Arguments());

        AssertExitCode(0, result, world);
        Assert.Equal(string.Empty, result.StandardError);
        foreach (var phase in new[] { "startup-ok", "drop-ok", "pickup-ok", "relogin-ok" })
            Assert.Contains("phase=" + phase, result.StandardOutput);
        Assert.Contains("result=PASS mode=roundtrip profile=VisibleDx8D13", result.StandardOutput);
        Assert.Equal(originalItems, File.ReadAllBytes(world.ItemPath));
        var item = Assert.Single(new PlayerItemSnapshotLoader(world.ItemsRoot)
            .LoadByCharacterUid(CharacterUid).InventoryItems);
        Assert.Equal(ItemUid, item.Uid);
        Assert.Equal(PotionId, item.ItemId);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Equal((ushort)1, item.Durability);
        Assert.Equal((ushort)1, item.MaximumDurability);
        Assert.Equal((ushort)0, item.Position);

        var logins = world.AcceptedLogins.ToArray();
        Assert.Equal(2, logins.Length);
        Assert.NotSame(logins[0].Connection, logins[1].Connection);
        Assert.NotSame(logins[0].Client, logins[1].Client);
        Assert.NotSame(logins[0].Player, logins[1].Player);
        Assert.All(logins, login =>
        {
            Assert.NotNull(login.Client);
            Assert.NotNull(login.Player);
            Assert.Equal(0u, login.IngressUid);
            Assert.Equal(CharacterUid, login.AdoptedUid);
            Assert.Equal((ushort)2, login.ServerId);
            Assert.True(login.CheckedPin);
        });
        Assert.Equal(2, world.Ingress.Where(frame => frame.CompleteLogin == true)
            .Select(frame => frame.Connection).Distinct().Count());
        Assert.Contains("WorldServer handled MsgItemUsage ground drop", world.Log.ToString());
        Assert.Contains("WorldServer handled LegacyFloorItem item pickup", world.Log.ToString());
        Assert.Equal(2, CountLogLines(world.Log.ToString(), "WorldServer legacy MsgLoginGame Success"));
        Assert.False(LegacyTradeJournal.HasPending(world.UsersRoot));
    }

    [Fact]
    public async Task ExplicitVerifyOnlyUsesRealStartupWithoutDroppingOrPickingTheItem()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        await using var world = new OwnedWorld();
        var before = File.ReadAllBytes(world.ItemPath);

        var result = await RunDataToolAsync(world.Arguments().Append("--verify-only").ToArray());

        AssertExitCode(0, result, world);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.Contains("phase=startup-ok", result.StandardOutput);
        Assert.Contains("phase=verify-only-ok", result.StandardOutput);
        Assert.Contains("result=PASS mode=verify-only", result.StandardOutput);
        Assert.DoesNotContain("phase=drop-ok", result.StandardOutput);
        Assert.DoesNotContain("phase=pickup-ok", result.StandardOutput);
        Assert.DoesNotContain("phase=relogin-ok", result.StandardOutput);
        Assert.Equal(before, File.ReadAllBytes(world.ItemPath));
        Assert.Single(world.AcceptedLogins);
        Assert.Contains(world.Ingress, frame => frame.CompleteLogin == true);
        Assert.DoesNotContain("handled MsgItemUsage ground drop", world.Log.ToString());
        Assert.DoesNotContain("handled LegacyFloorItem item pickup", world.Log.ToString());
    }

    [Fact]
    public async Task MissingRequestedItemCannotProduceSuccessfulMutationPhasesOrChangeItemFile()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        await using var world = new OwnedWorld();
        var before = File.ReadAllBytes(world.ItemPath);
        var arguments = world.Arguments(timeoutMilliseconds: 1000);
        arguments[5] = (ItemUid + 1).ToString(CultureInfo.InvariantCulture);

        var result = await RunDataToolAsync(arguments);

        AssertExitCode(2, result, world);
        Assert.Contains("Legacy loot roundtrip smoke failed:", result.StandardError);
        Assert.DoesNotContain("result=PASS", result.StandardOutput);
        Assert.DoesNotContain("phase=drop-ok", result.StandardOutput);
        Assert.DoesNotContain("phase=pickup-ok", result.StandardOutput);
        Assert.Equal(before, File.ReadAllBytes(world.ItemPath));
        Assert.Single(world.AcceptedLogins);
        Assert.DoesNotContain("handled MsgItemUsage ground drop", world.Log.ToString());
        Assert.DoesNotContain("handled LegacyFloorItem item pickup", world.Log.ToString());
        // Normal login may set Users.Online; this is not a no-login/no-character-write claim.
    }

    [Theory]
    [InlineData("external-address")]
    [InlineData("dns-name")]
    [InlineData("reserved-live-port")]
    [InlineData("malformed-account-key")]
    [InlineData("zero-account-key")]
    [InlineData("overflow-character-uid")]
    [InlineData("negative-item-uid")]
    public async Task CommandRejectsUnsafeOrMalformedArgumentsBeforeConnectingOrMutatingFiles(string scenario)
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        await using var world = new OwnedWorld();
        var userBefore = File.ReadAllBytes(world.UserPath);
        var itemsBefore = File.ReadAllBytes(world.ItemPath);
        var arguments = world.Arguments(timeoutMilliseconds: 1000);
        switch (scenario)
        {
            case "external-address": arguments[1] = "192.0.2.1"; break;
            case "dns-name": arguments[1] = "localhost"; break;
            case "reserved-live-port": arguments[2] = "5818"; break;
            case "malformed-account-key": arguments[3] = "not-an-id"; break;
            case "zero-account-key": arguments[3] = "0"; break;
            case "overflow-character-uid": arguments[4] = "4294967296"; break;
            case "negative-item-uid": arguments[5] = "-1"; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        var result = await RunDataToolAsync(arguments);

        AssertExitCode(1, result, world);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardError));
        Assert.DoesNotContain("result=PASS", result.StandardOutput);
        Assert.Empty(world.Ingress);
        Assert.Empty(world.AcceptedLogins);
        Assert.DoesNotContain("accepted client", world.Log.ToString());
        Assert.Equal(userBefore, File.ReadAllBytes(world.UserPath));
        Assert.Equal(itemsBefore, File.ReadAllBytes(world.ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(world.UsersRoot));
    }

    private static int CountLogLines(string output, string text) =>
        output.Split(Environment.NewLine).Count(line => line.Contains(text, StringComparison.Ordinal));

    private static void AssertExitCode(int expected, ChildResult result, OwnedWorld world) =>
        Assert.True(result.ExitCode == expected,
            $"Expected DataTool exit {expected}, actual {result.ExitCode}." + Environment.NewLine +
            $"stdout:{Environment.NewLine}{result.StandardOutput}" +
            $"stderr:{Environment.NewLine}{result.StandardError}" +
            $"server:{Environment.NewLine}{world.Log}");

    private static async Task<ChildResult> RunDataToolAsync(params string[] arguments)
    {
        using var process = TestProjectProcess.Create(FindWorkspace(), "WolfConquer.DataTool");
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "Could not start the built DataTool child process.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool timed out. stdout: {await output} stderr: {await error}");
        }
        return new ChildResult(process.ExitCode, await output, await error);
    }

    private static string FindWorkspace()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("The real child-process tests require the workspace build output.");
    }

    private sealed class OwnedWorld : IAsyncDisposable
    {
        private readonly string workspace = FindWorkspace();
        private readonly string root;
        private readonly ConquerTcpServer server;
        internal readonly StringWriter Log = new();
        internal readonly ConcurrentQueue<SocketIngress> Ingress = new();
        internal readonly ConcurrentQueue<AcceptedLogin> AcceptedLogins = new();
        internal string UsersRoot { get; }
        internal string ItemsRoot { get; }
        internal string UserPath { get; }
        internal string ItemPath { get; }

        internal OwnedWorld()
        {
            root = Path.Combine(workspace, ".artifacts", "native-loot-roundtrip-smoke-tests", "owned-" + Guid.NewGuid().ToString("N"));
            UsersRoot = Path.Combine(root, "Users");
            ItemsRoot = Path.Combine(root, "PlayersItems");
            UserPath = Path.Combine(UsersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini");
            ItemPath = Path.Combine(ItemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
            GuardOwnedRoot();
            Assert.False(Directory.Exists(root));
            foreach (var family in new[] { "Users", "PlayersItems", "PlayersSpells", "PlayersProfs", "PlayersCombatGear", "Quests" })
                Directory.CreateDirectory(Path.Combine(root, family));
            File.WriteAllText(UserPath, CharacterText(), new UTF8Encoding(false));
            File.WriteAllBytes(ItemPath, PotionImage());
            new PlayerSpellSnapshotLoader(Path.Combine(root, "PlayersSpells")).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Path.Combine(root, "PlayersProfs")).SaveByCharacterUid(CharacterUid, PlayerProficiencySnapshot.Empty);
            new CombatGearSnapshotLoader(Path.Combine(root, "PlayersCombatGear")).SaveByCharacterUid(CharacterUid, CombatGearSnapshot.Empty);
            new PlayerQuestSnapshotLoader(Path.Combine(root, "Quests")).SaveByCharacterUid(CharacterUid, PlayerQuestSnapshot.Empty);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(UsersRoot),
                handoffService: null, worldData: PotionCatalog(),
                runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled with { LocalServerIdentity = new(2) },
                log: Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            var synchronizedLog = TextWriter.Synchronized(Log);
            server = new ConquerTcpServer(new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.WorldServer, IPAddress.Loopback, ListenPort: 0,
                WireMode: ConquerTcpWireMode.LegacyGameServer, Log: synchronizedLog, ErrorLog: synchronizedLog),
                async (connection, frame, token) =>
                {
                    var native = connection.NativeGameClient; // Born by the real encrypted socket path only.
                    var ingressUid = native?.Player.UID;
                    Ingress.Enqueue(new(frame.Header.Opcode, connection, native,
                        ingressUid, native?.Player.ServerID, native?.Player.IsCheckedPass, native?.Player.CompleteLogin));
                    await handler.HandleFrameAsync(connection, frame, token);
                    if (frame.Header.Opcode == LegacyMsgLoginGamePacket.Opcode)
                        AcceptedLogins.Enqueue(new(connection, native, native?.Player, ingressUid,
                            native?.Player.UID, native?.Player.ServerID, native?.Player.IsCheckedPass));
                });
            server.Start();
            Assert.NotEqual(5818, server.BoundEndPoint.Port);
        }

        internal string[] Arguments(int timeoutMilliseconds = 8000) =>
        [
            "legacy-loot-roundtrip-smoke", "127.0.0.1",
            server.BoundEndPoint.Port.ToString(CultureInfo.InvariantCulture),
            CharacterUid.ToString(CultureInfo.InvariantCulture), CharacterUid.ToString(CultureInfo.InvariantCulture),
            ItemUid.ToString(CultureInfo.InvariantCulture), "--timeout-ms", timeoutMilliseconds.ToString(CultureInfo.InvariantCulture)
        ];

        public async ValueTask DisposeAsync()
        {
            await server.DisposeAsync();
            Log.Dispose();
            GuardOwnedRoot();
            if (!Directory.Exists(root)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
                Assert.Equal(0, (int)(File.GetAttributes(entry) & FileAttributes.ReparsePoint));
            Directory.Delete(root, recursive: true); // Exact generated test-owned root, checked above.
        }

        private void GuardOwnedRoot()
        {
            Assert.Equal(Path.Combine(workspace, ".artifacts", "native-loot-roundtrip-smoke-tests"),
                Path.GetDirectoryName(Path.GetFullPath(root)));
            Assert.Matches("^owned-[0-9a-f]{32}$", Path.GetFileName(root));
            for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            {
                if (directory.Exists) Assert.Equal(0, (int)(directory.Attributes & FileAttributes.ReparsePoint));
                if (string.Equals(directory.FullName, workspace, StringComparison.OrdinalIgnoreCase)) break;
            }
        }
    }

    private static WorldDataCatalog PotionCatalog()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = "1001000";
        fields[1] = "LifePotion";
        fields[ItemType.DurabilityFieldIndex] = "1";
        return LegacyItemWorldDataFixture.Create(new ItemType((int)PotionId, fields[1], fields));
    }

    // Independent native DBItem184 image, count1 + record + perfectioncount0.
    private static byte[] PotionImage()
    {
        var bytes = new byte[192];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), ItemUid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), PotionId);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(56), 1);
        return bytes;
    }

    private static string CharacterText() => $$"""
        [Character]
        UID={{CharacterUid.ToString(CultureInfo.InvariantCulture)}}
        Name=LootSmokeFixture
        Body=1003
        Face=154
        Haire=27
        Class=10
        FirstClass=0
        SecoundeClass=0
        Reborn=0
        Level=120
        Experience=0
        Map=1002
        X=410
        Y=354
        PMap=1002
        PMapX=410
        PMapY=354
        Strength=100
        Agility=50
        Vitaliti=100
        Spirit=0
        Atributes=0
        MinHitPoints=1000
        MinMana=100
        Money=100
        ConquerPoints=50
        BoundConquerPoints=0
        BankCps=0
        PkPoints=0
        Online=0
        SecurityPass=0,0,0
        WHMoney=0
        DynamicID=0
        Day={{DateTime.Now.DayOfYear.ToString(CultureInfo.InvariantCulture)}}

        """;

    private sealed record ChildResult(int ExitCode, string StandardOutput, string StandardError);
    private sealed record SocketIngress(ushort Opcode, ConquerTcpConnection Connection, LegacyNativeGameClientRuntime? Client,
        uint? NativeUid, ushort? ServerId, bool? CheckedPin, bool? CompleteLogin);
    private sealed record AcceptedLogin(ConquerTcpConnection Connection, LegacyNativeGameClientRuntime? Client,
        LegacyNativePlayerRuntime? Player, uint? IngressUid, uint? AdoptedUid, ushort? ServerId, bool? CheckedPin);
}
