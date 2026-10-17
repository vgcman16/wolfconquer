using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private const int FreezeWarRuntimeFreezeFlag = 0x3B;
    private const uint FreezeWarRuntimeFirstRightCornerChatType = 2_108;
    private const uint FreezeWarRuntimeContinueRightCornerChatType = 2_109;

    [Fact]
    public async Task HandlerFreezeWarRuntimeTickBroadcastsSignupCountdownEveryTenSeconds()
    {
        var fixture = await CreateJoinedFreezeWarRuntimeFixtureAsync();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(11));

        Assert.Contains(
            fixture.RedSent,
            frame => IsLegacyCenterMessage(frame, "[FreezeWar] Fight starts in 50 Secounds."));
        Assert.Contains(
            fixture.BlueSent,
            frame => IsLegacyCenterMessage(frame, "[FreezeWar] Fight starts in 50 Secounds."));
    }

    [Fact]
    public async Task HandlerFreezeWarRuntimeTickStartsAfterSignupWindowRemovesFreezeAndClosesSignup()
    {
        var fixture = await CreateJoinedFreezeWarRuntimeFixtureAsync();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(61));

        Assert.Equal(LegacyFreezeWarProcess.Alive, fixture.Store.Process);
        Assert.False(HasLegacyStatusFlag(
            GetSession(fixture.Handler, fixture.RedConnection).Character.Attributes["StatusFlags"],
            FreezeWarRuntimeFreezeFlag));
        Assert.False(HasLegacyStatusFlag(
            GetSession(fixture.Handler, fixture.BlueConnection).Character.Attributes["StatusFlags"],
            FreezeWarRuntimeFreezeFlag));
        Assert.False(fixture.Store.TryJoin(CreateDetachedFreezeWarSession(1_293_602), out _, out _));
        Assert.Contains(
            fixture.RedSent,
            frame => IsLegacyCenterMessage(frame, "FreezeWar has started! signup are now closed."));
        Assert.Contains(
            fixture.RedSent,
            frame => IsLegacyStatusUpdateWithoutFlag(frame, 1_293_600, FreezeWarRuntimeFreezeFlag));
        Assert.Contains(
            fixture.BlueSent,
            frame => IsLegacyStatusUpdateWithoutFlag(frame, 1_293_601, FreezeWarRuntimeFreezeFlag));
    }

    [Fact]
    public async Task HandlerFreezeWarRuntimeTickEndsAfterAliveWindowTeleportsPlayersAndRewardsWinner()
    {
        var fixture = await CreateJoinedFreezeWarRuntimeFixtureAsync();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(61));
        fixture.RedSent.Clear();
        fixture.BlueSent.Clear();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddMinutes(11).AddSeconds(2));

        var red = GetSession(fixture.Handler, fixture.RedConnection);
        var blue = GetSession(fixture.Handler, fixture.BlueConnection);

        Assert.Equal(LegacyFreezeWarProcess.Dead, fixture.Store.Process);
        Assert.Equal(1_002, red.Character.MapId);
        Assert.Equal(410, red.Character.X);
        Assert.Equal(354, red.Character.Y);
        Assert.Equal(1_505, red.Character.PreviousMapId);
        Assert.Equal(165, red.Character.PreviousMapX);
        Assert.Equal(245, red.Character.PreviousMapY);
        Assert.Equal("0", red.Character.Attributes["DynamicID"]);
        Assert.Equal("0", red.Character.Attributes["PDinamycID"]);
        Assert.Equal(120_000, red.Character.ConquerPoints);
        Assert.Equal(1_002, blue.Character.MapId);
        Assert.Equal(410, blue.Character.X);
        Assert.Equal(354, blue.Character.Y);
        Assert.Equal(1_505, blue.Character.PreviousMapId);
        Assert.Equal(153, blue.Character.PreviousMapX);
        Assert.Equal(105, blue.Character.PreviousMapY);
        Assert.Equal("0", blue.Character.Attributes["DynamicID"]);
        Assert.Equal("0", blue.Character.Attributes["PDinamycID"]);
        Assert.Equal(0, blue.Character.ConquerPoints);
        Assert.Contains(fixture.RedSent, IsLegacyTwinCityTeleport);
        Assert.Contains(fixture.BlueSent, IsLegacyTwinCityTeleport);
        Assert.Contains(
            fixture.RedSent,
            frame => IsLegacyCenterMessageContaining(frame, "FreezeWar has ended.Red"));
        Assert.Contains(
            fixture.RedSent,
            frame => IsLegacyDialogMessage(frame, "Your team won and received 1200 ConquerPoints."));
        Assert.Contains(
            fixture.BlueSent,
            frame => IsLegacyDialogMessage(frame, "Your team lose : ( .Better luck next time."));

        var redTransition = AssertLegacyFreezeWarTeleportTransition(
            fixture.RedSent,
            1_293_600,
            1_002,
            410,
            354);
        var blueTransition = AssertLegacyFreezeWarTeleportTransition(
            fixture.BlueSent,
            1_293_601,
            1_002,
            410,
            354);
        var redStatusIndex = FindLegacyFreezeWarFrameIndex(
            fixture.RedSent,
            frame => IsLegacyStatusUpdateWithoutFlag(frame, 1_293_600, FreezeWarRuntimeFreezeFlag),
            "winner finish status update");
        var blueStatusIndex = FindLegacyFreezeWarFrameIndex(
            fixture.BlueSent,
            frame => IsLegacyStatusUpdateWithoutFlag(frame, 1_293_601, FreezeWarRuntimeFreezeFlag),
            "loser finish status update");
        var rewardIndex = FindLegacyFreezeWarFrameIndex(
            fixture.RedSent,
            frame => IsLegacyFreezeWarConquerPointsUpdate(frame, 1_293_600, 120_000),
            "winner ConquerPoints update");
        var redDialogIndex = FindLegacyFreezeWarFrameIndex(
            fixture.RedSent,
            frame => IsLegacyDialogMessage(frame, "Your team won and received 1200 ConquerPoints."),
            "winner dialog");
        var blueDialogIndex = FindLegacyFreezeWarFrameIndex(
            fixture.BlueSent,
            frame => IsLegacyDialogMessage(frame, "Your team lose : ( .Better luck next time."),
            "loser dialog");

        Assert.True(redTransition.MapStatusIndex < redStatusIndex);
        Assert.True(redStatusIndex < rewardIndex);
        Assert.True(rewardIndex < redDialogIndex);
        Assert.True(blueTransition.MapStatusIndex < blueStatusIndex);
        Assert.True(blueStatusIndex < blueDialogIndex);
    }

    [Fact]
    public async Task HandlerFreezeWarRuntimeAliveTickBroadcastsSourceHudEveryFourSeconds()
    {
        var fixture = await CreateJoinedFreezeWarRuntimeFixtureAsync();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(61));
        fixture.RedSent.Clear();
        fixture.BlueSent.Clear();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(65));

        AssertLegacyRightCornerHud(
            fixture.RedSent,
            "FreezeWar: Time Left: 9 : 56",
            "BlueTeam Score: 0",
            "RedTeam Score: 0");
        AssertLegacyRightCornerHud(
            fixture.BlueSent,
            "FreezeWar: Time Left: 9 : 56",
            "BlueTeam Score: 0",
            "RedTeam Score: 0");
    }

    [Fact]
    public async Task HandlerFreezeWarRuntimeAllBlueFrozenAwardsRedRoundAndFreezesParticipants()
    {
        var fixture = await CreateJoinedFreezeWarRuntimeFixtureAsync();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(61));
        SetFreezeWarSessionPosition(fixture, fixture.RedConnection, x: 160, y: 160);
        SetFreezeWarSessionPosition(fixture, fixture.BlueConnection, x: 170, y: 170, frozen: true);
        fixture.RedSent.Clear();
        fixture.BlueSent.Clear();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(62));

        Assert.Equal(LegacyFreezeWarProcess.Alive, fixture.Store.Process);
        Assert.Equal(1, GetFreezeWarRuntimeScore(fixture.Store, "Red"));
        Assert.Equal(0, GetFreezeWarRuntimeScore(fixture.Store, "Blue"));
        Assert.True(HasLegacyStatusFlag(
            GetSession(fixture.Handler, fixture.RedConnection).Character.Attributes["StatusFlags"],
            FreezeWarRuntimeFreezeFlag));
        Assert.True(HasLegacyStatusFlag(
            GetSession(fixture.Handler, fixture.BlueConnection).Character.Attributes["StatusFlags"],
            FreezeWarRuntimeFreezeFlag));
        Assert.Contains(
            fixture.RedSent,
            frame => IsLegacySystemMessage(frame, "Red has won this round. The next round has started."));
        Assert.Contains(
            fixture.RedSent,
            frame => IsLegacySystemMessage(frame, "The next round will start in 5 seconds."));
        Assert.Contains(
            fixture.BlueSent,
            frame => IsLegacySystemMessage(frame, "Red has won this round. The next round has started."));
        Assert.DoesNotContain(
            fixture.RedSent,
            frame => IsLegacyCenterMessageContaining(frame, "FreezeWar has ended."));
    }

    [Fact]
    public async Task HandlerFreezeWarRuntimeRoundResetUnfreezesAndReturnsParticipantsToTeamSpawns()
    {
        var fixture = await CreateJoinedFreezeWarRuntimeFixtureAsync();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(61));
        SetFreezeWarSessionPosition(fixture, fixture.RedConnection, x: 160, y: 160);
        SetFreezeWarSessionPosition(fixture, fixture.BlueConnection, x: 170, y: 170, frozen: true);
        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(62));
        fixture.RedSent.Clear();
        fixture.BlueSent.Clear();

        await InvokeFreezeWarRuntimeTickAsync(fixture.Handler, fixture.OpenedAt.AddSeconds(68));

        var red = GetSession(fixture.Handler, fixture.RedConnection);
        var blue = GetSession(fixture.Handler, fixture.BlueConnection);

        Assert.Equal(LegacyFreezeWarProcess.Alive, fixture.Store.Process);
        Assert.Equal(1_505, red.Character.MapId);
        Assert.Equal(165, red.Character.X);
        Assert.Equal(245, red.Character.Y);
        Assert.Equal(1_015, red.Character.PreviousMapId);
        Assert.Equal(830, red.Character.PreviousMapX);
        Assert.Equal(584, red.Character.PreviousMapY);
        Assert.Equal(
            fixture.Store.DynamicId.ToString(CultureInfo.InvariantCulture),
            red.Character.Attributes["DynamicID"]);
        Assert.False(red.Character.Attributes.ContainsKey("PDinamycID"));
        Assert.Equal(1_505, blue.Character.MapId);
        Assert.Equal(153, blue.Character.X);
        Assert.Equal(105, blue.Character.Y);
        Assert.Equal(1_015, blue.Character.PreviousMapId);
        Assert.Equal(830, blue.Character.PreviousMapX);
        Assert.Equal(584, blue.Character.PreviousMapY);
        Assert.Equal(
            fixture.Store.DynamicId.ToString(CultureInfo.InvariantCulture),
            blue.Character.Attributes["DynamicID"]);
        Assert.False(blue.Character.Attributes.ContainsKey("PDinamycID"));
        Assert.False(HasLegacyStatusFlag(red.Character.Attributes["StatusFlags"], FreezeWarRuntimeFreezeFlag));
        Assert.False(HasLegacyStatusFlag(blue.Character.Attributes["StatusFlags"], FreezeWarRuntimeFreezeFlag));
        Assert.Contains(fixture.RedSent, frame => IsLegacyFreezeWarSpawnTeleport(frame, 1_293_600, 165, 245));
        Assert.Contains(fixture.BlueSent, frame => IsLegacyFreezeWarSpawnTeleport(frame, 1_293_601, 153, 105));

        var redStatusIndex = FindLegacyFreezeWarFrameIndex(
            fixture.RedSent,
            frame => IsLegacyStatusUpdateWithoutFlag(frame, 1_293_600, FreezeWarRuntimeFreezeFlag),
            "red round-reset status update");
        var blueStatusIndex = FindLegacyFreezeWarFrameIndex(
            fixture.BlueSent,
            frame => IsLegacyStatusUpdateWithoutFlag(frame, 1_293_601, FreezeWarRuntimeFreezeFlag),
            "blue round-reset status update");
        var redTransition = AssertLegacyFreezeWarTeleportTransition(
            fixture.RedSent,
            1_293_600,
            1_505,
            165,
            245);
        var blueTransition = AssertLegacyFreezeWarTeleportTransition(
            fixture.BlueSent,
            1_293_601,
            1_505,
            153,
            105);

        Assert.True(redStatusIndex < redTransition.TeleportIndex);
        Assert.True(blueStatusIndex < blueTransition.TeleportIndex);
    }

    private async Task<FreezeWarRuntimeFixture> CreateJoinedFreezeWarRuntimeFixtureAsync()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600, uid: 1_293_600, name: "RedFreeze", conquerPoints: 0);
        WriteSnapshot(1_293_601, uid: 1_293_601, name: "BlueFreeze", conquerPoints: 0);

        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var redSent = new List<byte[]>();
        var blueSent = new List<byte[]>();
        var redConnection = CreateSendingConnection(redSent);
        var blueConnection = CreateSendingConnection(blueSent);

        await handler.HandleFrameAsync(
            redConnection,
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));
        await handler.HandleFrameAsync(
            blueConnection,
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_601).ToFrameBytes()));

        var store = GetFreezeWarStates(handler);
        var openedAt = new DateTimeOffset(2026, 6, 20, 12, 55, 0, TimeSpan.Zero);
        Assert.NotNull(store.Open(openedAt));

        Assert.True(store.TryJoin(GetSession(handler, redConnection), out var redJoined, out var redTeam));
        Assert.Equal(LegacyFreezeWarTeamType.Red, redTeam);
        SetSession(handler, redConnection, redJoined);

        Assert.True(store.TryJoin(GetSession(handler, blueConnection), out var blueJoined, out var blueTeam));
        Assert.Equal(LegacyFreezeWarTeamType.Blue, blueTeam);
        SetSession(handler, blueConnection, blueJoined);

        redSent.Clear();
        blueSent.Clear();

        return new FreezeWarRuntimeFixture(
            handler,
            store,
            openedAt,
            redConnection,
            blueConnection,
            redSent,
            blueSent);
    }

    private static async ValueTask InvokeFreezeWarRuntimeTickAsync(
        WorldServerFrameHandler handler,
        DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendDueLegacyFreezeWarRuntimeUpdatesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(handler, [now, TimeZoneInfo.Utc, CancellationToken.None]);
        var tick = Assert.IsType<ValueTask>(result);
        await tick.ConfigureAwait(false);
    }

    private static LegacyFreezeWarRuntimeStateStore GetFreezeWarStates(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_freezeWarStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<LegacyFreezeWarRuntimeStateStore>(field.GetValue(handler));
    }

    private static WorldSession CreateDetachedFreezeWarSession(long uid)
    {
        return new WorldSession(
            AccountId: uid,
            Username: "late-freeze",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: "LateFreeze",
                Body: 1008,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 0,
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
                MinHitPoints: 61_258,
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
                    ["DynamicID"] = "0",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static bool IsLegacyCenterMessage(byte[] bytes, string message)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            packet.Content.Count > 3 &&
            string.Equals(packet.Content[3], message, StringComparison.Ordinal);
    }

    private static bool IsLegacyCenterMessageContaining(byte[] bytes, string message)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            packet.Content.Count > 3 &&
            packet.Content[3].Contains(message, StringComparison.Ordinal);
    }

    private static bool IsLegacyDialogMessage(byte[] bytes, string message)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ChatType == LegacyMsgMessagePacket.DialogChatType &&
            packet.Content.Count > 3 &&
            string.Equals(packet.Content[3], message, StringComparison.Ordinal);
    }

    private static bool IsLegacySystemMessage(byte[] bytes, string message)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content.Count > 3 &&
            string.Equals(packet.Content[3], message, StringComparison.Ordinal);
    }

    private static void AssertLegacyRightCornerHud(
        List<byte[]> sent,
        string timerMessage,
        string firstScoreMessage,
        string secondScoreMessage)
    {
        var messages = sent
            .Select(frame => LegacyMsgMessagePacket.TryParse(ReadFrame(frame), out var packet) ? packet : null)
            .Where(packet => packet is not null &&
                (packet.ChatType == FreezeWarRuntimeFirstRightCornerChatType ||
                    packet.ChatType == FreezeWarRuntimeContinueRightCornerChatType))
            .ToArray();

        Assert.Collection(
            messages,
            packet =>
            {
                Assert.NotNull(packet);
                Assert.Equal(FreezeWarRuntimeFirstRightCornerChatType, packet.ChatType);
                Assert.Equal(timerMessage, packet.Content[3]);
            },
            packet =>
            {
                Assert.NotNull(packet);
                Assert.Equal(FreezeWarRuntimeContinueRightCornerChatType, packet.ChatType);
                Assert.Equal(firstScoreMessage, packet.Content[3]);
            },
            packet =>
            {
                Assert.NotNull(packet);
                Assert.Equal(FreezeWarRuntimeContinueRightCornerChatType, packet.ChatType);
                Assert.Equal(secondScoreMessage, packet.Content[3]);
            });
    }

    private static void SetFreezeWarSessionPosition(
        FreezeWarRuntimeFixture fixture,
        ConquerTcpConnection connection,
        long x,
        long y,
        bool frozen = false)
    {
        var session = GetSession(fixture.Handler, connection);
        var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes["StatusFlags"] = frozen
            ? BuildStatusFlags(FreezeWarRuntimeFreezeFlag)
            : BuildStatusFlags();
        var updated = session with
        {
            Character = session.Character with
            {
                MapId = 1_505,
                X = x,
                Y = y,
                Attributes = attributes,
            },
        };
        SetSession(fixture.Handler, connection, updated);
    }

    private static int GetFreezeWarRuntimeScore(LegacyFreezeWarRuntimeStateStore store, string team)
    {
        var field = typeof(LegacyFreezeWarRuntimeStateStore).GetField(
            $"_{team.ToLowerInvariant()}Score",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<int>(field.GetValue(store));
    }

    private static bool IsLegacyStatusUpdateWithoutFlag(byte[] bytes, uint uid, int flag)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == uid &&
            packet.Updates.Count == 1 &&
            packet.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag &&
            packet.Updates[0].Values is { } values &&
            !HasLegacyStatusFlag(values, flag);
    }

    private static bool IsLegacyTwinCityTeleport(byte[] bytes)
    {
        return MsgActionPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Type == MsgActionType.Teleport &&
            packet.DwParam == 1_002 &&
            packet.DwParam3 == 1_002 &&
            packet.PositionX == 410 &&
            packet.PositionY == 354;
    }

    private static bool IsLegacyFreezeWarSpawnTeleport(byte[] bytes, uint uid, uint x, uint y)
    {
        return MsgActionPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ObjId == uid &&
            packet.Type == MsgActionType.Teleport &&
            packet.DwParam == 1_505 &&
            packet.DwParam3 == 1_505 &&
            packet.PositionX == x &&
            packet.PositionY == y;
    }

    private static (int TeleportIndex, int MapStatusIndex) AssertLegacyFreezeWarTeleportTransition(
        IReadOnlyList<byte[]> sent,
        uint uid,
        uint mapId,
        uint x,
        uint y)
    {
        var teleportIndex = FindLegacyFreezeWarFrameIndex(
            sent,
            frame => MsgActionPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.ObjId == uid &&
                packet.Type == MsgActionType.Teleport,
            $"Teleport action for {uid}");
        Assert.True(teleportIndex + 4 < sent.Count);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[teleportIndex]), out var teleport));
        Assert.Equal(mapId, teleport.DwParam);
        Assert.Equal((long)mapId, teleport.DwParam3);
        Assert.Equal(x, teleport.PositionX);
        Assert.Equal(y, teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[teleportIndex + 1]), out var merchantStartup));
        Assert.Equal(uid, merchantStartup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal((long)mapId, merchantStartup.DwParam3);
        Assert.Equal(x, merchantStartup.PositionX);
        Assert.Equal(y, merchantStartup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[teleportIndex + 2]), out var stopVending));
        Assert.Equal(uid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(mapId, stopVending.DwParam);
        Assert.Equal((long)mapId, stopVending.DwParam3);
        Assert.Equal(x, stopVending.PositionX);
        Assert.Equal(y, stopVending.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[teleportIndex + 3]), out var color));
        Assert.Equal(uid, color.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);
        Assert.Equal(x, color.PositionX);
        Assert.Equal(y, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[teleportIndex + 4]), out var mapStatus));
        Assert.Equal(mapId, mapStatus.Id);
        Assert.Equal(mapId, mapStatus.BaseId);

        return (teleportIndex, teleportIndex + 4);
    }

    private static int FindLegacyFreezeWarFrameIndex(
        IReadOnlyList<byte[]> sent,
        Func<byte[], bool> predicate,
        string description)
    {
        for (var index = 0; index < sent.Count; index++)
        {
            if (predicate(sent[index]))
            {
                return index;
            }
        }

        Assert.Fail($"Expected FreezeWar frame: {description}.");
        return -1;
    }

    private static bool IsLegacyFreezeWarConquerPointsUpdate(byte[] bytes, uint uid, ulong value)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == uid &&
            packet.Updates.Count == 1 &&
            packet.Updates[0].Type == LegacyMsgUpdateDataType.ConquerPoints &&
            packet.Updates[0].Value == value;
    }

    private sealed record FreezeWarRuntimeFixture(
        WorldServerFrameHandler Handler,
        LegacyFreezeWarRuntimeStateStore Store,
        DateTimeOffset OpenedAt,
        ConquerTcpConnection RedConnection,
        ConquerTcpConnection BlueConnection,
        List<byte[]> RedSent,
        List<byte[]> BlueSent);
}
