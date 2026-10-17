using System.Buffers;
using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPlayerDynamicVisibilityParityTests
{
    private const uint OwnerUid = 1_293_600;
    private const uint OtherUid = OwnerUid + 1;

    [Theory]
    [InlineData("71", null, "71", null, true)]
    [InlineData("71", null, "72", null, false)]
    [InlineData(null, null, null, null, true)]
    [InlineData(null, null, "0", null, true)]
    [InlineData(null, null, "71", null, false)]
    [InlineData(null, "71", "71", null, true)]
    [InlineData("71", null, null, "72", false)]
    [InlineData("bad", "71", "71", null, true)]
    [InlineData("71", "72", null, "71", true)]
    [InlineData("bad", null, null, null, true)]
    [InlineData("bad", null, "71", null, false)]
    [InlineData("-1", null, "0", null, true)]
    [InlineData("4294967296", null, "0", null, true)]
    [InlineData("18446744073709551616", null, null, null, true)]
    [InlineData(" +71 ", null, "71", null, true)]
    [InlineData("4294967295", null, "4294967295", null, true)]
    public void SharedVisibilityAndSupportResolutionHonorInstanceAliasesAndParsedDefaultZero(
        string? ownerPrimary, string? ownerAlias, string? otherPrimary, string? otherAlias, bool expected)
    {
        // RoleView.cs:96-105,135-159 filters visible candidates; it is not TryGetValue's contract.
        var owner = CreateSession(OwnerUid, CreateAttributes(ownerPrimary, ownerAlias));
        var other = CreateSession(OtherUid, CreateAttributes(otherPrimary, otherAlias));

        AssertVisibilityOwners(owner, other, expected);
    }

    [Theory]
    [InlineData(1015, 1015, 18, 18, true)]
    [InlineData(1015, 1015, -18, 18, true)]
    [InlineData(1015, 1015, 19, 0, false)]
    [InlineData(1015, 1015, 0, 19, false)]
    [InlineData(1015, 1002, 0, 0, false)]
    [InlineData(10428, 10428, 0, 0, false)]
    [InlineData(1015, 10428, 0, 0, false)]
    public void SharedVisibilityAndSupportResolutionRetainMapAndInclusiveChebyshevRange(
        long ownerMap, long otherMap, long dx, long dy, bool expected)
    {
        var owner = CreateSession(OwnerUid, CreateAttributes("71"), mapId: ownerMap);
        var other = CreateSession(OtherUid, CreateAttributes("71"), mapId: otherMap, x: 830 + dx, y: 584 + dy);

        AssertVisibilityOwners(owner, other, expected);
    }

    [Fact]
    public void PlayerSnapshotAttributeCannotInventAnAllowDynamicVisibilityBypass()
    {
        // Player.cs:4595 initializes AllowDynamic=false; no player attribute enables it.
        var attributes = CreateAttributes("72");
        attributes["AllowDynamic"] = "true";

        AssertVisibilityOwners(CreateSession(OwnerUid, CreateAttributes("71")),
            CreateSession(OtherUid, attributes), expected: false);
    }

    [Theory]
    [InlineData("71", true)]
    [InlineData("72", false)]
    public async Task VisibleD13PlayerSpawnPublicationRequiresSameDynamicInstance(string otherDynamicId, bool expected)
    {
        var owner = CreateSession(OwnerUid, CreateAttributes("71"));
        var other = CreateSession(OtherUid, CreateAttributes(otherDynamicId), x: 848, y: 602);
        var ownerSent = new List<byte[]>();
        var otherSent = new List<byte[]>();
        var ownerConnection = Capture(ownerSent);
        var otherConnection = Capture(otherSent);
        var sessions = new WorldSessionStore();
        sessions.Set(ownerConnection, owner);
        sessions.Set(otherConnection, other);
        var context = new WorldPacketHandlerContext(ownerConnection, TextWriter.Null, owner,
            Sessions: sessions, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var method = typeof(WorldPacketHandlers).GetMethod("SendVisiblePlayersAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        await Assert.IsType<ValueTask>(method.Invoke(null, [context, owner, CancellationToken.None]));

        if (expected)
        {
            AssertPlayerSpawn(Assert.Single(ownerSent), OtherUid);
            AssertPlayerSpawn(Assert.Single(otherSent), OwnerUid);
        }
        else
        {
            Assert.Empty(ownerSent);
            Assert.Empty(otherSent);
        }
        GC.KeepAlive(otherConnection);
    }

    [Theory]
    [InlineData("71", true)]
    [InlineData("72", false)]
    public async Task VisibleD13RuntimeStatusExpiryDoesNotPublishAcrossDynamicInstances(string otherDynamicId, bool expected)
    {
        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var attributes = CreateAttributes("71");
        var flags = new ulong[16];
        flags[0x7E / 32] = 1UL << (0x7E % 32);
        attributes["StatusFlags"] = string.Join('/', flags);
        attributes[WorldPacketHandlers.RuntimeLegacyDefensiveStanceExpiresAtUtcAttribute] =
            now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture);
        var owner = CreateSession(OwnerUid, attributes);
        var other = CreateSession(OtherUid, CreateAttributes(otherDynamicId), x: 848, y: 602);
        var ownerSent = new List<byte[]>();
        var otherSent = new List<byte[]>();
        var ownerConnection = Capture(ownerSent);
        var otherConnection = Capture(otherSent);
        var handler = new WorldServerFrameHandler(snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13, log: TextWriter.Null);
        var sessionsField = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(sessionsField);
        var sessions = Assert.IsType<WorldSessionStore>(sessionsField.GetValue(handler));
        sessions.Set(ownerConnection, owner);
        sessions.Set(otherConnection, other);
        var method = typeof(WorldServerFrameHandler).GetMethod("SendDueLegacyDefensiveStanceExpirationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        await Assert.IsType<ValueTask>(method.Invoke(handler, [now, CancellationToken.None]));

        var ownerBytes = Assert.Single(ownerSent);
        var frame = ReadFrame(ownerBytes);
        Assert.Equal((ushort)2176, frame.Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var update));
        Assert.Equal(OwnerUid, update.Uid);
        var status = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, status.Type);
        Assert.NotNull(status.Values);
        Assert.All(status.Values, value => Assert.Equal(0UL, value));
        Assert.True(sessions.TryGet(ownerConnection, out var expired));
        Assert.NotNull(expired);
        Assert.False(expired.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyDefensiveStanceExpiresAtUtcAttribute));
        if (expected)
        {
            Assert.Equal(ownerBytes, Assert.Single(otherSent));
        }
        else
        {
            Assert.Empty(otherSent);
        }
        GC.KeepAlive(otherConnection);
    }

    private static void AssertVisibilityOwners(WorldSession owner, WorldSession other, bool expected)
    {
        foreach (var type in new[] { typeof(WorldPacketHandlers), typeof(WorldServerFrameHandler), typeof(LegacyMagicSupportTargetResolver) })
        {
            var method = type.GetMethod("CanSeePlayer", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var visible = Assert.IsType<bool>(method.Invoke(null, [owner.Character, other.Character]));
            Assert.True(visible == expected, type.Name + ".CanSeePlayer returned " + visible);
            Assert.Equal(expected, Assert.IsType<bool>(method.Invoke(null, [other.Character, owner.Character])));
        }

        var ownerConnection = new ConquerTcpConnection(null, null);
        var otherConnection = new ConquerTcpConnection(null, null);
        var sessions = new WorldSessionStore();
        sessions.Set(ownerConnection, owner);
        sessions.Set(otherConnection, other);
        var context = new WorldPacketHandlerContext(ownerConnection, TextWriter.Null, owner,
            Sessions: sessions, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        Assert.Equal(expected, LegacyMagicSupportTargetResolver.TryResolveVisiblePlayerTarget(
            context, owner, OtherUid, out var target));
        if (expected)
        {
            Assert.Same(otherConnection, target.ActiveSession.Connection);
            Assert.Same(other, target.ActiveSession.Session);
        }
        else
        {
            Assert.Null(target);
        }
        GC.KeepAlive(otherConnection);
    }

    private static Dictionary<string, string> CreateAttributes(string? primary = null, string? alias = null)
    {
        // Keep keys case-sensitive so both alias precedence and fallback are exercised.
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (primary is not null)
        {
            attributes["DynamicID"] = primary;
        }
        if (alias is not null)
        {
            attributes["DynamicId"] = alias;
        }
        return attributes;
    }

    private static WorldSession CreateSession(uint uid, IReadOnlyDictionary<string, string> attributes,
        long mapId = 1015, long x = 830, long y = 584)
    {
        var character = new CharacterSnapshot(
            Uid: uid, Name: "Visibility" + uid, Body: 1003, Face: 0, Hair: 0,
            Class: 142, FirstClass: 142, SecondClass: 0, Reborn: 0, Level: 120, Experience: 0,
            MapId: mapId, X: x, Y: y, PreviousMapId: mapId, PreviousMapX: x, PreviousMapY: y,
            Strength: 0, Agility: 0, Vitality: 0, Spirit: 300, AttributePoints: 0,
            MinHitPoints: 5000, MinMana: 500, Money: 0, ConquerPoints: 0,
            BoundConquerPoints: 0, BankConquerPoints: 0, PkPoints: 0, Online: 1,
            SourceFile: "fixture://player-dynamic-visibility", Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
        return new WorldSession(uid, "visibility" + uid, character,
            PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection Capture(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, null, sendAsync: (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static void AssertPlayerSpawn(byte[] bytes, uint uid)
    {
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn));
        Assert.Equal((ulong)uid, spawn.GetNumericField(2));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
