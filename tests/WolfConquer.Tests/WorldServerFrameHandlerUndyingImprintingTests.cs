using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private const ushort LegacyTestUndyingImprintingSpellId = 15_820;
    private const int LegacyTestUndyingImprintingStatusFlag = 0xEA;

    [Fact]
    public async Task HandlerLegacyWorldLoginSeedsThunderStrikerUndyingImprintingAfterRuneStorage()
    {
        var (handler, connection, sent) = await LoginThunderStrikerWithUndyingImprintingAsync(
                extraCharacterLines: "Stamina=40",
                unionId: 1_370);

        var runeIndex = FindOpcodeIndex(sent, WorldPacketOpcodes.LegacyRuneStorage, startIndex: 1);
        AssertUndyingImprintingUpdate(sent[runeIndex + 1], 1_293_600, time: 60, damage: 0);
        AssertUndyingImprintingUpdate(sent[runeIndex + 2], 1_293_600, time: 60, damage: 1);
        Assert.True(LegacyOverheadLeagueInfoPacket.TryParse(ReadFrame(sent[runeIndex + 3]), out _));

        var session = GetSession(handler, connection);
        Assert.Equal(
            "60",
            session.Character.Attributes["ThunderStrikerUndyingImprinting"]);
        Assert.True(
            LegacyPlayerDeathState.HasStatusFlag(
                session.Character.Attributes,
                LegacyTestUndyingImprintingStatusFlag));
    }

    [Fact]
    public async Task HandlerLegacyThunderStrikerUndyingImprintingRuntimeTickRechargesAndRestoresStamina()
    {
        var (handler, connection, sent) = await LoginThunderStrikerWithUndyingImprintingAsync(
                extraCharacterLines: "Stamina=90");
        var session = GetSession(handler, connection);
        var attributes = new Dictionary<string, string>(
            session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["ThunderStrikerUndyingImprinting"] = "59",
            ["Stamina"] = "90",
        };
        SetSession(
            handler,
            connection,
            session with
            {
                Character = session.Character with { Attributes = attributes },
            });

        var startIndex = sent.Count;
        await InvokeUndyingImprintingRuntimeTickAsync(
                handler,
                connection,
                session.PlayerItems,
                session.PlayerSpells);

        Assert.Equal(startIndex + 2, sent.Count);
        AssertUndyingImprintingUpdate(sent[startIndex], 1_293_600, time: 60, damage: 1);
        AssertSingleUpdate(sent[startIndex + 1], 1_293_600, LegacyMsgUpdateDataType.Stamina, 100);

        var updated = GetSession(handler, connection);
        Assert.Equal("60", updated.Character.Attributes["ThunderStrikerUndyingImprinting"]);
        Assert.Equal("100", updated.Character.Attributes["Stamina"]);
        Assert.True(
            LegacyPlayerDeathState.HasStatusFlag(
                updated.Character.Attributes,
                LegacyTestUndyingImprintingStatusFlag));
    }

    [Fact]
    public async Task HandlerLegacyThunderStrikerUndyingImprintingRuntimeTickClampsAboveBaseLimit()
    {
        var (handler, connection, sent) = await LoginThunderStrikerWithUndyingImprintingAsync(
                extraCharacterLines: "Stamina=80");
        var session = GetSession(handler, connection);
        var attributes = new Dictionary<string, string>(
            session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["ThunderStrikerUndyingImprinting"] = "75",
            ["Stamina"] = "80",
        };
        SetSession(
            handler,
            connection,
            session with
            {
                Character = session.Character with { Attributes = attributes },
            });

        var startIndex = sent.Count;
        await InvokeUndyingImprintingRuntimeTickAsync(
                handler,
                connection,
                session.PlayerItems,
                session.PlayerSpells);

        Assert.Equal(startIndex + 1, sent.Count);
        AssertUndyingImprintingUpdate(sent[startIndex], 1_293_600, time: 60, damage: 1);

        var updated = GetSession(handler, connection);
        Assert.Equal("60", updated.Character.Attributes["ThunderStrikerUndyingImprinting"]);
        Assert.Equal("80", updated.Character.Attributes["Stamina"]);
        Assert.True(
            LegacyPlayerDeathState.HasStatusFlag(
                updated.Character.Attributes,
                LegacyTestUndyingImprintingStatusFlag));
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginThunderStrikerWithUndyingImprintingAsync(
            string? extraCharacterLines = null,
            long unionId = 0)
    {
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var spellsRoot = Path.Combine(_usersRoot, "PlayersSpells");
        Directory.CreateDirectory(spellsRoot);
        WriteSnapshot(
            usersRoot,
            1_293_600,
            uid: 1_293_600,
            classCode: 9_005,
            secondClass: 9_000,
            unionId: unionId,
            extraCharacterLines: extraCharacterLines);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(
            1_293_600,
            CreateUndyingImprintingSpellSnapshot());

        using var log = new StringWriter(CultureInfo.InvariantCulture);
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(usersRoot, playerItemsRoot: null, playerSpellsRoot: spellsRoot),
            log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
                connection,
                ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));

        Assert.Contains("WorldServer ordinary admission committed key=1293600.", log.ToString());
        Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), sent[0]);
        return (handler, connection, sent);
    }

    private static PlayerSpellSnapshot CreateUndyingImprintingSpellSnapshot()
    {
        return new PlayerSpellSnapshot(
            [
                new PlayerSpellSnapshotEntry(
                    LegacyTestUndyingImprintingSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ]);
    }

    private static async ValueTask InvokeUndyingImprintingRuntimeTickAsync(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendLegacyThunderStrikerUndyingImprintingRuntimeTickAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(
            handler,
            [connection, playerItems, playerSpells, CancellationToken.None]);
        Assert.NotNull(result);
        await (ValueTask)result;
    }

    private static void AssertUndyingImprintingUpdate(
        byte[] bytes,
        uint uid,
        uint time,
        uint damage)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.UndyingImprinting,
                    Value: 0,
                    Flag: LegacyTestUndyingImprintingStatusFlag,
                    Time: time,
                    Damage: damage,
                    SpellLevel: 0),
            ],
            packet.Updates);
    }
}
