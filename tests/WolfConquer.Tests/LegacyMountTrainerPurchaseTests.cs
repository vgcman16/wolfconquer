using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMountTrainerPurchaseTests : IDisposable
{
    private const uint MountTrainerNpcId = 5_600;
    private const long CharacterUid = 1_293_820;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyMountTrainerPurchaseTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, long, long, long, string> PurchasableMountSkills => new()
    {
        { 2, 7_001, 50_000, 1, 70_000, "Riding" },
        { 4, 7_002, 100_000, 1, 125_000, "Spook" },
        { 6, 7_003, 1_000_000, 1, 1_250_000, "WarCry" },
        { 8, 11_190, 500_000, 30, 600_000, "ChargingVortex" },
    };

    [Theory]
    [MemberData(nameof(PurchasableMountSkills))]
    public async Task MountTrainerPurchasePersistsMoneyAndLevelZeroSpellForReload(
        byte optionId,
        ushort spellId,
        long cost,
        long level,
        long money,
        string spellName)
    {
        var result = await InvokeMountTrainerAsync(optionId, level, money);

        Assert.True(result.Handled);
        Assert.Equal(money - cost, result.UpdatedSession.Character.Money);
        Assert.Equal([(uint)spellId], result.UpdatedSession.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(money - cost, LoadPersistedCharacter(result.UsersRoot).Money);

        var reloadedSpells = new PlayerSpellSnapshotLoader(result.SpellsRoot).LoadByCharacterUid(CharacterUid);
        var reloadedSpell = Assert.Single(reloadedSpells.Spells);
        Assert.Equal(spellId, reloadedSpell.Id);
        Assert.Equal(0, reloadedSpell.Level);
        Assert.Equal(0, reloadedSpell.Experience);

        Assert.Contains(ParseMsgUpdates(result.Sent), packet => packet.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.Money &&
            entry.Value == (ulong)(money - cost)));
        Assert.Equal([spellId], ParseSpellPackets(result.Sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task MountTrainerPersistedCommitDoesNotRepeatOrdinaryObserver()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "observer", "Users");
        var spellsRoot = Path.Combine(_root, "observer", "Spells");
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, 1, 70_000);
        var emptySpells = new PlayerSpellSnapshot(Array.Empty<PlayerSpellSnapshotEntry>());
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, emptySpells);
        var session = CreateSession(1, 70_000, emptySpells, usersRoot);
        int notifications = 0; bool armed = false;
        var sessions = new WorldSessionStore((_, _, _) =>
        {
            notifications++;
            if (armed) throw new IOException("ordinary observer must not run during persisted commit");
        });
        sessions.Set(connection, session);
        var context = LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
            connection, log, Session: session, Sessions: sessions, PlayerSpellsRoot: spellsRoot), ref session);
        int beforeNotifications = notifications; armed = true;
        var handled = await WorldPacketHandlers.TryHandleLegacyJailGuildServicesNpcAsync(
            context, session, MountTrainerNpcId, 2, CancellationToken.None);
        Assert.True(handled);
        Assert.Equal(beforeNotifications, notifications);
        Assert.True(sessions.TryGet(connection, out var current));
        Assert.NotSame(session, current);
        Assert.Equal(20_000, current!.Character.Money);
        Assert.Equal((ushort)7_001, Assert.Single(current.PlayerSpells.Spells).Id);
        Assert.Equal(20_000, LoadPersistedCharacter(usersRoot).Money);
        Assert.Equal((ushort)7_001, Assert.Single(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid).Spells).Id);
        Assert.False(context.TradeRecovery!.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(usersRoot));
        Assert.Contains(ParseMsgUpdates(sent), packet => packet.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.Money && entry.Value == 20_000));
        Assert.Equal([(ushort)7_001], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Congratulation! You just have learned the skill Riding");
    }

    [Fact]
    public void FreeSpellPersistedCommitDoesNotRepeatOrdinaryObserver()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "free-observer", "Users");
        var spellsRoot = Path.Combine(_root, "free-observer", "Spells");
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, 1, 70_000);
        var before = new PlayerSpellSnapshot(new[] { Entry(7_001) with { SoulLevel = 4 } });
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, before);
        var session = CreateSession(1, 70_000, before, usersRoot);
        int notifications = 0; bool armed = false;
        var sessions = new WorldSessionStore((_, _, _) =>
        {
            notifications++;
            if (armed) throw new IOException("ordinary observer must not run during persisted commit");
        });
        sessions.Set(connection, session);
        var context = LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
            connection, log, Session: session, Sessions: sessions, PlayerSpellsRoot: spellsRoot), ref session);
        var after = new PlayerSpellSnapshot(new[] { Entry(7_001) with { SoulLevel = 4, UseSpellSoul = 1 } });
        var method = typeof(WorldPacketHandlers).GetMethod("TryCommitLegacyFreeSpells",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        var originalCharacter = File.ReadAllBytes(Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini"));
        int beforeNotifications = notifications; armed = true;
        object?[] invocation = [context, session, after, CancellationToken.None, null, null];
        Assert.True((bool)method!.Invoke(null, invocation)!, "reason=" + invocation[5]);
        Assert.Equal(beforeNotifications, notifications);
        var final = Assert.IsType<WorldSession>(invocation[4]);
        Assert.True(sessions.TryGet(connection, out var current)); Assert.Same(final, current);
        Assert.NotSame(session, final);
        Assert.Equal(1, Assert.Single(final.PlayerSpells.Spells).UseSpellSoul);
        Assert.Equal(1, Assert.Single(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid).Spells).UseSpellSoul);
        Assert.Equal(originalCharacter, File.ReadAllBytes(Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini")));
        Assert.False(context.TradeRecovery!.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(usersRoot));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task MountTrainerDuplicateSpellDoesNotDebitOrDuplicatePersistence()
    {
        var learned = new PlayerSpellSnapshot([Entry(7_002)]);
        var result = await InvokeMountTrainerAsync(
            optionId: 4,
            level: 1,
            money: 100_000,
            playerSpells: learned,
            caseName: "duplicate-spook");

        Assert.True(result.Handled);
        Assert.Equal(100_000, result.UpdatedSession.Character.Money);
        Assert.Equal([7_002U], result.UpdatedSession.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(100_000, LoadPersistedCharacter(result.UsersRoot).Money);
        Assert.Equal([7_002U], new PlayerSpellSnapshotLoader(result.SpellsRoot).LoadByCharacterUid(CharacterUid).SpellIds.ToArray());
        Assert.Empty(ParseMsgUpdates(result.Sent));
        Assert.Empty(ParseSpellPackets(result.Sent));
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == "You`ve learned Spook!");
    }

    [Fact]
    public async Task MountTrainerInsufficientSilverDoesNotDebitOrLearn()
    {
        var result = await InvokeMountTrainerAsync(
            optionId: 6,
            level: 1,
            money: 999_999,
            caseName: "insufficient-warcry");

        Assert.True(result.Handled);
        Assert.Equal(999_999, result.UpdatedSession.Character.Money);
        Assert.Empty(result.UpdatedSession.PlayerSpells.SpellIds);
        Assert.Equal(999_999, LoadPersistedCharacter(result.UsersRoot).Money);
        Assert.Empty(new PlayerSpellSnapshotLoader(result.SpellsRoot).LoadByCharacterUid(CharacterUid).SpellIds);
        Assert.Empty(ParseMsgUpdates(result.Sent));
        Assert.Empty(ParseSpellPackets(result.Sent));
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.Text == "You don`t have 1,000,000 silver. You cannot learn WarCry.");
    }

    [Fact]
    public async Task ChargingVortexLevelGateRunsBeforeSilverOrLearning()
    {
        var result = await InvokeMountTrainerAsync(
            optionId: 8,
            level: 29,
            money: 0,
            caseName: "charging-vortex-level-gate");

        Assert.True(result.Handled);
        Assert.Equal(0, result.UpdatedSession.Character.Money);
        Assert.Empty(result.UpdatedSession.PlayerSpells.SpellIds);
        Assert.Equal(0, LoadPersistedCharacter(result.UsersRoot).Money);
        Assert.Empty(new PlayerSpellSnapshotLoader(result.SpellsRoot).LoadByCharacterUid(CharacterUid).SpellIds);
        Assert.Empty(ParseMsgUpdates(result.Sent));
        Assert.Empty(ParseSpellPackets(result.Sent));
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == "Sorry, you need to reach level 30 first.");
        Assert.DoesNotContain(ParseNpcReplies(result.Sent), reply =>
            reply.Text.Contains("do not have enough silver", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<MountTrainerInvokeResult> InvokeMountTrainerAsync(
        byte optionId,
        long level,
        long money,
        PlayerSpellSnapshot? playerSpells = null,
        string? caseName = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var directoryName = caseName ?? "option-" + optionId.ToString(CultureInfo.InvariantCulture);
        var usersRoot = Path.Combine(_root, directoryName, "Users");
        var spellsRoot = Path.Combine(_root, directoryName, "Spells");
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, level, money);

        var initialSpells = playerSpells ?? PlayerSpellSnapshot.Empty;
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, initialSpells);
        var session = CreateSession(level, money, initialSpells, usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyJailGuildServicesNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            MountTrainerNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new MountTrainerInvokeResult(
            handled,
            sent,
            updatedSession,
            usersRoot,
            spellsRoot,
            log.ToString());
    }

    private static WorldSession CreateSession(
        long level,
        long money,
        PlayerSpellSnapshot playerSpells,
        string usersRoot)
    {
        return new WorldSession(
            AccountId: CharacterUid,
            Username: "mount-trainer-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "MountHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
                Level: level,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: money,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerSpellSnapshotEntry Entry(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static void WriteCharacterSnapshot(string usersRoot, long level, long money)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=MountHero
            Class=7051
            Map=1002
            X=430
            Y=378
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level={{level}}
            Haire=0
            Experience=0
            PMap=1002
            PMapX=430
            PMapY=378
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            MaxMana=0
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money={{money}}
            PkPoints=0
            Online=0
            """);
    }

    private static CharacterSnapshot LoadPersistedCharacter(string usersRoot)
    {
        return CharacterSnapshotParser.ParseText(
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini")));
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacySpellPacket> ParseSpellPackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacySpellPacket>();
        foreach (var bytes in sent)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record MountTrainerInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        WorldSession UpdatedSession,
        string UsersRoot,
        string SpellsRoot,
        string Log);
}
