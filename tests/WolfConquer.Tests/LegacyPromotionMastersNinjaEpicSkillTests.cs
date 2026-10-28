using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersNinjaEpicSkillTests : IDisposable
{
    private const uint NinjaPromotionNpcId = 4_972;
    private const long CharacterUid = 1_293_670;
    private const ushort NinjaAvatarId = 203;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersNinjaEpicSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> EpicSkills => new()
    {
        { 83, 12_090, "ShadowClone", "NpcHandler.cs:44207-44240" },
        { 84, 12_070, "TwilightDance", "NpcHandler.cs:44173-44206" },
        { 85, 12_110, "FatalSpin", "NpcHandler.cs:44139-44172" },
    };

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task NinjaEpicSkillOptionLearnsPersistsAndDebitsConquerPoints(
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
            NinjaPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(850, updated.Character.ConquerPoints);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([(uint)spellId], LoadPersistedSpells(spellsRoot).SpellIds.ToArray());
        Assert.Equal(850, LoadPersistedCharacter(usersRoot).ConquerPoints);

        Assert.Contains(ParseMsgUpdates(sent), packet => packet.Updates.Any(
            entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints && entry.Value == 850));
        Assert.Equal([spellId], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        AssertDialog(
            ParseNpcReplies(sent),
            "Congratulation! You just have learned the skill " + spellName,
            "Ok , thank you.",
            NinjaAvatarId);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionNinja epic skill", logText);
        Assert.Contains("source=NpcHandler.PromotionNinja", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task NinjaEpicSkillOptionRejectsWhenConquerPointsAreInsufficient(
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
        WriteCharacterSnapshot(usersRoot, conquerPoints: 2_149);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(conquerPoints: 2_149, usersRoot: usersRoot);
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
            NinjaPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(2_149, updated.Character.ConquerPoints);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.Equal(2_149, LoadPersistedCharacter(usersRoot).ConquerPoints);
        Assert.Empty(LoadPersistedSpells(spellsRoot).SpellIds);
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseSpellPackets(sent));
        AssertDialog(
            ParseNpcReplies(sent),
            "Come~on,~you~don`t~have~2,150~CPs~with~you.",
            "Sorry...",
            5);
        Assert.DoesNotContain(ParseNpcReplies(sent), reply => reply.Text == $"You`ve learned {spellName}!");

        var logText = log.ToString();
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.Contains("insufficient CP", logText);
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task NinjaEpicSkillOptionAlreadyLearnedDoesNotDebitOrDuplicate(
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
        var learned = new PlayerSpellSnapshot([CreateSpellEntry(spellId)]);
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
            NinjaPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(3_000, updated.Character.ConquerPoints);
        Assert.Same(session, updated);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([(uint)spellId], LoadPersistedSpells(spellsRoot).SpellIds.ToArray());
        Assert.Equal(3_000, LoadPersistedCharacter(usersRoot).ConquerPoints);

        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseSpellPackets(sent));
        AssertDialog(ParseNpcReplies(sent), $"You`ve learned {spellName}!", "Okay.", NinjaAvatarId);

        var logText = log.ToString();
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.Contains("already learned", logText);
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
            Username: "ninja-epic-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "NinjaHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 5_005,
                FirstClass: 5_005,
                SecondClass: 0,
                Reborn: 2,
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

    private static PlayerSpellSnapshotEntry CreateSpellEntry(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
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
            Path.Combine(usersRoot, CharacterUid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=NinjaHero
            Class=5005
            Map=1002
            X=430
            Y=378
            FirstClass=5005
            SecoundeClass=0
            Reborn=2
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
                CharacterUid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini")));
    }

    private static PlayerSpellSnapshot LoadPersistedSpells(string spellsRoot)
    {
        return new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(CharacterUid);
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

    private static void AssertDialog(
        IReadOnlyList<LegacyNpcReplyPacket> replies,
        string expectedText,
        string expectedOptionText,
        ushort expectedAvatarId)
    {
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == expectedText);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.Text == expectedOptionText &&
            reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
            reply.InputMaxLength == expectedAvatarId);
    }
}
