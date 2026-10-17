using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint PhysicalTailNoMercyRuneItemId = 4_031_001;
    private const uint PhysicalTailAssassinRuneItemId = 4_010_301;
    private const long PhysicalTailTargetHitpoints = 1_000_000_000;

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalTailRunsAfterIronShieldAndBeforeBackDamageAndCallerDivision(
        ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            attackSpellId,
            new PhysicalPlayerTailOptions(
                SlayerPercent: 200,
                EquipNoMercyRune: true,
                EquipAssassinRune: true,
                OnAutoHunt: true,
                ForcedPostIronShieldDamage: 1));

        var target = Assert.Single(result.Animation.Targets);
        Assert.Equal(ApplyPhysicalTailCallerDamage(attackSpellId, 60_000), target.Damage);
        Assert.Equal(PhysicalTailTargetHitpoints - target.Damage, result.Targets[0].Character.MinHitPoints);

        var blueDamage = AssertSinglePhysicalTailBlueDamage(result.TargetSent[0]);
        Assert.Equal(result.PreIronShieldDamage, checked((uint)blueDamage.Query.Damage));
        Assert.Equal("0", result.Targets[0].Character.Attributes["IronShieldDamage"]);
        AssertPhysicalTailSlayerConsumedBeforeAnimation(result);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalSlayerScalesAndConsumesWithoutRunes(ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            attackSpellId,
            new PhysicalPlayerTailOptions(SlayerPercent: 200));

        var target = Assert.Single(result.Animation.Targets);
        var expectedTailDamage = checked(result.PreIronShieldDamage * 2);
        Assert.Equal(ApplyPhysicalTailCallerDamage(attackSpellId, expectedTailDamage), target.Damage);
        Assert.Empty(result.Caster.PlayerItems.RuneItems);
        AssertPhysicalTailSlayerConsumedBeforeAnimation(result);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId, false, 2L, 120L, true)]
    [InlineData(ManiacDanceRuntimeSpellId, true, 1L, 120L, true)]
    [InlineData(ShurikenVortexSpellId, true, 2L, 14L, true)]
    [InlineData(ManiacDanceRuntimeSpellId, true, 2L, 120L, false)]
    public async Task AutomaticPhysicalRunesRequireEquipmentEligibilityAndAssassinRequiresAutoHunt(
        ushort attackSpellId,
        bool equipAssassinRune,
        long reborn,
        long level,
        bool onAutoHunt)
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            attackSpellId,
            new PhysicalPlayerTailOptions(
                EquipNoMercyRune: true,
                EquipAssassinRune: equipAssassinRune,
                OnAutoHunt: onAutoHunt,
                Reborn: reborn,
                Level: level,
                ForcedPostIronShieldDamage: 1));

        var runeEligible = reborn >= 2 && level >= 15;
        var expectedTailDamage = runeEligible ? 22_000U : 1U;
        var target = Assert.Single(result.Animation.Targets);
        Assert.Equal(ApplyPhysicalTailCallerDamage(attackSpellId, expectedTailDamage), target.Damage);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalRunesIgnoreInactiveAlternateRuneSet(ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            attackSpellId,
            new PhysicalPlayerTailOptions(
                EquipNoMercyRune: true,
                NoMercyRunePosition: 121,
                ForcedPostIronShieldDamage: 1));

        var target = Assert.Single(result.Animation.Targets);
        Assert.Equal(ApplyPhysicalTailCallerDamage(attackSpellId, 1), target.Damage);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalRunesRequireMatchingRuneJob(ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            attackSpellId,
            new PhysicalPlayerTailOptions(
                EquipNoMercyRune: true,
                RuneRequiredClass: 16_000,
                ForcedPostIronShieldDamage: 1));

        var target = Assert.Single(result.Animation.Targets);
        Assert.Equal(ApplyPhysicalTailCallerDamage(attackSpellId, 1), target.Damage);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalNoMercyLeavesExactly22000Unchanged(ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            attackSpellId,
            new PhysicalPlayerTailOptions(
                EquipNoMercyRune: true,
                ForcedPostIronShieldDamage: 22_000));

        var target = Assert.Single(result.Animation.Targets);
        Assert.Equal(ApplyPhysicalTailCallerDamage(attackSpellId, 22_000), target.Damage);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalZeroPercentSlayerConsumesFlagAndLeavesZeroDamage(ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            attackSpellId,
            new PhysicalPlayerTailOptions(SlayerPercent: 0));

        var target = Assert.Single(result.Animation.Targets);
        Assert.Equal(0U, target.Damage);
        Assert.Equal(PhysicalTailTargetHitpoints, result.Targets[0].Character.MinHitPoints);
        AssertPhysicalTailSlayerConsumedBeforeAnimation(result);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId, LegacyShurikenVortexFlag)]
    [InlineData(ManiacDanceRuntimeSpellId, ManiacDanceRuntimeFlag)]
    public async Task AutomaticPhysicalStatusDefenseReturnsBeforeTailAndPreservesSlayer(
        ushort attackSpellId,
        int defenderStatusFlag)
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            attackSpellId,
            new PhysicalPlayerTailOptions(
                SlayerPercent: 200,
                EquipNoMercyRune: true,
                EquipAssassinRune: true,
                OnAutoHunt: true,
                DefenderStatusFlag: defenderStatusFlag));

        var target = Assert.Single(result.Animation.Targets);
        Assert.Equal(ApplyPhysicalTailCallerDamage(attackSpellId, 1), target.Damage);
        AssertPhysicalTailSlayerPreserved(result);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalWildPhoenixReturnsBeforeTailAndPreservesSlayer(ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            attackSpellId,
            new PhysicalPlayerTailOptions(
                SlayerPercent: 200,
                EquipNoMercyRune: true,
                EquipAssassinRune: true,
                OnAutoHunt: true,
                WildPhoenixDefended: true));

        var target = Assert.Single(result.Animation.Targets);
        Assert.Equal(ApplyPhysicalTailCallerDamage(attackSpellId, 1), target.Damage);
        Assert.Equal(
            "1",
            result.Targets[0].Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        AssertPhysicalTailSlayerPreserved(result);
    }

    [Fact]
    public async Task ShurikenVortexMultiTargetSlayerAffectsOnlyOneEligiblePlayer()
    {
        var result = await RunAutomaticPhysicalPlayerTailAsync(
            ShurikenVortexSpellId,
            new PhysicalPlayerTailOptions(
                SlayerPercent: 200,
                TargetCount: 2));

        Assert.Equal(2, result.Animation.Targets.Count);
        var expectedDamages = new[]
        {
            ApplyPhysicalTailCallerDamage(ShurikenVortexSpellId, result.PreIronShieldDamage),
            ApplyPhysicalTailCallerDamage(
                ShurikenVortexSpellId,
                checked(result.PreIronShieldDamage * 2)),
        };
        Assert.Equal(
            expectedDamages.Order().ToArray(),
            result.Animation.Targets.Select(target => target.Damage).Order().ToArray());
        AssertPhysicalTailSlayerConsumedBeforeAnimation(result);
    }

    private static async Task<PhysicalPlayerTailRunResult> RunAutomaticPhysicalPlayerTailAsync(
        ushort attackSpellId,
        PhysicalPlayerTailOptions options)
    {
        var isShuriken = attackSpellId == ShurikenVortexSpellId;
        Assert.True(isShuriken || attackSpellId == ManiacDanceRuntimeSpellId);
        Assert.InRange(options.TargetCount, 1, 2);

        var now = DateTimeOffset.Parse("2026-07-19T20:00:00Z", CultureInfo.InvariantCulture);
        var runeCatalogItems = new List<ItemType>();
        var runeItems = new List<PlayerItemSnapshotItem>();
        if (options.EquipNoMercyRune)
        {
            runeCatalogItems.Add(CreatePhysicalTailRuneItemType(
                PhysicalTailNoMercyRuneItemId,
                "NoMercy",
                options.RuneRequiredClass));
            runeItems.Add(new PlayerItemSnapshotItem
            {
                ItemId = PhysicalTailNoMercyRuneItemId,
                Position = options.NoMercyRunePosition,
            });
        }

        if (options.EquipAssassinRune)
        {
            runeCatalogItems.Add(CreatePhysicalTailRuneItemType(
                PhysicalTailAssassinRuneItemId,
                "Assassin",
                options.RuneRequiredClass));
            runeItems.Add(new PlayerItemSnapshotItem
            {
                ItemId = PhysicalTailAssassinRuneItemId,
                Position = 102,
            });
        }

        MagicType[] magicRows = isShuriken
            ? [CreateShurikenVortexAutomaticContractRow(), CreateShurikenVortexDamageRow()]
            : [CreateManiacDanceRuntimeRow()];
        var worldData = CreateMagicCatalogWithItems(
            CreateHighLifeMonsterFamily(),
            [],
            runeCatalogItems,
            magicRows);

        var casterAttributes = isShuriken
            ? CreateActiveShurikenVortexContractAttributes(
                now.AddSeconds(10),
                now.AddTicks(-1))
            : CreateActiveManiacDanceAttributes(
                now.AddSeconds(10),
                now.AddTicks(-1));
        var casterFlags = new List<int>
        {
            isShuriken ? LegacyShurikenVortexFlag : ManiacDanceRuntimeFlag,
        };
        if (options.SlayerPercent is { } slayerPercent)
        {
            casterFlags.Add(LegacySlayerFlag);
            casterAttributes["SlayerPercent"] = slayerPercent.ToString(CultureInfo.InvariantCulture);
        }

        casterAttributes["StatusFlags"] = BuildStatusFlags(casterFlags.ToArray());
        casterAttributes["OnAutoHunt"] = options.OnAutoHunt ? "1" : "0";
        var caster = CreateSession(
            strength: 100_000,
            minHitPoints: PhysicalTailTargetHitpoints,
            spellIds: [attackSpellId],
            attributes: casterAttributes,
            classCode: options.ClassCode,
            reborn: options.Reborn,
            playerItems: CreatePlayerItems(runeItems.ToArray()));
        caster = caster with
        {
            Character = caster.Character with { Level = options.Level },
            AlternateEquipmentActive = options.AlternateEquipmentActive,
        };

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

        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        sessions.Set(casterConnection, caster);
        var targetConnections = new List<WolfConquer.Core.Networking.ConquerTcpConnection>();
        var targetSent = new List<List<byte[]>>();
        uint? preIronShieldDamage = null;
        for (var index = 0; index < options.TargetCount; index++)
        {
            var currentSent = new List<byte[]>();
            var targetConnection = CreateSendingConnection(currentSent);
            var targetFlags = new List<int>();
            if (options.DefenderStatusFlag > 0)
            {
                targetFlags.Add(options.DefenderStatusFlag);
            }
            if (options.WildPhoenixDefended)
            {
                targetFlags.Add(LegacyWildPhoenixFlag);
            }
            if (options.ForcedPostIronShieldDamage is not null)
            {
                targetFlags.Add(IronShieldStatusFlag);
            }

            var targetAttributes = new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(targetFlags.ToArray()),
                [CounterKill16350BackDamageRollAttribute] = "0",
                [ManiacDanceHeavensWonderRollAttribute] = "0",
            };
            if (options.WildPhoenixDefended)
            {
                targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
                targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2";
            }

            var target = CreateSession(
                uid: checked(TargetPlayerUid + (uint)index),
                name: $"PhysicalTailTarget{index + 1}",
                x: 831,
                y: 584 + index,
                minHitPoints: PhysicalTailTargetHitpoints,
                spellIds: options.WildPhoenixDefended ? [WildPhoenixSpellId] : [],
                attributes: targetAttributes);
            var rawPhysicalDamage = InvokePrivateUInt32(
                "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
                caster,
                target,
                damageSpell,
                worldData,
                1U);
            var currentPreIronShieldDamage = checked(
                rawPhysicalDamage + (rawPhysicalDamage * 50 / 100));
            preIronShieldDamage ??= currentPreIronShieldDamage;
            Assert.Equal(preIronShieldDamage.Value, currentPreIronShieldDamage);

            if (options.ForcedPostIronShieldDamage is { } forcedDamage)
            {
                uint shieldDamage;
                if (forcedDamage == 1)
                {
                    shieldDamage = currentPreIronShieldDamage;
                }
                else
                {
                    Assert.True(
                        currentPreIronShieldDamage > forcedDamage,
                        "The fixture must leave a strict IronShield overflow for the requested tail damage.");
                    shieldDamage = currentPreIronShieldDamage - forcedDamage;
                }

                targetAttributes["IronShieldDamage"] = shieldDamage.ToString(CultureInfo.InvariantCulture);
                targetAttributes["LegacyIronShieldExpiresAtUtc"] = now.AddMinutes(1).ToString("O");
                targetAttributes["LegacyIronShieldRemoveOnDead"] = "true";
                target = target with
                {
                    Character = target.Character with { Attributes = targetAttributes },
                };
            }

            sessions.Set(targetConnection, target);
            targetConnections.Add(targetConnection);
            targetSent.Add(currentSent);
        }

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

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        var finalTargets = new List<WorldSession>();
        foreach (var targetConnection in targetConnections)
        {
            Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
            Assert.NotNull(finalTarget);
            finalTargets.Add(finalTarget);
        }

        var animation = Assert.Single(
            ParseSpellAnimations(casterSent),
            packet => packet.AttackerUid == PlayerUid && packet.SpellId == attackSpellId);
        return new PhysicalPlayerTailRunResult(
            attackSpellId,
            preIronShieldDamage ?? 0,
            animation,
            finalCaster,
            finalTargets,
            casterSent,
            targetSent.Select(frames => (IReadOnlyList<byte[]>)frames).ToArray());
    }

    private static uint ApplyPhysicalTailCallerDamage(ushort attackSpellId, uint tailDamage)
    {
        return attackSpellId == ShurikenVortexSpellId ? tailDamage / 6 : tailDamage;
    }

    private static ItemType CreatePhysicalTailRuneItemType(
        uint itemId,
        string name,
        uint requiredClass)
    {
        var fields = Enumerable.Repeat("0", ItemType.RequiredClassFieldIndex + 1).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.RequiredClassFieldIndex] = requiredClass.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), name, fields);
    }

    private static MsgAttackPacket AssertSinglePhysicalTailBlueDamage(IReadOnlyList<byte[]> frames)
    {
        var frame = Assert.Single(
            frames,
            candidate => MsgAttackPacket.TryParse(ReadFrame(candidate), out var packet) &&
                packet.Query.AttackType == MsgAttackType.BlueDamage);
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(frame), out var blueDamage));
        return blueDamage;
    }

    private static void AssertPhysicalTailSlayerConsumedBeforeAnimation(PhysicalPlayerTailRunResult result)
    {
        Assert.False(HasLegacyStatusFlag(result.Caster.Character.Attributes, LegacySlayerFlag));
        var removalIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, LegacySlayerFlag)));
        var animationIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.AttackerUid == PlayerUid &&
                animation.SpellId == result.AttackSpellId);
        Assert.True(removalIndex < animationIndex);
    }

    private static void AssertPhysicalTailSlayerPreserved(PhysicalPlayerTailRunResult result)
    {
        Assert.True(HasLegacyStatusFlag(result.Caster.Character.Attributes, LegacySlayerFlag));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, LegacySlayerFlag)));
    }

    private sealed record PhysicalPlayerTailOptions(
        uint? SlayerPercent = null,
        bool EquipNoMercyRune = false,
        bool EquipAssassinRune = false,
        bool OnAutoHunt = false,
        long Reborn = 2,
        long Level = 120,
        int ClassCode = 142,
        uint RuneRequiredClass = 0,
        ushort NoMercyRunePosition = 101,
        bool AlternateEquipmentActive = false,
        uint? ForcedPostIronShieldDamage = null,
        int DefenderStatusFlag = 0,
        bool WildPhoenixDefended = false,
        int TargetCount = 1);

    private sealed record PhysicalPlayerTailRunResult(
        ushort AttackSpellId,
        uint PreIronShieldDamage,
        LegacySpellAnimationPacket Animation,
        WorldSession Caster,
        IReadOnlyList<WorldSession> Targets,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<IReadOnlyList<byte[]>> TargetSent);
}
