using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static readonly DateTimeOffset HellVortexSelectedMonsterDeathAt =
        DateTimeOffset.Parse("2026-10-15T00:00:00Z", CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(830, 584, 853, 584, true)]
    [InlineData(845, 584, 864, 584, false)]
    [InlineData(846, 584, 864, 584, true)]
    [InlineData(830, 584, 840, 611, true)]
    [InlineData(830, 593, 840, 612, false)]
    public void HellVortexSelectedMonsterUsesNeighborBlocksWithoutDynamicOrDistanceFilters(
        int casterX, int casterY, int selectedX, int selectedY, bool found)
    {
        using var fixture = CreateHellVortexSelectedMonsterFixture(dynamicCenter: true);
        var caster = fixture.Caster with
        {
            Character = fixture.Caster.Character with { X = casterX, Y = casterY },
        };
        Assert.True(fixture.States.TryMoveTo(fixture.Center, checked((ushort)selectedX),
            checked((ushort)selectedY), HellVortexSelectedMonsterDeathAt, out _));
        var selected = InvokeHellVortexSelectedMonster(fixture.Context, caster, fixture.Center.Uid);
        if (found)
        {
            Assert.NotNull(selected);
            Assert.Equal((selectedX, selectedY), ((int)selected.X, (int)selected.Y));
            Assert.Equal(72U, selected.DynamicId);
            Assert.Equal("71", caster.Character.Attributes["DynamicID"]);
        }
        else
        {
            Assert.Null(selected);
        }
        Assert.Empty(fixture.Sent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerHellVortexForeignMonsterCenterKeepsOwnRecipientDamageAndOrder(bool dynamicCenter)
    {
        using var fixture = CreateHellVortexSelectedMonsterFixture(dynamicCenter: dynamicCenter);
        var center = InvokeHellVortexSelectedMonster(fixture.Context, fixture.Caster, fixture.Center.Uid);
        Assert.NotNull(center);
        Assert.Equal(72U, center.DynamicId);
        await AssertHellVortexSelectedMonsterCastAsync(fixture, receives: true);
    }

    [Theory]
    [InlineData(830, 853, 847, true)]
    [InlineData(845, 864, 858, false)]
    public async Task HandlerHellVortexMonsterCenterUsesBlocksWhileRecipientsStillUseViewDistance(
        int casterX, int selectedX, int recipientX, bool receives)
    {
        using var fixture = CreateHellVortexSelectedMonsterFixture(dynamicCenter: true,
            casterX: casterX, selectedX: selectedX, recipientX: recipientX);
        Assert.True(Math.Abs(selectedX - casterX) > 18);
        await AssertHellVortexSelectedMonsterCastAsync(fixture, receives);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerHellVortexDeadRetainedMonsterCentersLivingRecipients(bool dynamicCenter)
    {
        using var fixture = CreateHellVortexSelectedMonsterFixture(dynamicCenter: dynamicCenter);
        Assert.True(fixture.States.ApplyDamage(fixture.Center, fixture.Center.MaxLife,
            HellVortexSelectedMonsterDeathAt).Killed);
        Assert.False(fixture.States.TryGetVisibleInstance(fixture.Center, HellVortexSelectedMonsterDeathAt, out _));
        var center = InvokeHellVortexSelectedMonster(fixture.Context, fixture.Caster, fixture.Center.Uid);
        Assert.NotNull(center);
        Assert.Equal(0U, center.Hitpoints);
        await AssertHellVortexSelectedMonsterCastAsync(fixture, receives: true, deadCenter: true);
    }

    [Fact]
    public async Task HandlerHellVortexRemovedFromViewMonsterRemainsDeadMapRoleAtRespawnTile()
    {
        using var fixture = CreateHellVortexSelectedMonsterFixture(selectedX: 853, recipientX: 847);
        Assert.True(fixture.States.TryMoveTo(fixture.Center, 840, 584, HellVortexSelectedMonsterDeathAt, out _));
        Assert.True(fixture.States.ApplyDamage(fixture.Center, fixture.Center.MaxLife,
            HellVortexSelectedMonsterDeathAt).Killed);
        var beforeRemoval = InvokeHellVortexSelectedMonster(fixture.Context, fixture.Caster, fixture.Center.Uid);
        Assert.NotNull(beforeRemoval);
        Assert.Equal((840, 584), ((int)beforeRemoval.X, (int)beforeRemoval.Y));
        Assert.Equal(0U, beforeRemoval.Hitpoints);

        // MonsterRole.RemoveView:4988 moves the retained map role; it does not LeaveMap.
        Assert.True(fixture.States.TryConsumeDeathRemovalInstance(fixture.Center,
            HellVortexSelectedMonsterDeathAt.AddSeconds(9), out _));
        var afterRemoval = InvokeHellVortexSelectedMonster(fixture.Context, fixture.Caster, fixture.Center.Uid);
        Assert.NotNull(afterRemoval);
        Assert.Equal((853, 584), ((int)afterRemoval.X, (int)afterRemoval.Y));
        Assert.Equal(0U, afterRemoval.Hitpoints);
        Assert.False(fixture.States.TryGetVisibleInstance(fixture.Center,
            HellVortexSelectedMonsterDeathAt.AddDays(1), out _));
        await AssertHellVortexSelectedMonsterCastAsync(fixture, receives: true, deadCenter: true);
    }

    [Fact]
    public void HellVortexSelectedMonsterRejectsWrongMapMissingUidAndRemovedDynamicRegistration()
    {
        using var fixture = CreateHellVortexSelectedMonsterFixture(dynamicCenter: true);
        var otherMapCaster = fixture.Caster with
        {
            Character = fixture.Caster.Character with { MapId = 1016 },
        };
        Assert.Null(InvokeHellVortexSelectedMonster(fixture.Context, otherMapCaster, fixture.Center.Uid));
        Assert.Null(InvokeHellVortexSelectedMonster(fixture.Context, fixture.Caster, uint.MaxValue));
        Assert.NotNull(InvokeHellVortexSelectedMonster(fixture.Context with { WorldData = null },
            fixture.Caster, fixture.Center.Uid));
        Assert.Equal(2, fixture.States.RemoveDynamicMonsters(1015, 72));
        Assert.Null(InvokeHellVortexSelectedMonster(fixture.Context, fixture.Caster, fixture.Center.Uid));
        Assert.Empty(fixture.Sent);
    }

    private static LegacyMonsterInstance? InvokeHellVortexSelectedMonster(
        WorldPacketHandlerContext context, WorldSession caster, uint uid)
    {
        var method = typeof(WorldPacketHandlers).GetMethod("FindLegacyHellVortexSelectedMonster",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var selected = method.Invoke(null, [context, caster, uid]);
        return selected is null ? null : Assert.IsType<LegacyMonsterInstance>(selected);
    }

    private static async Task AssertHellVortexSelectedMonsterCastAsync(
        HellVortexSelectedMonsterFixture fixture, bool receives, bool deadCenter = false)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(fixture.Caster.PlayerSpells, fixture.WorldData,
            HellVortexSpellId, out _, out var spell));
        var expectedDamage = fixture.Recipients.Select(monster => InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage", fixture.Caster, fixture.WorldData,
            monster, fixture.Family, spell)).ToArray();
        Assert.All(expectedDamage, damage => Assert.InRange(damage, 1U, 99_999U));
        await PacketHandlers.HandleAsync(fixture.Context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: fixture.Center.Uid, X: 900, Y: 901,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                SpellId: HellVortexSpellId)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(fixture.Sent));
        var animationIndex = SingleFrameIndex(fixture.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        Assert.Equal((ushort)2239, ReadFrame(fixture.Sent[animationIndex]).Header.Opcode);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((900, 901), (animation.X, animation.Y));
        Assert.Equal(HellVortexSpellId, animation.SpellId);
        Assert.Equal(3, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
        Assert.Equal(receives ? fixture.Recipients.Select(monster => monster.Uid) : Enumerable.Empty<uint>(),
            animation.Targets.Select(hit => hit.Uid));

        for (var index = 0; index < fixture.Recipients.Count; index++)
        {
            var monster = fixture.Recipients[index];
            Assert.True(fixture.States.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var current));
            var applied = receives ? expectedDamage[index] : 0U;
            Assert.Equal(monster.MaxLife - applied, current.Hitpoints);
            if (!receives)
            {
                continue;
            }

            var hit = animation.Targets[index];
            // HellVortex.cs:35-37 keeps identity soul and does not use the player-only 12/28.
            Assert.Equal(expectedDamage[index], hit.Damage);
            Assert.True(hit.Hit);
            var hpIndex = SingleFrameIndex(fixture.Sent, frame =>
                LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == monster.Uid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == current.Hitpoints));
            Assert.Equal((ushort)2176, ReadFrame(fixture.Sent[hpIndex]).Header.Opcode);
            Assert.True(hpIndex < animationIndex);
        }

        var hpUpdates = ParseMsgUpdates(fixture.Sent).Where(update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(receives ? fixture.Recipients.Select(monster => monster.Uid) : Enumerable.Empty<uint>(),
            hpUpdates.Select(update => update.Uid));
        foreach (var monster in fixture.Untouched)
        {
            Assert.True(fixture.States.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var current));
            Assert.Equal(monster, current);
        }
        var centerState = fixture.States.ApplyDamage(fixture.Center, 0, DateTimeOffset.UtcNow);
        Assert.Equal(deadCenter ? 0U : fixture.Center.Hitpoints, centerState.RemainingHitpoints);
        Assert.DoesNotContain(ParseMsgUpdates(fixture.Sent), update => update.Uid == fixture.Center.Uid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.True(fixture.Context.Sessions!.TryGet(fixture.ForeignPlayerConnection, out var foreignPlayer));
        Assert.Equal(fixture.ForeignPlayer, foreignPlayer);
        Assert.Empty(fixture.ForeignPlayerSent);
        Assert.Empty(ParseDeathAttacks(fixture.Sent));
    }

    private static HellVortexSelectedMonsterFixture CreateHellVortexSelectedMonsterFixture(
        bool dynamicCenter = false, int casterX = 830, int selectedX = 840, int recipientX = 841)
    {
        var family = CreateMonsterFamily() with
        {
            Attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes) { ["life"] = "1000000" },
        };
        MonsterSpawn Spawn(int id, int x, uint dynamicId) =>
            CreateSingleInstanceMonsterSpawn(generatorId: id, boundX: x) with
            {
                Attributes = new Dictionary<string, string>
                {
                    ["legacy_source"] = "LoadMapMonsters",
                    ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
                },
            };
        var selectedSpawn = Spawn(41, selectedX, 72);
        var firstSpawn = Spawn(42, recipientX, 71);
        var secondSpawn = Spawn(43, recipientX + 1, 71);
        var outsideSpawn = Spawn(44, selectedX - 7, 71);
        var spawns = new List<MonsterSpawn>();
        if (!dynamicCenter)
        {
            spawns.Add(selectedSpawn);
        }
        spawns.AddRange([firstSpawn, secondSpawn, outsideSpawn]);
        var worldData = CreateMagicCatalog(family, spawns,
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 7, useStamina: 11,
                damage: 200, coldTime: 0, coolDown: 0, damage3: 30_030));
        var states = new MonsterRuntimeStateStore();
        var recipients = new[]
        {
            CreateRuntimeMonsterInstance(worldData, firstSpawn, family),
            CreateRuntimeMonsterInstance(worldData, secondSpawn, family),
        };
        var outside = CreateRuntimeMonsterInstance(worldData, outsideSpawn, family);
        var center = dynamicCenter
            ? states.RegisterDynamicMonster(recipients[0] with
            {
                Uid = 900_099,
                X = checked((ushort)selectedX),
                DynamicId = 72,
                GeneratorId = 99,
            }, family)
            : CreateRuntimeMonsterInstance(worldData, selectedSpawn, family);
        var foreign = states.RegisterDynamicMonster(recipients[0] with
        {
            Uid = 900_100,
            X = checked((ushort)(recipientX + 2)),
            DynamicId = 72,
            GeneratorId = 100,
        }, family);
        foreach (var monster in recipients.Append(outside).Append(center).Append(foreign))
        {
            Assert.True(states.TryGetVisibleInstance(monster, HellVortexSelectedMonsterDeathAt, out _));
        }

        var caster = CreateSession(x: casterX, strength: 301, classCode: 5_005, stamina: 100,
            spellEntries: [CreatePlayerSpell(HellVortexSpellId) with { SoulLevel = 3, UseSpellSoul = 3 }],
            attributes: new Dictionary<string, string> { ["DynamicID"] = "71", ["PkMode"] = "0" });
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var foreignSent = new List<byte[]>();
        var foreignConnection = CreateSendingConnection(foreignSent);
        var foreignPlayer = CreateSession(uid: TargetPlayerUid, name: "ForeignMonsterCenterObserver",
            x: recipientX, minHitPoints: 100_000, spellIds: [],
            attributes: new Dictionary<string, string> { ["DynamicID"] = "72" });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, caster);
        sessions.Set(foreignConnection, foreignPlayer);
        var context = new WorldPacketHandlerContext(connection, new StringWriter(), caster,
            WorldData: worldData, Sessions: sessions, MonsterStates: states,
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        return new(worldData, family, states, caster, context, center, recipients, [outside, foreign], sent,
            foreignPlayer, foreignConnection, foreignSent);
    }

    private sealed record HellVortexSelectedMonsterFixture(
        WorldDataCatalog WorldData,
        Monster Family,
        MonsterRuntimeStateStore States,
        WorldSession Caster,
        WorldPacketHandlerContext Context,
        LegacyMonsterInstance Center,
        IReadOnlyList<LegacyMonsterInstance> Recipients,
        IReadOnlyList<LegacyMonsterInstance> Untouched,
        IReadOnlyList<byte[]> Sent,
        WorldSession ForeignPlayer,
        WolfConquer.Core.Networking.ConquerTcpConnection ForeignPlayerConnection,
        IReadOnlyList<byte[]> ForeignPlayerSent) : IDisposable
    {
        public void Dispose() => Context.Log.Dispose();
    }
}
