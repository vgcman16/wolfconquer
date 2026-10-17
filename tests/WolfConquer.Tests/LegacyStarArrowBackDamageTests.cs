using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint RangeStatusPhysicalIncreaseItemId = 201_104;
    private const uint RangeStatusPhysicalDecreaseItemId = 202_104;
    private const uint RangeStatusPhysicalDamageIncrease = 700;
    private const uint RangeStatusPhysicalDamageDecrease = 200;
    private const uint RangeStatusExpectedExtraAttack =
        RangeStatusPhysicalDamageIncrease - RangeStatusPhysicalDamageDecrease;

    [Fact]
    public async Task HandlerStarArrowBackDamageCastsBeforeIncomingAnimationAndCarriesStateAndCallerScaling()
    {
        var result = await RunStarArrowBackDamageAttackAsync();

        var proc = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, proc.AttackerUid);
        Assert.Equal((0, 0), (proc.X, proc.Y));
        Assert.Equal(
            [result.Monster.Uid, PlayerUid],
            proc.Targets.Select(target => target.Uid).ToArray());

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == StarArrowRuntimeSpellId);
        var incomingTarget = Assert.Single(incoming.Targets);
        Assert.Equal(TargetPlayerUid, incomingTarget.Uid);
        Assert.Equal(result.ExpectedNormalDamage, incomingTarget.Damage);
        Assert.Equal(100_000L - result.ExpectedNormalDamage, result.TargetRemainingHitpoints);

        var procIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == StarArrowRuntimeSpellId);
        Assert.True(
            procIndex < incomingIndex,
            "Matching Range.OnPlayer invokes BackDmg before StarArrow sends its incoming animation.");

        Assert.True(result.MonsterStates.TryGetVisibleInstance(
            result.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < result.Monster.MaxLife);
        Assert.True(result.AttackerRemainingHitpoints < 100_000);
        Assert.Contains(HeavensWonderRuntimeSpellId, result.TargetMagicCooldowns.Keys);
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerStarArrowRangeStatusReturnUsesZeroBaseExtraAttackBeforeWildPhoenixAndBackDamage(
        int targetStatusFlag)
    {
        var result = await RunStarArrowBackDamageAttackAsync(
            targetStatusFlag,
            wildPhoenixCharges: 1,
            useRangeStatusDamage: true);

        AssertNoStarArrowBackDamageProc(result);
        var target = AssertStarArrowIncomingTarget(result);
        Assert.Equal(RangeStatusExpectedExtraAttack, target.Damage);
        Assert.True(target.Damage > 1);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(100_000L - RangeStatusExpectedExtraAttack, result.TargetRemainingHitpoints);
        Assert.Equal("1", result.RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerStarArrowBackDamageWildPhoenixReturnPrecedesDefensiveStanceAndDoesNotProc()
    {
        var result = await RunStarArrowBackDamageAttackAsync(
            DefensiveStanceStatusFlag,
            wildPhoenixCharges: 1);

        AssertNoStarArrowBackDamageProc(result);
        var target = AssertStarArrowIncomingTarget(result);
        Assert.Equal(1U, target.Damage);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(99_999, result.TargetRemainingHitpoints);
        Assert.Equal("0", result.RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerStarArrowBackDamageDefensiveStanceReturnDoesNotProc()
    {
        var result = await RunStarArrowBackDamageAttackAsync(DefensiveStanceStatusFlag);
        var reduction = result.ExpectedNormalDamage * 40 / 100;
        var expectedDamage = result.ExpectedNormalDamage <= reduction
            ? 1U
            : result.ExpectedNormalDamage - reduction;

        AssertNoStarArrowBackDamageProc(result);
        var target = AssertStarArrowIncomingTarget(result);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(100_000L - expectedDamage, result.TargetRemainingHitpoints);
    }

    private static LegacySpellAnimationTarget AssertStarArrowIncomingTarget(
        StarArrowBackDamageResult result)
    {
        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == StarArrowRuntimeSpellId);
        return Assert.Single(incoming.Targets);
    }

    private static void AssertNoStarArrowBackDamageProc(StarArrowBackDamageResult result)
    {
        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.TargetSent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.DoesNotContain(HeavensWonderRuntimeSpellId, result.TargetMagicCooldowns.Keys);
    }

    private static async Task<StarArrowBackDamageResult> RunStarArrowBackDamageAttackAsync(
        int? targetStatusFlag = null,
        uint wildPhoenixCharges = 0,
        bool useRangeStatusDamage = false)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [spawn],
            useRangeStatusDamage ? CreateRangeStatusPhysicalDamageItems() : [],
            CreateStarArrowRuntimeRow(range: 9),
            CreateCounterKillRuntimeRow(
                HeavensWonderRuntimeSpellId,
                coolDown: 0,
                coldTime: 1_000),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var targetAttributes = new Dictionary<string, string>
        {
            [CounterKill16350BackDamageRollAttribute] = 0.ToString(CultureInfo.InvariantCulture),
        };
        var targetStatusFlags = new List<int>();
        if (targetStatusFlag is not null)
        {
            targetStatusFlags.Add(targetStatusFlag.Value);
        }

        var targetSpellIds = new List<ushort> { HeavensWonderRuntimeSpellId };
        if (wildPhoenixCharges > 0)
        {
            targetSpellIds.Add(WildPhoenixSpellId);
            targetStatusFlags.Add(LegacyWildPhoenixFlag);
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] =
                wildPhoenixCharges.ToString(CultureInfo.InvariantCulture);
        }

        if (targetStatusFlags.Count > 0)
        {
            targetAttributes["StatusFlags"] = BuildStatusFlags(targetStatusFlags.ToArray());
        }

        const byte useSpellSoul = 3;
        var attackerSession = CreateSession(
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    StarArrowRuntimeSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: useSpellSoul),
            ],
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalIncreaseItemId, position: 10)
                : PlayerItemSnapshot.Empty);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "StarArrowBackDamageTarget",
            x: 836,
            y: 584,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: targetSpellIds,
            attributes: targetAttributes,
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalDecreaseItemId, position: 11)
                : PlayerItemSnapshot.Empty);
        var pulledTargetSession = targetSession with
        {
            Character = targetSession.Character with
            {
                X = attackerSession.Character.X,
                Y = attackerSession.Character.Y,
            },
        };

        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var attackerSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var attackerConnection = CreateSendingConnection(attackerSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(attackerConnection, attackerSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            attackerConnection,
            log,
            attackerSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveStarArrowRuntimeSpell(
            attackerSession,
            worldData,
            StarArrowRuntimeSpellId);
        var rangeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attackerSession,
            pulledTargetSession,
            spell,
            worldData,
            1U);
        var expectedNormalDamage = InvokePrivateUInt32(
            "CalculateLegacyAttackStatus7000SoulDamage",
            rangeDamage,
            useSpellSoul);

        Assert.True(await InvokeLegacyStarArrowAsync(
            context,
            attackerSession,
            CreateStarArrowRuntimeQuery(
                StarArrowRuntimeSpellId,
                TargetPlayerUid,
                x: checked((ushort)targetSession.Character.X),
                y: checked((ushort)targetSession.Character.Y)),
            playerSpell,
            spell));

        Assert.True(sessions.TryGet(attackerConnection, out var finalAttacker));
        Assert.NotNull(finalAttacker);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        finalTarget.Character.Attributes.TryGetValue(
            RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute,
            out var remainingWildPhoenixCharges);
        return new StarArrowBackDamageResult(
            monster,
            monsterStates,
            attackerSent,
            targetSent,
            expectedNormalDamage,
            finalAttacker.Character.MinHitPoints,
            finalTarget.Character.MinHitPoints,
            finalTarget.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>(),
            remainingWildPhoenixCharges);
    }

    private static IReadOnlyList<ItemType> CreateRangeStatusPhysicalDamageItems()
    {
        var increaseFields = Enumerable.Repeat("0", 64).ToArray();
        increaseFields[0] = RangeStatusPhysicalIncreaseItemId.ToString(CultureInfo.InvariantCulture);
        increaseFields[1] = "RangeStatusPhysicalIncrease";
        increaseFields[13] = RangeStatusPhysicalDamageIncrease.ToString(CultureInfo.InvariantCulture);

        var decreaseFields = Enumerable.Repeat("0", 64).ToArray();
        decreaseFields[0] = RangeStatusPhysicalDecreaseItemId.ToString(CultureInfo.InvariantCulture);
        decreaseFields[1] = "RangeStatusPhysicalDecrease";
        decreaseFields[15] = RangeStatusPhysicalDamageDecrease.ToString(CultureInfo.InvariantCulture);

        return
        [
            new ItemType(
                checked((int)RangeStatusPhysicalIncreaseItemId),
                increaseFields[1],
                increaseFields),
            new ItemType(
                checked((int)RangeStatusPhysicalDecreaseItemId),
                decreaseFields[1],
                decreaseFields),
        ];
    }

    private static PlayerItemSnapshot CreateRangeStatusEquipment(uint itemId, ushort position)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = itemId,
                    Durability = 100,
                    MaximumDurability = 100,
                    Position = position,
                },
            ],
            RuneItems: []);
    }

    private sealed record StarArrowBackDamageResult(
        LegacyMonsterInstance Monster,
        MonsterRuntimeStateStore MonsterStates,
        IReadOnlyList<byte[]> AttackerSent,
        IReadOnlyList<byte[]> TargetSent,
        uint ExpectedNormalDamage,
        long AttackerRemainingHitpoints,
        long TargetRemainingHitpoints,
        IReadOnlyDictionary<ushort, DateTimeOffset> TargetMagicCooldowns,
        string? RemainingWildPhoenixCharges);
}
