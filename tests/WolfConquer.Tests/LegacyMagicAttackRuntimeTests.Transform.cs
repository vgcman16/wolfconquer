using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort TransformSpellId = 1_350;
    private const byte TransformSpellLevel = 0;
    private const uint TransformLookface = 207;
    private const uint TransformMaxHitpoints = 3_000;
    private const uint TransformDurationSeconds = 40;
    private const int TransformXpListFlag = 0x04;
    private const int TransformFlyFlag = 0x1B;
    private const int TransformRideFlag = 0x32;
    private const int TransformSpawnMeshField = 1;
    private const int TransformSpawnUidField = 2;

    [Fact]
    public async Task HandlerTransformAppliesSourceStateAndD13PacketOrder()
    {
        var now = DateTimeOffset.Parse("2026-07-19T12:00:00Z", CultureInfo.InvariantCulture);
        var worldData = CreateTransformCatalog(includeTransformInfo: true);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var peerConnection = CreateSendingConnection(peerSent);
        var casterSession = CreateSession(
            minMana: 100,
            minHitPoints: 5_000,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    TransformSpellId,
                    TransformSpellLevel,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: CreateTransformStatusAttributes(TransformXpListFlag));
        var peerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Peer",
            x: 831,
            y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(peerConnection, peerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            TimeProvider: new TransformFixedTimeProvider(now));

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(CreateTransformAttack().ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(75, updated.Character.MinMana);
        Assert.Equal("85", updated.Character.Attributes["Stamina"]);
        Assert.Equal(TransformMaxHitpoints, checked((uint)updated.Character.MinHitPoints));
        Assert.Equal(
            TransformLookface.ToString(CultureInfo.InvariantCulture),
            updated.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
        Assert.Equal(
            TransformLookface.ToString(CultureInfo.InvariantCulture),
            updated.Character.Attributes[LegacyTransformState.TransformationIdAliasAttribute]);
        Assert.Equal("1", updated.Character.Attributes[LegacyTransformState.ActiveAttribute]);
        Assert.Equal(
            TransformMaxHitpoints.ToString(CultureInfo.InvariantCulture),
            updated.Character.Attributes[LegacyTransformState.MaxHitpointsAttribute]);
        Assert.Equal(
            now.AddSeconds(TransformDurationSeconds),
            DateTimeOffset.Parse(
                updated.Character.Attributes[LegacyTransformState.ExpiresAtUtcAttribute],
                CultureInfo.InvariantCulture));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(
            updated.Character.Attributes,
            TransformXpListFlag));
        Assert.Equal(
            300,
            Assert.Single(updated.PlayerSpells.Spells, spell => spell.Id == TransformSpellId).Experience);

        var animation = Assert.Single(
            ParseSpellAnimations(casterSent),
            packet => packet.SpellId == TransformSpellId);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((ushort)833, animation.X);
        Assert.Equal((ushort)586, animation.Y);
        Assert.Equal(TransformSpellLevel, animation.SpellLevel);
        Assert.Equal((byte)2, animation.LevelHu);
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, animationTarget.Uid);
        Assert.Equal(TransformDurationSeconds, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Equal(MsgAttackEffect.None, animationTarget.Effect);

        var expectedMesh = (TransformLookface * 10_000_000U) + 1_003U;
        Assert.Contains(
            peerSent,
            bytes => LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
                spawn.GetNumericField(TransformSpawnUidField) == PlayerUid &&
                spawn.GetNumericField(TransformSpawnMeshField) == expectedMesh);

        var xpListIndex = SingleFrameIndex(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.Count == 1 &&
            packet.Updates[0].Type == LegacyMsgUpdateDataType.XPList &&
            packet.Updates[0].Value == 0);
        var statusIndex = SingleFrameIndex(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
            packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.StatusFlag));
        // ClientTransform.UpdateStatus: HP setter uses prior Status.Max,
        // then transformed Max is assigned before its separate explicit pair.
        var hitpointFrames = casterSent.Select((bytes, index) => (Bytes: bytes, Index: index))
            .Where(frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame.Bytes), out var update) &&
                update.Uid == PlayerUid && update.Updates.Any(entry =>
                    entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
        Assert.Equal(4, hitpointFrames.Length); // ALL HP-bearing frames, not just the final value.
        (ulong Maximum, ulong Hitpoints)[] phases =
            [(5_000, TransformMaxHitpoints), (TransformMaxHitpoints, TransformMaxHitpoints)];
        for (var index = 0; index < phases.Length; index++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(hitpointFrames[index * 2].Bytes), out var maximum));
            var maxEntry = Assert.Single(maximum.Updates);
            Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maxEntry.Type);
            Assert.Equal(phases[index].Maximum, maxEntry.Value);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(hitpointFrames[index * 2 + 1].Bytes), out var combined));
            var phase = phases[index];
            Assert.Collection(combined.Updates,
                max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal(phase.Maximum, max.Value); },
                hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal(phase.Hitpoints, hp.Value); });
            Assert.Equal(hitpointFrames[index * 2].Index + 1, hitpointFrames[index * 2 + 1].Index);
        }
        Assert.Equal(hitpointFrames[1].Index + 1, hitpointFrames[2].Index);
        var animationIndex = SingleFrameIndex(casterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.SpellId == TransformSpellId);
        var progressIndex = SingleFrameIndex(casterSent, frame =>
            LegacySpellPacket.TryParse(frame, out var packet) &&
            packet.Id == TransformSpellId &&
            packet.Experience == 300);
        Assert.True(xpListIndex < statusIndex);
        Assert.True(statusIndex < hitpointFrames[0].Index);
        Assert.True(hitpointFrames[^1].Index < animationIndex);
        Assert.True(animationIndex < progressIndex);
    }

    [Fact]
    public async Task HandlerTransformMissingInfoStillConsumesXpAndSendsAnimationProgress()
    {
        var worldData = CreateTransformCatalog(includeTransformInfo: false);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 100,
            minHitPoints: 5_000,
            stamina: 100,
            spellIds: [TransformSpellId],
            attributes: CreateTransformStatusAttributes(TransformXpListFlag));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(context, ReadFrame(CreateTransformAttack().ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(75, updated.Character.MinMana);
        Assert.Equal("85", updated.Character.Attributes["Stamina"]);
        Assert.Equal(5_000, updated.Character.MinHitPoints);
        Assert.False(updated.Character.Attributes.ContainsKey(LegacyTransformState.TransformationIdAttribute));
        Assert.False(updated.Character.Attributes.ContainsKey(LegacyTransformState.MaxHitpointsAttribute));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(
            updated.Character.Attributes,
            TransformXpListFlag));
        Assert.Single(ParseSpellAnimations(sent), packet => packet.SpellId == TransformSpellId);
        Assert.Contains(
            sent,
            bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Id == TransformSpellId &&
                packet.Experience == 300);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(update =>
                update.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
    }

    [Theory]
    [InlineData(TransformFlyFlag)]
    [InlineData(TransformRideFlag)]
    public async Task HandlerTransformFlyOrRideRejectsBeforeResources(int blockedFlag)
    {
        var worldData = CreateTransformCatalog(includeTransformInfo: true);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 100,
            minHitPoints: 5_000,
            stamina: 100,
            spellIds: [TransformSpellId],
            attributes: CreateTransformStatusAttributes(blockedFlag));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(context, ReadFrame(CreateTransformAttack().ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(100, updated.Character.MinMana);
        Assert.Equal("100", updated.Character.Attributes["Stamina"]);
        Assert.Null(updated.MagicCooldowns);
        Assert.False(updated.Character.Attributes.ContainsKey(LegacyTransformState.TransformationIdAttribute));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(sent, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(update =>
                update.Type is LegacyMsgUpdateDataType.Mana or LegacyMsgUpdateDataType.Stamina));
        var message = Assert.Single(ParseLegacyMessages(sent));
        Assert.Equal(LegacyMsgMessagePacket.TopLeftSystemChatType, message.ChatType);
        Assert.Equal("You cant use this skill right now !", message.Content[3]);
    }

    private static WorldDataCatalog CreateTransformCatalog(bool includeTransformInfo)
    {
        var monster = CreateMonsterFamily();
        var rows = new[]
        {
            CreateMagicRow(
                TransformSpellId,
                sort: (byte)LegacyMagicSort.Transform,
                level: TransformSpellLevel,
                useMana: 25,
                experience: 1_000,
                useStamina: 15,
                coolDown: 0,
                coldTime: 0,
                duration: TransformDurationSeconds),
            CreateMagicRow(
                TransformSpellId,
                sort: (byte)LegacyMagicSort.Transform,
                level: 1,
                useMana: 25,
                experience: 1_000,
                useStamina: 15,
                coolDown: 0,
                coldTime: 0,
                duration: TransformDurationSeconds),
        };
        IReadOnlyList<TransformInfo> transforms = includeTransformInfo
            ?
            [
                new TransformInfo(
                    TransformSpellId,
                    TransformSpellLevel,
                    "DivineHare",
                    checked((int)TransformLookface),
                    checked((int)TransformMaxHitpoints),
                    "fixture://TransformInfo.txt",
                    LineNumber: 1,
                    RawLine: "1350 0 DivineHare 207 3000",
                    Fields: ["1350", "0", "DivineHare", "207", "3000"]),
            ]
            : [];
        return CreateMagicCatalogCore(
            monster,
            spawns: [],
            sobNpcs: [],
            magicRows: rows,
            transformInfo: transforms);
    }

    private static MsgAttackPacket CreateTransformAttack()
    {
        return new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 833,
            Y: 586,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: TransformSpellId));
    }

    private static Dictionary<string, string> CreateTransformStatusAttributes(params int[] flags)
    {
        var values = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
        foreach (var flag in flags)
        {
            values[flag / 32] |= 1UL << (flag % 32);
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = string.Join('/', values) + "/",
        };
    }

    private sealed class TransformFixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
