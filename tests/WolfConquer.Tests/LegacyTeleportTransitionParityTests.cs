using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeleportTransitionParityTests
{
    private const uint CharacterUid = 1_293_600;
    private const int PrayingFlag = 0x1F;
    private const int CastPrayFlag = 0x1E;
    private const int MagicDefenderFlag = 0x80;
    private const int AzureShieldFlag = 0x5D;
    private const int FocusedFlag = 0x15;
    private const int SupermanFlag = 0x12;
    private const string AuraMagicDefenderExpiresAtUtcAttribute = "LegacyAuraMagicDefenderExpiresAtUtc";
    private const string AuraMagicDefenderRemoveOnDeadAttribute = "LegacyAuraMagicDefenderRemoveOnDead";

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task ChangeMapFrom700RemovesTeleportBuffersBeforeTeleportAndSuppressesMerchantStartup()
    {
        var result = await RunChangeMapAsync(
            sourceMapId: 700,
            destinationMapId: 1_002,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute] = "true",
                [WorldPacketHandlers.RuntimeLegacyIntensifyStampAttribute] = "source-stamp",
                [WorldPacketHandlers.RuntimeLegacyIntensifyDamageAttribute] = "250",
                [WorldPacketHandlers.RuntimeLegacyFocusClientSpellIdAttribute] = "9000",
                [AuraMagicDefenderExpiresAtUtcAttribute] = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"),
                [AuraMagicDefenderRemoveOnDeadAttribute] = bool.TrueString,
                ["StatusFlags"] = BuildStatusFlags(
                    FocusedFlag,
                    SupermanFlag,
                    AzureShieldFlag,
                    CastPrayFlag,
                    PrayingFlag,
                    MagicDefenderFlag),
            });

        Assert.Equal(1_002, result.Session.Character.MapId);
        Assert.Equal(
            "false",
            result.Session.Character.Attributes[WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute]);
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, PrayingFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, CastPrayFlag));
        Assert.False(HasLegacyStatusFlag(result.Session.Character.Attributes, MagicDefenderFlag));
        Assert.False(result.Session.Character.Attributes.ContainsKey(AuraMagicDefenderExpiresAtUtcAttribute));
        Assert.False(result.Session.Character.Attributes.ContainsKey(AuraMagicDefenderRemoveOnDeadAttribute));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, AzureShieldFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, FocusedFlag));
        Assert.True(HasLegacyStatusFlag(result.Session.Character.Attributes, SupermanFlag));
        Assert.Equal(
            "source-stamp",
            result.Session.Character.Attributes[WorldPacketHandlers.RuntimeLegacyIntensifyStampAttribute]);
        Assert.Equal(
            "250",
            result.Session.Character.Attributes[WorldPacketHandlers.RuntimeLegacyIntensifyDamageAttribute]);
        Assert.Equal(
            "9000",
            result.Session.Character.Attributes[WorldPacketHandlers.RuntimeLegacyFocusClientSpellIdAttribute]);

        var teleport = AssertSingleAction(result.Sent, MsgActionType.Teleport);
        Assert.Equal(1_002, teleport.Packet.DwParam3);

        var indexedUpdates = ParseIndexedUpdates(result.Sent);
        var statusFrames = indexedUpdates
            .Where(frame =>
                frame.Packet.Uid == CharacterUid &&
                frame.Packet.Updates.Any(IsStatusVectorUpdate))
            .ToArray();
        Assert.Equal(3, statusFrames.Length);
        Assert.Equal(statusFrames[0].Index + 1, statusFrames[1].Index);
        Assert.Equal(statusFrames[1].Index + 1, statusFrames[2].Index);
        Assert.All(statusFrames, frame => Assert.True(frame.Index < teleport.Index));

        var afterPrayingRemoval = Assert.Single(
            statusFrames[0].Packet.Updates,
            IsStatusVectorUpdate).Values!;
        Assert.False(IsLegacyStatusFlagSet(afterPrayingRemoval, PrayingFlag));
        Assert.True(IsLegacyStatusFlagSet(afterPrayingRemoval, CastPrayFlag));
        Assert.True(IsLegacyStatusFlagSet(afterPrayingRemoval, MagicDefenderFlag));
        AssertUnrelatedFlagsRemain(afterPrayingRemoval);

        var afterCastPrayRemoval = Assert.Single(
            statusFrames[1].Packet.Updates,
            IsStatusVectorUpdate).Values!;
        Assert.False(IsLegacyStatusFlagSet(afterCastPrayRemoval, PrayingFlag));
        Assert.False(IsLegacyStatusFlagSet(afterCastPrayRemoval, CastPrayFlag));
        Assert.True(IsLegacyStatusFlagSet(afterCastPrayRemoval, MagicDefenderFlag));
        AssertUnrelatedFlagsRemain(afterCastPrayRemoval);

        var afterMagicDefenderRemoval = Assert.Single(
            statusFrames[2].Packet.Updates,
            IsStatusVectorUpdate).Values!;
        Assert.False(IsLegacyStatusFlagSet(afterMagicDefenderRemoval, PrayingFlag));
        Assert.False(IsLegacyStatusFlagSet(afterMagicDefenderRemoval, CastPrayFlag));
        Assert.False(IsLegacyStatusFlagSet(afterMagicDefenderRemoval, MagicDefenderFlag));
        AssertUnrelatedFlagsRemain(afterMagicDefenderRemoval);

        var azureShieldFrame = Assert.Single(
            indexedUpdates,
            frame =>
                frame.Packet.Uid == CharacterUid &&
                frame.Packet.Updates.Any(IsMagicDefenderAzureShieldReset));
        Assert.Equal(statusFrames[2].Index + 1, azureShieldFrame.Index);
        Assert.True(azureShieldFrame.Index < teleport.Index);
        var azureShieldReset = Assert.Single(
            azureShieldFrame.Packet.Updates,
            IsMagicDefenderAzureShieldReset);
        Assert.Equal(0UL, azureShieldReset.Value);
        Assert.Equal(0U, azureShieldReset.Time);
        Assert.Equal(0U, azureShieldReset.Damage);
        Assert.Equal(0U, azureShieldReset.SpellLevel);

        Assert.DoesNotContain(
            ParseActions(result.Sent),
            action => action.Type == MsgActionType.LegacyMerchantStartup);

        var observerUpdates = ParseIndexedUpdates(result.ObserverSent);
        Assert.Equal(4, observerUpdates.Count);
        Assert.Equal(
            [0, 1, 2],
            observerUpdates
                .Where(frame => frame.Packet.Updates.Any(IsStatusVectorUpdate))
                .Select(frame => frame.Index)
                .ToArray());
        Assert.Contains(observerUpdates[3].Packet.Updates, IsMagicDefenderAzureShieldReset);
        var observerLeave = Assert.Single(
            result.ObserverSent.Select((bytes, index) => new { Frame = ReadFrame(bytes), Index = index }),
            frame => LegacyRemoveEntityPacket.TryParse(frame.Frame, out _));
        Assert.Equal(4, observerLeave.Index);
        Assert.True(LegacyRemoveEntityPacket.TryParse(observerLeave.Frame, out var removed));
        Assert.Equal(CharacterUid, removed.Uid);
    }

    [Fact]
    public async Task ChangeMapTo700FromNon700EmitsMerchantStartupWithDestinationMap()
    {
        var result = await RunChangeMapAsync(sourceMapId: 1_002, destinationMapId: 700);

        Assert.Equal(700, result.Session.Character.MapId);
        var teleport = AssertSingleAction(result.Sent, MsgActionType.Teleport);
        Assert.Equal(700, teleport.Packet.DwParam3);

        var merchantStartup = AssertSingleAction(result.Sent, MsgActionType.LegacyMerchantStartup);
        Assert.True(merchantStartup.Index > teleport.Index);
        Assert.Equal(2U, merchantStartup.Packet.DwParam);
        Assert.Equal(700, merchantStartup.Packet.DwParam3);
    }

    private static async Task<TransitionResult> RunChangeMapAsync(
        int sourceMapId,
        int destinationMapId,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        const int sourceX = 52;
        const int sourceY = 50;
        const int portalX = 50;
        const int portalY = 50;
        const int destinationX = 60;
        const int destinationY = 60;

        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(sourceMapId, sourceX, sourceY, attributes);
        var observerSent = new List<byte[]>();
        var observerConnection = CreateSendingConnection(observerSent);
        var observerSession = CreateSession(
            sourceMapId,
            sourceX + 1,
            sourceY,
            attributes: null,
            uid: CharacterUid + 1);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        sessions.Set(observerConnection, observerSession);
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            portals:
            [
                new Portal(
                    sourceMapId,
                    portalX,
                    portalY,
                    destinationMapId,
                    destinationX,
                    destinationY),
            ],
            maps:
            [
                CreateMapMetadata(sourceMapId),
                CreateMapMetadata(destinationMapId),
            ]);
        using var log = new StringWriter();

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgActionPacket
            {
                ObjId = CharacterUid,
                Timestamp = 4_321,
                Type = MsgActionType.ChangeMap,
            }.ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        return new TransitionResult(Assert.IsType<WorldSession>(updatedSession), sent, observerSent);
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        IReadOnlyDictionary<string, string>? attributes,
        uint uid = CharacterUid)
    {
        return new WorldSession(
            AccountId: uid,
            Username: $"HeroOne{uid}",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: $"HeroOne{uid}",
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: 1_000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "",
                Attributes: attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static MapMetadata CreateMapMetadata(int mapId)
    {
        return new MapMetadata(
            mapId,
            mapId.ToString(CultureInfo.InvariantCulture) + ".ini",
            Name: null,
            MapDoc: mapId,
            TypeRaw: "0",
            Type: 0,
            Portal0X: null,
            Portal0Y: null,
            RebornMap: mapId,
            RebornPortal: 0,
            ResLev: 0,
            LinkMap: 0,
            LinkX: 0,
            LinkY: 0,
            ColorRaw: "0",
            Color: 0,
            Attributes: new Dictionary<string, string>());
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        if (!attributes.TryGetValue("StatusFlags", out var raw) &&
            !attributes.TryGetValue("BitVector", out raw))
        {
            return false;
        }

        var values = raw
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0UL)
            .ToArray();
        return IsLegacyStatusFlagSet(values, flag);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static void AssertUnrelatedFlagsRemain(IReadOnlyList<ulong> values)
    {
        Assert.True(IsLegacyStatusFlagSet(values, AzureShieldFlag));
        Assert.True(IsLegacyStatusFlagSet(values, FocusedFlag));
        Assert.True(IsLegacyStatusFlagSet(values, SupermanFlag));
    }

    private static bool IsStatusVectorUpdate(LegacyMsgUpdateEntry update)
    {
        return update.Type == LegacyMsgUpdateDataType.StatusFlag && update.Values is not null;
    }

    private static bool IsMagicDefenderAzureShieldReset(LegacyMsgUpdateEntry update)
    {
        return update.Type == LegacyMsgUpdateDataType.AzureShield &&
            update.Flag == MagicDefenderFlag &&
            update.Value == 0;
    }

    private static IndexedAction AssertSingleAction(IReadOnlyList<byte[]> sent, MsgActionType type)
    {
        return Assert.Single(
            sent.Select((bytes, index) => new { Frame = ReadFrame(bytes), Index = index })
                .Where(frame => MsgActionPacket.TryParse(frame.Frame, out var packet) && packet.Type == type)
                .Select(frame =>
                {
                    Assert.True(MsgActionPacket.TryParse(frame.Frame, out var packet));
                    return new IndexedAction(frame.Index, packet);
                }));
    }

    private static IReadOnlyList<MsgActionPacket> ParseActions(IEnumerable<byte[]> sent)
    {
        var actions = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                actions.Add(packet);
            }
        }

        return actions;
    }

    private static IReadOnlyList<IndexedUpdate> ParseIndexedUpdates(IReadOnlyList<byte[]> sent)
    {
        var updates = new List<IndexedUpdate>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                updates.Add(new IndexedUpdate(index, packet));
            }
        }

        return updates;
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

    private sealed record TransitionResult(
        WorldSession Session,
        IReadOnlyList<byte[]> Sent,
        IReadOnlyList<byte[]> ObserverSent);

    private sealed record IndexedAction(int Index, MsgActionPacket Packet);

    private sealed record IndexedUpdate(int Index, LegacyMsgUpdatePacket Packet);
}
