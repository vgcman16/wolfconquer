using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMagicStatusBuffParityTests
{
    private const uint PlayerUid = 1_293_600;
    private const ushort StigmaSpellId = 1_095;
    private const ushort HealingSnowSpellId = 12_950;
    private const ushort ChillingSnowSpellId = 12_960;
    private const ushort FreezingPelterSpellId = 13_020;
    private const ushort PhoenixBlessingSpellId = 20_300;
    private const ushort HolyProtectionSpellId = 20_350;
    private const ushort WildPhoenixSpellId = 20_370;
    private const int LegacyStigmaFlag = 0x09;
    private const int LegacyHealingSnowFlag = 0xC4;
    private const int LegacyChillingSnowFlag = 0xC5;
    private const int LegacyFreezingPelterFlag = 0xC8;
    private const int LegacyPhoenixBlessingFlag = 0x17F;
    private const int LegacyHolyProtectionFlag = 0x178;
    private const int LegacyWildPhoenixFlag = 0x170;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Theory]
    [InlineData(PhoenixBlessingSpellId, LegacyPhoenixBlessingFlag, 20U, 45U)]
    [InlineData(HolyProtectionSpellId, LegacyHolyProtectionFlag, 20U, 35U)]
    public async Task HandlerCombatGearSelfStatusSendsSourceArchiveAnimationAndEquipmentRefresh(
        ushort spellId,
        int statusFlag,
        uint duration,
        uint archiveDamage)
    {
        var worldData = CreateWorldData(
            CreateMagicRow(
                spellId,
                sort: 6,
                useMana: 10,
                duration: duration,
                damageOnHuman: archiveDamage,
                coldTime: 0));
        var playerItems = CreateEquippedRightWeapon();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [spellId], playerItems: playerItems);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(spellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, statusFlag));

        var sentSnapshot = sent.ToArray();
        Assert.Empty(ParseMsgAttacks(sentSnapshot));

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, statusFlag));

        var archiveUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == (uint)statusFlag));
        var archiveEntry = Assert.Single(archiveUpdate.Updates);
        Assert.Equal(duration, archiveEntry.Time);
        Assert.Equal(archiveDamage, archiveEntry.Damage);
        Assert.Equal(0U, archiveEntry.SpellLevel);

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(spellId, spell.SpellId);
        Assert.Equal((ushort)0, spell.X);
        Assert.Equal((ushort)0, spell.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.False(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(sentSnapshot));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(playerItems, alternateEquipmentActive: false),
            equipmentRefresh);
        Assert.Equal(54_003U, equipmentRefresh.EquipmentSlots.RightWeapon);

        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, statusFlag)));
        var archiveIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == (uint)statusFlag));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == spellId);
        var equipmentIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(statusIndex < archiveIndex);
        Assert.True(archiveIndex < animationIndex);
        Assert.True(animationIndex < equipmentIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear self-status magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains($"statusFlag={statusFlag}", output);
        Assert.Contains($"duration={duration}", output);
        Assert.Contains($"archiveDamage={archiveDamage}", output);
        Assert.Contains("source=MsgAttackPacket.ProcescMagic -> CombatGearAttack.Execute", output);
    }

    [Fact]
    public async Task HandlerCombatGearWildPhoenixSelfStatusSendsPackedArchiveChargeAnimationAndEquipmentRefresh()
    {
        const uint duration = 32;
        const uint damageOnHuman = 18;
        const uint damage2 = 7;
        const uint charges = 4;
        const uint packedDamage = damageOnHuman * 1_000 + damage2;
        var worldData = CreateWorldData(
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 10,
                damage: charges,
                duration: duration,
                damageOnHuman: damageOnHuman,
                damage2: damage2,
                coldTime: 0));
        var playerItems = CreateEquippedRightWeapon();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [WildPhoenixSpellId], playerItems: playerItems);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(WildPhoenixSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyWildPhoenixFlag));
        Assert.Equal(
            packedDamage.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["LegacyCombatGearWildPhoenixPackedDamage"]);
        Assert.Equal(
            charges.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["LegacyCombatGearWildPhoenixChargeCount"]);

        var sentSnapshot = sent.ToArray();
        Assert.Empty(ParseMsgAttacks(sentSnapshot));

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyWildPhoenixFlag));

        var archiveUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag));
        var archiveEntry = Assert.Single(archiveUpdate.Updates);
        Assert.Equal(duration, archiveEntry.Time);
        Assert.Equal(packedDamage, archiveEntry.Damage);
        Assert.Equal(charges, archiveEntry.SpellLevel);

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(WildPhoenixSpellId, spell.SpellId);
        Assert.Equal((ushort)0, spell.X);
        Assert.Equal((ushort)0, spell.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.False(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(sentSnapshot));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(playerItems, alternateEquipmentActive: false),
            equipmentRefresh);
        Assert.Equal(54_003U, equipmentRefresh.EquipmentSlots.RightWeapon);

        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyWildPhoenixFlag)));
        var archiveIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == LegacyWildPhoenixFlag));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == WildPhoenixSpellId);
        var equipmentIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(statusIndex < archiveIndex);
        Assert.True(archiveIndex < animationIndex);
        Assert.True(animationIndex < equipmentIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear self-status magic", output);
        Assert.Contains("spellId=20370", output);
        Assert.Contains("statusFlag=368", output);
        Assert.Contains($"duration={duration}", output);
        Assert.Contains($"archiveDamage={packedDamage}", output);
        Assert.Contains($"archiveSpellLevel={charges}", output);
        Assert.Contains("source=MsgAttackPacket.ProcescMagic -> CombatGearAttack.Execute", output);
    }

    [Fact]
    public async Task HandlerStigmaSelfBuffAddsLegacyStatusAndSpellAnimationTarget()
    {
        var worldData = CreateWorldData(
            CreateMagicRow(
                StigmaSpellId,
                sort: 6,
                useMana: 10,
                useStamina: 5,
                duration: 90,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [StigmaSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(StigmaSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyStigmaFlag));

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyStigmaFlag));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(StigmaSpellId, spell.SpellId);
        Assert.Equal(checked((ushort)session.Character.X), spell.X);
        Assert.Equal(checked((ushort)session.Character.Y), spell.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy classic AttachStatus buff", output);
        Assert.Contains("spellId=1095", output);
        Assert.Contains("statusFlag=9", output);
        Assert.Contains("duration=90", output);
    }

    [Fact]
    public async Task HandlerHealingSnowSelfToggleAddsLegacyStatusClearsOtherSnowFlagsAndSendsInteraction()
    {
        var worldData = CreateWorldData(
            CreateMagicRow(
                HealingSnowSpellId,
                sort: 6,
                experience: 20,
                duration: 45,
                coldTime: 0),
            CreateMagicRow(
                HealingSnowSpellId,
                sort: 6,
                level: 1,
                experience: 200,
                duration: 45,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [HealingSnowSpellId],
            statusFlags: BuildStatusFlags(LegacyChillingSnowFlag, LegacyFreezingPelterFlag));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(HealingSnowSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyHealingSnowFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyChillingSnowFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFreezingPelterFlag));

        var interaction = Assert.Single(ParseMsgAttacks(sent));
        Assert.Equal(PlayerUid, interaction.Query.Uid);
        Assert.Equal(PlayerUid, interaction.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Magic, interaction.Query.AttackType);
        Assert.Equal(HealingSnowSpellId, interaction.Query.SpellId);

        var statusEntry = Assert.Single(
            Assert.Single(
                ParseMsgUpdates(sent),
                update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)).Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyHealingSnowFlag));
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyChillingSnowFlag));
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyFreezingPelterFlag));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(HealingSnowSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var spellUpdate = Assert.Single(ParseSpellUpdates(sent));
        Assert.Equal(HealingSnowSpellId, spellUpdate.Id);
        Assert.Equal(1, spellUpdate.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy HealingSnow attach-status magic", output);
        Assert.Contains("spellId=12950", output);
        Assert.Contains("statusFlag=196", output);
        Assert.Contains("clearedStatusFlag197=True", output);
        Assert.Contains("clearedStatusFlag200=True", output);
        Assert.Contains("updateDamage=45", output);
    }

    [Fact]
    public async Task HandlerHealingSnowSelfToggleRecastRemovesLegacyStatusWithoutAnimation()
    {
        var worldData = CreateWorldData(CreateMagicRow(HealingSnowSpellId, sort: 6, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [HealingSnowSpellId],
            statusFlags: BuildStatusFlags(LegacyHealingSnowFlag));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(HealingSnowSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyHealingSnowFlag));

        var interaction = Assert.Single(ParseMsgAttacks(sent));
        Assert.Equal(PlayerUid, interaction.Query.Uid);
        Assert.Equal(PlayerUid, interaction.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Magic, interaction.Query.AttackType);
        Assert.Equal(HealingSnowSpellId, interaction.Query.SpellId);

        var statusEntry = Assert.Single(
            Assert.Single(
                ParseMsgUpdates(sent),
                update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)).Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyHealingSnowFlag));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseSpellUpdates(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy HealingSnow attach-status toggle", output);
        Assert.Contains("spellId=12950", output);
        Assert.Contains("removedStatusFlag=196", output);
    }

    [Fact]
    public async Task HandlerChillingSnowSelfToggleAddsLegacyStatusClearsOtherSnowFlagsAndSendsInteraction()
    {
        var worldData = CreateWorldData(CreateMagicRow(ChillingSnowSpellId, sort: 6, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [ChillingSnowSpellId],
            statusFlags: BuildStatusFlags(LegacyHealingSnowFlag, LegacyFreezingPelterFlag));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(ChillingSnowSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyChillingSnowFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyHealingSnowFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFreezingPelterFlag));

        var interaction = Assert.Single(ParseMsgAttacks(sent));
        Assert.Equal(PlayerUid, interaction.Query.Uid);
        Assert.Equal(PlayerUid, interaction.Query.OpponentUid);
        Assert.Equal(checked((ushort)session.Character.X), interaction.Query.X);
        Assert.Equal(checked((ushort)session.Character.Y), interaction.Query.Y);
        Assert.Equal(MsgAttackType.Magic, interaction.Query.AttackType);
        Assert.Equal(ChillingSnowSpellId, interaction.Query.SpellId);

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyChillingSnowFlag));
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyHealingSnowFlag));
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyFreezingPelterFlag));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ChillingSnowSpellId, spell.SpellId);
        Assert.Equal(checked((ushort)session.Character.X), spell.X);
        Assert.Equal(checked((ushort)session.Character.Y), spell.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ChillingSnow attach-status magic", output);
        Assert.Contains("spellId=12960", output);
        Assert.Contains("statusFlag=197", output);
        Assert.Contains("clearedStatusFlag196=True", output);
        Assert.Contains("clearedStatusFlag200=True", output);
        Assert.Contains("updateDamage=500", output);
    }

    [Fact]
    public async Task HandlerChillingSnowSelfToggleRecastRemovesLegacyStatusWithoutAnimation()
    {
        var worldData = CreateWorldData(CreateMagicRow(ChillingSnowSpellId, sort: 6, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [ChillingSnowSpellId],
            statusFlags: BuildStatusFlags(LegacyChillingSnowFlag));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(ChillingSnowSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyChillingSnowFlag));

        var interaction = Assert.Single(ParseMsgAttacks(sent));
        Assert.Equal(PlayerUid, interaction.Query.Uid);
        Assert.Equal(PlayerUid, interaction.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Magic, interaction.Query.AttackType);
        Assert.Equal(ChillingSnowSpellId, interaction.Query.SpellId);

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyChillingSnowFlag));
        Assert.Empty(ParseSpellAnimations(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ChillingSnow attach-status toggle", output);
        Assert.Contains("spellId=12960", output);
        Assert.Contains("removedStatusFlag=197", output);
    }

    [Fact]
    public async Task HandlerFreezingPelterSelfToggleAddsLegacyStatusClearsOtherSnowFlagsSendsResistWoodAndDiagnostic()
    {
        var worldData = CreateWorldData(
            CreateMagicRow(
                FreezingPelterSpellId,
                sort: 6,
                experience: 20,
                duration: 60,
                coldTime: 0),
            CreateMagicRow(
                FreezingPelterSpellId,
                sort: 6,
                level: 1,
                experience: 200,
                duration: 60,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [FreezingPelterSpellId],
            statusFlags: BuildStatusFlags(LegacyHealingSnowFlag, LegacyChillingSnowFlag));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(FreezingPelterSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFreezingPelterFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyHealingSnowFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyChillingSnowFlag));

        var interaction = Assert.Single(ParseMsgAttacks(sent));
        Assert.Equal(PlayerUid, interaction.Query.Uid);
        Assert.Equal(PlayerUid, interaction.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Magic, interaction.Query.AttackType);
        Assert.Equal(FreezingPelterSpellId, interaction.Query.SpellId);

        var statusEntry = Assert.Single(
            Assert.Single(
                ParseMsgUpdates(sent),
                update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)).Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyFreezingPelterFlag));
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyHealingSnowFlag));
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyChillingSnowFlag));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(FreezingPelterSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.ResistWood, target.Effect);

        var spellUpdate = Assert.Single(ParseSpellUpdates(sent));
        Assert.Equal(FreezingPelterSpellId, spellUpdate.Id);
        Assert.Equal(1, spellUpdate.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy FreezingPelter attach-status magic", output);
        Assert.Contains("spellId=13020", output);
        Assert.Contains("statusFlag=200", output);
        Assert.Contains("clearedStatusFlag196=True", output);
        Assert.Contains("clearedStatusFlag197=True", output);
        Assert.Contains("updateDamage=60", output);
        Assert.Contains("equipmentRefreshPackets=1", output);
        Assert.Contains("equipmentQueryMode=local QueryEquipment-equivalent equipment refresh on add", output);
    }

    [Fact]
    public async Task HandlerFreezingPelterSelfToggleRecastRemovesLegacyStatusWithoutAnimationAndKeepsEquipmentDiagnostic()
    {
        var worldData = CreateWorldData(CreateMagicRow(FreezingPelterSpellId, sort: 6, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [FreezingPelterSpellId],
            statusFlags: BuildStatusFlags(LegacyFreezingPelterFlag));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(FreezingPelterSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFreezingPelterFlag));

        var interaction = Assert.Single(ParseMsgAttacks(sent));
        Assert.Equal(PlayerUid, interaction.Query.Uid);
        Assert.Equal(PlayerUid, interaction.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Magic, interaction.Query.AttackType);
        Assert.Equal(FreezingPelterSpellId, interaction.Query.SpellId);

        var statusEntry = Assert.Single(
            Assert.Single(
                ParseMsgUpdates(sent),
                update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)).Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyFreezingPelterFlag));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseSpellUpdates(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy FreezingPelter attach-status toggle", output);
        Assert.Contains("spellId=13020", output);
        Assert.Contains("removedStatusFlag=200", output);
        Assert.Contains("equipmentRefreshPackets=0", output);
        Assert.Contains("equipmentQueryMode=local QueryEquipment-equivalent equipment refresh on add", output);
    }

    [Fact]
    public async Task HandlerFreezingPelterActivationSendsLegacyShowEquipmentPacketForQueryEquipmentSideEffect()
    {
        var worldData = CreateWorldData(CreateMagicRow(FreezingPelterSpellId, sort: 6, coldTime: 0));
        var playerItems = CreateEquippedRightWeapon();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [FreezingPelterSpellId], playerItems: playerItems);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(FreezingPelterSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFreezingPelterFlag));

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(sent));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(playerItems, alternateEquipmentActive: false),
            equipmentRefresh);
    }

    [Fact]
    public async Task HandlerFreezingPelterRemovalSuppressesLegacyShowEquipmentForCallItemsFalse()
    {
        var worldData = CreateWorldData(CreateMagicRow(FreezingPelterSpellId, sort: 6, coldTime: 0));
        var playerItems = CreateEquippedRightWeapon();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [FreezingPelterSpellId],
            statusFlags: BuildStatusFlags(LegacyFreezingPelterFlag),
            playerItems: playerItems);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfMagicQuery(FreezingPelterSpellId, session)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFreezingPelterFlag));

        Assert.Empty(ParseShowEquipmentRefreshes(sent));
    }

    private static MsgAttackQuery CreateSelfMagicQuery(ushort spellId, WorldSession session)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: checked((ushort)session.Character.X),
            Y: checked((ushort)session.Character.Y),
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static WorldSession CreateSession(
        long uid = PlayerUid,
        string name = "Caster",
        long x = 830,
        long y = 584,
        long minMana = 500,
        long minHitPoints = 5_000,
        long spirit = 300,
        ulong? stamina = null,
        IReadOnlyList<ushort>? spellIds = null,
        string? statusFlags = null,
        PlayerItemSnapshot? playerItems = null)
    {
        spellIds ??= [StigmaSpellId];
        var spells = spellIds
            .Select(spellId => new PlayerSpellSnapshotEntry(
                spellId,
                Level: 0,
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0))
            .ToArray();

        return new WorldSession(
            AccountId: 1,
            Username: name.ToLowerInvariant(),
            CreateCharacter(uid, name, x, y, minMana, minHitPoints, spirit, stamina, statusFlags),
            playerItems ?? PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(spells),
            PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot CreateEquippedRightWeapon()
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 54_003,
                    ItemId = 421_003,
                    Durability = 100,
                    MaximumDurability = 100,
                    Position = 4,
                },
            ],
            RuneItems: []);
    }

    private static CharacterSnapshot CreateCharacter(
        long uid,
        string name,
        long x,
        long y,
        long minMana,
        long minHitPoints,
        long spirit,
        ulong? stamina,
        string? statusFlags)
    {
        var attributes = new Dictionary<string, string>();
        if (stamina is { } value)
        {
            attributes["Stamina"] = value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(statusFlags))
        {
            attributes["StatusFlags"] = statusFlags;
        }

        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: 1_015,
            X: x,
            Y: y,
            PreviousMapId: 1_015,
            PreviousMapX: x,
            PreviousMapY: y,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: spirit,
            AttributePoints: 0,
            MinHitPoints: minHitPoints,
            MinMana: minMana,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://stigma-status-buff",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static WorldDataCatalog CreateWorldData(params MagicType[] magicRows)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-magic-status-buff",
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
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
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

    private static MagicType CreateMagicRow(
        ushort spellId,
        byte sort,
        byte level = 0,
        ushort useMana = 7,
        uint damage = 120,
        uint experience = 0,
        byte maxTargets = 0,
        byte range = 9,
        ushort needLevel = 0,
        uint weaponType = 0,
        byte useStamina = 0,
        ushort coolDown = 1_000,
        int coldTime = 300,
        uint duration = 0,
        uint damageOnHuman = 0,
        uint damage2 = 0)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = sort.ToString(CultureInfo.InvariantCulture);
        fields[3] = $"Spell{spellId}";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = useMana.ToString(CultureInfo.InvariantCulture);
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[13] = duration.ToString(CultureInfo.InvariantCulture);
        fields[18] = experience.ToString(CultureInfo.InvariantCulture);
        fields[20] = needLevel.ToString(CultureInfo.InvariantCulture);
        fields[22] = weaponType.ToString(CultureInfo.InvariantCulture);
        fields[14] = maxTargets.ToString(CultureInfo.InvariantCulture);
        fields[15] = range.ToString(CultureInfo.InvariantCulture);
        fields[29] = useStamina.ToString(CultureInfo.InvariantCulture);
        fields[33] = coolDown.ToString(CultureInfo.InvariantCulture);
        fields[35] = damageOnHuman.ToString(CultureInfo.InvariantCulture);
        fields[36] = damage2.ToString(CultureInfo.InvariantCulture);
        fields[47] = coldTime.ToString(CultureInfo.InvariantCulture);
        return new MagicType(spellId, spellId, $"Spell{spellId}", fields);
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

    private static List<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var updates = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update))
            {
                updates.Add(update);
            }
        }

        return updates;
    }

    private static List<MsgAttackPacket> ParseMsgAttacks(IEnumerable<byte[]> frames)
    {
        var attacks = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack))
            {
                attacks.Add(attack);
            }
        }

        return attacks;
    }

    private static List<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var spells = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var spell))
            {
                spells.Add(spell);
            }
        }

        return spells;
    }

    private static List<LegacySpellPacket> ParseSpellUpdates(IEnumerable<byte[]> frames)
    {
        var spells = new List<LegacySpellPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(bytes), out var spell))
            {
                spells.Add(spell);
            }
        }

        return spells;
    }

    private static List<LegacyShowEquipmentPacket> ParseShowEquipmentRefreshes(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyShowEquipmentPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static int SingleFrameIndex(IReadOnlyList<byte[]> frames, Func<PacketFrame, bool> predicate)
    {
        var indexes = new List<int>();
        for (var index = 0; index < frames.Count; index++)
        {
            if (predicate(ReadFrame(frames[index])))
            {
                indexes.Add(index);
            }
        }

        return Assert.Single(indexes);
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            IsLegacyStatusFlagSet(
                raw
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                        ? value
                        : 0)
                    .ToArray(),
                flag);
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[16];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }
}
