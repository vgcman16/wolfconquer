using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDrYinYangGenderMutationTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;
    private const uint DrYinYangNpcId = 15_805;
    private const long GenderChangeCost = 1_075;
    private const ushort GarmentPosition = 9;
    private const ushort AlternateGarmentPosition = 59;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyDrYinYangGenderMutationTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(1_008L, 2_007L)]
    [InlineData(2_007L, 1_008L)]
    public async Task OptionOnePersistsBeforeCommitAndSendsSourceMutationOrder(
        long initialBody,
        long expectedBody)
    {
        var result = await InvokeAsync(
            body: initialBody,
            conquerPoints: 5_000,
            spouse: "None",
            includePeer: true);

        Assert.NotSame(result.InitialSession, result.UpdatedSession);
        Assert.Equal(expectedBody, result.UpdatedSession.Character.Body);
        Assert.Equal(5_000 - GenderChangeCost, result.UpdatedSession.Character.ConquerPoints);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(expectedBody, result.PersistedAtCommit!.Body);
        Assert.Equal(5_000 - GenderChangeCost, result.PersistedAtCommit.ConquerPoints);
        Assert.NotNull(result.Reloaded);
        Assert.Equal(expectedBody, result.Reloaded!.Body);
        Assert.Equal(5_000 - GenderChangeCost, result.Reloaded.ConquerPoints);
        Assert.Contains("Sentinel=keep-me", result.PersistedText, StringComparison.Ordinal);
        Assert.Contains("Note=preserve-this-section", result.PersistedText, StringComparison.Ordinal);

        var expectedMesh = checked((ulong)((154 * 10_000) + expectedBody));
        Assert.Collection(
            result.MainSent,
            frame => AssertScalarUpdate(frame, LegacyMsgUpdateDataType.ConquerPoints, 5_000 - GenderChangeCost),
            frame => AssertScalarUpdate(frame, LegacyMsgUpdateDataType.Mesh, expectedMesh),
            frame => AssertGlobalTalk(frame, "YinYangTester, Changed He's/Hers mesh into male / female."));

        Assert.Collection(
            result.PeerSent,
            frame =>
            {
                Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame), out var spawn));
                Assert.Equal(expectedMesh, spawn.GetNumericField(1));
                Assert.Equal(CharacterUid, spawn.GetNumericField(2));
            },
            frame => AssertGlobalTalk(frame, "YinYangTester, Changed He's/Hers mesh into male / female."));

        Assert.Contains("updateOrder=ConquerPoints,MeshSelf,ReSendView,GlobalTalk", result.Log);
        Assert.Contains("deliberatelyUnmodeledCacheFanout=Nobility.Gender,Nobility.Mesh,GuildMember.Mesh", result.Log);
    }

    [Fact]
    public async Task ExactCostBoundarySucceedsAndDebitsWalletToZero()
    {
        var result = await InvokeAsync(conquerPoints: GenderChangeCost);

        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(0, result.Reloaded!.ConquerPoints);
        AssertScalarUpdate(result.MainSent[0], LegacyMsgUpdateDataType.ConquerPoints, 0);
    }

    [Theory]
    [InlineData("RayzoSpouse")]
    [InlineData("none")]
    public async Task SpouseMustEqualSourceSentinelExactly(string spouse)
    {
        var result = await InvokeAsync(spouse: spouse);

        AssertNoMutation(result);
        AssertExactDialog(
            result.MainSent,
            "You`re married. To change your gender, you need to get a divorce, first.",
            "Okay.");
        Assert.Empty(result.PeerSent);
        Assert.Contains("reason=married", result.Log);
    }

    [Fact]
    public async Task MissingSpouseAttributeUsesSourceFieldDefaultNone()
    {
        var result = await InvokeAsync(spouse: null);

        Assert.NotSame(result.InitialSession, result.UpdatedSession);
        Assert.Equal(2_007, result.UpdatedSession.Character.Body);
        Assert.Equal(5_000 - GenderChangeCost, result.UpdatedSession.Character.ConquerPoints);
    }

    [Fact]
    public async Task InsufficientConquerPointsSendsExactSourceDialogWithoutMutation()
    {
        var result = await InvokeAsync(conquerPoints: GenderChangeCost - 1);

        AssertNoMutation(result);
        AssertExactDialog(
            result.MainSent,
            "Gender reassignment requires 1,075 CPs. Make sure you have enough money with you.",
            "Okay.");
        Assert.Empty(result.PeerSent);
        Assert.Contains("reason=insufficient-conquer-points", result.Log);
    }

    [Theory]
    [InlineData(GarmentPosition)]
    [InlineData(AlternateGarmentPosition)]
    public async Task EitherGarmentSlotMustBeFree(ushort equippedPosition)
    {
        var result = await InvokeAsync(equipmentPositions: [equippedPosition]);

        AssertNoMutation(result);
        AssertExactDialog(result.MainSent, "Please remove your garment.", "Okay.");
        Assert.Empty(result.PeerSent);
        Assert.Contains("reason=garment-equipped", result.Log);
    }

    [Fact]
    public async Task UnrelatedEquipmentDoesNotBlockGenderMutation()
    {
        var result = await InvokeAsync(equipmentPositions: [1, 3, 4, 5, 12]);

        Assert.NotSame(result.InitialSession, result.UpdatedSession);
        Assert.Equal(2_007, result.UpdatedSession.Character.Body);
        Assert.Equal(5, result.UpdatedSession.PlayerItems.EquipmentItems.Count);
    }

    [Fact]
    public async Task ActiveTradeGateIsSourceSilentAndMutationFree()
    {
        var result = await InvokeAsync(legacyTradeActive: true);

        AssertNoMutation(result);
        Assert.Empty(result.MainSent);
        Assert.Empty(result.PeerSent);
        Assert.Contains("reason=in-trade; source branch is silent", result.Log);
    }

    [Theory]
    [InlineData(false, true, "session-store-missing")]
    [InlineData(true, false, "users-root-missing")]
    public async Task MissingMutationAuthorityFencesWithoutPersistenceOrPackets(
        bool includeSessionStore,
        bool includeUsersRoot,
        string expectedReason)
    {
        var result = await InvokeAsync(
            includeSessionStore: includeSessionStore,
            includeUsersRoot: includeUsersRoot);

        AssertNoMutation(result);
        Assert.Empty(result.MainSent);
        Assert.Empty(result.PeerSent);
        Assert.Contains($"reason={expectedReason}", result.Log);
        Assert.Contains("PARITY-GAP LegacyNpc RealmTransferServices DrYinYang", result.Log);
    }

    [Fact]
    public async Task PersistenceFailureDoesNotCommitSessionOrSendMutationPackets()
    {
        var result = await InvokeAsync(removeSnapshotBeforeHandle: true);

        AssertNoMutation(result, expectSnapshot: false);
        Assert.Empty(result.MainSent);
        Assert.Empty(result.PeerSent);
        Assert.Contains("reason=snapshotMissing", result.Log);
        Assert.Contains("no session mutation or packets were committed", result.Log);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public async Task ExistingDialogOptionsRemainMutationFree(byte optionId)
    {
        var result = await InvokeAsync(optionId: optionId);

        AssertNoMutation(result);
        Assert.NotEmpty(result.MainSent);
        Assert.All(result.MainSent, frame =>
        {
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out _));
        });
        Assert.Empty(result.PeerSent);
        Assert.DoesNotContain(result.MainSent, IsMsgUpdate);
        Assert.DoesNotContain(result.MainSent, IsPlayerSpawn);
        Assert.DoesNotContain(result.MainSent, IsMsgMessage);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        long body = 1_008,
        long conquerPoints = 5_000,
        string? spouse = "None",
        IReadOnlyList<ushort>? equipmentPositions = null,
        bool legacyTradeActive = false,
        bool includeSessionStore = true,
        bool includeUsersRoot = true,
        bool removeSnapshotBeforeHandle = false,
        bool includePeer = false,
        byte optionId = 1)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = Path.Combine(usersRoot, AccountId + ".ini");
        WriteSnapshot(snapshotPath, body, conquerPoints, spouse);
        var originalText = File.ReadAllText(snapshotPath);

        var loader = new CharacterSnapshotLoader(usersRoot);
        var character = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(AccountId));
        var equipment = (equipmentPositions ?? [])
            .Select((position, index) => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(100 + index)),
                ItemId = checked((uint)(180_000 + index)),
                Durability = 100,
                MaximumDurability = 100,
                Position = position,
                StackSize = 1,
            })
            .ToArray();
        var playerItems = new PlayerItemSnapshot([], [], [], equipment, []);
        var session = new WorldSession(
            AccountId,
            "yin-yang-test",
            character,
            playerItems,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty,
            UsersRoot: includeUsersRoot ? usersRoot : null,
            LegacyTradeActive: legacyTradeActive);

        var mainSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var mainConnection = CreateSendingConnection(mainSent, port: 9_958);
        var peerConnection = CreateSendingConnection(peerSent, port: 9_959);
        CharacterSnapshot? persistedAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is not null &&
                current.Character.Uid == CharacterUid &&
                (current.Character.Body != previous.Character.Body ||
                 current.Character.ConquerPoints != previous.Character.ConquerPoints))
            {
                persistedAtCommit = loader.LoadByAccountId(AccountId);
            }
        });
        sessions.Set(mainConnection, session);

        if (includePeer)
        {
            var peerCharacter = character with
            {
                Uid = CharacterUid + 1,
                Name = "NearbyPeer",
                X = character.X + 1,
                SourceFile = "peer-fixture",
            };
            var peerSession = new WorldSession(
                AccountId + 1,
                "yin-yang-peer",
                peerCharacter,
                PlayerItemSnapshot.Empty,
                PlayerSpellSnapshot.Empty,
                PlayerProficiencySnapshot.Empty,
                CombatGear: CombatGearSnapshot.Empty);
            sessions.Set(peerConnection, peerSession);
        }

        if (removeSnapshotBeforeHandle)
        {
            File.Delete(snapshotPath);
        }

        using var log = new StringWriter();
        var handled = await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            new WorldPacketHandlerContext(
                mainConnection,
                log,
                Session: session,
                Sessions: includeSessionStore ? sessions : null),
            session,
            DrYinYangNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(mainConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var reloaded = File.Exists(snapshotPath) ? loader.LoadByAccountId(AccountId) : null;
        var persistedText = File.Exists(snapshotPath) ? File.ReadAllText(snapshotPath) : string.Empty;
        return new InvokeResult(
            session,
            updatedSession,
            persistedAtCommit,
            reloaded,
            mainSent,
            peerSent,
            log.ToString(),
            originalText,
            persistedText);
    }

    private static void WriteSnapshot(
        string path,
        long body,
        long conquerPoints,
        string? spouse)
    {
        var spouseLine = spouse is null ? string.Empty : $"Spouse={spouse}{Environment.NewLine}";
        File.WriteAllText(
            path,
            $"[Character]{Environment.NewLine}" +
            $"UID={CharacterUid}{Environment.NewLine}" +
            $"Body={body}{Environment.NewLine}" +
            $"Face=154{Environment.NewLine}" +
            $"Name=YinYangTester{Environment.NewLine}" +
            $"Class=7051{Environment.NewLine}" +
            $"Map=1002{Environment.NewLine}" +
            $"X=430{Environment.NewLine}" +
            $"Y=378{Environment.NewLine}" +
            $"FirstClass=7051{Environment.NewLine}" +
            $"SecoundeClass=0{Environment.NewLine}" +
            $"Reborn=0{Environment.NewLine}" +
            $"Level=140{Environment.NewLine}" +
            $"Haire=27{Environment.NewLine}" +
            $"Experience=123456{Environment.NewLine}" +
            $"PMap=1002{Environment.NewLine}" +
            $"PMapX=430{Environment.NewLine}" +
            $"PMapY=378{Environment.NewLine}" +
            $"Agility=12{Environment.NewLine}" +
            $"Strength=11{Environment.NewLine}" +
            $"Vitaliti=100{Environment.NewLine}" +
            $"Spirit=14{Environment.NewLine}" +
            $"Atributes=7{Environment.NewLine}" +
            $"MinHitPoints=500{Environment.NewLine}" +
            $"MinMana=25{Environment.NewLine}" +
            $"MaxMana=25{Environment.NewLine}" +
            $"ConquerPoints={conquerPoints}{Environment.NewLine}" +
            $"BankCps=0{Environment.NewLine}" +
            $"BoundConquerPoints=0{Environment.NewLine}" +
            $"Money=1000{Environment.NewLine}" +
            $"PkPoints=0{Environment.NewLine}" +
            $"Online=1{Environment.NewLine}" +
            spouseLine +
            $"Sentinel=keep-me{Environment.NewLine}" +
            Environment.NewLine +
            $"[Other]{Environment.NewLine}" +
            $"Note=preserve-this-section{Environment.NewLine}");
    }

    private static void AssertNoMutation(InvokeResult result, bool expectSnapshot = true)
    {
        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        if (expectSnapshot)
        {
            Assert.NotNull(result.Reloaded);
            Assert.Equal(result.InitialSession.Character.Body, result.Reloaded!.Body);
            Assert.Equal(result.InitialSession.Character.ConquerPoints, result.Reloaded.ConquerPoints);
            Assert.Equal(result.OriginalText, result.PersistedText);
        }
        else
        {
            Assert.Null(result.Reloaded);
            Assert.Empty(result.PersistedText);
        }
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string expectedText,
        string expectedOption)
    {
        Assert.Equal(4, sent.Count);
        var text = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(expectedText, text.Text);

        var option = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(expectedOption, option.Text);
        Assert.Equal(byte.MaxValue, option.OptionId);

        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal((ushort)218, avatar.InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        Assert.Equal(expectedType, reply.InteractType);
        return reply;
    }

    private static void AssertScalarUpdate(
        byte[] bytes,
        LegacyMsgUpdateDataType expectedType,
        long expectedValue)
    {
        AssertScalarUpdate(bytes, expectedType, checked((ulong)expectedValue));
    }

    private static void AssertScalarUpdate(
        byte[] bytes,
        LegacyMsgUpdateDataType expectedType,
        ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(CharacterUid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(expectedType, entry.Type);
        Assert.Equal(expectedValue, entry.Value);
    }

    private static void AssertGlobalTalk(byte[] bytes, string expectedMessage)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, message.Color);
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Contains(expectedMessage, message.Content);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent, int port)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, port), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static bool IsMsgUpdate(byte[] bytes)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsPlayerSpawn(byte[] bytes)
    {
        return LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsMsgMessage(byte[] bytes)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out _);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record InvokeResult(
        WorldSession InitialSession,
        WorldSession UpdatedSession,
        CharacterSnapshot? PersistedAtCommit,
        CharacterSnapshot? Reloaded,
        IReadOnlyList<byte[]> MainSent,
        IReadOnlyList<byte[]> PeerSent,
        string Log,
        string OriginalText,
        string PersistedText);
}
