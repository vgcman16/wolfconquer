using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVoteManagerRewardMutationTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;
    private const uint VoteManagerNpcId = 52_300;
    private const byte ConquerPointRewardOption = 42;
    private const long ConquerPointReward = 3_000_000;
    private const long ConquerPointInventoryCap = 2_000_000_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyVoteManagerRewardMutationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option42PersistsScalarsBeforeCommitThenSendsConquerPointsAndTopLeftSystem()
    {
        var result = await InvokeAsync(
            votePoints: "2",
            conquerPoints: 25_000,
            bankConquerPoints: 75);

        Assert.NotSame(result.InitialSession, result.UpdatedSession);
        Assert.Equal(1UL, VotePoints(result.UpdatedSession.Character));
        Assert.Equal(25_000 + ConquerPointReward, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(75, result.UpdatedSession.Character.BankConquerPoints);
        Assert.Equal(3_025_000UL, ScalarAttribute(result.UpdatedSession.Character, "ConquerPoints"));
        Assert.Equal(75UL, ScalarAttribute(result.UpdatedSession.Character, "BankCps"));

        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(1UL, VotePoints(result.PersistedAtCommit!));
        Assert.Equal(25_000 + ConquerPointReward, result.PersistedAtCommit.ConquerPoints);
        Assert.Equal(75, result.PersistedAtCommit.BankConquerPoints);
        Assert.NotNull(result.Reloaded);
        Assert.Equal(1UL, VotePoints(result.Reloaded!));
        Assert.Equal(25_000 + ConquerPointReward, result.Reloaded.ConquerPoints);
        Assert.Equal(75, result.Reloaded.BankConquerPoints);
        Assert.Contains("VotePoints=1", result.PersistedText, StringComparison.Ordinal);
        Assert.Contains("ConquerPoints=3025000", result.PersistedText, StringComparison.Ordinal);
        Assert.Contains("BankCps=75", result.PersistedText, StringComparison.Ordinal);

        Assert.Collection(
            result.Sent,
            frame => AssertConquerPointsUpdate(frame, 25_000 + ConquerPointReward),
            frame => AssertTopLeftSystem(frame, "...."));
        Assert.Contains("packetOrder=ConquerPoints,TopLeftSystem2005", result.Log);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    public async Task ZeroOrMissingVotePointsIsSilentAndMutationFree(string? votePoints)
    {
        var result = await InvokeAsync(
            votePoints,
            conquerPoints: 25_000,
            bankConquerPoints: 75);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.Sent);
        Assert.Equal(result.OriginalText, result.PersistedText);
        Assert.NotNull(result.Reloaded);
        Assert.Equal(25_000, result.Reloaded!.ConquerPoints);
        Assert.Equal(75, result.Reloaded.BankConquerPoints);
        Assert.Equal(votePoints is null ? 0UL : ulong.Parse(votePoints, CultureInfo.InvariantCulture), VotePoints(result.Reloaded));
    }

    [Fact]
    public async Task OverflowPersistsBankCpsAndSendsSourcePacketOrderWithoutBankUpdate()
    {
        const long initialConquerPoints = 1_999_000_000;
        const long initialBankConquerPoints = 125;
        const long expectedOverflow = 2_000_000;
        var result = await InvokeAsync(
            votePoints: "1",
            conquerPoints: initialConquerPoints,
            bankConquerPoints: initialBankConquerPoints);

        Assert.Equal(0UL, VotePoints(result.UpdatedSession.Character));
        Assert.Equal(ConquerPointInventoryCap, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(initialBankConquerPoints + expectedOverflow, result.UpdatedSession.Character.BankConquerPoints);
        Assert.Equal(2_000_000_000UL, ScalarAttribute(result.UpdatedSession.Character, "ConquerPoints"));
        Assert.Equal(2_000_125UL, ScalarAttribute(result.UpdatedSession.Character, "BankCps"));
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(0UL, VotePoints(result.PersistedAtCommit!));
        Assert.Equal(ConquerPointInventoryCap, result.PersistedAtCommit.ConquerPoints);
        Assert.Equal(initialBankConquerPoints + expectedOverflow, result.PersistedAtCommit.BankConquerPoints);
        Assert.NotNull(result.Reloaded);
        Assert.Equal(0UL, VotePoints(result.Reloaded!));
        Assert.Equal(ConquerPointInventoryCap, result.Reloaded.ConquerPoints);
        Assert.Equal(initialBankConquerPoints + expectedOverflow, result.Reloaded.BankConquerPoints);

        Assert.Collection(
            result.Sent,
            frame => AssertNpcMessageBox(
                frame,
                "2000000 Your ConquerPoints[CPS] has Transfare To Your Bank Thanks."),
            frame => AssertConquerPointsUpdate(frame, ConquerPointInventoryCap),
            frame => AssertTopLeftSystem(frame, "...."));
        Assert.Contains("packetOrder=NpcMessageBox,ConquerPoints,TopLeftSystem2005", result.Log);
    }

    [Fact]
    public async Task PersistenceFailureLeavesSessionAndPacketsUnchanged()
    {
        var result = await InvokeAsync(
            votePoints: "1",
            conquerPoints: 25_000,
            bankConquerPoints: 75,
            removeSnapshotBeforeHandle: true);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Equal(1UL, VotePoints(result.UpdatedSession.Character));
        Assert.Equal(25_000, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(75, result.UpdatedSession.Character.BankConquerPoints);
        Assert.Null(result.PersistedAtCommit);
        Assert.Null(result.Reloaded);
        Assert.Empty(result.PersistedText);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=snapshotMissing", result.Log);
        Assert.Contains("no session mutation or packets were committed", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        string? votePoints,
        long conquerPoints,
        long bankConquerPoints,
        bool removeSnapshotBeforeHandle = false)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = Path.Combine(usersRoot, AccountId + ".ini");
        WriteSnapshot(snapshotPath, votePoints, conquerPoints, bankConquerPoints);
        var originalText = File.ReadAllText(snapshotPath);

        var loader = new CharacterSnapshotLoader(usersRoot);
        var character = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(AccountId));
        var session = new WorldSession(
            AccountId,
            "vote-manager-reward-test",
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty,
            UsersRoot: usersRoot);

        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        CharacterSnapshot? persistedAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is not null && !ReferenceEquals(previous, current))
            {
                persistedAtCommit = loader.LoadByAccountId(AccountId);
            }
        });
        sessions.Set(connection, session);

        if (removeSnapshotBeforeHandle)
        {
            File.Delete(snapshotPath);
        }

        using var log = new StringWriter();
        Assert.True(await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions),
            session,
            VoteManagerNpcId,
            ConquerPointRewardOption,
            CancellationToken.None));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var reloaded = File.Exists(snapshotPath) ? loader.LoadByAccountId(AccountId) : null;
        var persistedText = File.Exists(snapshotPath) ? File.ReadAllText(snapshotPath) : string.Empty;
        return new InvokeResult(
            session,
            updatedSession,
            persistedAtCommit,
            reloaded,
            sent,
            log.ToString(),
            originalText,
            persistedText);
    }

    private static void WriteSnapshot(
        string path,
        string? votePoints,
        long conquerPoints,
        long bankConquerPoints)
    {
        var votePointsLine = votePoints is null
            ? string.Empty
            : $"VotePoints={votePoints}{Environment.NewLine}";
        File.WriteAllText(
            path,
            $"[Character]{Environment.NewLine}" +
            $"UID={CharacterUid}{Environment.NewLine}" +
            $"Body=1003{Environment.NewLine}" +
            $"Face=154{Environment.NewLine}" +
            $"Name=VoteRewardTester{Environment.NewLine}" +
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
            $"BankCps={bankConquerPoints}{Environment.NewLine}" +
            $"BoundConquerPoints=0{Environment.NewLine}" +
            $"Money=1000{Environment.NewLine}" +
            $"PkPoints=0{Environment.NewLine}" +
            $"Online=1{Environment.NewLine}" +
            votePointsLine +
            $"Sentinel=keep-me{Environment.NewLine}");
    }

    private static ulong VotePoints(CharacterSnapshot character)
    {
        return ScalarAttribute(character, "VotePoints");
    }

    private static ulong ScalarAttribute(CharacterSnapshot character, string key)
    {
        return character.Attributes.TryGetValue(key, out var raw) &&
            ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
    }

    private static void AssertNpcMessageBox(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal(expectedText, reply.Text);
    }

    private static void AssertConquerPointsUpdate(byte[] bytes, long expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(CharacterUid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, entry.Type);
        Assert.Equal(checked((ulong)expectedValue), entry.Value);
    }

    private static void AssertTopLeftSystem(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, message.Color);
        Assert.Equal(2_005U, message.ChatType);
        Assert.Equal(expectedText, message.Content[3]);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9_958), (frame, _) =>
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

    private sealed record InvokeResult(
        WorldSession InitialSession,
        WorldSession UpdatedSession,
        CharacterSnapshot? PersistedAtCommit,
        CharacterSnapshot? Reloaded,
        IReadOnlyList<byte[]> Sent,
        string Log,
        string OriginalText,
        string PersistedText);
}
