using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTransformExpiryRuntimeTests
{
    private const long OwnerUid = 1_293_600;
    private const int SpawnMeshField = 1;
    private const int SpawnUidField = 2;
    private const int SpawnHitpointsField = 21;
    private const int SpawnMaxLifeField = 23;
    private static readonly DateTimeOffset TestNow =
        DateTimeOffset.Parse("2026-07-19T13:00:00Z", CultureInfo.InvariantCulture);

    [Fact]
    public async Task DueTransformExpirySendsQueryEquipmentHpPeerSpawnAndFinishActionInOrder()
    {
        using var log = new StringWriter();
        var events = new List<(string Recipient, byte[] Bytes)>();
        var ownerConnection = CreateSendingConnection(events, "owner", 9_958);
        var visibleConnection = CreateSendingConnection(events, "visible", 9_959);
        var distantConnection = CreateSendingConnection(events, "distant", 9_960);
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var owner = CreateSession(
            OwnerUid,
            "Owner",
            x: 430,
            y: 380,
            transformed: true);
        var visible = CreateSession(
            OwnerUid + 1,
            "Visible",
            x: 435,
            y: 383,
            transformed: false);
        var distant = CreateSession(
            OwnerUid + 2,
            "Distant",
            x: 900,
            y: 900,
            transformed: false);
        var sessions = GetHandlerSessions(handler);
        sessions.Set(ownerConnection, owner);
        sessions.Set(visibleConnection, visible);
        sessions.Set(distantConnection, distant);

        await InvokeTransformExpirationsAsync(handler, TestNow);

        var updatedOwner = AssertHandlerSession(handler, ownerConnection);
        Assert.Equal(720, updatedOwner.Character.MinHitPoints);
        Assert.Equal("0", updatedOwner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
        Assert.False(updatedOwner.Character.Attributes.ContainsKey(LegacyTransformState.ActiveAttribute));
        Assert.False(updatedOwner.Character.Attributes.ContainsKey(LegacyTransformState.ExpiresAtUtcAttribute));
        Assert.False(updatedOwner.Character.Attributes.ContainsKey(LegacyTransformState.MaxHitpointsAttribute));
        Assert.Equal("preserved", updatedOwner.Character.Attributes["Unrelated"]);

        // ClientTransform.FinishTransform queries BEFORE its extra peer spawn/action.
        // The first query spawn and BloodTide +=0 pair precede OnDequeue's clamp.
        Assert.Collection(
            events,
            item =>
            {
                Assert.Equal("owner", item.Recipient);
                Assert.True(LegacyShowEquipmentPacket.TryParse(ReadFrame(item.Bytes), out var equipment));
                Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipment.Action);
            },
            item =>
            {
                Assert.Equal("owner", item.Recipient);
                Assert.True(LegacyServerInfoPacket.TryParse(ReadFrame(item.Bytes), out var server));
                Assert.Equal(0U, server.Vigor);
            },
            item => AssertExpiryVisibleSpawn(item, hitpoints: 3_000),
            AssertExpiryOwnerMaximum,
            item => AssertExpiryOwnerPair(item, hitpoints: 3_000),
            item =>
            {
                Assert.Equal("owner", item.Recipient);
                Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(item.Bytes), out var status));
                Assert.Equal(checked((uint)OwnerUid), status.Uid);
                Assert.Equal(720U, status.MaxHP);
            },
            AssertExpiryOwnerMaximum,
            item => AssertExpiryOwnerPair(item, hitpoints: 720),
            AssertExpiryOwnerMaximum,
            item => AssertExpiryOwnerPair(item, hitpoints: 720),
            item =>
            {
                Assert.Equal("owner", item.Recipient);
                Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(item.Bytes), out var perfection));
                Assert.Equal(0U, perfection.Type);
                Assert.Equal(checked((uint)OwnerUid), perfection.Uid);
                Assert.Equal(0U, perfection.Level);
            },
            item => AssertExpiryVisibleSpawn(item, hitpoints: 720),
            item =>
            {
                Assert.Equal("owner", item.Recipient);
                Assert.True(MsgActionPacket.TryParse(ReadFrame(item.Bytes), out var action));
                Assert.Equal(MsgActionType.FinishTransform, action.Type);
                Assert.Equal(checked((uint)OwnerUid), action.ObjId);
            });
        Assert.DoesNotContain(events, item => item.Recipient == "distant");
        Assert.Contains(
            "WorldServer expired legacy Transform state playerCount=1 viewerCount=1",
            log.ToString());
    }

    private static void AssertExpiryOwnerMaximum((string Recipient, byte[] Bytes) item)
    {
        Assert.Equal("owner", item.Recipient);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(item.Bytes), out var update));
        Assert.Equal(checked((uint)OwnerUid), update.Uid);
        var maximum = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type);
        Assert.Equal(720UL, maximum.Value);
    }

    private static void AssertExpiryOwnerPair((string Recipient, byte[] Bytes) item, ulong hitpoints)
    {
        Assert.Equal("owner", item.Recipient);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(item.Bytes), out var update));
        Assert.Equal(checked((uint)OwnerUid), update.Uid);
        Assert.Collection(
            update.Updates,
            maximum =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type);
                Assert.Equal(720UL, maximum.Value);
            },
            current =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, current.Type);
                Assert.Equal(hitpoints, current.Value);
            });
    }

    private static void AssertExpiryVisibleSpawn((string Recipient, byte[] Bytes) item, ulong hitpoints)
    {
        Assert.Equal("visible", item.Recipient);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(item.Bytes), out var spawn));
        Assert.Equal(checked((ulong)OwnerUid), spawn.GetNumericField(SpawnUidField));
        Assert.Equal(1_003UL, spawn.GetNumericField(SpawnMeshField));
        Assert.Equal(hitpoints, spawn.GetNumericField(SpawnHitpointsField));
        Assert.Equal(720UL, spawn.GetNumericField(SpawnMaxLifeField));
    }

    private static WorldSession CreateSession(
        long uid,
        string name,
        long x,
        long y,
        bool transformed)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaxHitpoints"] = "720",
            ["Unrelated"] = "preserved",
        };
        var character = new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 0,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 1,
            Experience: 0,
            MapId: 1_002,
            X: x,
            Y: y,
            PreviousMapId: 1_002,
            PreviousMapX: x,
            PreviousMapY: y,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 720,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://legacy-transform-expiry",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
        if (transformed)
        {
            character = LegacyTransformState.Apply(
                character,
                lookface: 207,
                maxHitpoints: 3_000,
                TestNow.AddTicks(-1));
        }

        return new WorldSession(
            AccountId: uid,
            Username: name,
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(
        List<(string Recipient, byte[] Bytes)> events,
        string recipient,
        int port)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, port),
            sendAsync: (frame, _) =>
            {
                lock (events)
                {
                    events.Add((recipient, frame.ToArray()));
                }

                return ValueTask.CompletedTask;
            });
    }

    private static async ValueTask InvokeTransformExpirationsAsync(
        WorldServerFrameHandler handler,
        DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendDueLegacyTransformExpirationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var invocation = Assert.IsType<ValueTask>(method.Invoke(
            handler,
            [now, CancellationToken.None]));
        await invocation;
    }

    private static WorldSessionStore GetHandlerSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static WorldSession AssertHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection)
    {
        Assert.True(GetHandlerSessions(handler).TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
