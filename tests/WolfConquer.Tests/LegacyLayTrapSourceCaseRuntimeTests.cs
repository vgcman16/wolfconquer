using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed class LegacyLayTrapSourceCaseRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const ushort DaggerStormSpellId = 11_600;
    private const ushort AuroraLotusSpellId = 12_370;
    private const ushort FlameLotusSpellId = 12_380;
    private const ushort InfernalEchoSpellId = 12_550;
    private const ushort WrathOfTheEmperorSpellId = 12_570;
    private const ushort SpaceLeapSpellId = 15_620;
    private const ushort WaterShockwaveSpellId = 16_310;
    private const ushort WaterShockwavePassiveSpellId = 16_320;
    private const ushort DustDetachmentSpellId = 16_460;
    private const ushort DustDetachmentPassiveSpellId = 16_470;
    private const ushort SickleWindSpellId = 16_500;
    private const ushort SickleWindPassiveSpellId = 16_510;
    private const ushort FlameOfDestructionSpellId = 16_540;
    private const ushort FlameOfDestructionPassiveSpellId = 16_550;
    private const ushort LightningShieldSpellId = 15_750;
    private const int LightningShieldActivatedStatusFlag = 0xED;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Theory]
    [InlineData(DustDetachmentSpellId, 30_175, 30_350, 100U, 200U)]
    [InlineData(SickleWindSpellId, 30_175, 30_350, 100U, 200U)]
    [InlineData(DustDetachmentPassiveSpellId, 30_280, 30_420, 100U, 150U)]
    [InlineData(SickleWindPassiveSpellId, 30_280, 30_420, 100U, 150U)]
    public void CalculateLayTrapRepeatPlayerTargetUsesDecompiledDamageOnHumanDivisor(
        ushort spellId,
        int baselineDamageOnHuman,
        int boostedDamageOnHuman,
        uint baselineFixedPercent,
        uint boostedFixedPercent)
    {
        const ushort fixedPercentSpellId = 31_005;
        var attacker = CreateSession(spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: PlayerUid + 2,
            name: "Target",
            x: 842,
            y: 590,
            spellIds: []);
        var baselineWorldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(spellId, level: 0, experience: 0, damageOnHuman: baselineDamageOnHuman)]);
        var boostedWorldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(spellId, level: 0, experience: 0, damageOnHuman: boostedDamageOnHuman)]);
        var fixedBaselineWorldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(fixedPercentSpellId, level: 0, experience: 0, damage: baselineFixedPercent)]);
        var fixedBoostedWorldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(fixedPercentSpellId, level: 0, experience: 0, damage: boostedFixedPercent)]);
        var (_, baselineSpell) = ResolveLayTrapSpell(attacker, baselineWorldData, spellId);
        var (_, boostedSpell) = ResolveLayTrapSpell(attacker, boostedWorldData, spellId);
        var (_, fixedBaselineSpell) = ResolveLayTrapSpell(attacker, fixedBaselineWorldData, fixedPercentSpellId);
        var (_, fixedBoostedSpell) = ResolveLayTrapSpell(attacker, fixedBoostedWorldData, fixedPercentSpellId);
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal((uint)(baselineDamageOnHuman % 1_000), baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal((uint)(boostedDamageOnHuman % 1_000), boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Fact]
    public async Task HandlerLayTrap12550CreatesFourSourceShapedFloorEffectsAndAnimationTargets()
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(InfernalEchoSpellId, level: 0, experience: 1),
                CreateLayTrapMagicRow(InfernalEchoSpellId, level: 1, experience: 100)]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    InfernalEchoSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);
        var before = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                InfernalEchoSpellId)).ToFrameBytes()));

        var after = DateTimeOffset.UtcNow;
        var selfInteraction = Assert.Single(ParseMsgAttacks(sent));
        Assert.Equal(PlayerUid, selfInteraction.Query.Uid);
        Assert.Equal(PlayerUid, selfInteraction.Query.OpponentUid);
        Assert.Equal((ushort)840, selfInteraction.Query.X);
        Assert.Equal((ushort)590, selfInteraction.Query.Y);
        Assert.Equal(MsgAttackType.None, selfInteraction.Query.AttackType);

        var floorPackets = ParseFloorItems(sent)
            .Where(packet => packet.ItemId == LegacyLayTrap.SourceCase12550FloorItemId)
            .ToArray();
        Assert.Equal(4, floorPackets.Length);
        Assert.All(floorPackets, floorItem =>
        {
            Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
            Assert.Equal((byte)14, floorItem.PlusOrColor);
            Assert.Equal(0U, floorItem.ItemOwnerUid);
            Assert.Equal(0U, floorItem.GuildId);
            Assert.Equal((ushort)0, floorItem.OwnerX);
            Assert.Equal((ushort)0, floorItem.OwnerY);
            Assert.Equal("", floorItem.Name);

            Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, after, out var runtimeEntry));
            Assert.NotNull(runtimeEntry.Effect);
            Assert.Equal(InfernalEchoSpellId, runtimeEntry.Effect!.SpellId);
            Assert.Equal(PlayerUid, runtimeEntry.Effect.RuntimeOwnerUid);
            Assert.Equal((byte)0, runtimeEntry.Effect.SpellLevel);
            Assert.Equal((byte)2, runtimeEntry.Effect.LevelHu);
            Assert.Equal(3.0, runtimeEntry.Effect.HitRadius);
            Assert.Equal(18, runtimeEntry.Effect.RemovalVisibilityRadius);
            Assert.InRange(
                runtimeEntry.Effect.DueAt,
                before.AddMilliseconds(LegacyLayTrap.SourceCase12550FloorDurationMilliseconds),
                after.AddMilliseconds(LegacyLayTrap.SourceCase12550FloorDurationMilliseconds));
        });

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal((ushort)840, animation.X);
        Assert.Equal((ushort)590, animation.Y);
        Assert.Equal(InfernalEchoSpellId, animation.SpellId);
        Assert.Equal((byte)0, animation.SpellLevel);
        Assert.Equal((byte)2, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
        Assert.Equal(floorPackets.Select(packet => packet.Uid).Order().ToArray(), animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.All(animation.Targets, target =>
        {
            Assert.Equal(1U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal((ushort)830, target.NewX);
            Assert.Equal((ushort)584, target.NewY);
        });

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy LayTrap magic", output);
        Assert.Contains("spellId=12550", output);
        Assert.Contains("floorEffectCount=4", output);
        Assert.Contains("source=LayTrap.cs cases 12550/12570", output);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 41)]
    [InlineData(2, 42)]
    public async Task HandlerDaggerStormLayTrapCreatesSourceFloorEffectAndCasterAnimation(
        byte useSpellSoul,
        uint expectedItemId)
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [
                CreateLayTrapMagicRow(DaggerStormSpellId, level: 0, experience: 900_000),
                CreateLayTrapMagicRow(DaggerStormSpellId, level: 1, experience: 3_500_000),
            ]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    DaggerStormSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: useSpellSoul),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
        context = combatSpellOwner.Context;
        var before = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                DaggerStormSpellId)).ToFrameBytes()));

        var after = DateTimeOffset.UtcNow;
        var floorItem = Assert.Single(ParseFloorItems(sent));
        Assert.Equal(expectedItemId, floorItem.ItemId);
        Assert.Equal((ushort)840, floorItem.X);
        Assert.Equal((ushort)590, floorItem.Y);
        Assert.Equal((ushort)0, floorItem.MaxLife);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Equal(0U, floorItem.Life);
        Assert.Equal((byte)11, floorItem.PlusOrColor);
        Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
        Assert.Equal(77U, floorItem.GuildId);
        Assert.Equal((byte)11, floorItem.FlowerType);
        Assert.Equal((ushort)0, floorItem.OwnerX);
        Assert.Equal((ushort)0, floorItem.OwnerY);
        Assert.Equal("trap", floorItem.Name);

        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.Equal(1_015, runtimeEntry.MapId);
        Assert.Equal(floorItem, runtimeEntry.Packet);
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(DaggerStormSpellId, runtimeEntry.Effect!.SpellId);
        Assert.Equal((ushort)840, runtimeEntry.Effect.OriginX);
        Assert.Equal((ushort)590, runtimeEntry.Effect.OriginY);
        Assert.Equal(0, runtimeEntry.Effect.Distance);
        Assert.Equal(0, runtimeEntry.Effect.HitRadius);
        Assert.InRange(
            runtimeEntry.Effect.DueAt,
            before.AddMilliseconds(4_000),
            after.AddMilliseconds(4_000));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal((ushort)840, animation.X);
        Assert.Equal((ushort)590, animation.Y);
        Assert.Equal(DaggerStormSpellId, animation.SpellId);
        Assert.Equal((byte)0, animation.SpellLevel);
        Assert.Equal(useSpellSoul, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(DaggerStormSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);
        Assert.Equal(10_000, updatedSpell.Experience);

        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            runtimeEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));
        var remove = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        var expectedRemoveItemId = expectedItemId is 41 or 42 ? 50U : expectedItemId;
        Assert.Equal(expectedRemoveItemId, remove.ItemId);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy DaggerStorm LayTrap magic", output);
        Assert.Contains("spellId=11600", output);
        Assert.Contains($"floorItemId={expectedItemId}", output);
        Assert.Contains("WorldServer handled legacy DaggerStorm LayTrap floor effect expiry", output);
        Assert.Contains("source=LayTrap.cs case 11600", output);
    }

    [Fact]
    public async Task HandlerAuroraLotusLayTrapCreatesSourceFloorEffectResetsWaterPowerAndExpires()
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [
                CreateLayTrapMagicRow(AuroraLotusSpellId, level: 0, experience: 900_000),
                CreateLayTrapMagicRow(AuroraLotusSpellId, level: 1, experience: 3_500_000),
            ]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    AuroraLotusSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
                ["TaoistPower"] = "9",
                ["TaoPower"] = "9",
                ["StatusFlags"] = BuildStatusFlags(LegacyLayTrap.FullPowerWaterStatusFlag),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
        context = combatSpellOwner.Context;
        var before = DateTimeOffset.UtcNow;
        var beforeTimer = DateTime.Now;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                AuroraLotusSpellId)).ToFrameBytes()));

        var afterTimer = DateTime.Now;
        var after = DateTimeOffset.UtcNow;
        var selfInteraction = Assert.Single(ParseMsgAttacks(sent));
        Assert.Equal(PlayerUid, selfInteraction.Query.Uid);
        Assert.Equal(PlayerUid, selfInteraction.Query.OpponentUid);
        Assert.Equal((ushort)840, selfInteraction.Query.X);
        Assert.Equal((ushort)590, selfInteraction.Query.Y);
        Assert.Equal(MsgAttackType.None, selfInteraction.Query.AttackType);
        Assert.Equal(0, selfInteraction.Query.Damage);
        Assert.Equal(0U, selfInteraction.Query.ResponseDamage);

        var floorItem = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.ItemId == LegacyLayTrap.AuroraLotusFloorItemId &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.Equal((ushort)840, floorItem.X);
        Assert.Equal((ushort)590, floorItem.Y);
        Assert.Equal(LegacyLayTrap.LotusFloorLife, floorItem.MaxLife);
        Assert.Equal((uint)LegacyLayTrap.LotusFloorLife, floorItem.Life);
        Assert.Equal(LegacyLayTrap.LotusFloorColor, floorItem.PlusOrColor);
        Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
        Assert.Equal(77U, floorItem.GuildId);
        Assert.Equal(LegacyLayTrap.LotusFlowerType, floorItem.FlowerType);
        Assert.InRange(
            floorItem.Timer,
            LegacyLayTrap.PackLegacyTqTimer(beforeTimer.AddSeconds(7)),
            LegacyLayTrap.PackLegacyTqTimer(afterTimer.AddSeconds(7)));
        Assert.Equal("AuroraLotus", floorItem.Name);

        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, after, out var runtimeEntry));
        Assert.Equal(1_015, runtimeEntry.MapId);
        Assert.Equal(floorItem, runtimeEntry.Packet);
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(AuroraLotusSpellId, runtimeEntry.Effect!.SpellId);
        Assert.Equal((ushort)840, runtimeEntry.Effect.OriginX);
        Assert.Equal((ushort)590, runtimeEntry.Effect.OriginY);
        Assert.Equal(0, runtimeEntry.Effect.Distance);
        Assert.Equal(0, runtimeEntry.Effect.HitRadius);
        Assert.InRange(
            runtimeEntry.Effect.DueAt,
            before.AddMilliseconds(LegacyLayTrap.LotusFloorLifetimeMilliseconds),
            after.AddMilliseconds(LegacyLayTrap.LotusFloorLifetimeMilliseconds));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal((ushort)840, animation.X);
        Assert.Equal((ushort)590, animation.Y);
        Assert.Equal(AuroraLotusSpellId, animation.SpellId);
        Assert.Equal((byte)0, animation.SpellLevel);
        Assert.Equal((byte)2, animation.LevelHu);
        Assert.Empty(animation.Targets);

        var updates = ParseMsgUpdates(sent);
        var appendIcon = Assert.Single(
            updates.SelectMany(packet => packet.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AppendIcon);
        Assert.Equal(LegacyLayTrap.AuroraLotusAppendIconFlag, appendIcon.Flag);
        Assert.Equal(LegacyLayTrap.LotusAppendIconTime, appendIcon.Time);
        Assert.Equal(0U, appendIcon.Damage);
        Assert.Equal(0U, appendIcon.SpellLevel);

        var statusVector = Assert.Single(
            updates.SelectMany(packet => packet.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.NotNull(statusVector.Values);
        Assert.False(HasStatusFlag(statusVector.Values!, LegacyLayTrap.FullPowerWaterStatusFlag));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.Equal("0", updatedSession.Character.Attributes["TaoistPower"]);
        Assert.Equal("0", updatedSession.Character.Attributes["TaoPower"]);
        Assert.False(HasStatusFlag(updatedSession.Character.Attributes, LegacyLayTrap.FullPowerWaterStatusFlag));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(AuroraLotusSpellId, updatedSpell.Id);
        Assert.Equal(10_000, updatedSpell.Experience);

        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            runtimeEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));
        var remove = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(LegacyLayTrap.AuroraLotusFloorItemId, remove.ItemId);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Lotus LayTrap magic", output);
        Assert.Contains("spellId=12370", output);
        Assert.Contains("floorItemId=930", output);
        Assert.Contains("WorldServer handled legacy Lotus LayTrap floor effect expiry", output);
        Assert.Contains("source=AddFloor Timer=TqTimer(DateTime.Now.AddSeconds(7))", output);
    }

    [Theory]
    [InlineData(WaterShockwaveSpellId)]
    [InlineData(WaterShockwavePassiveSpellId)]
    public async Task HandlerWaterShockwaveLayTrapCreatesSourceFloorEffectAndDueFinalLineRuntime(
        ushort spellId)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 590);
        var worldData = CreateWorldData(
            monsterFamily,
            [spawn],
            [CreateLayTrapMagicRow(spellId, level: 0, experience: 900_000)]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            FloorItems: floorItems);
        var before = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: 0,
                x: 840,
                y: 590,
                spellId)).ToFrameBytes()));

        var after = DateTimeOffset.UtcNow;
        var floorItem = Assert.Single(ParseFloorItems(sent));
        Assert.Equal(LegacyLayTrap.WaterShockwaveFloorItemId, floorItem.ItemId);
        Assert.Equal((ushort)830, floorItem.X);
        Assert.Equal((ushort)584, floorItem.Y);
        Assert.Equal((ushort)0, floorItem.MaxLife);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Equal(0U, floorItem.Life);
        Assert.Equal(LegacyLayTrap.WaterShockwaveFloorColor, floorItem.PlusOrColor);
        Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
        Assert.Equal(77U, floorItem.GuildId);
        Assert.Equal((byte)0, floorItem.FlowerType);
        Assert.Equal(0UL, floorItem.Timer);
        Assert.Equal((ushort)840, floorItem.OwnerX);
        Assert.Equal((ushort)590, floorItem.OwnerY);
        Assert.Equal(LegacyLayTrap.WaterShockwaveFloorName, floorItem.Name);

        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.Equal(1_015, runtimeEntry.MapId);
        Assert.Equal(floorItem, runtimeEntry.Packet);
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(spellId, runtimeEntry.Effect!.SpellId);
        Assert.Equal((ushort)840, runtimeEntry.Effect.OriginX);
        Assert.Equal((ushort)590, runtimeEntry.Effect.OriginY);
        Assert.Equal(LegacyLayTrap.WaterShockwaveLineRange, runtimeEntry.Effect.Distance);
        Assert.Equal(LegacyLayTrap.WaterShockwaveLineWidth, runtimeEntry.Effect.HitRadius);
        Assert.Equal(LegacyLayTrap.WaterShockwaveRemovalVisibilityRadius, runtimeEntry.Effect.RemovalVisibilityRadius);
        Assert.Equal(LegacyLayTrap.WaterShockwaveRepeatMilliseconds, runtimeEntry.Effect.RepeatMilliseconds);
        Assert.NotNull(runtimeEntry.Effect.NextRepeatAt);
        Assert.InRange(
            runtimeEntry.Effect.NextRepeatAt!.Value,
            before,
            after);
        Assert.Equal((byte)0, runtimeEntry.Effect.SpellLevel);
        Assert.Equal((byte)2, runtimeEntry.Effect.LevelHu);
        Assert.Equal(PlayerUid, runtimeEntry.Effect.RuntimeOwnerUid);
        Assert.InRange(
            runtimeEntry.Effect.DueAt,
            before.AddMilliseconds(LegacyLayTrap.WaterShockwaveInvokeMilliseconds),
            after.AddMilliseconds(LegacyLayTrap.WaterShockwaveInvokeMilliseconds));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((ushort)840, animation.X);
        Assert.Equal((ushort)590, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal((byte)0, animation.SpellLevel);
        Assert.Equal((byte)2, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
        Assert.Empty(animation.Targets);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(spellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Experience);

        var repeatAt = runtimeEntry.Effect.DueAt.AddMilliseconds(
            -LegacyLayTrap.WaterShockwaveInvokeMilliseconds + LegacyLayTrap.WaterShockwaveRepeatMilliseconds);
        var repeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(1, repeatProcessedCount);
        var repeatAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 1);
        Assert.Equal(0U, repeatAnimation.AttackedUid);
        Assert.Equal(floorItem.X, repeatAnimation.X);
        Assert.Equal(floorItem.Y, repeatAnimation.Y);
        Assert.Equal(runtimeEntry.Effect.SpellLevel, repeatAnimation.SpellLevel);
        Assert.Equal(runtimeEntry.Effect.LevelHu, repeatAnimation.LevelHu);
        var repeatTarget = Assert.Single(repeatAnimation.Targets);
        Assert.Equal(monster.Uid, repeatTarget.Uid);
        Assert.True(repeatTarget.Damage > 0);
        Assert.True(repeatTarget.Hit);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, repeatAt, out _));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var repeatDamagedMonster));
        Assert.True(repeatDamagedMonster.Hitpoints < monster.MaxLife);
        Assert.Equal((ushort)836, repeatDamagedMonster.X);
        Assert.Equal((ushort)590, repeatDamagedMonster.Y);

        var duplicateRepeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(0, duplicateRepeatProcessedCount);
        Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 1);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, repeatAt, out _));

        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            runtimeEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        var runtimeAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 2);
        Assert.Equal(0U, runtimeAnimation.AttackedUid);
        Assert.Equal(floorItem.OwnerX, runtimeAnimation.X);
        Assert.Equal(floorItem.OwnerY, runtimeAnimation.Y);
        Assert.Equal(runtimeEntry.Effect.SpellLevel, runtimeAnimation.SpellLevel);
        Assert.Equal(runtimeEntry.Effect.LevelHu, runtimeAnimation.LevelHu);
        var runtimeTarget = Assert.Single(runtimeAnimation.Targets);
        Assert.Equal(monster.Uid, runtimeTarget.Uid);
        Assert.True(runtimeTarget.Damage > 0);
        Assert.True(runtimeTarget.Hit);
        Assert.Equal((ushort)836, runtimeTarget.NewX);
        Assert.Equal((ushort)592, runtimeTarget.NewY);

        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < monster.MaxLife);
        Assert.Equal(runtimeTarget.NewX, damagedMonster.X);
        Assert.Equal(runtimeTarget.NewY, damagedMonster.Y);
        var runtimeAnimationFrameIndex = sent.FindIndex(
            frame => LegacySpellAnimationPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 2);
        Assert.True(runtimeAnimationFrameIndex >= 0);
        var removeFrameIndex = sent.FindIndex(
            frame => LegacyFloorItemPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.True(removeFrameIndex > runtimeAnimationFrameIndex);
        var remove = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(floorItem.ItemId, remove.ItemId);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy WaterShockwave LayTrap magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("source=LayTrap.cs cases 16310/16320", output);
        Assert.Contains("WorldServer handled legacy WaterShockwave LayTrap repeat pulse runtime", output);
        Assert.Contains("WorldServer handled legacy WaterShockwave LayTrap final floor spell runtime", output);
        Assert.Contains("targetCount=1", output);
        Assert.DoesNotContain("watershockwave-delayed-repeat-pulse-runtime", output);
        Assert.DoesNotContain("PARITY-GAP repeat pulses remain", output);
    }

    [Theory]
    [InlineData(DustDetachmentSpellId)]
    [InlineData(DustDetachmentPassiveSpellId)]
    public async Task HandlerDustDetachmentLayTrapCreatesSourceFloorEffectCastSobRepeatPulseAndRemoveOnlyFinal(
        ushort spellId)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 842, boundY: 590);
        var sobNpc = CreateDustDetachmentSobNpc();
        var worldData = CreateWorldData(
            monsterFamily,
            [spawn],
            [CreateLayTrapMagicRow(spellId, level: 0, experience: 900_000)],
            [sobNpc]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var peerConnection = CreateSendingConnection(peerSent);
        var observerConnection = CreateSendingConnection(observerSent);
        var session = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        var peerSession = CreateSession(
            uid: PlayerUid + 2,
            name: "DustTarget",
            x: 842,
            y: 590,
            spellIds: Array.Empty<ushort>());
        var observerSession = CreateSession(
            uid: PlayerUid + 1,
            name: "TrapObserver",
            x: 858,
            y: 590,
            spellIds: Array.Empty<ushort>());
        sessions.Set(connection, session);
        sessions.Set(peerConnection, peerSession);
        sessions.Set(observerConnection, observerSession);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates,
            FloorItems: floorItems);
        var before = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: 0,
                x: 840,
                y: 590,
                spellId)).ToFrameBytes()));

        var after = DateTimeOffset.UtcNow;
        var floorItem = Assert.Single(ParseFloorItems(sent));
        Assert.Equal(LegacyLayTrap.DustDetachmentFloorItemId, floorItem.ItemId);
        Assert.Equal((ushort)840, floorItem.X);
        Assert.Equal((ushort)590, floorItem.Y);
        Assert.Equal((ushort)0, floorItem.MaxLife);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Equal(0U, floorItem.Life);
        Assert.Equal(LegacyLayTrap.DustDetachmentFloorColor, floorItem.PlusOrColor);
        Assert.Equal(0U, floorItem.ItemOwnerUid);
        Assert.Equal(77U, floorItem.GuildId);
        Assert.Equal((byte)0, floorItem.FlowerType);
        Assert.Equal(0UL, floorItem.Timer);
        Assert.Equal((ushort)830, floorItem.OwnerX);
        Assert.Equal((ushort)584, floorItem.OwnerY);
        Assert.Equal("", floorItem.Name);

        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.Equal(1_015, runtimeEntry.MapId);
        Assert.Equal(floorItem, runtimeEntry.Packet);
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(spellId, runtimeEntry.Effect!.SpellId);
        Assert.Equal((ushort)830, runtimeEntry.Effect.OriginX);
        Assert.Equal((ushort)584, runtimeEntry.Effect.OriginY);
        Assert.Equal(4, runtimeEntry.Effect.Distance);
        Assert.Equal(4.0, runtimeEntry.Effect.HitRadius);
        Assert.Equal(LegacyLayTrap.DustDetachmentRemovalVisibilityRadius, runtimeEntry.Effect.RemovalVisibilityRadius);
        Assert.Equal(LegacyLayTrap.DustDetachmentRepeatMilliseconds, runtimeEntry.Effect.RepeatMilliseconds);
        Assert.NotNull(runtimeEntry.Effect.NextRepeatAt);
        Assert.InRange(runtimeEntry.Effect.NextRepeatAt!.Value, before, after);
        Assert.Equal((byte)0, runtimeEntry.Effect.SpellLevel);
        Assert.Equal((byte)2, runtimeEntry.Effect.LevelHu);
        Assert.Equal(PlayerUid, runtimeEntry.Effect.RuntimeOwnerUid);
        Assert.InRange(
            runtimeEntry.Effect.DueAt,
            before.AddMilliseconds(LegacyLayTrap.DustDetachmentInvokeMilliseconds),
            after.AddMilliseconds(LegacyLayTrap.DustDetachmentInvokeMilliseconds));

        var castAnimation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, castAnimation.AttackerUid);
        Assert.Equal(0U, castAnimation.AttackedUid);
        Assert.Equal((ushort)840, castAnimation.X);
        Assert.Equal((ushort)590, castAnimation.Y);
        Assert.Equal(spellId, castAnimation.SpellId);
        Assert.Equal((byte)0, castAnimation.SpellLevel);
        Assert.Equal((byte)2, castAnimation.LevelHu);
        Assert.Equal(0, castAnimation.BombFlag);
        var sobCastTarget = Assert.Single(castAnimation.Targets);
        Assert.Equal((uint)sobNpc.Id, sobCastTarget.Uid);
        Assert.True(sobCastTarget.Damage > 0);
        Assert.True(sobCastTarget.Hit);
        var visibleSob = sobNpcStates.GetVisible(sobNpc);
        Assert.True(visibleSob.Life < sobNpc.Life);
        Assert.Equal(sobCastTarget.Damage, checked((uint)(sobNpc.Life - visibleSob.Life)));

        var repeatAt = runtimeEntry.Effect.DueAt.AddMilliseconds(
            -LegacyLayTrap.DustDetachmentInvokeMilliseconds + LegacyLayTrap.DustDetachmentRepeatMilliseconds);
        var repeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(1, repeatProcessedCount);
        var repeatAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 1);
        Assert.Equal(0U, repeatAnimation.AttackedUid);
        Assert.Equal(floorItem.X, repeatAnimation.X);
        Assert.Equal(floorItem.Y, repeatAnimation.Y);
        Assert.Equal(runtimeEntry.Effect.SpellLevel, repeatAnimation.SpellLevel);
        Assert.Equal(runtimeEntry.Effect.LevelHu, repeatAnimation.LevelHu);
        Assert.Contains(repeatAnimation.Targets, target => target.Uid == monster.Uid && target.Damage > 0 && target.Hit);
        Assert.Contains(repeatAnimation.Targets, target => target.Uid == PlayerUid + 2 && target.Damage > 0 && target.Hit);
        Assert.DoesNotContain(repeatAnimation.Targets, target => target.Uid == (uint)sobNpc.Id);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, repeatAt, out _));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var repeatDamagedMonster));
        Assert.True(repeatDamagedMonster.Hitpoints < monster.MaxLife);
        Assert.True(sessions.TryGet(peerConnection, out var updatedPeerSession));
        Assert.NotNull(updatedPeerSession);
        Assert.True(updatedPeerSession.Character.MinHitPoints < peerSession.Character.MinHitPoints);

        var duplicateRepeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(0, duplicateRepeatProcessedCount);
        Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 1);

        var spellAnimationCountBeforeFinal = ParseSpellAnimations(sent).Count;
        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            runtimeEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.Equal(spellAnimationCountBeforeFinal, ParseSpellAnimations(sent).Count);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));
        var remove = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(floorItem.ItemId, remove.ItemId);
        Assert.Equal(0U, remove.ItemOwnerUid);
        Assert.DoesNotContain(
            ParseFloorItems(observerSent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy DustDetachment LayTrap magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("source=LayTrap.cs cases 16460/16470", output);
        Assert.Contains("WorldServer handled legacy DustDetachment LayTrap repeat pulse runtime", output);
        Assert.Contains("WorldServer handled legacy DustDetachment LayTrap final floor spell removal", output);
        Assert.Contains("DustSigilStunn gear stun side effect runs after player damage", output);
        Assert.Contains("removeViewerCount=2", output);
    }

    [Theory]
    [InlineData(SickleWindSpellId)]
    [InlineData(SickleWindPassiveSpellId)]
    public async Task HandlerSickleWindLayTrapCreatesSourceFloorEffectSelfOnlyCastSobRepeatPulseAndRemoveOnlyFinal(
        ushort spellId)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 842, boundY: 590);
        var sobNpc = CreateDustDetachmentSobNpc();
        var worldData = CreateWorldData(
            monsterFamily,
            [spawn],
            [CreateLayTrapMagicRow(spellId, level: 0, experience: 900_000)],
            [sobNpc]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var peerConnection = CreateSendingConnection(peerSent);
        var observerConnection = CreateSendingConnection(observerSent);
        var session = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        var peerSession = CreateSession(
            uid: PlayerUid + 2,
            name: "SickleTarget",
            x: 842,
            y: 590,
            spellIds: Array.Empty<ushort>());
        var observerSession = CreateSession(
            uid: PlayerUid + 1,
            name: "TrapObserver",
            x: 858,
            y: 590,
            spellIds: Array.Empty<ushort>());
        sessions.Set(connection, session);
        sessions.Set(peerConnection, peerSession);
        sessions.Set(observerConnection, observerSession);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates,
            FloorItems: floorItems);
        var before = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: 0,
                x: 840,
                y: 590,
                spellId)).ToFrameBytes()));

        var after = DateTimeOffset.UtcNow;
        var floorItem = Assert.Single(ParseFloorItems(sent));
        Assert.Equal(LegacyLayTrap.SickleWindFloorItemId, floorItem.ItemId);
        Assert.Equal((ushort)840, floorItem.X);
        Assert.Equal((ushort)590, floorItem.Y);
        Assert.Equal((ushort)0, floorItem.MaxLife);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Equal(0U, floorItem.Life);
        Assert.Equal(LegacyLayTrap.SickleWindFloorColor, floorItem.PlusOrColor);
        Assert.Equal(0U, floorItem.ItemOwnerUid);
        Assert.Equal(77U, floorItem.GuildId);
        Assert.Equal((byte)0, floorItem.FlowerType);
        Assert.Equal(0UL, floorItem.Timer);
        Assert.Equal((ushort)830, floorItem.OwnerX);
        Assert.Equal((ushort)584, floorItem.OwnerY);
        Assert.Equal("", floorItem.Name);

        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.Equal(1_015, runtimeEntry.MapId);
        Assert.Equal(floorItem, runtimeEntry.Packet);
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(spellId, runtimeEntry.Effect!.SpellId);
        Assert.Equal((ushort)830, runtimeEntry.Effect.OriginX);
        Assert.Equal((ushort)584, runtimeEntry.Effect.OriginY);
        Assert.Equal(LegacyLayTrap.SickleWindRepeatRadius, runtimeEntry.Effect.Distance);
        Assert.Equal((double)LegacyLayTrap.SickleWindRepeatRadius, runtimeEntry.Effect.HitRadius);
        Assert.Equal(LegacyLayTrap.SickleWindRemovalVisibilityRadius, runtimeEntry.Effect.RemovalVisibilityRadius);
        Assert.Equal(LegacyLayTrap.SickleWindRepeatMilliseconds, runtimeEntry.Effect.RepeatMilliseconds);
        Assert.NotNull(runtimeEntry.Effect.NextRepeatAt);
        Assert.InRange(runtimeEntry.Effect.NextRepeatAt!.Value, before, after);
        Assert.Equal((byte)0, runtimeEntry.Effect.SpellLevel);
        Assert.Equal((byte)2, runtimeEntry.Effect.LevelHu);
        Assert.Equal(PlayerUid, runtimeEntry.Effect.RuntimeOwnerUid);
        Assert.InRange(
            runtimeEntry.Effect.DueAt,
            before.AddMilliseconds(LegacyLayTrap.SickleWindInvokeMilliseconds),
            after.AddMilliseconds(LegacyLayTrap.SickleWindInvokeMilliseconds));

        var castAnimation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, castAnimation.AttackerUid);
        Assert.Equal(0U, castAnimation.AttackedUid);
        Assert.Equal((ushort)840, castAnimation.X);
        Assert.Equal((ushort)590, castAnimation.Y);
        Assert.Equal(spellId, castAnimation.SpellId);
        Assert.Equal((byte)0, castAnimation.SpellLevel);
        Assert.Equal((byte)2, castAnimation.LevelHu);
        Assert.Equal(0, castAnimation.BombFlag);
        var sobCastTarget = Assert.Single(castAnimation.Targets);
        Assert.Equal((uint)sobNpc.Id, sobCastTarget.Uid);
        Assert.True(sobCastTarget.Damage > 0);
        Assert.True(sobCastTarget.Hit);
        var visibleSob = sobNpcStates.GetVisible(sobNpc);
        Assert.True(visibleSob.Life < sobNpc.Life);
        Assert.Equal(sobCastTarget.Damage, checked((uint)(sobNpc.Life - visibleSob.Life)));
        Assert.DoesNotContain(
            ParseSpellAnimations(peerSent),
            packet => packet.AttackerUid == PlayerUid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 0);
        Assert.DoesNotContain(
            ParseSpellAnimations(observerSent),
            packet => packet.AttackerUid == PlayerUid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 0);

        var repeatAt = runtimeEntry.Effect.DueAt.AddMilliseconds(
            -LegacyLayTrap.SickleWindInvokeMilliseconds + LegacyLayTrap.SickleWindRepeatMilliseconds);
        var repeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(1, repeatProcessedCount);
        var repeatAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 1);
        Assert.Equal(0U, repeatAnimation.AttackedUid);
        Assert.Equal(floorItem.X, repeatAnimation.X);
        Assert.Equal(floorItem.Y, repeatAnimation.Y);
        Assert.Equal(runtimeEntry.Effect.SpellLevel, repeatAnimation.SpellLevel);
        Assert.Equal(runtimeEntry.Effect.LevelHu, repeatAnimation.LevelHu);
        Assert.Contains(repeatAnimation.Targets, target => target.Uid == monster.Uid && target.Damage > 0 && target.Hit);
        Assert.Contains(repeatAnimation.Targets, target => target.Uid == PlayerUid + 2 && target.Damage > 0 && target.Hit);
        Assert.DoesNotContain(repeatAnimation.Targets, target => target.Uid == (uint)sobNpc.Id);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, repeatAt, out _));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var repeatDamagedMonster));
        Assert.True(repeatDamagedMonster.Hitpoints < monster.MaxLife);
        Assert.True(sessions.TryGet(peerConnection, out var updatedPeerSession));
        Assert.NotNull(updatedPeerSession);
        Assert.True(updatedPeerSession.Character.MinHitPoints < peerSession.Character.MinHitPoints);

        var duplicateRepeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(0, duplicateRepeatProcessedCount);
        Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 1);

        var spellAnimationCountBeforeFinal = ParseSpellAnimations(sent).Count;
        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            runtimeEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.Equal(spellAnimationCountBeforeFinal, ParseSpellAnimations(sent).Count);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));
        var remove = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(floorItem.ItemId, remove.ItemId);
        Assert.Equal(0U, remove.ItemOwnerUid);
        Assert.DoesNotContain(
            ParseFloorItems(observerSent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy SickleWind LayTrap magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("source=LayTrap.cs cases 16500/16510", output);
        Assert.Contains("WorldServer handled legacy SickleWind LayTrap repeat pulse runtime", output);
        Assert.Contains("hard-coded radius <= 5", output);
        Assert.Contains("WorldServer handled legacy SickleWind LayTrap final floor spell removal", output);
        Assert.Contains("removeViewerCount=2", output);
    }

    [Theory]
    [InlineData(FlameOfDestructionSpellId)]
    [InlineData(FlameOfDestructionPassiveSpellId)]
    public async Task HandlerFlameOfDestructionLayTrapCreatesSourceFloorEffectRepeatPulseAndRemoveOnlyFinal(
        ushort spellId)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 842, boundY: 590);
        var worldData = CreateWorldData(
            monsterFamily,
            [spawn],
            [CreateLayTrapMagicRow(spellId, level: 0, experience: 900_000)]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var observerConnection = CreateSendingConnection(observerSent);
        var session = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        var observerSession = CreateSession(
            uid: PlayerUid + 1,
            name: "TrapObserver",
            x: 858,
            y: 590,
            spellIds: Array.Empty<ushort>());
        sessions.Set(connection, session);
        sessions.Set(observerConnection, observerSession);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            FloorItems: floorItems);
        var before = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: 0,
                x: 840,
                y: 590,
                spellId)).ToFrameBytes()));

        var after = DateTimeOffset.UtcNow;
        var floorItem = Assert.Single(ParseFloorItems(sent));
        Assert.Equal(LegacyLayTrap.FlameOfDestructionFloorItemId, floorItem.ItemId);
        Assert.Equal((ushort)840, floorItem.X);
        Assert.Equal((ushort)590, floorItem.Y);
        Assert.Equal((ushort)0, floorItem.MaxLife);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Equal(0U, floorItem.Life);
        Assert.Equal(LegacyLayTrap.FlameOfDestructionFloorColor, floorItem.PlusOrColor);
        Assert.Equal(0U, floorItem.ItemOwnerUid);
        Assert.Equal(77U, floorItem.GuildId);
        Assert.Equal((byte)0, floorItem.FlowerType);
        Assert.Equal(0UL, floorItem.Timer);
        Assert.Equal((ushort)830, floorItem.OwnerX);
        Assert.Equal((ushort)584, floorItem.OwnerY);
        Assert.Equal("", floorItem.Name);

        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.Equal(1_015, runtimeEntry.MapId);
        Assert.Equal(floorItem, runtimeEntry.Packet);
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(spellId, runtimeEntry.Effect!.SpellId);
        Assert.Equal((ushort)830, runtimeEntry.Effect.OriginX);
        Assert.Equal((ushort)584, runtimeEntry.Effect.OriginY);
        Assert.Equal(4, runtimeEntry.Effect.Distance);
        Assert.Equal(4.0, runtimeEntry.Effect.HitRadius);
        Assert.Equal(LegacyLayTrap.FlameOfDestructionRemovalVisibilityRadius, runtimeEntry.Effect.RemovalVisibilityRadius);
        Assert.Equal(LegacyLayTrap.FlameOfDestructionRepeatMilliseconds, runtimeEntry.Effect.RepeatMilliseconds);
        Assert.NotNull(runtimeEntry.Effect.NextRepeatAt);
        Assert.InRange(runtimeEntry.Effect.NextRepeatAt!.Value, before, after);
        Assert.Equal((byte)0, runtimeEntry.Effect.SpellLevel);
        Assert.Equal((byte)2, runtimeEntry.Effect.LevelHu);
        Assert.Equal(PlayerUid, runtimeEntry.Effect.RuntimeOwnerUid);
        Assert.InRange(
            runtimeEntry.Effect.DueAt,
            before.AddMilliseconds(LegacyLayTrap.FlameOfDestructionInvokeMilliseconds),
            after.AddMilliseconds(LegacyLayTrap.FlameOfDestructionInvokeMilliseconds));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((ushort)840, animation.X);
        Assert.Equal((ushort)590, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal((byte)0, animation.SpellLevel);
        Assert.Equal((byte)2, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
        Assert.Empty(animation.Targets);

        var repeatAt = runtimeEntry.Effect.DueAt.AddMilliseconds(
            -LegacyLayTrap.FlameOfDestructionInvokeMilliseconds + LegacyLayTrap.FlameOfDestructionRepeatMilliseconds);
        var repeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(1, repeatProcessedCount);
        var repeatAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 1);
        Assert.Equal(0U, repeatAnimation.AttackedUid);
        Assert.Equal(floorItem.X, repeatAnimation.X);
        Assert.Equal(floorItem.Y, repeatAnimation.Y);
        Assert.Equal(runtimeEntry.Effect.SpellLevel, repeatAnimation.SpellLevel);
        Assert.Equal(runtimeEntry.Effect.LevelHu, repeatAnimation.LevelHu);
        var repeatTarget = Assert.Single(repeatAnimation.Targets);
        Assert.Equal(monster.Uid, repeatTarget.Uid);
        Assert.True(repeatTarget.Damage > 0);
        Assert.True(repeatTarget.Hit);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, repeatAt, out _));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var repeatDamagedMonster));
        Assert.True(repeatDamagedMonster.Hitpoints < monster.MaxLife);

        var duplicateRepeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(0, duplicateRepeatProcessedCount);
        Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 1);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, repeatAt, out _));

        var spellAnimationCountBeforeFinal = ParseSpellAnimations(sent).Count;
        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            runtimeEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.Equal(spellAnimationCountBeforeFinal, ParseSpellAnimations(sent).Count);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));
        var remove = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(floorItem.ItemId, remove.ItemId);
        Assert.Equal(0U, remove.ItemOwnerUid);
        Assert.DoesNotContain(
            ParseFloorItems(observerSent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy FlameofDestruction LayTrap magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("source=LayTrap.cs cases 16540/16550", output);
        Assert.Contains("WorldServer handled legacy FlameofDestruction LayTrap repeat pulse runtime", output);
        Assert.Contains("WorldServer handled legacy FlameofDestruction LayTrap final floor spell removal", output);
        Assert.Contains("removeViewerCount=1", output);
        Assert.Contains("CheckInvocke sends RemoveEffect only", output);
    }

    [Theory]
    [InlineData(FlameOfDestructionSpellId)]
    [InlineData(FlameOfDestructionPassiveSpellId)]
    public async Task HandlerFlameOfDestructionRepeatPlayerPulseAppliesLethalGuardBeforeDamageOnHumanClamp(
        ushort spellId)
    {
        const long initialHitPoints = 10_000;
        const uint damageOnHuman = 5_000;
        var result = await ExecuteFlameOfDestructionPlayerRepeatPulseAsync(
            spellId,
            attackerStrength: 20_000,
            targetHitPoints: initialHitPoints);

        Assert.True(
            result.PreGuardPhysicalDamage >= initialHitPoints - 1,
            $"Fixture must enter Physical.OnPlayer's 16540/16550 lethal-hit guard. " +
            $"PreGuardDamage={result.PreGuardPhysicalDamage}, targetHp={initialHitPoints}.");
        Assert.Equal(initialHitPoints - damageOnHuman, result.UpdatedTargetSession.Character.MinHitPoints);
        Assert.Equal(damageOnHuman, result.HitPointDelta);
        Assert.Equal(result.HitPointDelta, result.AnimationTarget.Damage);
        Assert.True(result.AnimationTarget.Hit);
    }

    [Theory]
    [InlineData(FlameOfDestructionSpellId)]
    [InlineData(FlameOfDestructionPassiveSpellId)]
    public async Task HandlerFlameOfDestructionRepeatPlayerPulseEnqueuesZeroDamageTargetAfterReceiveShield(
        ushort spellId)
    {
        const long initialHitPoints = 50_000;
        const uint initialShieldLeft = 50_000;
        var result = await ExecuteFlameOfDestructionPlayerRepeatPulseAsync(
            spellId,
            attackerStrength: 300,
            targetHitPoints: initialHitPoints,
            targetSpellIds: [LightningShieldSpellId],
            targetAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LightningShieldActivatedStatusFlag),
                ["LightningShieldLeft"] = initialShieldLeft.ToString(CultureInfo.InvariantCulture),
            });

        Assert.True(result.PreGuardPhysicalDamage > 0);
        Assert.Equal(initialHitPoints, result.UpdatedTargetSession.Character.MinHitPoints);
        Assert.Equal(0U, result.HitPointDelta);
        Assert.Equal(0U, result.AnimationTarget.Damage);
        Assert.True(result.AnimationTarget.Hit);
        Assert.True(
            uint.TryParse(
                result.UpdatedTargetSession.Character.Attributes["LightningShieldLeft"],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var shieldLeft));
        Assert.True(shieldLeft < initialShieldLeft);
    }

    private static async Task<FlamePlayerRepeatPulseResult> ExecuteFlameOfDestructionPlayerRepeatPulseAsync(
        ushort spellId,
        long attackerStrength,
        long targetHitPoints,
        IReadOnlyList<ushort>? targetSpellIds = null,
        IReadOnlyDictionary<string, string>? targetAttributes = null)
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(spellId, level: 0, experience: 900_000, damageOnHuman: 5_000)]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var targetConnection = CreateSendingConnection(targetSent);
        var session = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
                ["LegacyPhysicalLuckyStrikeRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "99",
                ["LegacyPhysicalDashRoll"] = "99",
            },
            strength: attackerStrength);
        var targetSession = CreateSession(
            uid: PlayerUid + 2,
            name: "FlameTarget",
            x: 842,
            y: 590,
            spellIds: targetSpellIds ?? [],
            attributes: targetAttributes,
            minHitPoints: targetHitPoints);
        sessions.Set(connection, session);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);
        var (_, spell) = ResolveLayTrapSpell(session, worldData, spellId);
        var preGuardPhysicalDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            session,
            targetSession,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: 0,
                x: 840,
                y: 590,
                spellId)).ToFrameBytes()));

        var floorItem = Assert.Single(ParseFloorItems(sent));
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.NotNull(runtimeEntry.Effect);
        var repeatAt = runtimeEntry.Effect!.DueAt.AddMilliseconds(
            -LegacyLayTrap.FlameOfDestructionInvokeMilliseconds + LegacyLayTrap.FlameOfDestructionRepeatMilliseconds);

        var repeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(1, repeatProcessedCount);
        var repeatAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == spellId &&
                packet.BombFlag == 1);
        var animationTarget = Assert.Single(
            repeatAnimation.Targets,
            target => target.Uid == PlayerUid + 2);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        var hitPointDelta = checked((uint)(targetHitPoints - updatedTargetSession.Character.MinHitPoints));

        return new FlamePlayerRepeatPulseResult(
            preGuardPhysicalDamage,
            hitPointDelta,
            updatedTargetSession,
            animationTarget);
    }

    [Fact]
    public async Task HandlerFlameLotusLayTrapCreatesSourceFloorEffectOnNoRevMapAndResetsFirePower()
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [
                CreateLayTrapMagicRow(FlameLotusSpellId, level: 0, experience: 900_000),
                CreateLayTrapMagicRow(FlameLotusSpellId, level: 1, experience: 3_500_000),
            ]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 10_397,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    FlameLotusSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 1),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "88",
                ["TaoPower"] = "9",
                ["StatusFlags"] = BuildStatusFlags(LegacyLayTrap.FullPowerFireStatusFlag),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                FlameLotusSpellId)).ToFrameBytes()));

        Assert.Single(ParseMsgAttacks(sent));
        var floorItem = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.ItemId == LegacyLayTrap.FlameLotusFloorItemId &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.Equal((ushort)840, floorItem.X);
        Assert.Equal((ushort)590, floorItem.Y);
        Assert.Equal(LegacyLayTrap.LotusFloorLife, floorItem.MaxLife);
        Assert.Equal((uint)LegacyLayTrap.LotusFloorLife, floorItem.Life);
        Assert.Equal(LegacyLayTrap.LotusFloorColor, floorItem.PlusOrColor);
        Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
        Assert.Equal(88U, floorItem.GuildId);
        Assert.Equal(LegacyLayTrap.LotusFlowerType, floorItem.FlowerType);
        Assert.NotEqual(0UL, floorItem.Timer);
        Assert.Equal("FlameLotus", floorItem.Name);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(FlameLotusSpellId, runtimeEntry.Effect!.SpellId);

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(FlameLotusSpellId, animation.SpellId);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Empty(animation.Targets);

        var updates = ParseMsgUpdates(sent);
        var appendIcon = Assert.Single(
            updates.SelectMany(packet => packet.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AppendIcon);
        Assert.Equal(LegacyLayTrap.FlameLotusAppendIconFlag, appendIcon.Flag);
        Assert.Equal(LegacyLayTrap.LotusAppendIconTime, appendIcon.Time);
        Assert.Equal(0U, appendIcon.Damage);

        var statusVector = Assert.Single(
            updates.SelectMany(packet => packet.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.NotNull(statusVector.Values);
        Assert.False(HasStatusFlag(statusVector.Values!, LegacyLayTrap.FullPowerFireStatusFlag));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.Equal("0", updatedSession.Character.Attributes["TaoistPower"]);
        Assert.Equal("0", updatedSession.Character.Attributes["TaoPower"]);
        Assert.False(HasStatusFlag(updatedSession.Character.Attributes, LegacyLayTrap.FullPowerFireStatusFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Lotus LayTrap magic", output);
        Assert.Contains("spellId=12380", output);
        Assert.Contains("floorItemId=940", output);
        Assert.DoesNotContain("unsupported LayTrap", output);
    }

    [Fact]
    public async Task HandlerAuroraLotusLayTrapNoRevGateReturnsAfterPreparationWithoutUnsupportedSpam()
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(AuroraLotusSpellId, level: 0, experience: 900_000)]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 10_397,
            spellIds: [AuroraLotusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["TaoistPower"] = "9",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                AuroraLotusSpellId)).ToFrameBytes()));

        Assert.Empty(ParseMsgAttacks(sent));
        Assert.Empty(ParseFloorItems(sent));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent).SelectMany(packet => packet.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AppendIcon);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);

        var output = log.ToString();
        Assert.Contains("WorldServer returned legacy Lotus LayTrap magic after preparation", output);
        Assert.Contains("spellId=12370", output);
        Assert.Contains("noRevBlocked=True", output);
        Assert.DoesNotContain("unsupported LayTrap", output);
        Assert.DoesNotContain("PARITY-GAP", output);
    }

    [Fact]
    public async Task HandlerLotusLayTrapLowTaoistPowerGateReturnsAfterPreparationWithoutUnsupportedSpam()
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(FlameLotusSpellId, level: 0, experience: 900_000)]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [FlameLotusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["TaoPower"] = "8",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                FlameLotusSpellId)).ToFrameBytes()));

        Assert.Empty(ParseMsgAttacks(sent));
        Assert.Empty(ParseFloorItems(sent));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent).SelectMany(packet => packet.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AppendIcon);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);

        var output = log.ToString();
        Assert.Contains("WorldServer returned legacy Lotus LayTrap magic after preparation", output);
        Assert.Contains("spellId=12380", output);
        Assert.Contains("taoistPower=8", output);
        Assert.DoesNotContain("unsupported LayTrap", output);
        Assert.DoesNotContain("PARITY-GAP", output);
    }

    [Fact]
    public async Task HandlerAuroraLotusLayTrapWithoutFloorItemsLogsExplicitRuntimeStateGap()
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(AuroraLotusSpellId, level: 0, experience: 900_000)]);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [AuroraLotusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["TaoistPower"] = "9",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                AuroraLotusSpellId)).ToFrameBytes()));

        var floorItem = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.ItemId == LegacyLayTrap.AuroraLotusFloorItemId &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.Equal("AuroraLotus", floorItem.Name);
        Assert.Single(ParseSpellAnimations(sent));

        var output = log.ToString();
        Assert.Contains("PARITY-GAP", output);
        Assert.Contains("auroralotus-floor-effect-runtime", output);
        Assert.Contains("AuroraLotus floor effect was sent, but floor item runtime state is not loaded", output);
        Assert.Contains("spellId=12370", output);
        Assert.DoesNotContain("unsupported LayTrap", output);
    }

    [Fact]
    public async Task HandlerSpaceLeapLayTrapCreatesSourceFloorEffectRuntimeAnimationExperienceAndTeamAction()
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [
                CreateLayTrapMagicRow(SpaceLeapSpellId, level: 0, experience: 900_000),
                CreateLayTrapMagicRow(SpaceLeapSpellId, level: 1, experience: 3_500_000),
            ]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var sent = new List<byte[]>();
        var teammateSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var teammateConnection = CreateSendingConnection(teammateSent);
        var session = CreateSession(
            classCode: 13_002,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    SpaceLeapSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        var teammateSession = CreateSession(
            uid: 1_293_601,
            name: "Teammate",
            x: 831,
            y: 585,
            classCode: 13_002,
            spellIds: []);
        sessions.Set(connection, session);
        sessions.Set(teammateConnection, teammateSession);
        teamStates.CreateTeam(connection, PlayerUid);
        Assert.True(teamStates.TryAddMember(connection, teammateConnection, 1_293_601));
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates,
            FloorItems: floorItems);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
        context = combatSpellOwner.Context;
        var before = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                SpaceLeapSpellId)).ToFrameBytes()));

        var after = DateTimeOffset.UtcNow;
        var floorItem = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.ItemId == LegacyLayTrap.SpaceLeapFloorItemId &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.Equal((ushort)840, floorItem.X);
        Assert.Equal((ushort)590, floorItem.Y);
        Assert.Equal(LegacyLayTrap.SpaceLeapFloorLife, floorItem.MaxLife);
        Assert.Equal((uint)LegacyLayTrap.SpaceLeapFloorLife, floorItem.Life);
        Assert.Equal((byte)17, floorItem.PlusOrColor);
        Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
        Assert.Equal(77U, floorItem.GuildId);
        Assert.Equal((byte)3, floorItem.FlowerType);
        Assert.Equal("SpaceLeap", floorItem.Name);

        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, after, out var runtimeEntry));
        Assert.Equal(1_015, runtimeEntry.MapId);
        Assert.Equal(floorItem, runtimeEntry.Packet);
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(SpaceLeapSpellId, runtimeEntry.Effect!.SpellId);
        Assert.Equal((ushort)840, runtimeEntry.Effect.OriginX);
        Assert.Equal((ushort)590, runtimeEntry.Effect.OriginY);
        Assert.Equal(0, runtimeEntry.Effect.Distance);
        Assert.Equal(0, runtimeEntry.Effect.HitRadius);
        Assert.InRange(
            runtimeEntry.Effect.DueAt,
            before.AddMilliseconds(17_000),
            after.AddMilliseconds(17_000));

        var casterTeamAction = Assert.Single(
            ParseMsgActions(sent),
            packet => (ushort)packet.Type == LegacyLayTrap.SpaceLeapTeamActionType);
        Assert.Equal(PlayerUid, casterTeamAction.DwParam);
        Assert.Equal(11, casterTeamAction.RemainTime);
        Assert.Equal(new[] { "Caster" }, casterTeamAction.Strings);

        var teammateTeamAction = Assert.Single(
            ParseMsgActions(teammateSent),
            packet => (ushort)packet.Type == LegacyLayTrap.SpaceLeapTeamActionType);
        Assert.Equal(PlayerUid, teammateTeamAction.DwParam);
        Assert.Equal(11, teammateTeamAction.RemainTime);
        Assert.Equal(new[] { "Caster" }, teammateTeamAction.Strings);

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal((ushort)840, animation.X);
        Assert.Equal((ushort)590, animation.Y);
        Assert.Equal(SpaceLeapSpellId, animation.SpellId);
        Assert.Equal((byte)0, animation.SpellLevel);
        Assert.Equal((byte)2, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal((ushort)0, target.NewX);
        Assert.Equal((ushort)584, target.NewY);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(SpaceLeapSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);
        Assert.Equal(10_000, updatedSpell.Experience);

        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            runtimeEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));
        var remove = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(LegacyLayTrap.SpaceLeapFloorItemId, remove.ItemId);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy SpaceLeap LayTrap magic", output);
        Assert.Contains("spellId=15620", output);
        Assert.Contains("floorItemId=930", output);
        Assert.Contains("teamActionRecipientCount=2", output);
        Assert.Contains("WorldServer handled legacy SpaceLeap LayTrap floor effect expiry", output);
        Assert.Contains("source=LayTrap.cs case 15620", output);
    }

    [Theory]
    [InlineData(142, 1_015, false)]
    [InlineData(13_002, 10_397, false)]
    [InlineData(13_002, 1_015, true)]
    public async Task HandlerSpaceLeapLayTrapGateReturnsAfterPreparationWithoutUnsupportedSpam(
        long classCode,
        long mapId,
        bool inQualifier)
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(SpaceLeapSpellId, level: 0, experience: 900_000)]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = inQualifier
            ? new Dictionary<string, string> { ["InQualifier"] = "true" }
            : null;
        var session = CreateSession(
            classCode: classCode,
            mapId: mapId,
            spellIds: [SpaceLeapSpellId],
            attributes: attributes);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                SpaceLeapSpellId)).ToFrameBytes()));

        Assert.Empty(ParseFloorItems(sent));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseMsgActions(sent));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);

        var output = log.ToString();
        Assert.Contains("WorldServer returned legacy SpaceLeap LayTrap magic after preparation", output);
        Assert.Contains("source=LayTrap.cs case 15620 gate breaks without floor effect", output);
        Assert.DoesNotContain("unsupported LayTrap", output);
        Assert.DoesNotContain("PARITY-GAP", output);
    }

    [Fact]
    public async Task HandlerSpaceLeapLayTrapWithoutFloorItemsLogsExplicitRuntimeStateGap()
    {
        var worldData = CreateWorldData(
            CreateMonsterFamily(),
            [],
            [CreateLayTrapMagicRow(SpaceLeapSpellId, level: 0, experience: 900_000)]);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode: 13_002, spellIds: [SpaceLeapSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: PlayerUid,
                x: 840,
                y: 590,
                SpaceLeapSpellId)).ToFrameBytes()));

        var floorItem = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.ItemId == LegacyLayTrap.SpaceLeapFloorItemId &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.Equal("SpaceLeap", floorItem.Name);
        Assert.Single(ParseSpellAnimations(sent));

        var output = log.ToString();
        Assert.Contains("PARITY-GAP", output);
        Assert.Contains("spaceleap-floor-effect-runtime", output);
        Assert.Contains("SpaceLeap floor effect was sent, but floor item runtime state is not loaded", output);
        Assert.Contains("spellId=15620", output);
        Assert.DoesNotContain("unsupported LayTrap", output);
    }

    [Fact]
    public async Task HandlerLayTrap12570DelayedFloorRuntimeHitsMonsterAndRemovesEffect()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 590);
        var worldData = CreateWorldData(
            monsterFamily,
            [spawn],
            [CreateLayTrapMagicRow(WrathOfTheEmperorSpellId, level: 0, experience: 1),
                CreateLayTrapMagicRow(WrathOfTheEmperorSpellId, level: 1, experience: 100)]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [WrathOfTheEmperorSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster.Uid,
                x: 840,
                y: 590,
                WrathOfTheEmperorSpellId)).ToFrameBytes()));

        var floorItem = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.ItemId == LegacyLayTrap.SourceCase12570FloorItemId &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.Equal((ushort)836, floorItem.X);
        Assert.Equal((ushort)590, floorItem.Y);
        Assert.Equal(0U, floorItem.ItemOwnerUid);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var floorEntry));
        Assert.NotNull(floorEntry.Effect);
        Assert.Equal(PlayerUid, floorEntry.Effect!.RuntimeOwnerUid);
        Assert.Equal(2.0, floorEntry.Effect.HitRadius);

        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapSourceCases12550And12570FloorSpellsAsync(
            context,
            floorEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        var runtimeAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            animation => animation.SpellId == WrathOfTheEmperorSpellId &&
                animation.BombFlag == 1);
        Assert.Equal(PlayerUid, runtimeAnimation.AttackerUid);
        Assert.Equal(0U, runtimeAnimation.AttackedUid);
        Assert.Equal(floorItem.X, runtimeAnimation.X);
        Assert.Equal(floorItem.Y, runtimeAnimation.Y);
        var target = Assert.Single(runtimeAnimation.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(target.Damage > 0);

        var remove = Assert.Single(
            ParseFloorItems(sent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(LegacyLayTrap.SourceCase12570FloorItemId, remove.ItemId);
        Assert.Equal(0U, remove.ItemOwnerUid);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < monster.MaxLife);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy LayTrap source-case floor spell runtime", output);
        Assert.Contains("source=ThreadPool.cs cases 12550/12570", output);
    }

    private static MsgAttackQuery CreateMagicQuery(
        uint opponentUid,
        ushort x,
        ushort y,
        ushort spellId)
    {
        return new MsgAttackQuery(
            PlayerUid,
            opponentUid,
            x,
            y,
            MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static WorldSession CreateSession(
        long uid = PlayerUid,
        string name = "Caster",
        long x = 830,
        long y = 584,
        long minMana = 500,
        long classCode = 142,
        long mapId = 1_015,
        IReadOnlyList<ushort>? spellIds = null,
        IReadOnlyList<PlayerSpellSnapshotEntry>? spellEntries = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        long strength = 300,
        long minHitPoints = 50_000)
    {
        spellIds ??= [InfernalEchoSpellId];
        // Positive component fixtures use the actual loader's closed array representation.
        var spells = spellEntries?.ToArray() ??
            spellIds
                .Select(spellId => new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0))
                .ToArray();
        var runtimeAttributes = attributes is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(attributes);

        return new WorldSession(
            AccountId: 1,
            Username: name.ToLowerInvariant(),
            new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: mapId,
                PreviousMapX: x,
                PreviousMapY: y,
                Strength: strength,
                Agility: 0,
                Vitality: 0,
                Spirit: 300,
                AttributePoints: 0,
                MinHitPoints: minHitPoints,
                MinMana: minMana,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://laytrap-source-case-character",
                Attributes: runtimeAttributes,
                PaidNobility: new Dictionary<string, string>()),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(spells),
            PlayerProficiencySnapshot.Empty);
    }

    private readonly record struct FlamePlayerRepeatPulseResult(
        uint PreGuardPhysicalDamage,
        uint HitPointDelta,
        WorldSession UpdatedTargetSession,
        LegacySpellAnimationTarget AnimationTarget);

    private static Monster CreateMonsterFamily()
    {
        return new Monster(
            9_001,
            "TrainingMonster",
            new Dictionary<string, string>
            {
                ["life"] = "100000",
                ["lookface"] = "100",
                ["level"] = "100",
                ["defense"] = "1",
                ["defense2"] = "10000",
                ["attack_range"] = "0",
            },
            []);
    }

    private static MonsterSpawn CreateSingleInstanceMonsterSpawn(
        int generatorId = 1,
        int boundX = 836,
        int boundY = 590)
    {
        return new MonsterSpawn(
            GeneratorId: generatorId,
            MapId: 1_015,
            MonsterTypeId: 9_001,
            BoundX: boundX,
            BoundY: boundY,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 5,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: null,
            SourceMonsterName: "TrainingMonster",
            SourceRelativePath: "1015/1.txt",
            Attributes: new Dictionary<string, string>());
    }

    private static LegacyMonsterInstance CreateRuntimeMonsterInstance(
        WorldDataCatalog worldData,
        MonsterSpawn spawn,
        Monster monster)
    {
        return LegacyMonsterInstance
            .FromGenerator(spawn, monster, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
    }

    private static SobNpc CreateDustDetachmentSobNpc()
    {
        return new SobNpc(
            Id: 65_460,
            Name: "DustPole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 1_015,
            X: 834,
            Y: 584,
            Life: 50_000_000,
            MaxLife: 50_000_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 16_460,
            RawLine: "65460,DustPole,10,1137,1015,834,584,50000000,50000000,17,0",
            Fields: ["65460", "DustPole", "10", "1137", "1015", "834", "584", "50000000", "50000000", "17", "0"]);
    }

    private static WorldDataCatalog CreateWorldData(
        Monster monster,
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        IReadOnlyList<MagicType> magicRows,
        IReadOnlyList<SobNpc>? sobNpcs = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-22T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://laytrap-source-cases",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: magicRows,
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: sobNpcs ?? [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [monster],
            monsterSpawns: monsterSpawns,
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static MagicType CreateLayTrapMagicRow(
        ushort spellId,
        byte level,
        uint experience,
        int? damageOnHuman = null,
        uint? damage = null)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = ((byte)LegacyMagicSort.LayTrap).ToString(CultureInfo.InvariantCulture);
        fields[3] = spellId switch
        {
            DaggerStormSpellId => "DaggerStorm",
            AuroraLotusSpellId => "AuroraLotus",
            FlameLotusSpellId => "FlameLotus",
            InfernalEchoSpellId => "InfernalEcho",
            SpaceLeapSpellId => "SpaceLeap",
            WaterShockwaveSpellId => "WaterShockwave",
            WaterShockwavePassiveSpellId => "WaterShockwave(Passive)",
            DustDetachmentSpellId => "DustDetachment",
            DustDetachmentPassiveSpellId => "DustDetachment(Passive)",
            SickleWindSpellId => "SickleWind",
            SickleWindPassiveSpellId => "SickleWind(Passive)",
            FlameOfDestructionSpellId => "FlameofDestruction",
            FlameOfDestructionPassiveSpellId => "FlameofDestruction(Passive)",
            _ => "WrathoftheEmperor",
        };
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = spellId is DaggerStormSpellId or AuroraLotusSpellId or FlameLotusSpellId or WaterShockwaveSpellId or WaterShockwavePassiveSpellId or DustDetachmentSpellId or DustDetachmentPassiveSpellId or SickleWindSpellId or SickleWindPassiveSpellId ? "0" : "7";
        fields[10] = spellId switch
        {
            DaggerStormSpellId => "30300",
            FlameLotusSpellId => "35000",
            SpaceLeapSpellId => "30600",
            WaterShockwaveSpellId or WaterShockwavePassiveSpellId => "30200",
            DustDetachmentSpellId or DustDetachmentPassiveSpellId => "30200",
            SickleWindSpellId or SickleWindPassiveSpellId => "30280",
            _ => "1000",
        };
        if (damage is { } damageValue)
        {
            fields[10] = damageValue.ToString(CultureInfo.InvariantCulture);
        }
        fields[12] = spellId switch
        {
            WaterShockwaveSpellId => "100",
            DustDetachmentSpellId => "100",
            SickleWindSpellId => "100",
            FlameOfDestructionSpellId => "100",
            WaterShockwavePassiveSpellId => "0",
            DustDetachmentPassiveSpellId => "0",
            SickleWindPassiveSpellId => "30",
            FlameOfDestructionPassiveSpellId => "0",
            _ => fields[12],
        };
        fields[13] = spellId is WaterShockwaveSpellId or WaterShockwavePassiveSpellId or DustDetachmentSpellId or DustDetachmentPassiveSpellId or SickleWindSpellId or SickleWindPassiveSpellId or FlameOfDestructionSpellId or FlameOfDestructionPassiveSpellId ? "3" : fields[13];
        fields[14] = spellId switch
        {
            AuroraLotusSpellId => "4",
            DaggerStormSpellId or FlameLotusSpellId => "5",
            WaterShockwaveSpellId => "3",
            WaterShockwavePassiveSpellId => "2",
            DustDetachmentSpellId or DustDetachmentPassiveSpellId => "4",
            SickleWindSpellId or SickleWindPassiveSpellId => "5",
            FlameOfDestructionSpellId or FlameOfDestructionPassiveSpellId => "4",
            _ => "1",
        };
        fields[15] = "15";
        fields[18] = experience.ToString(CultureInfo.InvariantCulture);
        fields[20] = spellId switch
        {
            DaggerStormSpellId => "100",
            AuroraLotusSpellId or FlameLotusSpellId => "50",
            _ => "0",
        };
        fields[22] = spellId switch
        {
            DaggerStormSpellId => "613",
            AuroraLotusSpellId or FlameLotusSpellId => "620",
            WaterShockwaveSpellId or WaterShockwavePassiveSpellId or DustDetachmentSpellId or DustDetachmentPassiveSpellId or SickleWindSpellId or SickleWindPassiveSpellId or FlameOfDestructionSpellId or FlameOfDestructionPassiveSpellId => "511616601",
            _ => "0",
        };
        fields[24] = spellId switch
        {
            WaterShockwaveSpellId => "1032",
            WaterShockwavePassiveSpellId => "1028",
            DustDetachmentSpellId => "1032",
            DustDetachmentPassiveSpellId => "1028",
            SickleWindSpellId => "1032",
            SickleWindPassiveSpellId => "1028",
            FlameOfDestructionSpellId => "1032",
            FlameOfDestructionPassiveSpellId => "1028",
            _ => fields[24],
        };
        fields[33] = spellId == DaggerStormSpellId ? "500" : "0";
        fields[35] = spellId switch
        {
            AuroraLotusSpellId => "1",
            FlameLotusSpellId => "280",
            WaterShockwaveSpellId or WaterShockwavePassiveSpellId or FlameOfDestructionSpellId or FlameOfDestructionPassiveSpellId => "5000",
            DustDetachmentSpellId => "30090",
            DustDetachmentPassiveSpellId => "30160",
            SickleWindSpellId => "30090",
            SickleWindPassiveSpellId => "30200",
            _ => "0",
        };
        if (damageOnHuman is { } damageOnHumanValue)
        {
            fields[35] = damageOnHumanValue.ToString(CultureInfo.InvariantCulture);
        }
        fields[36] = spellId switch
        {
            AuroraLotusSpellId or FlameLotusSpellId => "5",
            WaterShockwaveSpellId or WaterShockwavePassiveSpellId or DustDetachmentSpellId or DustDetachmentPassiveSpellId or SickleWindSpellId or SickleWindPassiveSpellId or FlameOfDestructionSpellId or FlameOfDestructionPassiveSpellId => "6000",
            _ => "0",
        };
        fields[37] = spellId switch
        {
            AuroraLotusSpellId or FlameLotusSpellId => "200005",
            WaterShockwaveSpellId or WaterShockwavePassiveSpellId => "30136",
            _ => "0",
        };
        fields[44] = spellId switch
        {
            AuroraLotusSpellId => "1310",
            FlameLotusSpellId => "1320",
            WaterShockwaveSpellId or WaterShockwavePassiveSpellId => "3",
            DustDetachmentSpellId => "2527",
            DustDetachmentPassiveSpellId => "2528",
            FlameOfDestructionSpellId => "2535",
            FlameOfDestructionPassiveSpellId => "2536",
            _ => "0",
        };
        fields[45] = spellId switch
        {
            WaterShockwaveSpellId => "253704",
            WaterShockwavePassiveSpellId => "253804",
            _ => fields[45],
        };
        fields[44] = spellId switch
        {
            SickleWindSpellId => "2533",
            SickleWindPassiveSpellId => "2534",
            _ => fields[44],
        };
        fields[47] = spellId switch
        {
            DaggerStormSpellId => "10000",
            WaterShockwaveSpellId => "10000",
            DustDetachmentSpellId => "10000",
            SickleWindSpellId => "16000",
            FlameOfDestructionSpellId => "13000",
            _ => "0",
        };
        if (spellId == FlameLotusSpellId)
        {
            fields[47] = "60000";
        }
        return new MagicType(spellId, spellId, fields[3], fields);
    }

    private static uint InvokePrivateUInt32(string methodName, params object?[] arguments)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(null, arguments);
        return Assert.IsType<uint>(result);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveLayTrapSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            spellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static List<LegacyFloorItemPacket> ParseFloorItems(IEnumerable<byte[]> frames)
    {
        var floorItems = new List<LegacyFloorItemPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                floorItems.Add(packet);
            }
        }

        return floorItems;
    }

    private static List<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<MsgAttackPacket> ParseMsgAttacks(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in frames)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var maxFlag = flags.Where(flag => flag >= 0).DefaultIfEmpty(0).Max();
        var values = new ulong[Math.Max(10, maxFlag / 32 + 1)];
        foreach (var flag in flags)
        {
            if (flag < 0)
            {
                continue;
            }

            values[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', values) + "/";
    }

    private static bool HasStatusFlag(
        IReadOnlyDictionary<string, string> attributes,
        int flag)
    {
        if (attributes.TryGetValue("StatusFlags", out var statusFlags))
        {
            return HasStatusFlag(statusFlags, flag);
        }

        return attributes.TryGetValue("BitVector", out var bitVector) &&
            HasStatusFlag(bitVector, flag);
    }

    private static bool HasStatusFlag(string raw, int flag)
    {
        if (flag < 0)
        {
            return false;
        }

        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordIndex = flag / 32;
        if (wordIndex >= parts.Length ||
            !ulong.TryParse(parts[wordIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word))
        {
            return false;
        }

        var mask = 1UL << (flag % 32);
        return (word & mask) == mask;
    }

    private static bool HasStatusFlag(IReadOnlyList<ulong> values, int flag)
    {
        if (flag < 0)
        {
            return false;
        }

        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }
}
