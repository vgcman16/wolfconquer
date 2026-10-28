using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersMonkEpicSkillTests : IDisposable
{
    private const uint MonkPromotionNpcId = 8_314;
    private const long CharacterUid = 1_293_600;
    private const long EpicSkillCost = 2_150;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersMonkEpicSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> EpicSkills => new()
    {
        { 83, 12_550, "InfernalEcho", "NpcHandler.cs:43158-43202" },
        { 84, 12_570, "WrathoftheEmperor", "NpcHandler.cs:43113-43157" },
        { 85, 12_560, "GraceofHeaven", "NpcHandler.cs:43068-43112" },
    };

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task EpicSkillOptionLearnsPersistsAndDebitsConquerPoints(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "UsersSuccess" + optionId);
        var spellsRoot = Path.Combine(_root, "SpellsSuccess" + optionId);
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, conquerPoints: 3_000);
        var session = CreateSession(conquerPoints: 3_000, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            MonkPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(850, updated.Character.ConquerPoints);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(850, LoadPersistedCharacter(usersRoot).ConquerPoints);
        Assert.Equal([(uint)spellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        Assert.Contains(ParseMsgUpdates(sent), packet => packet.Updates.Any(
            entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints && entry.Value == 850));
        Assert.Equal([spellId], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        var replies = ParseNpcReplies(sent);
        AssertReply(replies, "Congratulation! You just have learned the skill " + spellName);
        AssertOption(replies, "Ok , thank you.");
        AssertAvatar(replies, 203);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMonk epic skill", logText);
        Assert.Contains("source=NpcHandler.PromotionMonk", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.Contains("costConquerPoints=" + EpicSkillCost, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task EpicSkillOptionRejectsInsufficientConquerPointsWithoutLearningOrDebit(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "UsersInsufficient" + optionId);
        var spellsRoot = Path.Combine(_root, "SpellsInsufficient" + optionId);
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, conquerPoints: EpicSkillCost - 1);
        var session = CreateSession(conquerPoints: EpicSkillCost - 1, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            MonkPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(EpicSkillCost - 1, updated.Character.ConquerPoints);
        Assert.DoesNotContain((uint)spellId, updated.PlayerSpells.SpellIds);
        Assert.Equal(EpicSkillCost - 1, LoadPersistedCharacter(usersRoot).ConquerPoints);
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(CharacterUid).SpellIds);
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseSpellPackets(sent));
        var replies = ParseNpcReplies(sent);
        AssertReply(replies, "Come~on,~you~don`t~have~2,150~CPs~with~you.");
        AssertOption(replies, "Sorry...");
        AssertAvatar(replies, 5);

        var logText = log.ToString();
        Assert.Contains("WorldServer skipped LegacyNpc PromotionMonk epic skill", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("insufficient CP", logText);
        Assert.DoesNotContain("spellId=" + spellId + ".", logText);
        Assert.NotEmpty(spellName);
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task EpicSkillOptionAlreadyLearnedDoesNotDebitConquerPointsOrDuplicate(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "UsersAlreadyLearned" + optionId);
        var spellsRoot = Path.Combine(_root, "SpellsAlreadyLearned" + optionId);
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, conquerPoints: 3_000);
        var learned = new PlayerSpellSnapshot([Entry(spellId)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(
            conquerPoints: 3_000,
            playerSpells: learned,
            usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            MonkPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(3_000, updated.Character.ConquerPoints);
        Assert.Same(session, updated);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(3_000, LoadPersistedCharacter(usersRoot).ConquerPoints);
        Assert.Equal([(uint)spellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseSpellPackets(sent));
        var replies = ParseNpcReplies(sent);
        AssertReply(replies, $"You`ve learned {spellName}!");
        AssertOption(replies, "Okay.");
        AssertAvatar(replies, 203);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMonk epic skill", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.Contains("already learned costConquerPoints=0", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long conquerPoints,
        PlayerSpellSnapshot? playerSpells = null,
        string? usersRoot = null)
    {
        return new WorldSession(
            AccountId: CharacterUid,
            Username: "monk-epic-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "MonkHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 6_004,
                FirstClass: 6_004,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
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
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerSpellSnapshotEntry Entry(ushort id)
    {
        return new PlayerSpellSnapshotEntry(
            id,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static void WriteCharacterSnapshot(string usersRoot, long conquerPoints)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=MonkHero
            Class=6004
            Map=1002
            X=430
            Y=378
            FirstClass=6004
            SecoundeClass=0
            Reborn=0
            Level=140
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
            ConquerPoints={{conquerPoints}}
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            """);
    }

    private static CharacterSnapshot LoadPersistedCharacter(string usersRoot)
    {
        return CharacterSnapshotParser.ParseText(
            File.ReadAllText(Path.Combine(
                usersRoot,
                CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini")));
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

    private static void AssertReply(IReadOnlyList<LegacyNpcReplyPacket> replies, string text)
    {
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog && reply.Text == text);
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text)
    {
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
                reply.Text == text);
    }

    private static void AssertAvatar(IReadOnlyList<LegacyNpcReplyPacket> replies, ushort avatarId)
    {
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == avatarId);
    }
}
