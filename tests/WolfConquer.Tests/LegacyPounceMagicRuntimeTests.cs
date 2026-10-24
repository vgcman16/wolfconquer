using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte PounceRuntimeSort = 84;
    private const ushort PounceRuntimeSpellId = 12_770;
    private const uint PounceShieldItemId = 900_000;
    private const uint PounceNonShieldItemId = 420_301;

    [Fact]
    public void LegacyMagicRuntimeResolvesExactPounceRowAndEightHundredMillisecondAttackStampCadence()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreatePounceRuntimeRow());

        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, PounceRuntimeSpellId, 0, out var spell));
        Assert.Equal((LegacyMagicSort)PounceRuntimeSort, spell.Sort);
        Assert.Equal("Pounce", spell.Name);
        Assert.Equal(30U, spell.Damage);
        Assert.Equal(30_030U, spell.RawDamage);
        Assert.Equal(500U, spell.Experience);
        Assert.Equal(5, spell.MaxTargets);
        Assert.Equal(20, spell.Range);
        Assert.Equal(60, spell.NeedLevel);
        Assert.Equal(900U, spell.WeaponType);
        Assert.Equal(50, spell.UseStamina);
        Assert.Equal(800, spell.CoolDown);
        Assert.Equal(0, spell.ColdTime);
        Assert.Equal(12U, spell.DamageOnHuman);
        Assert.Equal(30_200U, spell.Damage2);
        Assert.Equal(30_300U, spell.DamageOnMonster);
        Assert.False(LegacyMagicRuntime.UsesLegacyCooldown(spell));

        var cadenceMethod = typeof(WorldPacketHandlers).GetMethod(
            "TryGetLegacyPlayerMagicAttackStampCadence",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(cadenceMethod);
        object?[] cadenceArguments = [spell, null];

        Assert.True(Assert.IsType<bool>(cadenceMethod.Invoke(null, cadenceArguments)));
        Assert.Equal(TimeSpan.FromMilliseconds(800), Assert.IsType<TimeSpan>(cadenceArguments[1]));

        var capability = LegacyMagicSortCapabilities.Classify(spell);
        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, capability.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, capability.Status);
        Assert.True(capability.Supported);
        Assert.Equal("pounce-combat-runtime", capability.NextSeam);
    }

    [Fact]
    public async Task HandlerPounceMissingShieldRejectsBothImmediateRequestsWithoutAdmissionMutation()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreatePounceRuntimeRow());
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [PounceRuntimeSpellId],
            playerItems: CreatePounceEquipment(mainShield: false, alternateShield: false));
        sessions.Set(connection, session);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
            sessions, connection, session.Character.Uid, out var identity));
        var before = System.Text.Json.JsonSerializer.Serialize(session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var frame = ReadFrame(new MsgAttackPacket(CreatePounceRuntimeQuery()).ToFrameBytes());

        await PacketHandlers.HandleAsync(context, frame);

        Assert.True(sessions.TryGet(connection, out var firstCastSession));
        Assert.NotNull(firstCastSession);
        Assert.Same(session, firstCastSession);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(firstCastSession));
        Assert.Equal("100", firstCastSession.Character.Attributes["Stamina"]);
        Assert.False(firstCastSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        Assert.Null(firstCastSession.MagicCooldowns);
        Assert.Equal(session.Character.X, firstCastSession.Character.X);
        Assert.Equal(session.Character.Y, firstCastSession.Character.Y);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(sent);

        await PacketHandlers.HandleAsync(context with { Session = firstCastSession }, frame);

        Assert.True(sessions.TryGet(connection, out var blockedSession));
        Assert.NotNull(blockedSession);
        Assert.Same(session, blockedSession);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(blockedSession));
        Assert.Equal("100", blockedSession.Character.Attributes["Stamina"]);
        Assert.False(blockedSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        Assert.Null(blockedSession.MagicCooldowns);
        Assert.Empty(sent);
        Assert.True(LegacyNativeClientPublication.TryCurrent(
            sessions, connection, session.Character.Uid, identity, out var current));
        Assert.Same(session, current);
        Assert.Equal(2, log.ToString().Split("activeLeftShield=false", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("attackStampRemainingMs=", log.ToString());
        Assert.DoesNotContain("cooldownRemainingMs=", log.ToString());
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, true)]
    public async Task HandlerPounceRequiresSelectedLeftShieldAndSuccessfulEmptyCastUsesSourceAnimation(
        bool alternateEquipmentActive,
        bool mainShield,
        bool alternateShield,
        bool expectedSuccess)
    {
        const ushort destinationX = 840;
        const ushort destinationY = 590;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreatePounceRuntimeRow());
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var playerItems = CreatePounceEquipment(mainShield, alternateShield);
        var activeLeftHand = Assert.Single(
            LegacyEquipmentSelector.SelectCurrentEquipment(playerItems, alternateEquipmentActive),
            item => item.Position % 50 == 5);
        Assert.Equal(expectedSuccess, ItemType.IsLegacyShield(activeLeftHand.ItemId));
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [PounceRuntimeSpellId],
            playerItems: playerItems) with
        {
            AlternateEquipmentActive = alternateEquipmentActive,
        };
        sessions.Set(connection, session);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
            sessions, connection, session.Character.Uid, out var identity));
        var before = System.Text.Json.JsonSerializer.Serialize(session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(
                CreatePounceRuntimeQuery(destinationX, destinationY)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Null(updatedSession.MagicCooldowns);
        Assert.True(LegacyNativeClientPublication.TryCurrent(
            sessions, connection, session.Character.Uid, identity, out var current));
        Assert.Same(updatedSession, current);

        if (!expectedSuccess)
        {
            Assert.Same(session, updatedSession);
            Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(updatedSession));
            Assert.Equal("100", updatedSession.Character.Attributes["Stamina"]);
            Assert.False(updatedSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
            Assert.Equal(session.Character.X, updatedSession.Character.X);
            Assert.Equal(session.Character.Y, updatedSession.Character.Y);
            Assert.Empty(ParseSpellAnimations(sent));
            Assert.Empty(sent);
            Assert.Contains("activeLeftShield=false", log.ToString());
            return;
        }

        Assert.Equal("50", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(updatedSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        Assert.Equal(destinationX, updatedSession.Character.X);
        Assert.Equal(destinationY, updatedSession.Character.Y);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        AssertPounceSourceAnimation(animation, destinationX, destinationY, expectedSoul: 0);
        Assert.Empty(animation.Targets);
        AssertNoPounceMovementFrame(sent);
    }

    [Fact]
    public async Task HandlerPounceShiftsBeforeMixedRadiusTargetsAndProgressesInSourceOrderWithoutExtraScaling()
    {
        const ushort destinationX = 840;
        const ushort destinationY = 590;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var inRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 845, boundY: 590);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 846, boundY: 590);
        var inRangeSobNpc = CreateMoveLineSobNpc(id: 62_770, x: 835, y: 590);
        var outsideSobNpc = CreateMoveLineSobNpc(id: 62_771, x: 834, y: 590);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [inRangeSpawn, outsideSpawn],
            [inRangeSobNpc, outsideSobNpc],
            [
                CreatePounceRuntimeRow(experience: uint.MaxValue),
                CreatePounceRuntimeRow(rowId: PounceRuntimeSpellId + 1, level: 1, experience: uint.MaxValue),
            ]);
        var inRangeMonster = CreateRuntimeMonsterInstance(worldData, inRangeSpawn, monsterFamily);
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var inRangePlayerSent = new List<byte[]>();
        var outsidePlayerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var inRangePlayerConnection = CreateSendingConnection(inRangePlayerSent);
        var outsidePlayerConnection = CreateSendingConnection(outsidePlayerSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    PounceRuntimeSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 3,
                    UseSpellSoul: 3),
            ],
            playerItems: CreatePounceEquipment(mainShield: true, alternateShield: false));
        var inRangePlayerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "PounceInRange",
            x: 840,
            y: 595,
            minHitPoints: 100_000);
        var outsidePlayerSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "PounceOutside",
            x: 840,
            y: 596,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(inRangePlayerConnection, inRangePlayerSession);
        sessions.Set(outsidePlayerConnection, outsidePlayerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            PounceRuntimeSpellId,
            out _,
            out var spell));
        var shiftedCasterSession = casterSession with
        {
            Character = casterSession.Character with { X = destinationX, Y = destinationY },
        };
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            shiftedCasterSession,
            worldData,
            inRangeMonster,
            monsterFamily,
            spell);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            shiftedCasterSession,
            inRangePlayerSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            shiftedCasterSession,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(
                CreatePounceRuntimeQuery(destinationX, destinationY)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        AssertPounceSourceAnimation(animation, destinationX, destinationY, expectedSoul: 3);
        Assert.Equal(
            [inRangeMonster.Uid, TargetPlayerUid, checked((uint)inRangeSobNpc.Id)],
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, animation.Targets[0].Damage);
        Assert.Equal(expectedPlayerDamage, animation.Targets[1].Damage);
        Assert.Equal(expectedSobNpcDamage, animation.Targets[2].Damage);
        Assert.All(animation.Targets, target => Assert.True(target.Hit));
        Assert.All(animation.Targets, target => Assert.Equal(MsgAttackEffect.None, target.Effect));
        Assert.All(animation.Targets, target => Assert.Equal(0, target.NewX));
        Assert.All(animation.Targets, target => Assert.Equal(0, target.NewY));

        Assert.True(monsterStates.TryGetVisibleInstance(inRangeMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(inRangeMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.Equal(
            inRangeSobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(inRangeSobNpc).Life);
        Assert.Equal(outsideSobNpc.Life, sobNpcStates.GetVisible(outsideSobNpc).Life);
        Assert.True(sessions.TryGet(inRangePlayerConnection, out var damagedPlayer));
        Assert.NotNull(damagedPlayer);
        Assert.Equal(100_000 - expectedPlayerDamage, damagedPlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(outsidePlayerConnection, out var untouchedPlayer));
        Assert.NotNull(untouchedPlayer);
        Assert.Equal(100_000, untouchedPlayer.Character.MinHitPoints);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(destinationX, updatedCaster.Character.X);
        Assert.Equal(destinationY, updatedCaster.Character.Y);
        Assert.Equal("50", updatedCaster.Character.Attributes["Stamina"]);
        Assert.True(updatedCaster.Character.Experience > 0);
        Assert.True(Assert.Single(updatedCaster.PlayerSpells.Spells).Experience > 0);
        AssertNoPounceMovementFrame(casterSent);

        var staminaIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 50)]));
        var experienceIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        var spellProgressIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == PounceRuntimeSpellId &&
                packet.Experience > 0);
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == PounceRuntimeSpellId);
        Assert.True(staminaIndex < experienceIndex);
        Assert.True(experienceIndex < spellProgressIndex);
        Assert.True(spellProgressIndex < animationIndex);
    }

    private static MagicType CreatePounceRuntimeRow(
        int rowId = PounceRuntimeSpellId,
        byte level = 0,
        uint? experience = null)
    {
        var rawDamage = level == 0 ? 30_030U : 30_035U;
        var sourceExperience = level == 0 ? 500U : 1_000U;
        var needLevel = level == 0 ? (ushort)60 : (ushort)70;
        var damage2 = level == 0 ? 30_200 : 30_220;
        var damageOnMonster = level == 0 ? 30_300 : 30_320;
        var template = CreateMagicRow(
            PounceRuntimeSpellId,
            PounceRuntimeSort,
            level,
            useMana: 0,
            damage: rawDamage,
            experience: experience ?? sourceExperience,
            maxTargets: 5,
            range: 20,
            needLevel: needLevel,
            weaponType: 900,
            useStamina: 50,
            coolDown: 800,
            coldTime: 0,
            damageOnHuman: 12,
            damage2: damage2,
            damageOnMonster: damageOnMonster);
        var fields = template.Fields.ToArray();
        fields[0] = rowId.ToString(CultureInfo.InvariantCulture);
        fields[1] = PounceRuntimeSpellId.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Pounce";
        fields[4] = "1";
        fields[5] = "1";
        fields[7] = "4";
        fields[12] = "100";
        fields[19] = level == 0 ? "3000" : "6000";
        fields[30] = "1";
        fields[48] = level == 0 ? "800" : "2700";
        return new MagicType(rowId, PounceRuntimeSpellId, "Pounce", fields);
    }

    private static PlayerItemSnapshot CreatePounceEquipment(bool mainShield, bool alternateShield)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 1,
                    ItemId = mainShield ? PounceShieldItemId : PounceNonShieldItemId,
                    Durability = 100,
                    MaximumDurability = 100,
                    Position = 5,
                },
                new PlayerItemSnapshotItem
                {
                    Uid = 2,
                    ItemId = alternateShield ? PounceShieldItemId : PounceNonShieldItemId,
                    Durability = 100,
                    MaximumDurability = 100,
                    Position = 55,
                },
            ],
            RuneItems: []);
    }

    private static MsgAttackQuery CreatePounceRuntimeQuery(ushort x = 840, ushort y = 590)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: 0,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: PounceRuntimeSpellId);
    }

    private static void AssertPounceSourceAnimation(
        LegacySpellAnimationPacket animation,
        ushort expectedX,
        ushort expectedY,
        byte expectedSoul)
    {
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(expectedX, animation.X);
        Assert.Equal(expectedY, animation.Y);
        Assert.Equal(PounceRuntimeSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(0, animation.NextSpell);
        Assert.Equal(expectedSoul, animation.LevelHu);
        Assert.Equal(0, animation.SpellEffect);
    }

    private static void AssertNoPounceMovementFrame(IReadOnlyList<byte[]> frames)
    {
        Assert.Empty(ParseMsgActions(frames));
        Assert.DoesNotContain(
            frames,
            bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
                attack.Query.AttackType == MsgAttackType.InMoveSpell);
    }
}
