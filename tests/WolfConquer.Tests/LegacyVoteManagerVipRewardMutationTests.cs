using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVoteManagerVipRewardMutationTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;
    private const uint VoteManagerNpcId = 52_300;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyVoteManagerVipRewardMutationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExpiredVipPersistsTwelveHoursBeforeExposureAndSendsExactSequence()
    {
        var expired = DateTime.Now.AddHours(-2);
        var beforeReward = DateTime.Now;
        var result = await InvokeAsync(
            votePoints: 9,
            expiryAttributes: new Dictionary<string, string>
            {
                ["VipTime"] = Binary(expired),
            });
        var afterReward = DateTime.Now;

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var vipTime = result.UpdatedSession.Character.Attributes["VipTime"];
        var expiry = ParseBinary(vipTime);
        Assert.InRange(expiry, beforeReward.AddHours(12), afterReward.AddHours(12));
        AssertVipState(result.UpdatedSession.Character, votePoints: 5, vipTime);

        Assert.NotNull(result.PersistedAtSessionExposure);
        AssertVipState(result.PersistedAtSessionExposure!, votePoints: 5, vipTime);
        Assert.NotNull(result.ReloadedCharacter);
        AssertVipState(result.ReloadedCharacter!, votePoints: 5, vipTime);
        Assert.Equal(
            new[] { "session", "packet", "packet", "packet", "packet" },
            result.ExposureOrder);

        Assert.Equal(4, result.Sent.Count);
        AssertVipLevelUpdate(result.Sent[0]);
        AssertVipLevelUpdate(result.Sent[1]);
        Assert.True(result.Sent[0].AsSpan().SequenceEqual(result.Sent[1]));

        var vipStatusFrame = ReadFrame(result.Sent[2]);
        Assert.Equal(LegacyVipStatusPacket.Opcode, vipStatusFrame.Header.Opcode);
        Assert.True(LegacyVipStatusPacket.TryParse(vipStatusFrame, out var vipStatus));
        Assert.Equal(LegacyVipStatusFlags.FullVip, vipStatus.Flags);

        var messageFrame = ReadFrame(result.Sent[3]);
        Assert.Equal(LegacyNpcReplyPacket.Opcode, messageFrame.Header.Opcode);
        Assert.True(LegacyNpcReplyPacket.TryParse(messageFrame, out var messageBox));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.InteractType);
        Assert.Equal(".....", messageBox.Text);

        Assert.Contains("sourceLines=25870-25887", result.Log);
        Assert.Contains("packetOrder=VipLevel,VipLevel,VipStatusFullVip,NpcMessageBox", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveVipExtendsExistingExpiryByTwelveHours()
    {
        var activeExpiry = DateTime.Now.AddDays(2);
        var expectedVipTime = Binary(activeExpiry.AddHours(12));
        var result = await InvokeAsync(
            votePoints: 4,
            expiryAttributes: new Dictionary<string, string>
            {
                ["VipTime"] = Binary(activeExpiry),
            });

        Assert.True(result.Handled);
        AssertVipState(result.UpdatedSession.Character, votePoints: 0, expectedVipTime);
        AssertVipState(Assert.IsType<CharacterSnapshot>(result.PersistedAtSessionExposure), votePoints: 0, expectedVipTime);
        AssertVipState(Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter), votePoints: 0, expectedVipTime);
        Assert.Equal(4, result.Sent.Count);
    }

    [Fact]
    public async Task VipTimeTakesPrecedenceOverExpireVip()
    {
        var vipTimeExpiry = DateTime.Now.AddDays(1);
        var expireVipExpiry = DateTime.Now.AddDays(5);
        var expireVipRaw = Binary(expireVipExpiry);
        var expectedVipTime = Binary(vipTimeExpiry.AddHours(12));
        var result = await InvokeAsync(
            votePoints: 4,
            expiryAttributes: new Dictionary<string, string>
            {
                ["VipTime"] = Binary(vipTimeExpiry),
                ["ExpireVip"] = expireVipRaw,
            });

        AssertVipState(result.UpdatedSession.Character, votePoints: 0, expectedVipTime);
        Assert.Equal(expireVipRaw, result.UpdatedSession.Character.Attributes["ExpireVip"]);
        var persisted = Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter);
        AssertVipState(persisted, votePoints: 0, expectedVipTime);
        Assert.Equal(expireVipRaw, persisted.Attributes["ExpireVip"]);
    }

    [Fact]
    public async Task MissingVipTimeFallsBackToExpireVip()
    {
        var fallbackExpiry = DateTime.Now.AddDays(3);
        var fallbackRaw = Binary(fallbackExpiry);
        var expectedVipTime = Binary(fallbackExpiry.AddHours(12));
        var result = await InvokeAsync(
            votePoints: 4,
            expiryAttributes: new Dictionary<string, string>
            {
                ["ExpireVip"] = fallbackRaw,
            });

        AssertVipState(result.UpdatedSession.Character, votePoints: 0, expectedVipTime);
        Assert.Equal(fallbackRaw, result.UpdatedSession.Character.Attributes["ExpireVip"]);
        var persisted = Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter);
        AssertVipState(persisted, votePoints: 0, expectedVipTime);
        Assert.Equal(fallbackRaw, persisted.Attributes["ExpireVip"]);
    }

    [Fact]
    public async Task MissingOrMalformedSelectedExpiryIsTreatedAsExpired()
    {
        var activeFallback = Binary(DateTime.Now.AddDays(7));
        IReadOnlyDictionary<string, string>[] cases =
        [
            new Dictionary<string, string>
            {
                ["VipTime"] = "not-a-binary-date",
                ["ExpireVip"] = activeFallback,
            },
            new Dictionary<string, string>
            {
                ["ExpireVip"] = "also-not-a-binary-date",
            },
            new Dictionary<string, string>(),
        ];

        foreach (var expiryAttributes in cases)
        {
            var beforeReward = DateTime.Now;
            var result = await InvokeAsync(votePoints: 4, expiryAttributes);
            var afterReward = DateTime.Now;

            Assert.True(result.Handled);
            var expiry = ParseBinary(result.UpdatedSession.Character.Attributes["VipTime"]);
            Assert.InRange(expiry, beforeReward.AddHours(12), afterReward.AddHours(12));
            AssertVipState(
                Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter),
                votePoints: 0,
                result.UpdatedSession.Character.Attributes["VipTime"]);
        }
    }

    [Fact]
    public async Task InsufficientVotePointsIsHandledCompletelySilently()
    {
        var originalVipTime = Binary(DateTime.Now.AddDays(1));
        var result = await InvokeAsync(
            votePoints: 3,
            expiryAttributes: new Dictionary<string, string>
            {
                ["VipTime"] = originalVipTime,
            });

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtSessionExposure);
        Assert.Empty(result.ExposureOrder);
        Assert.Empty(result.Sent);
        Assert.Equal(string.Empty, result.Log);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        var persisted = Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter);
        Assert.Equal("3", persisted.Attributes["VotePoints"]);
        Assert.Equal("2", persisted.Attributes["VipLevel"]);
        Assert.Equal(originalVipTime, persisted.Attributes["VipTime"]);
    }

    [Fact]
    public async Task MissingSessionStoreFailsClosedWithParityLog()
    {
        var result = await InvokeAsync(
            votePoints: 4,
            expiryAttributes: new Dictionary<string, string>
            {
                ["VipTime"] = Binary(DateTime.Now.AddDays(1)),
            },
            includeSessionStore: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtSessionExposure);
        Assert.Empty(result.ExposureOrder);
        Assert.Empty(result.Sent);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Contains("PARITY-GAP LegacyNpc RealmTransferServices VoteManager VIP reward", result.Log);
        Assert.Contains("sourceLines=25870-25887", result.Log);
        Assert.Contains("reason=session-store-missing", result.Log);
        Assert.Contains("no mutation or packets were committed", result.Log);
    }

    [Fact]
    public async Task PersistenceFailureRollsBackPendingMutationAndSendsNothing()
    {
        var originalVipTime = Binary(DateTime.Now.AddDays(1));
        var result = await InvokeAsync(
            votePoints: 4,
            expiryAttributes: new Dictionary<string, string>
            {
                ["VipTime"] = originalVipTime,
            },
            persistedCharacterUid: CharacterUid + 1);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtSessionExposure);
        Assert.Empty(result.ExposureOrder);
        Assert.Empty(result.Sent);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        var persisted = Assert.IsType<CharacterSnapshot>(result.ReloadedCharacter);
        Assert.Equal("4", persisted.Attributes["VotePoints"]);
        Assert.Equal("2", persisted.Attributes["VipLevel"]);
        Assert.Equal(originalVipTime, persisted.Attributes["VipTime"]);
        Assert.Contains("VIP reward persistence", result.Log);
        Assert.Contains("reason=uidMismatch", result.Log);
        Assert.Contains("no session mutation or packets were committed", result.Log);
        Assert.Empty(Directory.EnumerateFiles(result.UsersRoot, "*.reward-points-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        ulong votePoints,
        IReadOnlyDictionary<string, string>? expiryAttributes = null,
        bool includeSessionStore = true,
        long persistedCharacterUid = CharacterUid)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VotePoints"] = votePoints.ToString(CultureInfo.InvariantCulture),
            ["VipLevel"] = "2",
        };
        if (expiryAttributes is not null)
        {
            foreach (var (key, value) in expiryAttributes)
            {
                attributes[key] = value;
            }
        }

        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacterSnapshot(characterPath, persistedCharacterUid, attributes);
        var originalCharacterBytes = File.ReadAllBytes(characterPath);

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var exposureOrder = new List<string>();
        var connection = CreateSendingConnection(sent, exposureOrder);
        var session = CreateSession(attributes, usersRoot);
        var characterLoader = new CharacterSnapshotLoader(usersRoot);
        CharacterSnapshot? persistedAtSessionExposure = null;
        WorldSessionStore? sessions = null;
        if (includeSessionStore)
        {
            sessions = new WorldSessionStore((_, previous, current) =>
            {
                if (previous is not null && !ReferenceEquals(previous, current))
                {
                    persistedAtSessionExposure = Assert.IsType<CharacterSnapshot>(
                        characterLoader.LoadByAccountId(AccountId));
                    exposureOrder.Add("session");
                }
            });
            sessions.Set(connection, session);
        }

        var handled = await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions),
            session,
            VoteManagerNpcId,
            optionId: 47,
            CancellationToken.None);

        var updatedSession = session;
        if (sessions is not null && sessions.TryGet(connection, out var storedSession))
        {
            updatedSession = Assert.IsType<WorldSession>(storedSession);
        }

        var reloadedCharacter = File.Exists(characterPath)
            ? CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath), characterPath)
            : null;
        return new InvokeResult(
            handled,
            session,
            updatedSession,
            persistedAtSessionExposure,
            reloadedCharacter,
            sent,
            exposureOrder,
            log.ToString(),
            usersRoot,
            characterPath,
            originalCharacterBytes);
    }

    private static void WriteCharacterSnapshot(
        string path,
        long characterUid,
        IReadOnlyDictionary<string, string> attributes)
    {
        var lines = new List<string>
        {
            "[Character]",
            "UID=" + characterUid.ToString(CultureInfo.InvariantCulture),
            "Body=1003",
            "Face=0",
            "Name=VoteVipRewardHero",
            "Class=7051",
            "Map=1002",
            "X=430",
            "Y=378",
            "FirstClass=7051",
            "SecoundeClass=0",
            "Reborn=0",
            "Level=140",
            "Haire=0",
            "Experience=0",
            "PMap=1002",
            "PMapX=430",
            "PMapY=378",
            "Agility=0",
            "Strength=0",
            "Vitaliti=0",
            "Spirit=0",
            "Atributes=0",
            "MinHitPoints=1",
            "MinMana=0",
            "ConquerPoints=0",
            "BankCps=0",
            "BoundConquerPoints=0",
            "Money=0",
            "PkPoints=0",
            "Online=0",
        };
        lines.AddRange(attributes.Select(pair => pair.Key + "=" + pair.Value));
        lines.Add("Sentinel=keep-me");
        File.WriteAllLines(path, lines);
    }

    private static WorldSession CreateSession(
        IReadOnlyDictionary<string, string> attributes,
        string usersRoot)
    {
        return new WorldSession(
            AccountId,
            Username: "legacy-vote-vip-reward-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "VoteVipRewardHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static ConquerTcpConnection CreateSendingConnection(
        List<byte[]> sent,
        List<string> exposureOrder)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9_958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            exposureOrder.Add("packet");
            return ValueTask.CompletedTask;
        });
    }

    private static void AssertVipState(CharacterSnapshot character, ulong votePoints, string vipTime)
    {
        Assert.Equal(votePoints.ToString(CultureInfo.InvariantCulture), character.Attributes["VotePoints"]);
        Assert.Equal("6", character.Attributes["VipLevel"]);
        Assert.Equal(vipTime, character.Attributes["VipTime"]);
    }

    private static void AssertVipLevelUpdate(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(LegacyMsgUpdatePacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var packet));
        Assert.Equal(CharacterUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.VIPLevel, update.Type);
        Assert.Equal(6UL, update.Value);
        Assert.Equal(0U, update.Flag);
        Assert.Equal(0U, update.Time);
        Assert.Equal(0U, update.Damage);
        Assert.Equal(0U, update.SpellLevel);
        Assert.Null(update.Values);
    }

    private static string Binary(DateTime value)
    {
        return value.ToBinary().ToString(CultureInfo.InvariantCulture);
    }

    private static DateTime ParseBinary(string value)
    {
        return DateTime.FromBinary(long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record InvokeResult(
        bool Handled,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        CharacterSnapshot? PersistedAtSessionExposure,
        CharacterSnapshot? ReloadedCharacter,
        IReadOnlyList<byte[]> Sent,
        IReadOnlyList<string> ExposureOrder,
        string Log,
        string UsersRoot,
        string CharacterPath,
        byte[] OriginalCharacterBytes);
}
