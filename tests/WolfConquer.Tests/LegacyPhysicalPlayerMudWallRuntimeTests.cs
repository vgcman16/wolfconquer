using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort PhysicalMudWallSpellId = 16_440;
    private const int PhysicalMudWallStatusFlag = 0x112;
    private const int PhysicalMudWallDragonSwingStatusFlag = 0xA0;
    private const int PhysicalMudWallSigilType = 301;
    private const int PhysicalMudWallSigilLevel = 7;
    private const int PhysicalMudWallSigilPower = 1_700;
    private const uint PhysicalMudWallForcedDamage = 24;
    private const long PhysicalMudWallTargetHitpoints = 1_000_000_000;
    private const string PhysicalMudWallOldRollAttribute = "LegacyMudWallRoll";
    private const string PhysicalMudWallDragonSwingRollAttribute = "LegacyPhysicalDragonSwingRoll";

    [Theory]
    [InlineData(ShurikenVortexSpellId, 3U)]
    [InlineData(ManiacDanceRuntimeSpellId, 20U)]
    public async Task AutomaticPhysicalMudWallAppliesOneDirectReductionBeforeCallerDivision(
        ushort attackSpellId,
        uint expectedDamage)
    {
        var result = await RunAutomaticPhysicalMudWallAsync(attackSpellId, new PhysicalMudWallOptions());

        AssertPhysicalMudWallDamage(result, expectedDamage);
        if (attackSpellId == ShurikenVortexSpellId)
        {
            Assert.NotEqual(PhysicalMudWallForcedDamage / 6, expectedDamage);
        }
    }

    [Theory]
    [InlineData("normal-active", true)]
    [InlineData("alternate-active", true)]
    [InlineData("alternate-inactive", false)]
    [InlineData("missing-flag", false)]
    [InlineData("missing-item", false)]
    [InlineData("wrong-item-type", false)]
    [InlineData("zero-level", false)]
    [InlineData("missing-gouyu-row", false)]
    [InlineData("wrong-class", false)]
    public async Task AutomaticPhysicalMudWallRequiresFlagNinjaAndActiveMudSigilImpeccable(
        string scenario,
        bool expectedReduction)
    {
        var options = scenario switch
        {
            "normal-active" => new PhysicalMudWallOptions(),
            "alternate-active" => new PhysicalMudWallOptions(
                MudSigilPosition: 9,
                AlternateEquipmentActive: true),
            "alternate-inactive" => new PhysicalMudWallOptions(MudSigilPosition: 9),
            "missing-flag" => new PhysicalMudWallOptions(HasMudWallFlag: false),
            "missing-item" => new PhysicalMudWallOptions(IncludeMudSigil: false),
            "wrong-item-type" => new PhysicalMudWallOptions(MudSigilType: 302),
            "zero-level" => new PhysicalMudWallOptions(MudSigilLevel: 0),
            "missing-gouyu-row" => new PhysicalMudWallOptions(IncludeGouyuRow: false),
            "wrong-class" => new PhysicalMudWallOptions(TargetClassCode: 142),
            _ => throw new InvalidOperationException($"Unknown MudWall scenario '{scenario}'."),
        };

        var result = await RunAutomaticPhysicalMudWallAsync(ManiacDanceRuntimeSpellId, options);

        AssertPhysicalMudWallDamage(result, expectedReduction ? 20U : PhysicalMudWallForcedDamage);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId, 4U)]
    [InlineData(ManiacDanceRuntimeSpellId, PhysicalMudWallForcedDamage)]
    public async Task AutomaticPhysicalMudWallIgnoresSource7770LearnedSpellChanceWithoutType301(
        ushort attackSpellId,
        uint expectedDamage)
    {
        var result = await RunAutomaticPhysicalMudWallAsync(
            attackSpellId,
            new PhysicalMudWallOptions(
                IncludeMudSigil: false,
                IncludeLearnedMudWall: true));

        AssertPhysicalMudWallDamage(result, expectedDamage);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId, LegacyShurikenVortexFlag, 0U)]
    [InlineData(ManiacDanceRuntimeSpellId, ManiacDanceRuntimeFlag, 1U)]
    public async Task AutomaticPhysicalMudWallStatusDefenseEarlyReturnSkipsTail(
        ushort attackSpellId,
        int defenderStatusFlag,
        uint expectedDamage)
    {
        var result = await RunAutomaticPhysicalMudWallAsync(
            attackSpellId,
            new PhysicalMudWallOptions(
                MudSigilPower: 10_000,
                ForcedPostIronShieldDamage: null,
                DefenderStatusFlag: defenderStatusFlag));

        AssertPhysicalMudWallDamage(result, expectedDamage);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId, 0U)]
    [InlineData(ManiacDanceRuntimeSpellId, 1U)]
    public async Task AutomaticPhysicalMudWallWildPhoenixEarlyReturnSkipsTail(
        ushort attackSpellId,
        uint expectedDamage)
    {
        var result = await RunAutomaticPhysicalMudWallAsync(
            attackSpellId,
            new PhysicalMudWallOptions(
                MudSigilPower: 10_000,
                ForcedPostIronShieldDamage: null,
                WildPhoenixDefended: true));

        AssertPhysicalMudWallDamage(result, expectedDamage);
        Assert.Equal(
            "1",
            result.Target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalMudWallLeavesDragonSwingZeroUnchanged(ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalMudWallAsync(
            attackSpellId,
            new PhysicalMudWallOptions(DragonSwingDefended: true));

        AssertPhysicalMudWallDamage(result, 0);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId, 3U)]
    [InlineData(ManiacDanceRuntimeSpellId, 20U)]
    public async Task AutomaticPhysicalMudWallRunsAfterHeavensWonderBackDamageAndBeforeCallerDivision(
        ushort attackSpellId,
        uint expectedDamage)
    {
        var result = await RunAutomaticPhysicalMudWallAsync(
            attackSpellId,
            new PhysicalMudWallOptions(EnableHeavensWonderBackDamage: true));

        AssertPhysicalMudWallDamage(result, expectedDamage);
        var backDamage = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.AttackerUid == TargetPlayerUid &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var counterTarget = Assert.Single(backDamage.Targets);
        Assert.Equal(PlayerUid, counterTarget.Uid);
        var backDamageIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.AttackerUid == TargetPlayerUid &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var outerAnimationIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.AttackerUid == PlayerUid &&
                animation.SpellId == attackSpellId);
        Assert.True(backDamageIndex < outerAnimationIndex);
    }

    private static async Task<PhysicalMudWallRunResult> RunAutomaticPhysicalMudWallAsync(
        ushort attackSpellId,
        PhysicalMudWallOptions options)
    {
        var isShuriken = attackSpellId == ShurikenVortexSpellId;
        Assert.True(isShuriken || attackSpellId == ManiacDanceRuntimeSpellId);
        var now = DateTimeOffset.Parse("2026-07-19T20:00:00Z", CultureInfo.InvariantCulture);

        var magicRows = isShuriken
            ? new List<MagicType>
            {
                CreateShurikenVortexAutomaticContractRow(),
                CreateShurikenVortexDamageRow(),
            }
            : [CreateManiacDanceRuntimeRow()];
        if (options.IncludeLearnedMudWall)
        {
            magicRows.Add(CreatePhysicalMudWallLearnedSpellRow());
        }
        if (options.EnableHeavensWonderBackDamage)
        {
            magicRows.Add(CreateCounterKillRuntimeRow(
                HeavensWonderRuntimeSpellId,
                coolDown: 0,
                coldTime: 1_000));
        }

        var worldData = CreateMagicCatalogWithItems(
            CreateHighLifeMonsterFamily(),
            [],
            [],
            magicRows.ToArray());
        if (options.IncludeGouyuRow)
        {
            AddPhysicalMudWallGouyuRow(
                worldData,
                CreatePhysicalMudWallGouyuRow(
                    PhysicalMudWallSigilType,
                    options.GouyuLevel,
                    options.MudSigilPower));
        }

        var casterAttributes = isShuriken
            ? CreateActiveShurikenVortexContractAttributes(
                now.AddSeconds(10),
                now.AddTicks(-1))
            : CreateActiveManiacDanceAttributes(
                now.AddSeconds(10),
                now.AddTicks(-1));
        var caster = CreateSession(
            strength: 100_000,
            minHitPoints: PhysicalMudWallTargetHitpoints,
            spellIds: [attackSpellId],
            attributes: casterAttributes);

        var targetFlags = new List<int>();
        if (options.HasMudWallFlag)
        {
            targetFlags.Add(PhysicalMudWallStatusFlag);
        }
        if (options.ForcedPostIronShieldDamage is not null)
        {
            targetFlags.Add(IronShieldStatusFlag);
        }
        if (options.DefenderStatusFlag > 0)
        {
            targetFlags.Add(options.DefenderStatusFlag);
        }
        if (options.WildPhoenixDefended)
        {
            targetFlags.Add(LegacyWildPhoenixFlag);
        }
        if (options.DragonSwingDefended)
        {
            targetFlags.Add(PhysicalMudWallDragonSwingStatusFlag);
        }

        var targetAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(targetFlags.ToArray()),
            [CounterKill16350BackDamageRollAttribute] = "0",
            [ManiacDanceHeavensWonderRollAttribute] = "0",
            [PhysicalMudWallOldRollAttribute] = "0",
        };
        if (options.IncludeMudSigil)
        {
            targetAttributes["MsgNinja"] = BuildPhysicalMudWallMsgNinja(
                checked((uint)options.MudSigilType),
                options.MudSigilLevel,
                options.MudSigilPosition);
        }
        if (options.WildPhoenixDefended)
        {
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2";
        }
        if (options.DragonSwingDefended)
        {
            targetAttributes[WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute] = "100";
            targetAttributes[PhysicalMudWallDragonSwingRollAttribute] = "0";
        }

        var targetSpellIds = new List<ushort>();
        if (options.IncludeLearnedMudWall)
        {
            targetSpellIds.Add(PhysicalMudWallSpellId);
        }
        if (options.WildPhoenixDefended)
        {
            targetSpellIds.Add(WildPhoenixSpellId);
        }
        if (options.EnableHeavensWonderBackDamage)
        {
            targetSpellIds.Add(HeavensWonderRuntimeSpellId);
        }

        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "PhysicalMudWallTarget",
            x: 831,
            y: 584,
            minHitPoints: PhysicalMudWallTargetHitpoints,
            spellIds: targetSpellIds,
            attributes: targetAttributes,
            classCode: options.TargetClassCode);
        target = target with { AlternateEquipmentActive = options.AlternateEquipmentActive };

        if (options.ForcedPostIronShieldDamage is { } forcedDamage)
        {
            LegacyMagicRuntimeSpell damageSpell;
            if (isShuriken)
            {
                Assert.True(LegacyMagicRuntime.TryResolveSpellByRowId(
                    worldData,
                    ShurikenVortexDamageRowId,
                    out damageSpell));
            }
            else
            {
                Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
                    caster.PlayerSpells,
                    worldData,
                    ManiacDanceRuntimeSpellId,
                    out _,
                    out damageSpell));
            }

            var rawPhysicalDamage = InvokePrivateUInt32(
                "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
                caster,
                target,
                damageSpell,
                worldData,
                1U);
            var preIronShieldDamage = checked(rawPhysicalDamage + (rawPhysicalDamage * 50 / 100));
            Assert.True(
                preIronShieldDamage > forcedDamage,
                "The MudWall fixture must leave a strict IronShield overflow for the forced damage.");
            targetAttributes["IronShieldDamage"] =
                (preIronShieldDamage - forcedDamage).ToString(CultureInfo.InvariantCulture);
            targetAttributes["LegacyIronShieldExpiresAtUtc"] = now.AddMinutes(1).ToString("O");
            targetAttributes["LegacyIronShieldRemoveOnDead"] = bool.TrueString;
            target = target with
            {
                Character = target.Character with { Attributes = targetAttributes },
            };
        }

        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: null,
            SobNpcStates: null);
        var sentAttack = isShuriken
            ? await WorldPacketHandlers.TrySendDueLegacyShurikenVortexAttackAsync(
                context,
                now,
                CancellationToken.None)
            : await WorldPacketHandlers.TrySendDueLegacyManiacDanceAttackAsync(
                context,
                now,
                CancellationToken.None);
        Assert.True(sentAttack, log.ToString());

        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        var outerAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.AttackerUid == PlayerUid && animation.SpellId == attackSpellId);
        return new PhysicalMudWallRunResult(
            attackSpellId,
            outerAnimation,
            finalTarget,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static void AssertPhysicalMudWallDamage(PhysicalMudWallRunResult result, uint expectedDamage)
    {
        var target = Assert.Single(result.OuterAnimation.Targets);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(
            PhysicalMudWallTargetHitpoints - expectedDamage,
            result.Target.Character.MinHitPoints);
    }

    private static MagicType CreatePhysicalMudWallLearnedSpellRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = PhysicalMudWallSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = PhysicalMudWallSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = ((byte)LegacyMagicSort.AttachStatus).ToString(CultureInfo.InvariantCulture);
        fields[3] = "MudWall";
        fields[8] = "2";
        fields[14] = "1";
        fields[15] = "18";
        fields[20] = "1";
        fields[35] = "10000";
        fields[36] = "10000";
        return new MagicType(
            PhysicalMudWallSpellId,
            PhysicalMudWallSpellId,
            "MudWall",
            fields);
    }

    private static GouyuType CreatePhysicalMudWallGouyuRow(int type, int level, int power)
    {
        var values = Enumerable.Repeat(0, 10).ToArray();
        values[5] = power;
        return new GouyuType(
            Id: type * 100 + level,
            Type: type,
            Level: level,
            Values: values,
            SourceFile: "fixture://physical-mud-wall-gouyu-type",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }

    private static void AddPhysicalMudWallGouyuRow(WorldDataCatalog worldData, GouyuType row)
    {
        var byId = Assert.IsAssignableFrom<IDictionary<int, GouyuType>>(worldData.GouyuTypesById);
        var byType = Assert.IsAssignableFrom<IDictionary<int, IReadOnlyList<GouyuType>>>(
            worldData.GouyuTypesByType);
        var byLevel = Assert.IsAssignableFrom<IDictionary<int, IReadOnlyList<GouyuType>>>(
            worldData.GouyuTypesByLevel);
        byId[row.Id] = row;
        byType[row.Type] = [row];
        byLevel[row.Level] = [row];
    }

    private static string BuildPhysicalMudWallMsgNinja(uint itemType, uint level, uint position)
    {
        var fields = Enumerable.Repeat("0", 20)
            .Concat(
            [
                itemType.ToString(CultureInfo.InvariantCulture),
                level.ToString(CultureInfo.InvariantCulture),
                position.ToString(CultureInfo.InvariantCulture),
                "0",
                "0",
            ]);
        return string.Join('/', fields) + "/";
    }

    private sealed record PhysicalMudWallOptions(
        bool HasMudWallFlag = true,
        bool IncludeMudSigil = true,
        int MudSigilType = PhysicalMudWallSigilType,
        uint MudSigilLevel = PhysicalMudWallSigilLevel,
        uint MudSigilPosition = 1,
        bool AlternateEquipmentActive = false,
        bool IncludeGouyuRow = true,
        int GouyuLevel = PhysicalMudWallSigilLevel,
        int MudSigilPower = PhysicalMudWallSigilPower,
        int TargetClassCode = 5_005,
        uint? ForcedPostIronShieldDamage = PhysicalMudWallForcedDamage,
        int DefenderStatusFlag = 0,
        bool WildPhoenixDefended = false,
        bool DragonSwingDefended = false,
        bool IncludeLearnedMudWall = false,
        bool EnableHeavensWonderBackDamage = false);

    private sealed record PhysicalMudWallRunResult(
        ushort AttackSpellId,
        LegacySpellAnimationPacket OuterAnimation,
        WorldSession Target,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
