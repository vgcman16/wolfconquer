using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBankingServicesNpcTests
{
    [Fact]
    public async Task BankCpsRootShowsEnglishBalancesAndSourceOptions()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(conquerPoints: 1_000, bankConquerPoints: 250);

        var handled = await HandleAsync(sent, log, session, npcId: 102_177, optionId: 0);

        Assert.True(handled);
        AssertReply(sent[0], LegacyNpcReplyInteractType.Dialog, "Dear Banker, you have 250 CPs in the bank.");
        AssertReply(sent[2], LegacyNpcReplyInteractType.Option, "Deposit CPs.", optionId: 1);
        AssertReply(sent[3], LegacyNpcReplyInteractType.Option, "Withdraw CPs.", optionId: 5);
        Assert.Contains("source=NpcHandler.BankCps", log.ToString());
        AssertAsciiReplies(sent);
    }

    [Fact]
    public async Task BankCpsDepositInputStoresPendingConfirmationWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var sessions = new WorldSessionStore();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(conquerPoints: 1_000, bankConquerPoints: 250);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyBankingServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 102_177,
            optionId: 10,
            input: "300",
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var pending));
        Assert.Equal(300U, pending!.PendingBankConquerPoints);
        Assert.True(pending.PendingBankConquerPointsIsDeposit);
        Assert.Equal(1_000, pending.Character.ConquerPoints);
        Assert.Equal(250, pending.Character.BankConquerPoints);
        AssertReply(sent[0], LegacyNpcReplyInteractType.Dialog, "Are you sure you want to deposit 300 CPs into your bank?");
        AssertReply(sent[1], LegacyNpcReplyInteractType.Option, "Yes.", optionId: 2);
        AssertAsciiReplies(sent);
    }

    [Fact]
    public async Task BankCpsDepositConfirmationMovesConquerPointsToBankAndSendsUpdate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var sessions = new WorldSessionStore();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(conquerPoints: 1_000, bankConquerPoints: 250) with
        {
            PendingBankConquerPoints = 300,
            PendingBankConquerPointsIsDeposit = true,
        };
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyBankingServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 102_177,
            optionId: 2,
            input: "",
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.Null(updated!.PendingBankConquerPoints);
        Assert.Equal(700, updated.Character.ConquerPoints);
        Assert.Equal(550, updated.Character.BankConquerPoints);
        Assert.Contains(sent, frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints && entry.Value == 700UL));
        var output = log.ToString();
        Assert.Contains("applied LegacyNpc BankCps transfer", output);
        Assert.Contains("mode=deposit amount=300 conquerPoints=700 bankCps=550", output);
        AssertAsciiReplies(sent.Where(frame => LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out _)));
    }

    [Fact]
    public async Task BankCpsWithdrawConfirmationMovesBankToConquerPointsPersistsAndSendsUpdate()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-bankcp-withdraw-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(usersRoot);
            var snapshotPath = Path.Combine(usersRoot, "1293700.ini");
            File.WriteAllText(
                snapshotPath,
                """
                [Character]
                UID=1293700
                Name=Banker
                ConquerPoints=1000
                BankCps=250
                BoundConquerPoints=0

                [Custom]
                Note=keep-me
                """);

            using var log = new StringWriter();
            var sent = new List<byte[]>();
            var sessions = new WorldSessionStore();
            var connection = CreateSendingConnection(sent);
            var session = CreateSession(conquerPoints: 1_000, bankConquerPoints: 250) with
            {
                UsersRoot = usersRoot,
                PendingBankConquerPoints = 125,
                PendingBankConquerPointsIsDeposit = false,
            };
            sessions.Set(connection, session);

            var handled = await WorldPacketHandlers.TryHandleLegacyBankingServicesNpcAsync(
                new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
                session,
                npcId: 102_177,
                optionId: 3,
                input: "",
                CancellationToken.None);

            Assert.True(handled);
            Assert.True(sessions.TryGet(connection, out var updated));
            Assert.Null(updated!.PendingBankConquerPoints);
            Assert.Equal(1_125, updated.Character.ConquerPoints);
            Assert.Equal(125, updated.Character.BankConquerPoints);
            Assert.Contains(sent, frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints && entry.Value == 1_125UL));

            var persistedText = File.ReadAllText(snapshotPath);
            Assert.Contains("ConquerPoints=1125", persistedText);
            Assert.Contains("BankCps=125", persistedText);
            Assert.Contains("Note=keep-me", persistedText);

            var output = log.ToString();
            Assert.Contains("persisted LegacyNpc BankCps transfer", output);
            Assert.Contains("keys=ConquerPoints,BankCps", output);
            Assert.Contains("mode=withdraw amount=125 conquerPoints=1125 bankCps=125", output);
            AssertAsciiReplies(sent.Where(frame => LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out _)));
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
    public async Task BankCpsDepositConfirmationSkipsPersistenceWhenSnapshotUidDoesNotMatchAccount()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-bankcp-uid-mismatch-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(usersRoot);
            var snapshotPath = Path.Combine(usersRoot, "1293700.ini");
            File.WriteAllText(
                snapshotPath,
                """
                [Character]
                UID=1299999
                Name=WrongOwner
                ConquerPoints=1000
                BankCps=250

                [Custom]
                Note=keep-me
                """);

            using var log = new StringWriter();
            var sent = new List<byte[]>();
            var sessions = new WorldSessionStore();
            var connection = CreateSendingConnection(sent);
            var session = CreateSession(conquerPoints: 1_000, bankConquerPoints: 250) with
            {
                UsersRoot = usersRoot,
                PendingBankConquerPoints = 300,
                PendingBankConquerPointsIsDeposit = true,
            };
            sessions.Set(connection, session);

            var handled = await WorldPacketHandlers.TryHandleLegacyBankingServicesNpcAsync(
                new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
                session,
                npcId: 102_177,
                optionId: 2,
                input: "",
                CancellationToken.None);

            Assert.True(handled);
            Assert.True(sessions.TryGet(connection, out var updated));
            Assert.Equal(700, updated!.Character.ConquerPoints);
            Assert.Equal(550, updated.Character.BankConquerPoints);

            var persistedText = File.ReadAllText(snapshotPath);
            Assert.Contains("UID=1299999", persistedText);
            Assert.Contains("ConquerPoints=1000", persistedText);
            Assert.Contains("BankCps=250", persistedText);
            Assert.DoesNotContain("ConquerPoints=700", persistedText);
            Assert.DoesNotContain("BankCps=550", persistedText);

            var output = log.ToString();
            Assert.Contains("skipped LegacyNpc BankCps transfer persistence", output);
            Assert.Contains("reason=uidMismatch", output);
        }
        finally
        {
            if (Directory.Exists(usersRoot))
            {
                Directory.Delete(usersRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(2_071U, "NpcHandler.MarketCpAdmin", true)]
    [InlineData(1_000U, "NpcHandler.JailCPAdmin", false)]
    public async Task CpExchangeAdminsSendEnglishRootDialogs(uint npcId, string sourceMethod, bool hasChiExchange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession();

        var handled = await HandleAsync(sent, log, session, npcId, optionId: 0);

        Assert.True(handled);
        AssertReply(sent[0], LegacyNpcReplyInteractType.Dialog, "Dragon Balls are powerful. I can exchange Dragon Balls or DB Scrolls for CPs.");
        Assert.Contains(ParseReplies(sent), reply => reply.Text == "Exchange a DB Scroll." && reply.OptionId == 1);
        Assert.Contains(ParseReplies(sent), reply => reply.Text == "Exchange a Dragon Ball." && reply.OptionId == 2);
        Assert.Equal(hasChiExchange, ParseReplies(sent).Any(reply => reply.OptionId == 9));
        Assert.Contains("source=" + sourceMethod, log.ToString());
        AssertAsciiReplies(sent);
    }

    [Theory]
    [InlineData(2_071U, 1, "NpcHandler.MarketCpAdmin")]
    [InlineData(1_000U, 4, "NpcHandler.JailCPAdmin")]
    public async Task CpExchangeMutationsAreLoggedAsSafeGaps(uint npcId, byte optionId, string sourceMethod)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var session = CreateSession(conquerPoints: 1_000, money: 10_000_000);

        var handled = await HandleAsync(sent, log, session, npcId, optionId);

        Assert.True(handled);
        AssertReply(sent[0], LegacyNpcReplyInteractType.Dialog, "This exchange is unavailable here until the item and currency transaction path is fully audited.");
        var output = log.ToString();
        Assert.Contains("PARITY-GAP LegacyNpc CpExchangeAdmin", output);
        Assert.Contains("source=" + sourceMethod, output);
        Assert.Contains("optionId=" + optionId, output);
        AssertAsciiReplies(sent);
    }

    [Fact]
    public async Task UnknownNpcIsNotHandled()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();

        Assert.False(await HandleAsync(sent, log, CreateSession(), npcId: 123, optionId: 0));
    }

    private static async ValueTask<bool> HandleAsync(
        List<byte[]> sent,
        TextWriter log,
        WorldSession session,
        uint npcId,
        byte optionId,
        string input = "")
    {
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        return await WorldPacketHandlers.TryHandleLegacyBankingServicesNpcAsync(
                new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
                session,
                npcId,
                optionId,
                input,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static WorldSession CreateSession(
        long conquerPoints = 0,
        long bankConquerPoints = 0,
        long money = 0)
    {
        return new WorldSession(
            AccountId: 1_293_700,
            Username: "banking-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_700,
                Name: "Banker",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
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
                Money: money,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: bankConquerPoints,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseReplies(IEnumerable<byte[]> sent)
    {
        var replies = new List<LegacyNpcReplyPacket>();
        foreach (var frame in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet))
            {
                replies.Add(packet);
            }
        }

        return replies;
    }

    private static LegacyNpcReplyPacket AssertReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType,
        string expectedText,
        byte? optionId = null)
    {
        var packet = AssertNpcReply(bytes, expectedType);
        Assert.Equal(expectedText, packet.Text);
        if (optionId.HasValue)
        {
            Assert.Equal(optionId.Value, packet.OptionId);
        }

        return packet;
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static void AssertAsciiReplies(IEnumerable<byte[]> sent)
    {
        foreach (var bytes in sent)
        {
            if (!LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                continue;
            }

            Assert.All(packet.Text, character => Assert.True(character <= 127));
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
