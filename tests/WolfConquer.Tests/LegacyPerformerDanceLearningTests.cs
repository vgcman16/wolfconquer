using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPerformerDanceLearningTests : IDisposable
{
    private const uint PerformerNpcId = 7_776;
    private const long CharacterUid = 1_293_600;
    private const ushort PerformerAvatarId = 110;
    private const ushort ExistingSpellId = 900;

    private static readonly DanceCase[] DanceCases =
    [
        new(
            OptionId: 12,
            SpellId: 1_420,
            DanceName: "Phoenix",
            RequiredPhase: 3,
            PhaseGateText: "I`m sorry, but only Phase 3 Performers can learn this dance. I can`t teach you this dance now.",
            PhaseGateOptionText: "Alright.",
            AlreadyLearnedText: "But you`ve already learned this dance, remember?",
            AlreadyLearnedOptionText: "Oh, alright.",
            SystemMessage: "You`ve learned the Partner Dance, Phoenix!",
            SuccessDialogText: "You`ve learned the Partner Dance, Phoenix!",
            SuccessOptionText: "Thanks."),
        new(
            OptionId: 11,
            SpellId: 1_421,
            DanceName: "Melody",
            RequiredPhase: 6,
            PhaseGateText: "I`m sorry, but only Phase 6 Performers can learn this dance. I can`t teach you this dance now.",
            PhaseGateOptionText: "Alright.",
            AlreadyLearnedText: "But you`ve already learned this dance, remember?",
            AlreadyLearnedOptionText: "Oh, alright.",
            SystemMessage: "You`ve learned the Partner Dance, Melody!",
            SuccessDialogText: "You`ve learned the Partner Dance, Melody!",
            SuccessOptionText: "Thanks."),
        new(
            OptionId: 10,
            SpellId: 1_422,
            DanceName: "Tipsy",
            RequiredPhase: 9,
            PhaseGateText: "I`m sorry, but only Phase 9 Performers can learn this dance. I can`t teach you this dance now.",
            PhaseGateOptionText: "Alright.",
            AlreadyLearnedText: "But you`ve already learned this dance, remember?",
            AlreadyLearnedOptionText: "Oh, alright.",
            SystemMessage: "You`ve learned the Partner Dance, Tipsy!",
            SuccessDialogText: "You`ve learned the Partner Dance, Tipsy!",
            SuccessOptionText: "Thanks."),
        new(
            OptionId: 8,
            SpellId: 1_415,
            DanceName: "Battle Dance",
            RequiredPhase: 1,
            PhaseGateText: "You can only learn it at Phase 1.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Battle Dance.",
            SuccessDialogText: "You`ve learned the Battle Dance. Hope you`ll work on it and learn more dances in the future. See you!",
            SuccessOptionText: "See you!"),
        new(
            OptionId: 7,
            SpellId: 1_416,
            DanceName: "Triumph",
            RequiredPhase: 3,
            PhaseGateText: "You can only learn it at Phase 3.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Triumph.",
            SuccessDialogText: "You`ve learned the Triumph. Hope you`ll work on it and learn more dances in the future. See you!",
            SuccessOptionText: "See you!"),
        new(
            OptionId: 6,
            SpellId: 1_417,
            DanceName: "Step Stomp",
            RequiredPhase: 5,
            PhaseGateText: "You can only learn it at Phase 5.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Step Stomp.",
            SuccessDialogText: "You`ve learned the Step Stomp. Hope you`ll work on it and learn more dances in the future. See you!",
            SuccessOptionText: "See you!"),
        new(
            OptionId: 5,
            SpellId: 1_418,
            DanceName: "Moonlight",
            RequiredPhase: 7,
            PhaseGateText: "You can only learn it at Phase 7.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Moonlight.",
            SuccessDialogText: "You`ve learned the Moonlight. Hope you`ll work on it and learn more dances in the future. See you!",
            SuccessOptionText: "See you!"),
        new(
            OptionId: 4,
            SpellId: 1_419,
            DanceName: "Snow Wind",
            RequiredPhase: 9,
            PhaseGateText: "You can only learn it at Phase 9.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Snow Wind.",
            SuccessDialogText: "You`ve learned the Snow Wind. You are the master of dances now!",
            SuccessOptionText: "Yay!"),
    ];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-performer-dance-learning-" + Guid.NewGuid().ToString("N"));

    public static IEnumerable<object[]> AllDanceSuccessCases()
    {
        foreach (var dance in DanceCases)
        {
            yield return
            [
                dance.OptionId,
                dance.SpellId,
                dance.DanceName,
                dance.RequiredPhase,
                dance.SystemMessage,
                dance.SuccessDialogText,
                dance.SuccessOptionText,
            ];
        }
    }

    public static IEnumerable<object[]> AllDancePhaseGateCases()
    {
        foreach (var dance in DanceCases)
        {
            yield return
            [
                dance.OptionId,
                dance.SpellId,
                dance.RequiredPhase,
                dance.PhaseGateText,
                dance.PhaseGateOptionText,
            ];
        }
    }

    public static IEnumerable<object[]> AllDanceDuplicateCases()
    {
        foreach (var dance in DanceCases)
        {
            yield return
            [
                dance.OptionId,
                dance.SpellId,
                dance.RequiredPhase,
                dance.AlreadyLearnedText,
                dance.AlreadyLearnedOptionText,
            ];
        }
    }

    [Theory]
    [MemberData(nameof(AllDanceSuccessCases))]
    public async Task DanceChoicesLearnExactSourceSpellPersistBeforeCommitAndSendSourcePacketOrder(
        byte optionId,
        ushort spellId,
        string danceName,
        byte requiredPhase,
        string systemMessage,
        string successDialogText,
        string successOptionText)
    {
        var spellsRoot = Path.Combine(_root, "success-" + optionId);
        var sessions = new WorldSessionStore();
        var originalSpells = CreateSpells(CreateExistingSpell());
        var result = await InvokeAsync(
            optionId,
            performerPhrase: requiredPhase,
            originalSpells,
            sessions,
            spellsRoot);

        Assert.True(result.Handled);
        Assert.True(sessions.TryGet(result.Connection, out var stored));
        Assert.NotNull(stored);
        Assert.NotSame(result.OriginalSession, stored);
        Assert.Same(result.OriginalSession.PlayerItems, stored.PlayerItems);
        Assert.False(result.OriginalSession.PlayerSpells.ContainsSpell(spellId));
        Assert.Equal([ExistingSpellId, spellId], stored.PlayerSpells.Spells.Select(spell => spell.Id).ToArray());

        var learned = Assert.Single(stored.PlayerSpells.Spells, spell => spell.Id == spellId);
        Assert.Equal(new PlayerSpellSnapshotEntry(spellId, 0, 0, 0, 0, 0), learned);

        var persisted = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(CharacterUid);
        Assert.Equal(stored.PlayerSpells.Spells.ToArray(), persisted.Spells.ToArray());
        AssertSourceLearningPacketOrder(
            result.Sent,
            spellId,
            systemMessage,
            successDialogText,
            successOptionText);

        Assert.Contains("source=NpcHandler.Performer", result.Log);
        Assert.Contains("sourceRange=NpcHandler.cs:12181-12380", result.Log);
        Assert.Contains("spellHelper=MrRayzo.Role.Instance\\Spell.cs:61-83", result.Log);
        Assert.Contains($"option={optionId} dance={danceName} spellId={spellId}", result.Log);
        Assert.Contains("persisted PlayerSpells before session commit", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AllDancePhaseGateCases))]
    public async Task DanceChoicesRejectExactSourcePhaseGateWithoutMutation(
        byte optionId,
        ushort spellId,
        byte requiredPhase,
        string phaseGateText,
        string phaseGateOptionText)
    {
        var spellsRoot = Path.Combine(_root, "phase-gate-" + optionId);
        var sessions = new WorldSessionStore();
        var performerPhrase = requiredPhase == 1
            ? (byte?)null
            : checked((byte)(requiredPhase - 1));
        var result = await InvokeAsync(
            optionId,
            performerPhrase,
            CreateSpells(CreateExistingSpell()),
            sessions,
            spellsRoot);

        Assert.True(result.Handled);
        Assert.True(sessions.TryGet(result.Connection, out var stored));
        Assert.Same(result.OriginalSession, stored);
        Assert.Same(result.OriginalSession.PlayerItems, stored!.PlayerItems);
        Assert.False(stored.PlayerSpells.ContainsSpell(spellId));
        Assert.False(File.Exists(GetSpellSnapshotPath(spellsRoot)));
        AssertDialogOnly(result.Sent, phaseGateText, phaseGateOptionText);
        Assert.Contains($"option={optionId}", result.Log);
        Assert.Contains($"spellId={spellId}", result.Log);
        Assert.Contains($"requiredPhase={requiredPhase}; source subclass gate rejected", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AllDanceDuplicateCases))]
    public async Task DanceChoicesRejectExactSourceDuplicateWithoutMutationOrPersistence(
        byte optionId,
        ushort spellId,
        byte requiredPhase,
        string alreadyLearnedText,
        string alreadyLearnedOptionText)
    {
        var spellsRoot = Path.Combine(_root, "duplicate-" + optionId);
        var originalSpells = CreateSpells(
            CreateExistingSpell(),
            new PlayerSpellSnapshotEntry(spellId, 2, 345, 1, 2, 1));
        var loader = new PlayerSpellSnapshotLoader(spellsRoot);
        loader.SaveByCharacterUid(CharacterUid, originalSpells);
        var persistedBefore = File.ReadAllBytes(GetSpellSnapshotPath(spellsRoot));
        var sessions = new WorldSessionStore();

        var result = await InvokeAsync(
            optionId,
            performerPhrase: requiredPhase,
            originalSpells,
            sessions,
            spellsRoot);

        Assert.True(result.Handled);
        Assert.True(sessions.TryGet(result.Connection, out var stored));
        Assert.Same(result.OriginalSession, stored);
        Assert.Same(originalSpells, stored!.PlayerSpells);
        Assert.Same(result.OriginalSession.PlayerItems, stored.PlayerItems);
        Assert.Equal(persistedBefore, File.ReadAllBytes(GetSpellSnapshotPath(spellsRoot)));
        AssertDialogOnly(result.Sent, alreadyLearnedText, alreadyLearnedOptionText);
        Assert.Contains($"option={optionId}", result.Log);
        Assert.Contains($"spellId={spellId}; already learned", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true, false, "PlayerSpellsRoot")]
    [InlineData(false, true, "Sessions")]
    [InlineData(false, false, "Sessions and PlayerSpellsRoot")]
    public async Task DanceLearningRequiresBothLiveStoreAndPersistenceRootWithoutMutation(
        bool includeSessions,
        bool includeSpellsRoot,
        string expectedMissing)
    {
        var spellsRoot = includeSpellsRoot ? Path.Combine(_root, "missing-wiring") : null;
        var sessions = includeSessions ? new WorldSessionStore() : null;
        var result = await InvokeAsync(
            optionId: 7,
            performerPhrase: 3,
            CreateSpells(CreateExistingSpell()),
            sessions,
            spellsRoot);

        Assert.True(result.Handled);
        if (sessions is not null)
        {
            Assert.True(sessions.TryGet(result.Connection, out var stored));
            Assert.Same(result.OriginalSession, stored);
        }

        if (spellsRoot is not null)
        {
            Assert.False(File.Exists(GetSpellSnapshotPath(spellsRoot)));
        }

        AssertDialogOnly(
            result.Sent,
            "Dance learning is not available yet. Your spells were not changed.",
            "I~see.");
        Assert.Contains("PARITY-GAP LegacyNpc SubclassMasters", result.Log);
        Assert.Contains("range=NpcHandler.cs:11387-12416", result.Log);
        Assert.Contains($"requires {expectedMissing} for durable live spell mutation", result.Log);
    }

    [Fact]
    public async Task DanceLearningPersistenceFailureDoesNotCommitSessionOrSendLearningPackets()
    {
        Directory.CreateDirectory(_root);
        var blockedSpellsRoot = Path.Combine(_root, "blocked-spells-root");
        File.WriteAllText(blockedSpellsRoot, "not a directory");
        var sessions = new WorldSessionStore();
        var result = await InvokeAsync(
            optionId: 7,
            performerPhrase: 3,
            CreateSpells(CreateExistingSpell()),
            sessions,
            blockedSpellsRoot);

        Assert.True(result.Handled);
        Assert.True(sessions.TryGet(result.Connection, out var stored));
        Assert.Same(result.OriginalSession, stored);
        Assert.Same(result.OriginalSession.PlayerSpells, stored!.PlayerSpells);
        Assert.Same(result.OriginalSession.PlayerItems, stored.PlayerItems);
        Assert.Empty(result.Sent);
        Assert.Equal("not a directory", File.ReadAllText(blockedSpellsRoot));
        Assert.Contains("PARITY-GAP LegacyNpc SubclassMasters Performer dance persistence", result.Log);
        Assert.Contains("option=7 dance=Triumph spellId=1416", result.Log);
        Assert.Contains("reason=IOException; no session mutation was committed", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static async Task<DanceInvocationResult> InvokeAsync(
        byte optionId,
        byte? performerPhrase,
        PlayerSpellSnapshot playerSpells,
        WorldSessionStore? sessions,
        string? playerSpellsRoot)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(performerPhrase, playerSpells);
        sessions?.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: playerSpellsRoot),
            session,
            PerformerNpcId,
            optionId,
            CancellationToken.None);

        return new DanceInvocationResult(
            handled,
            sent,
            log.ToString(),
            connection,
            session);
    }

    private static WorldSession CreateSession(
        byte? performerPhrase,
        PlayerSpellSnapshot playerSpells)
    {
        var subProfInfo = performerPhrase.HasValue
            ? $"0#0#1#6#9#{performerPhrase.Value}#"
            : "0#0#0#";
        return new WorldSession(
            AccountId: 100,
            Username: "performer-dance-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "DanceStudent",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SubProfInfo"] = subProfInfo,
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: CreateSentinelItems(),
            PlayerSpells: playerSpells,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot CreateSentinelItems()
    {
        return new PlayerItemSnapshot(
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 900_001,
                    ItemId = 753_003,
                    StackSize = 3,
                },
            ],
            [],
            [],
            [],
            []);
    }

    private static PlayerSpellSnapshot CreateSpells(params PlayerSpellSnapshotEntry[] spells)
    {
        return new PlayerSpellSnapshot(spells);
    }

    private static PlayerSpellSnapshotEntry CreateExistingSpell()
    {
        return new PlayerSpellSnapshotEntry(
            ExistingSpellId,
            Level: 4,
            Experience: 567,
            PreviousLevel: 3,
            SoulLevel: 2,
            UseSpellSoul: 1);
    }

    private static void AssertSourceLearningPacketOrder(
        IReadOnlyList<byte[]> sent,
        ushort spellId,
        string systemMessage,
        string dialogText,
        string optionText)
    {
        Assert.Equal(7, sent.Count);
        Assert.Equal(
            [
                LegacySpellPacket.Opcode,
                LegacyStringPacket.Opcode,
                LegacyMsgMessagePacket.Opcode,
                LegacyNpcReplyPacket.Opcode,
                LegacyNpcReplyPacket.Opcode,
                LegacyNpcReplyPacket.Opcode,
                LegacyNpcReplyPacket.Opcode,
            ],
            sent.Select(bytes => ReadFrame(bytes).Header.Opcode).ToArray());

        Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[0]), out var spell));
        Assert.Equal(spellId, spell.Id);
        Assert.Equal(0, spell.Experience);
        Assert.Equal(0, spell.Level);
        Assert.Equal(0, spell.Unknown);
        Assert.Equal(0, spell.Type);
        Assert.Equal(0U, spell.UseSoul);
        Assert.Equal(0, spell.Soul);
        Assert.Equal(0, spell.HashLevelSoul);

        Assert.True(LegacyStringPacket.TryParse(ReadFrame(sent[1]), out var effect));
        Assert.Equal(CharacterUid, effect.Uid);
        Assert.Equal(LegacyStringId.Effect, effect.Id);
        Assert.Equal(["zf2-e300"], effect.Strings);

        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[2]), out var message));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Equal(systemMessage, message.Content[3]);

        AssertDialogPackets(sent.Skip(3).ToArray(), dialogText, optionText);
    }

    private static void AssertDialogOnly(
        IReadOnlyList<byte[]> sent,
        string dialogText,
        string optionText)
    {
        Assert.Equal(4, sent.Count);
        Assert.All(sent, bytes => Assert.Equal(LegacyNpcReplyPacket.Opcode, ReadFrame(bytes).Header.Opcode));
        AssertDialogPackets(sent, dialogText, optionText);
    }

    private static void AssertDialogPackets(
        IReadOnlyList<byte[]> sent,
        string dialogText,
        string optionText)
    {
        Assert.Equal(4, sent.Count);
        var text = ParseNpcReply(sent[0]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, text.InteractType);
        Assert.Equal(dialogText, text.Text);

        var option = ParseNpcReply(sent[1]);
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(byte.MaxValue, option.OptionId);
        Assert.Equal(optionText, option.Text);

        var avatar = ParseNpcReply(sent[2]);
        Assert.Equal(LegacyNpcReplyInteractType.Avatar, avatar.InteractType);
        Assert.Equal(PerformerAvatarId, avatar.InputMaxLength);

        var finish = ParseNpcReply(sent[3]);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
    }

    private static LegacyNpcReplyPacket ParseNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static string GetSpellSnapshotPath(string playerSpellsRoot)
    {
        return Path.Combine(playerSpellsRoot, CharacterUid + ".bin");
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record DanceCase(
        byte OptionId,
        ushort SpellId,
        string DanceName,
        byte RequiredPhase,
        string PhaseGateText,
        string PhaseGateOptionText,
        string AlreadyLearnedText,
        string AlreadyLearnedOptionText,
        string SystemMessage,
        string SuccessDialogText,
        string SuccessOptionText);

    private sealed record DanceInvocationResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        ConquerTcpConnection Connection,
        WorldSession OriginalSession);
}
