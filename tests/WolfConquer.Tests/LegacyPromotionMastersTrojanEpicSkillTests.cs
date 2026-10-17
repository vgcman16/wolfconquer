using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTrojanEpicSkillTests : IDisposable
{
    private const long CharacterUid = 1_293_600;
    private const uint TrojanPromotionNpcId = 10_022;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTrojanEpicSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string, ushort, uint> EpicSkills => new()
    {
        { 17, 11_990, "MortalStrike", "NpcHandler.cs:47985-48042", 4, 614_000 },
        { 16, 11_980, "FatalCross", "NpcHandler.cs:48043-48100", 5, 614_001 },
    };

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task EpicSkillOptionDebitsPersistsAndSendsSpellPacket(
        byte optionId,
        ushort spellId,
        string skillName,
        string sourceRange,
        ushort equipmentPosition,
        uint epicWeaponItemId)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "UsersSuccess" + optionId);
        var spellsRoot = Path.Combine(_root, "SpellsSuccess" + optionId);
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, conquerPoints: 3_000);
        var session = CreateSession(
            conquerPoints: 3_000,
            playerItems: EquippedEpicWeapon(epicWeaponItemId, equipmentPosition),
            usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            TrojanPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(850, updated.Character.ConquerPoints);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([(uint)spellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Equal(850, LoadPersistedCharacter(usersRoot).ConquerPoints);

        Assert.Contains(ParseMsgUpdates(sent), packet => packet.Updates.Any(
            entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints && entry.Value == 850));
        Assert.Equal([spellId], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.Text.StartsWith(
                "You~successfully~acquired~the~skill,~[Mortal~Strike].",
                StringComparison.Ordinal));
        Assert.DoesNotContain(replies, reply => reply.Text == $"You`ve learned {skillName}!");
        Assert.Contains(replies, reply => reply.Text == "~and~more~violent~to~monsters,~within~a~certain~range.");
        Assert.Contains(replies, reply => reply.Text == "Thanks!" && reply.OptionId == byte.MaxValue);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionTrojan epic skill", logText);
        Assert.Contains($"sourceRange={sourceRange}", logText);
        Assert.Contains($"option={optionId}", logText);
        Assert.Contains($"spellId={spellId}", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
        Assert.NotEmpty(skillName);
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task EpicSkillOptionRejectsWhenTrojanEpicWeaponIsMissing(
        byte optionId,
        ushort spellId,
        string skillName,
        string sourceRange,
        ushort equipmentPosition,
        uint epicWeaponItemId)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "UsersNoWeapon" + optionId);
        var spellsRoot = Path.Combine(_root, "SpellsNoWeapon" + optionId);
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, conquerPoints: 3_000);
        var session = CreateSession(conquerPoints: 3_000, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            TrojanPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(3_000, updated.Character.ConquerPoints);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.DoesNotContain((uint)spellId, updated.PlayerSpells.SpellIds);
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(CharacterUid).SpellIds);
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == $"You need to equip a Trojan`s Epic Weapon to learn [{skillName}].");
        Assert.Contains($"sourceRange={sourceRange}", log.ToString());
        Assert.Contains("missing equipped Trojan epic weapon", log.ToString());
        Assert.True(equipmentPosition is 4 or 5);
        Assert.True(epicWeaponItemId is >= 614_000 and <= 614_439);
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task EpicSkillOptionRejectsWhenConquerPointsAreInsufficient(
        byte optionId,
        ushort spellId,
        string skillName,
        string sourceRange,
        ushort equipmentPosition,
        uint epicWeaponItemId)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "UsersInsufficient" + optionId);
        var spellsRoot = Path.Combine(_root, "SpellsInsufficient" + optionId);
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, conquerPoints: 2_149);
        var session = CreateSession(
            conquerPoints: 2_149,
            playerItems: EquippedEpicWeapon(epicWeaponItemId, equipmentPosition),
            usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            TrojanPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(2_149, updated.Character.ConquerPoints);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.DoesNotContain((uint)spellId, updated.PlayerSpells.SpellIds);
        Assert.Equal(2_149, LoadPersistedCharacter(usersRoot).ConquerPoints);
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(CharacterUid).SpellIds);
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseSpellPackets(sent));
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Come~on,~you~don`t~have~2,150~CPs~with~you.");
        Assert.DoesNotContain(replies, reply => reply.Text == $"You`ve learned {skillName}!");
        Assert.Contains($"sourceRange={sourceRange}", log.ToString());
        Assert.Contains("insufficient CP", log.ToString());
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task EpicSkillOptionAlreadyLearnedDoesNotDebitOrPersistDuplicate(
        byte optionId,
        ushort spellId,
        string skillName,
        string sourceRange,
        ushort equipmentPosition,
        uint epicWeaponItemId)
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
            playerItems: EquippedEpicWeapon(epicWeaponItemId, equipmentPosition),
            playerSpells: learned,
            usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            TrojanPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(3_000, updated.Character.ConquerPoints);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(3_000, LoadPersistedCharacter(usersRoot).ConquerPoints);
        Assert.Equal([(uint)spellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == $"You`ve learned {skillName}!");
        Assert.Contains($"sourceRange={sourceRange}", log.ToString());
        Assert.Contains("already learned", log.ToString());
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
        PlayerItemSnapshot? playerItems = null,
        PlayerSpellSnapshot? playerSpells = null,
        string? usersRoot = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "trojan-epic-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "TrojanHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 1_051,
                FirstClass: 1_051,
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
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshot EquippedEpicWeapon(uint itemId, ushort position)
    {
        return PlayerItemSnapshot.Empty with
        {
            EquipmentItems =
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 10_000U + position,
                    ItemId = itemId,
                    Position = position,
                    Durability = 50,
                    MaximumDurability = 50,
                },
            ],
        };
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
            Name=TrojanHero
            Class=1051
            Map=1002
            X=430
            Y=378
            FirstClass=1051
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
                CharacterUid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini")));
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
}
