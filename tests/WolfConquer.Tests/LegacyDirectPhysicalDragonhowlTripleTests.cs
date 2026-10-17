using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DirectTripleSpellId = 17_490;
    private const ushort DirectTripleFallbackSpellId = 17_460;
    private const uint DirectTripleGearId = 201;
    private const uint DirectTripleFanItemId = 300_000;
    private const uint DirectTripleHelmetItemId = 111_000;
    private const uint DirectTripleArmorItemId = 130_000;
    private const uint DirectTripleRingItemId = 150_000;
    private const uint DirectTripleTowerItemId = 201_000;
    private const uint DirectTripleBreakthroughItemId = 111_010;
    private const long DirectTripleStartingHitpoints = 100_000;

    [Theory]
    [InlineData("fixed-soul-zero")]
    [InlineData("fixed-soul-three")]
    [InlineData("priority-at-two")]
    [InlineData("outside-priority")]
    [InlineData("resources-once")]
    [InlineData("overkill")]
    [InlineData("status-before-wild-phoenix")]
    [InlineData("opening-solid")]
    [InlineData("line-boundaries")]
    [InlineData("finish-flat-only")]
    [InlineData("finish-tortoise-flat")]
    [InlineData("finish-bless-flat")]
    [InlineData("finish-reborn-two")]
    [InlineData("finish-reborn-two-flat")]
    [InlineData("finish-all-rounded")]
    [InlineData("finish-compound-nonpenetrating")]
    [InlineData("finish-compound-penetrated")]
    [InlineData("finish-compound-taoist-target")]
    public async Task DirectTriplePhysicalReplacementUsesSource17490ReceiveAndAnimationContract(string scenario)
    {
        var result = await DirectTripleRunAsync(scenario);
        var fallback = scenario == "outside-priority";
        var expectedSpellId = fallback ? DirectTripleFallbackSpellId : DirectTripleSpellId;
        var expectedDamage = scenario switch
        {
            "status-before-wild-phoenix" => 1U,
            "finish-flat-only" => 600U, // (300 +100) +50%.
            "finish-tortoise-flat" => 487U, // (300 -75 +100)=325, then +162.
            "finish-bless-flat" => 568U, // (300 -21 +100)=379, then +189.
            "finish-reborn-two" => 225U, // Defense2 halves300 before +50%.
            "finish-reborn-two-flat" => 375U, // (150 +100), then +125.
            "finish-all-rounded" => 309U, // 300->225->113->106->206->309.
            "finish-compound-nonpenetrating" => 357U, // 300->240->120->112->199->238->357.
            "finish-compound-penetrated" => 347U, // 300->390->312->156->146->233; floor(233/.67).
            "finish-compound-taoist-target" => 318U, // 300->240->192->96->90->177->212->318.
            _ => 450U,
        };
        var expectedEffect = scenario == "finish-compound-penetrated"
            ? MsgAttackEffect.Penetration : MsgAttackEffect.None;
        uint[] expectedTargetUids = scenario == "line-boundaries"
            ? new uint[] { TargetPlayerUid, TargetPlayerUid + 1, TargetPlayerUid + 2 }
            : [TargetPlayerUid];
        Assert.Equal(result.TargetFrames.Keys.OrderBy(uid => uid), result.Targets.Keys.OrderBy(uid => uid));

        // CheckCombatGear consumes the Physical request. Only the source spell
        // animation (D13 2239), never ordinary Physical 2135, represents the hit.
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical &&
            attack.Query.Uid == PlayerUid && attack.Query.OpponentUid == TargetPlayerUid);
        foreach (var frames in new[] { result.CasterFrames, result.TargetFrames[TargetPlayerUid] })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(expectedSpellId, animation.SpellId);
            Assert.Equal((830 + result.Distance, 584), ((int)animation.X, (int)animation.Y));
            Assert.Equal(expectedTargetUids.OrderBy(uid => uid),
                animation.Targets.Select(hit => hit.Uid).OrderBy(uid => uid));
            Assert.All(animation.Targets, hit =>
            {
                Assert.True(hit.Hit);
                if (!fallback)
                {
                    // Actual source row2857 has GDamage30250, Human0, D2=33,
                    // D3=2, Monster30085. GetDamage:1129-1228 includes zero
                    // candidates in the player MIN, then ADDS that bonus:
                    // base300 +0%=300. Physical:338 sets flag so Damage250
                    // is not applied again; :930-934 adds50% without a
                    // breakthrough =>450 for the neutral fixture. Nonneutral
                    // rows above follow source Tortoise->Defense2->Bless->flat,
                    // late penetration compensation, BP bonus, then +50%.
                    // Base.BigMulDiv:116-118 rounds half up:225/2=>113.
                    // Soul:Base.cs280-283 is a NO-OP.
                    Assert.Equal(expectedDamage, hit.Damage);
                    Assert.Equal(expectedEffect, hit.Effect);
                }
            });
            if (!fallback)
            {
                Assert.Equal((byte)(scenario == "fixed-soul-zero" ? 0 : 3), animation.LevelHu);
            }
            var animationBytes = Assert.Single(frames, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.SpellId == expectedSpellId);
            Assert.Equal((ushort)2239, ReadFrame(animationBytes).Header.Opcode);
        }

        foreach (var (uid, target) in result.Targets)
        {
            var included = expectedTargetUids.Contains(uid);
            var initialHitpoints = scenario == "overkill" && uid == TargetPlayerUid
                ? 5L : DirectTripleStartingHitpoints;
            if (fallback)
            {
                // This row proves priority's <=3 boundary, not the separate
                // source17460 damage/progression implementation.
                Assert.InRange(target.Character.MinHitPoints, 1, initialHitpoints - 1);
            }
            else
            {
                Assert.Equal(included ? Math.Max(0, initialHitpoints - expectedDamage) : initialHitpoints,
                    target.Character.MinHitPoints);
            }
            var hpEntries = ParseMsgUpdates(result.TargetFrames[uid])
                .Where(update => update.Uid == uid)
                .SelectMany(update => update.Updates)
                .Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).ToArray();
            if (included)
            {
                if (scenario == "overkill")
                {
                    // Source normal Dead queries while currentHP is still5,
                    // then assigns0. Preserve raw animation450 independently.
                    Assert.Equal(new ulong[] { 5, 5, 5, 0 }, hpEntries.Select(entry => entry.Value));
                    var hpFrames = result.TargetFrames[uid].Where(bytes =>
                        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == uid &&
                        update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
                        .ToArray();
                    Assert.Equal(8, hpFrames.Length);
                    Assert.All(hpFrames, bytes => Assert.Equal((ushort)2176, ReadFrame(bytes).Header.Opcode));
                    for (var phase = 0; phase < 4; phase++)
                    {
                        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(hpFrames[phase * 2]), out var maximum));
                        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(hpFrames[phase * 2 + 1]), out var pair));
                        Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], maximum.Updates.Select(entry => entry.Type));
                        Assert.Equal(
                            [LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
                            pair.Updates.Select(entry => entry.Type));
                        Assert.Equal(100_000UL, maximum.Updates[0].Value);
                        Assert.Equal(100_000UL, pair.Updates[0].Value);
                        Assert.Equal(phase < 3 ? 5UL : 0UL, pair.Updates[1].Value);
                    }
                    Assert.Equal(checked((ulong)target.Character.MinHitPoints), hpEntries[^1].Value);
                }
                else
                {
                    Assert.Equal(checked((ulong)target.Character.MinHitPoints), Assert.Single(hpEntries).Value);
                }
            }
            else
            {
                Assert.Empty(hpEntries);
            }
        }

        if (!fallback)
        {
            // CombatGearAttack.cs2224-2273 receives damage BEFORE enqueue/send
            // and has neither IncreaseExperience.Up nor UpdateSpell.CheckUpdate.
            // The raw animation is450 even when only5 HP can actually be lost.
            Assert.Equal(0, result.Caster.Character.Experience);
            Assert.All(result.Caster.PlayerSpells.Spells, spell => Assert.Equal(0, spell.Experience));
            Assert.DoesNotContain(result.DeliveryOrder, bytes =>
                LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
            Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update =>
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
            Assert.NotEmpty(result.AnimationObservations);
            Assert.All(result.AnimationObservations, observation =>
            {
                foreach (var uid in expectedTargetUids)
                {
                    Assert.Equal(result.Targets[uid].Character.MinHitPoints, observation[uid]);
                }
            });
            foreach (var uid in expectedTargetUids)
            {
                var hpIndices = Enumerable.Range(0, result.DeliveryOrder.Length).Where(index =>
                    LegacyMsgUpdatePacket.TryParse(ReadFrame(result.DeliveryOrder[index]), out var update) &&
                    update.Uid == uid && update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.Hitpoints)).ToArray();
                Assert.NotEmpty(hpIndices);
                var firstAnimation = Array.FindIndex(result.DeliveryOrder, bytes =>
                    LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _));
                Assert.All(hpIndices, index => Assert.True(index < firstAnimation));
            }
        }

        var resourceUpdates = ParseMsgUpdates(result.CasterFrames)
            .Where(update => update.Uid == PlayerUid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana)
            .ToArray();
        if (scenario == "resources-once")
        {
            Assert.Equal([LegacyMsgUpdateDataType.Stamina, LegacyMsgUpdateDataType.Mana],
                resourceUpdates.Select(entry => entry.Type));
            Assert.Equal([95UL, 493UL], resourceUpdates.Select(entry => entry.Value));
            var staminaIndex = SingleFrameIndex(result.CasterFrames, frame =>
                LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
            var manaIndex = SingleFrameIndex(result.CasterFrames, frame =>
                LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Mana));
            var animationIndex = SingleFrameIndex(result.CasterFrames, frame =>
                LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == DirectTripleSpellId);
            Assert.True(staminaIndex < manaIndex && manaIndex < animationIndex);
            Assert.Equal(493, result.Caster.Character.MinMana);
            Assert.Equal("95", result.Caster.Character.Attributes["Stamina"]);
        }
        else
        {
            Assert.Empty(resourceUpdates);
            Assert.Equal(500, result.Caster.Character.MinMana);
            Assert.Equal("100", result.Caster.Character.Attributes["Stamina"]);
        }
        Assert.Equal(DirectTripleStartingHitpoints, result.Caster.Character.MinHitPoints);

        if (scenario == "status-before-wild-phoenix")
        {
            var target = result.Targets[TargetPlayerUid];
            Assert.Equal("2", target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
            Assert.Equal("18007", target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute]);
            Assert.True(HasLegacyStatusFlag(target.Character.Attributes, LegacyWildPhoenixFlag));
            Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update =>
                update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == LegacyWildPhoenixFlag));
        }
        if (scenario == "opening-solid")
        {
            Assert.Equal(DirectTripleStartingHitpoints, Assert.Single(result.SolidObservedHitpoints));
            Assert.Equal("2400", result.Targets[TargetPlayerUid].Character.Attributes["Solid"]);
            var archiveIndex = AssertStarArrowReceiveEnvelopeArchive(
                result.TargetFrames[TargetPlayerUid], TargetPlayerUid,
                StarArrowReceiveEnvelopeSolidFlag, duration: 5, damage: 2_400);
            var hpIndex = SingleFrameIndex(result.TargetFrames[TargetPlayerUid], frame =>
                LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
            Assert.True(archiveIndex < hpIndex);
        }
    }

    [Fact]
    public async Task DirectTriplePhysicalReplacementRunsLiteralRevengeTailBeforeItsOwnReceiveAndAnimation()
    {
        var result = await DirectTripleRunAsync("revenge-tail");

        Assert.Equal(95_000, result.Caster.Character.MinHitPoints);
        Assert.Equal(99_550, result.Targets[TargetPlayerUid].Character.MinHitPoints);
        Assert.Equal("1", result.Targets[TargetPlayerUid].Character.Attributes["LegacyRevengeTailChange"]);
        foreach (var frames in new[] { result.CasterFrames, result.TargetFrames[TargetPlayerUid] })
        {
            var animations = ParseSpellAnimations(frames);
            var tail = Assert.Single(animations, animation => animation.SpellId == RevengeTailAttachStatusSpellId);
            Assert.Equal(TargetPlayerUid, tail.AttackerUid);
            var reflected = Assert.Single(tail.Targets);
            Assert.Equal(PlayerUid, reflected.Uid);
            Assert.Equal(5_000U, reflected.Damage);
            var triple = Assert.Single(animations, animation => animation.SpellId == DirectTripleSpellId);
            Assert.Equal(450U, Assert.Single(triple.Targets).Damage);
            var tailIndex = SingleFrameIndex(frames, frame =>
                LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == RevengeTailAttachStatusSpellId);
            var tripleIndex = SingleFrameIndex(frames, frame =>
                LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == DirectTripleSpellId);
            Assert.True(tailIndex < tripleIndex);
        }
        var tailHpIndex = SingleFrameIndex(result.CasterFrames, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints && entry.Value == 95_000));
        var firstTripleDelivery = Array.FindIndex(result.DeliveryOrder, bytes =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
            animation.SpellId == DirectTripleSpellId);
        Assert.True(Array.IndexOf(result.DeliveryOrder, result.CasterFrames[tailHpIndex]) < firstTripleDelivery);
        var targetHpIndex = SingleFrameIndex(result.TargetFrames[TargetPlayerUid], frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints && entry.Value == 99_550));
        var lastTailDelivery = Enumerable.Range(0, result.DeliveryOrder.Length).Where(index =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(result.DeliveryOrder[index]), out var animation) &&
            animation.SpellId == RevengeTailAttachStatusSpellId).Max();
        Assert.True(lastTailDelivery < Array.IndexOf(result.DeliveryOrder, result.TargetFrames[TargetPlayerUid][targetHpIndex]));
        Assert.NotEmpty(result.AnimationObservations);
        Assert.All(result.AnimationObservations, observation => Assert.Equal(99_550, observation[TargetPlayerUid]));
        Assert.Equal(0, result.Caster.Character.Experience);
        Assert.All(result.Caster.PlayerSpells.Spells, spell => Assert.Equal(0, spell.Experience));
        Assert.DoesNotContain(result.DeliveryOrder, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Theory]
    [InlineData("wonder-rebind-attacker")]
    [InlineData("solid-rebind-target")]
    [InlineData("solid-remove-target")]
    public async Task DirectTriplePhysicalReplacementKeepsOriginalIdentitiesAcrossRealCounterAndOpeningSends(string scenario)
    {
        var result = await DirectTripleRunAsync(scenario);

        Assert.Equal(1, result.MutationCount);
        Assert.NotNull(result.CasterBeforeMutation);
        Assert.NotNull(result.TargetBeforeMutation);
        Assert.Equal(TargetPlayerUid, result.TargetBeforeMutation.Character.Uid);
        Assert.DoesNotContain(ParseSpellAnimations(result.DeliveryOrder), animation => animation.SpellId == DirectTripleSpellId);
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) && attack.Query.AttackType == MsgAttackType.Physical);
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update => update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update => update.Uid == result.Replacement.Character.Uid);
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.BloodthirstElan);

        if (scenario == "wonder-rebind-attacker")
        {
            var wonder = Assert.Single(ParseSpellAnimations(result.TargetFrames[TargetPlayerUid]),
                animation => animation.SpellId == HeavensWonderRuntimeSpellId);
            Assert.Equal(TargetPlayerUid, wonder.AttackerUid);
            var counter = Assert.Single(wonder.Targets);
            Assert.Equal(PlayerUid, counter.Uid);
            Assert.True(counter.Damage > 0);
            Assert.Equal(100_000L - counter.Damage, result.CasterBeforeMutation.Character.MinHitPoints);
            Assert.Equal(100_000, result.TargetBeforeMutation.Character.MinHitPoints);
            Assert.Equal(493, result.TargetBeforeMutation.Character.MinMana);
            Assert.Equal("95", result.TargetBeforeMutation.Character.Attributes["Stamina"]);
            Assert.True(result.TargetBeforeMutation.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
            Assert.Same(result.Replacement, result.Caster);
            Assert.Equal(100_000, result.Targets[TargetPlayerUid].Character.MinHitPoints);
            Assert.Empty(result.SolidObservedHitpoints);
        }
        else
        {
            Assert.Equal(50_000, result.TargetBeforeMutation.Character.MinHitPoints);
            Assert.Equal(50_000, Assert.Single(result.SolidObservedHitpoints));
            Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
            AssertStarArrowReceiveEnvelopeArchive(result.TargetFrames[TargetPlayerUid], TargetPlayerUid,
                StarArrowReceiveEnvelopeSolidFlag, duration: 5, damage: 2_400);
            if (scenario == "solid-remove-target")
            {
                Assert.DoesNotContain(TargetPlayerUid, result.Targets.Keys);
            }
            else
            {
                Assert.Same(result.Replacement, result.Targets[TargetPlayerUid]);
            }
        }
        // Exact replacement values must survive the return from the awaited
        // counter/opening send. A different UID is not the accepted Player.
        Assert.Equal(77_777, result.Replacement.Character.MinHitPoints);
        Assert.Equal(77, result.Replacement.Character.MinMana);
        Assert.Equal("33", result.Replacement.Character.Attributes["Stamina"]);
        Assert.Equal("replacement", result.Replacement.Character.Attributes["DirectTripleIdentityMarker"]);
    }

    private static async Task<DirectTripleResult> DirectTripleRunAsync(string scenario)
    {
        var distance = scenario is "priority-at-two" or "wonder-rebind-attacker" ? 2 : scenario == "outside-priority" ? 4 : 9;
        var priority = scenario is "priority-at-two" or "outside-priority";
        var resources = scenario == "resources-once";
        var finishFixture = scenario.StartsWith("finish-", StringComparison.Ordinal);
        var finishCompound = scenario.StartsWith("finish-compound-", StringComparison.Ordinal);
        var finishPenetrated = scenario == "finish-compound-penetrated";
        var finishFlat = finishCompound || scenario is "finish-flat-only" or "finish-tortoise-flat" or
            "finish-bless-flat" or "finish-reborn-two-flat" or "finish-all-rounded";
        var finishTortoise = finishCompound || scenario is "finish-tortoise-flat" or "finish-all-rounded";
        var finishBless = finishCompound || scenario is "finish-bless-flat" or "finish-all-rounded";
        var finishRebornTwo = finishCompound || scenario is "finish-reborn-two" or "finish-reborn-two-flat" or "finish-all-rounded";
        var tripleRow = CreateMagicRow(DirectTripleSpellId, sort: 99,
            useMana: resources ? (ushort)7 : (ushort)0, damage: 30_250,
            experience: 1_000_000, range: 4, useStamina: resources ? (byte)5 : (byte)0,
            coolDown: 0, coldTime: 0, damageOnHuman: 0, damage2: 33, damage3: 2,
            damageOnMonster: 30_085, gDamage: priority ? 100U : 0U);
        // Preserve the supplied row's five damage candidates. Field12 is RATE,
        // despite the existing fixture/runtime helper naming it GDamage.
        Assert.Equal("30250", tripleRow.Fields[10]);
        Assert.Equal("0", tripleRow.Fields[35]);
        Assert.Equal("33", tripleRow.Fields[36]);
        Assert.Equal("2", tripleRow.Fields[37]);
        Assert.Equal("30085", tripleRow.Fields[44]);
        var magicRows = new List<MagicType>
        {
            tripleRow,
            CreateMagicRow(DirectTripleFallbackSpellId, sort: 87, useMana: 0, damage: 120,
                experience: 1_000_000, range: 20, useStamina: 0, coolDown: 0, coldTime: 0),
        };
        if (scenario == "status-before-wild-phoenix")
        {
            magicRows.Add(CreateMagicRow(WildPhoenixSpellId, sort: 6, useMana: 0, damage: 4,
                coldTime: 0, duration: 32, damageOnHuman: 18, damage2: 7));
        }
        if (scenario == "revenge-tail")
        {
            magicRows.Add(CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6,
                useMana: 0, damage: 5_000, coldTime: 0));
        }
        if (scenario == "wonder-rebind-attacker")
        {
            magicRows.Add(CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 1_000));
        }
        var worldData = CreateMagicCatalogCore(CreateMonsterFamily(), spawns: [], sobNpcs: [], magicRows: magicRows,
            items: finishFixture ?
            [
                CreateDirectTripleItemType(DirectTripleFanItemId, maxAttack: 100),
                CreateDirectTripleItemType(DirectTripleHelmetItemId),
                CreateDirectTripleItemType(DirectTripleArmorItemId),
                CreateDirectTripleItemType(DirectTripleRingItemId),
                CreateDirectTripleItemType(DirectTripleTowerItemId, physicalDefense: 13),
                CreateDirectTripleItemType(DirectTripleBreakthroughItemId, breakthrough: 20),
            ] : []);
        var casterEquipment = new List<PlayerItemSnapshotItem>();
        if (finishFlat)
        {
            casterEquipment.Add(new PlayerItemSnapshotItem
            {
                ItemId = DirectTripleFanItemId, Position = 10, Durability = 100, MaximumDurability = 100,
            });
        }
        if (finishPenetrated)
        {
            casterEquipment.Add(new PlayerItemSnapshotItem
            {
                ItemId = DirectTripleBreakthroughItemId, Position = 1, Durability = 100, MaximumDurability = 100,
            });
        }
        var soul = (byte)(scenario == "fixed-soul-zero" ? 0 : 3);
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, strength: 300, spirit: 0,
            minHitPoints: DirectTripleStartingHitpoints, stamina: 100,
            playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = casterEquipment },
            spellEntries:
            [
                CreatePlayerSpell(DirectTripleSpellId) with { SoulLevel = soul, UseSpellSoul = soul },
                CreatePlayerSpell(DirectTripleFallbackSpellId),
            ],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
                ["BattlePower"] = finishCompound ? (finishPenetrated ? "100" : "134") : "120",
                ["LegacyCombatGearAttackSpellIndex"] = priority ? "0" : "3",
                ["LegacyCombatGearAttackRateRoll"] = "0",
                [PhysicalBreakthroughRefineryRollAttribute] = "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1,
                Items =
                [
                    new CombatGearSnapshotItem(DirectTripleGearId, Level: 1, Exp: 0,
                        Equip: 1, Transform: 0, MasteryScore: 0, Animas: [], BlackJades: []),
                ],
            },
        };
        var attackerFields = LegacyPlayerStatusCalculator.CalculateFields(
            caster.Character, caster.PlayerItems, worldData,
            checked((uint)caster.Character.MinHitPoints), prestigeLevel: 0);
        Assert.Equal(300U, attackerFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(301U, attackerFields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(finishPenetrated ? 20U : 0U, attackerFields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        Assert.Equal(finishFlat ? 100U : 0U, attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(0U, attackerFields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);

        WorldSession CreateTarget(uint uid, long x, long y, bool primary = false)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100000",
                ["BattlePower"] = finishPenetrated ? "200" : "120",
            };
            var targetEquipment = new List<PlayerItemSnapshotItem>();
            if (primary && (finishTortoise || finishBless))
            {
                targetEquipment.Add(new PlayerItemSnapshotItem
                {
                    ItemId = DirectTripleHelmetItemId, Position = 1, Durability = 100, MaximumDurability = 100,
                    Bless = finishBless ? (byte)7 : (byte)0,
                    SocketOne = finishTortoise ? (byte)73 : (byte)0,
                    SocketTwo = finishTortoise ? (byte)73 : (byte)0,
                });
            }
            if (primary && finishTortoise)
            {
                targetEquipment.Add(new PlayerItemSnapshotItem
                {
                    ItemId = DirectTripleArmorItemId, Position = 3, Durability = 100, MaximumDurability = 100,
                    SocketOne = 73, SocketTwo = finishCompound ? (byte)71 : (byte)73,
                });
                if (!finishCompound)
                {
                    // Four Super Tortoises plus one Normal total26; the source
                    // physical tail caps their effective reduction at25%.
                    targetEquipment.Add(new PlayerItemSnapshotItem
                    {
                        ItemId = DirectTripleRingItemId, Position = 6, Durability = 100, MaximumDurability = 100,
                        SocketOne = 71,
                    });
                }
            }
            if (primary && finishCompound)
            {
                targetEquipment.Add(new PlayerItemSnapshotItem
                {
                    ItemId = DirectTripleTowerItemId, Position = 11, Durability = 100, MaximumDurability = 100,
                });
            }
            var targetSpells = new List<ushort>();
            if (primary && scenario == "status-before-wild-phoenix")
            {
                attributes["StatusFlags"] = BuildStatusFlags(LegacyPhysicalSpellsShurikenVortexFlag, LegacyWildPhoenixFlag);
                attributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
                attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2";
                targetSpells.Add(WildPhoenixSpellId);
            }
            if (primary && (scenario is "opening-solid" or "wonder-rebind-attacker" or "solid-rebind-target" or "solid-remove-target"))
            {
                attributes["LegacySpeffectLevel.Solid"] = "4";
                attributes["LegacySpeffectRoll.Solid"] = "0";
            }
            if (primary && scenario == "revenge-tail")
            {
                targetSpells.Add(RevengeTailAttachStatusSpellId);
                attributes["StatusFlags"] = BuildStatusFlags(RevengeTailStatusFlag);
                attributes["LegacyRevengeTailChange"] = "2";
            }
            if (primary && scenario == "wonder-rebind-attacker")
            {
                targetSpells.Add(HeavensWonderRuntimeSpellId);
                attributes[CounterKill16350BackDamageRollAttribute] = "0";
                attributes["PkMode"] = "0";
            }
            if (primary && (scenario is "solid-rebind-target" or "solid-remove-target"))
            {
                attributes["LegacySpeffectLevel.Bloodthirst"] = "4";
                attributes["LegacySpeffectRoll.Bloodthirst"] = "0";
            }
            var target = CreateSession(uid: uid, name: "DirectTripleTarget" + uid.ToString(CultureInfo.InvariantCulture),
                mapId: 1_000, x: x, y: y, classCode: scenario == "finish-compound-taoist-target" ? 10_000 : 2_005,
                reborn: primary && (finishRebornTwo || scenario == "status-before-wild-phoenix") ? 2 : 0,
                strength: primary && scenario == "wonder-rebind-attacker" ? 1_000 : 0, spirit: 0,
                minHitPoints: primary && scenario == "overkill" ? 5 :
                    primary && (scenario is "solid-rebind-target" or "solid-remove-target") ? 50_000 : DirectTripleStartingHitpoints,
                stamina: 100, spellIds: targetSpells,
                playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = targetEquipment },
                attributes: attributes);
            var fields = LegacyPlayerStatusCalculator.CalculateFields(
                target.Character, target.PlayerItems, worldData,
                checked((uint)target.Character.MinHitPoints), prestigeLevel: 0);
            Assert.Equal(0U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
            Assert.Equal(0U, fields[LegacyPlayerStatusPacket.CounteractionIndex]);
            Assert.Equal(primary && finishTortoise ? (finishCompound ? 20U : 26U) : 0U,
                fields[LegacyPlayerStatusPacket.DamageIndex]);
            Assert.Equal(primary && finishBless ? 7U : 0U, fields[LegacyPlayerStatusPacket.ItemBlessIndex]);
            Assert.Equal(primary && finishCompound ? 13U : 0U,
                fields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
            return target;
        }

        var targets = new Dictionary<uint, WorldSession>
        {
            [TargetPlayerUid] = CreateTarget(TargetPlayerUid, 830 + distance, 584, primary: true),
        };
        if (scenario == "line-boundaries")
        {
            // InLineAlgorithm:linedda extends through15 points, not just to the
            // clicked target at839,584; InLine accepts source-distance<=15 and
            // line-distance<=2. These include both exact boundary values and
            // reject one tile beyond each, despite DBSpell.Range being only4.
            targets[TargetPlayerUid + 1] = CreateTarget(TargetPlayerUid + 1, 845, 584);
            targets[TargetPlayerUid + 2] = CreateTarget(TargetPlayerUid + 2, 840, 586);
            targets[TargetPlayerUid + 3] = CreateTarget(TargetPlayerUid + 3, 840, 587);
            targets[TargetPlayerUid + 4] = CreateTarget(TargetPlayerUid + 4, 846, 584);
        }
        var sessions = new WorldSessionStore();
        var casterFrames = new ConcurrentQueue<byte[]>();
        var targetFrames = targets.Keys.ToDictionary(uid => uid, _ => new ConcurrentQueue<byte[]>());
        var deliveryOrder = new ConcurrentQueue<byte[]>();
        var animationObservations = new ConcurrentQueue<IReadOnlyDictionary<uint, long>>();
        var solidObservedHitpoints = new ConcurrentQueue<long>();
        var replacement = CreateSession(uid: TargetPlayerUid + 2_000, name: "DirectTripleReplacement",
            mapId: 1_000, x: 832, y: 584, minHitPoints: 77_777, minMana: 77, stamina: 33, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "77777",
                ["DirectTripleIdentityMarker"] = "replacement",
            });
        var mutationCount = 0;
        WorldSession? casterBeforeMutation = null;
        WorldSession? targetBeforeMutation = null;
        ConquerTcpConnection? casterConnection = null;
        var targetConnections = new Dictionary<uint, ConquerTcpConnection>();

        ValueTask Capture(uint recipientUid, ConcurrentQueue<byte[]> frames, ReadOnlyMemory<byte> frame)
        {
            var bytes = frame.ToArray();
            frames.Enqueue(bytes);
            deliveryOrder.Enqueue(bytes);
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                animation.SpellId == DirectTripleSpellId)
            {
                animationObservations.Enqueue(sessions.Snapshot()
                    .Where(active => targets.ContainsKey(checked((uint)active.Session.Character.Uid)))
                    .ToDictionary(active => checked((uint)active.Session.Character.Uid),
                        active => active.Session.Character.MinHitPoints));
            }
            if (recipientUid == TargetPlayerUid &&
                LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == StarArrowReceiveEnvelopeSolidFlag))
            {
                solidObservedHitpoints.Enqueue(sessions.Snapshot()
                    .Single(active => active.Session.Character.Uid == TargetPlayerUid).Session.Character.MinHitPoints);
            }
            var wonderMutation = scenario == "wonder-rebind-attacker" && recipientUid == TargetPlayerUid &&
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var counter) &&
                counter.SpellId == HeavensWonderRuntimeSpellId && counter.AttackerUid == TargetPlayerUid;
            var solidMutation = (scenario is "solid-rebind-target" or "solid-remove-target") &&
                recipientUid == TargetPlayerUid && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var archive) &&
                archive.Uid == TargetPlayerUid && archive.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == StarArrowReceiveEnvelopeSolidFlag);
            if ((wonderMutation || solidMutation) && Interlocked.CompareExchange(ref mutationCount, 1, 0) == 0)
            {
                Assert.True(sessions.TryGet(casterConnection!, out casterBeforeMutation));
                Assert.NotNull(casterBeforeMutation);
                Assert.True(sessions.TryGet(targetConnections[TargetPlayerUid], out targetBeforeMutation));
                Assert.NotNull(targetBeforeMutation);
                var affectedConnection = wonderMutation ? casterConnection! : targetConnections[TargetPlayerUid];
                if (scenario == "solid-remove-target")
                {
                    Assert.True(sessions.TryRemove(affectedConnection, out var removed));
                    Assert.Same(targetBeforeMutation, removed);
                }
                else
                {
                    sessions.Set(affectedConnection, replacement);
                }
            }
            return ValueTask.CompletedTask;
        }

        casterConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) => Capture(PlayerUid, casterFrames, frame));
        sessions.Set(casterConnection, caster);
        foreach (var (uid, target) in targets)
        {
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
                sendAsync: (frame, _) => Capture(uid, targetFrames[uid], frame));
            targetConnections.Add(uid, connection);
            sessions.Set(connection, target);
        }
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, TextWriter.Synchronized(log), caster,
            WorldData: worldData, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        Assert.True(sessions.TryGet(casterConnection, out var currentCaster));
        Assert.NotNull(currentCaster);
        var currentTargets = new Dictionary<uint, WorldSession>();
        foreach (var (uid, connection) in targetConnections)
        {
            if (sessions.TryGet(connection, out var target) && target is not null)
            {
                currentTargets.Add(uid, target);
            }
        }
        return new DirectTripleResult(currentCaster, currentTargets, casterFrames.ToArray(),
            targetFrames.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
            deliveryOrder.ToArray(), animationObservations.ToArray(), solidObservedHitpoints.ToArray(), distance,
            replacement, casterBeforeMutation, targetBeforeMutation, Volatile.Read(ref mutationCount));
    }

    private static ItemType CreateDirectTripleItemType(
        uint itemId,
        uint maxAttack = 0,
        uint physicalDefense = 0,
        uint breakthrough = 0)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "DirectTripleFinishFixture";
        fields[13] = maxAttack.ToString(CultureInfo.InvariantCulture);
        fields[15] = physicalDefense.ToString(CultureInfo.InvariantCulture);
        fields[44] = breakthrough.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), "DirectTripleFinishFixture", fields);
    }

    private sealed record DirectTripleResult(
        WorldSession Caster,
        IReadOnlyDictionary<uint, WorldSession> Targets,
        byte[][] CasterFrames,
        IReadOnlyDictionary<uint, byte[][]> TargetFrames,
        byte[][] DeliveryOrder,
        IReadOnlyDictionary<uint, long>[] AnimationObservations,
        long[] SolidObservedHitpoints,
        int Distance,
        WorldSession Replacement,
        WorldSession? CasterBeforeMutation,
        WorldSession? TargetBeforeMutation,
        int MutationCount);
}
