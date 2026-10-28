using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTaoistEpicSkillTests : IDisposable
{
    private const uint TaoistPromotionNpcId = 10_000;
    private const long CharacterUid = 1_293_760;
    private const long EpicSkillCost = 2_150;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTaoistEpicSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string> EpicSkills => new()
    {
        { 91, 12_370, "AuroraLotus" },
        { 92, 12_390, "BlessingTouch" },
        { 93, 12_380, "FlameLotus" },
        { 94, 12_400, "SearingTouch" },
    };

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task TaoistEpicSkillOptionLearnsPersistsAndDebitsConquerPoints(
        byte optionId,
        ushort spellId,
        string spellName)
    {
        var result = await InvokeTaoistEpicPromotionAsync(
            optionId,
            level: 15,
            conquerPoints: 3_000);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(850, updated.Character.ConquerPoints);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(850, LoadPersistedCharacter(result.UsersRoot).ConquerPoints);
        Assert.Equal([(uint)spellId], LoadPersistedSpells(result.SpellsRoot).SpellIds.ToArray());

        Assert.Contains(ParseMsgUpdates(result.Sent), packet => packet.Updates.Any(
            entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints && entry.Value == 850));
        Assert.Equal([spellId], ParseSpellPackets(result.Sent).Select(packet => packet.Id).ToArray());
        AssertDialogWithoutAvatar(
            ParseNpcReplies(result.Sent),
            "\tYou`ve acquired [" + spellName + "]. Go practice and feel its power.",
            "Thanks!");

        Assert.Contains("WorldServer handled LegacyNpc PromotionTaoist epic skill", result.Log);
        Assert.Contains("source=NpcHandler.PromotionTaoist", result.Log);
        Assert.Contains("sourceRange=NpcHandler.cs:41288-41365", result.Log);
        Assert.Contains("option=" + optionId, result.Log);
        Assert.Contains("spellId=" + spellId, result.Log);
        Assert.Contains("costConquerPoints=" + EpicSkillCost, result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task TaoistEpicSkillOptionRejectsBelowLevelBeforeConquerPointsOrLearning(
        byte optionId,
        ushort spellId,
        string spellName)
    {
        var result = await InvokeTaoistEpicPromotionAsync(
            optionId,
            level: 14,
            conquerPoints: 0);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(0, updated.Character.ConquerPoints);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.Equal(0, LoadPersistedCharacter(result.UsersRoot).ConquerPoints);
        Assert.Empty(LoadPersistedSpells(result.SpellsRoot).SpellIds);
        Assert.Empty(ParseMsgUpdates(result.Sent));
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertDialogWithoutAvatar(
            ParseNpcReplies(result.Sent),
            "\tThe study of [" + spellName + "] requires you to reach Level 15.",
            "I`ll~find~you~at~Lv.15!");
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "Come~on,~you~don`t~have~2,150~CPs~with~you.");

        Assert.Contains("sourceRange=NpcHandler.cs:41288-41365", result.Log);
        Assert.Contains("option=" + optionId, result.Log);
        Assert.Contains("spellId=" + spellId, result.Log);
        Assert.Contains("level=14 requiredLevel=15; source level gate rejected", result.Log);
        Assert.DoesNotContain("insufficient CP", result.Log);
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task TaoistEpicSkillOptionRejectsInsufficientConquerPointsWithoutLearningOrDebit(
        byte optionId,
        ushort spellId,
        string spellName)
    {
        var result = await InvokeTaoistEpicPromotionAsync(
            optionId,
            level: 15,
            conquerPoints: EpicSkillCost - 1);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(EpicSkillCost - 1, updated.Character.ConquerPoints);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.Equal(EpicSkillCost - 1, LoadPersistedCharacter(result.UsersRoot).ConquerPoints);
        Assert.Empty(LoadPersistedSpells(result.SpellsRoot).SpellIds);
        Assert.Empty(ParseMsgUpdates(result.Sent));
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertDialogWithAvatar(
            ParseNpcReplies(result.Sent),
            "Come~on,~you~don`t~have~2,150~CPs~with~you.",
            "Sorry...",
            5);

        Assert.Contains("sourceRange=NpcHandler.cs:41288-41365", result.Log);
        Assert.Contains("option=" + optionId, result.Log);
        Assert.Contains("spellId=" + spellId, result.Log);
        Assert.Contains("insufficient CP", result.Log);
        Assert.DoesNotContain("\tYou`ve acquired [" + spellName + "]", result.Log);
    }

    [Theory]
    [MemberData(nameof(EpicSkills))]
    public async Task TaoistEpicSkillOptionAlreadyLearnedDoesNotDebitConquerPointsOrDuplicate(
        byte optionId,
        ushort spellId,
        string spellName)
    {
        var learned = new PlayerSpellSnapshot([Entry(spellId)]);
        var result = await InvokeTaoistEpicPromotionAsync(
            optionId,
            level: 1,
            conquerPoints: 0,
            playerSpells: learned,
            caseName: "already-learned-" + spellName);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(0, updated.Character.ConquerPoints);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(0, LoadPersistedCharacter(result.UsersRoot).ConquerPoints);
        Assert.Equal([(uint)spellId], LoadPersistedSpells(result.SpellsRoot).SpellIds.ToArray());
        Assert.Empty(ParseMsgUpdates(result.Sent));
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertDialogWithoutAvatar(
            ParseNpcReplies(result.Sent),
            "Taoist Master has already acquired this skill. Go practice and feel its power.",
            "Okay.");
        Assert.DoesNotContain(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "\tThe study of [" + spellName + "] requires you to reach Level 15.");

        Assert.Contains("sourceRange=NpcHandler.cs:41288-41365", result.Log);
        Assert.Contains("option=" + optionId, result.Log);
        Assert.Contains("spellId=" + spellId + "; already learned", result.Log);
        Assert.DoesNotContain("costConquerPoints=" + EpicSkillCost, result.Log);
        Assert.DoesNotContain("insufficient CP", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<TaoistEpicPromotionInvokeResult> InvokeTaoistEpicPromotionAsync(
        byte optionId,
        long level,
        long conquerPoints,
        PlayerSpellSnapshot? playerSpells = null,
        string? caseName = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-" + (caseName ?? optionId.ToString(CultureInfo.InvariantCulture)));
        var spellsRoot = Path.Combine(_root, "Spells-" + (caseName ?? optionId.ToString(CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(spellsRoot);
        WriteCharacterSnapshot(usersRoot, level, conquerPoints);

        var initialSpells = playerSpells ?? PlayerSpellSnapshot.Empty;
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, initialSpells);
        var session = CreateSession(level, conquerPoints, initialSpells, usersRoot);
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
            TaoistPromotionNpcId,
            optionId,
            CancellationToken.None);

        return new TaoistEpicPromotionInvokeResult(
            handled,
            sent,
            connection,
            sessions,
            usersRoot,
            spellsRoot,
            log.ToString());
    }

    private static WorldSession CreateSession(
        long level,
        long conquerPoints,
        PlayerSpellSnapshot playerSpells,
        string usersRoot)
    {
        return new WorldSession(
            AccountId: CharacterUid,
            Username: "taoist-epic-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "TaoistHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 13_002,
                FirstClass: 13_002,
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

    private static void WriteCharacterSnapshot(string usersRoot, long level, long conquerPoints)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=TaoistHero
            Class=13002
            Map=1002
            X=430
            Y=378
            FirstClass=13002
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

    private static void AssertDialogWithoutAvatar(
        IReadOnlyList<LegacyNpcReplyPacket> replies,
        string expectedText,
        string expectedOptionText)
    {
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == expectedText);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == expectedOptionText);
        Assert.DoesNotContain(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar);
    }

    private static void AssertDialogWithAvatar(
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
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == expectedOptionText);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
            reply.InputMaxLength == expectedAvatarId);
    }

    private sealed record TaoistEpicPromotionInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        ConquerTcpConnection Connection,
        WorldSessionStore Sessions,
        string UsersRoot,
        string SpellsRoot,
        string Log);
}
