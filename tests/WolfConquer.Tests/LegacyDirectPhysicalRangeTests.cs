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
    private const int DirectRangeIronGuardFlag = 0xE4;
    private const int DirectRangeNeptuneCurseFlag = 0xDE;
    private const uint DirectRangeFuryStrikeRuneItemId = 4_020_901;
    private const long DirectRangeTargetHitpoints = 100_000;

    [Theory]
    [InlineData("ordinary", 2, true)]
    [InlineData("ordinary", 3, false)]
    [InlineData("fury", 3, true)]
    [InlineData("fury", 7, true)]
    [InlineData("fury", 8, false)]
    [InlineData("dragonhowl", 3, true)]
    [InlineData("dragonhowl", 9, true)]
    [InlineData("dragonhowl", 10, false)]
    [InlineData("stone-cracker", 3, true)]
    [InlineData("stone-cracker", 18, true)]
    [InlineData("stone-cracker", 19, false)]
    [InlineData("cold-moon", 18, true)]
    [InlineData("cold-moon", 19, false)]
    [InlineData("tharn-cutter", 18, true)]
    [InlineData("tharn-cutter", 19, false)]
    [InlineData("fury-and-dragonhowl", 9, true)]
    public async Task DirectRangePhysicalUsesOrdinaryFuryStrikeAndClassSelectedCombatGearLimits(
        string scenario, int distance, bool accepted)
    {
        // MsgAttackPacket.cs:635 accepts ordinary <=2 OR FuryStrike+IronGuard
        // <=7 OR MyCombatGear.GetDistance. CombatGear.cs:1420-1442 supplies 9/18.
        // Distance19 also exceeds player visibility: it is an end-to-end reject,
        // not isolated proof of the CombatGear range predicate beyond the view.
        // Equipped gear with no replacement catalog still consumes the attempt;
        // accepted here means the outer range gate permits that attempt, not an
        // ordinary Physical.OnPlayer fallback after an unavailable selected spell.
        var result = await DirectRangeRunAsync(scenario, distance);

        DirectRangeAssertOutcome(result, accepted);
    }

    [Theory]
    [InlineData("fury-no-flag", 3, false)]
    [InlineData("fury-no-rune", 3, false)]
    [InlineData("fury-wrong-job", 3, false)]
    [InlineData("fury-inactive-alternate", 3, false)]
    [InlineData("fury-active-alternate", 7, true)]
    [InlineData("fury-bag", 3, false)]
    [InlineData("fury-low-reborn", 3, false)]
    [InlineData("fury-low-level", 3, false)]
    [InlineData("dragonhowl-wrong-class", 3, false)]
    [InlineData("dragonhowl-unequipped", 3, false)]
    [InlineData("stone-cracker-wrong-class", 3, false)]
    [InlineData("stone-cracker-unequipped", 3, false)]
    public async Task DirectRangeExtensionRequiresEligibleActiveRuneOrEquippedClassGear(
        string scenario, int distance, bool accepted)
    {
        // Rune.IsEquipped:328-342 requires reborn/level, the active worn set,
        // rune family/name and job. CombatGear.isEquip:1347-1363 selects class
        // items and requires Equip==1; mere ownership cannot extend range.
        var result = await DirectRangeRunAsync(scenario, distance);

        DirectRangeAssertOutcome(result, accepted);
    }

    [Theory]
    [InlineData("ordinary", 2)]
    [InlineData("fury", 7)]
    [InlineData("dragonhowl", 9)]
    [InlineData("stone-cracker", 18)]
    [InlineData("cold-moon", 18)]
    [InlineData("tharn-cutter", 18)]
    public async Task DirectRangeNeptuneCurseRejectsEveryOtherwiseEligibleRange(string scenario, int distance)
    {
        var result = await DirectRangeRunAsync(scenario, distance, neptuneCurse: true);

        DirectRangeAssertOutcome(result, accepted: false);
    }

    private static async Task<DirectRangeResult> DirectRangeRunAsync(
        string scenario, int distance, bool neptuneCurse = false)
    {
        var fury = scenario.StartsWith("fury", StringComparison.Ordinal);
        var dragonhowl = scenario.StartsWith("dragonhowl", StringComparison.Ordinal) ||
            scenario == "fury-and-dragonhowl";
        var archerGear = scenario.StartsWith("stone-cracker", StringComparison.Ordinal) ||
            scenario is "cold-moon" or "tharn-cutter";
        var classCode = fury || dragonhowl ? 2_005 : archerGear ? 4_005 : 9_001;
        if (scenario == "dragonhowl-wrong-class")
        {
            classCode = 4_005;
        }
        else if (scenario == "stone-cracker-wrong-class")
        {
            classCode = 2_005;
        }

        var runeTypes = new List<ItemType>();
        var runeItems = new List<PlayerItemSnapshotItem>();
        if (fury && scenario != "fury-no-rune")
        {
            var requiredClass = scenario == "fury-wrong-job" ? 4_000U : 2_000U;
            runeTypes.Add(DirectRangeCreateRuneItemType(requiredClass));
            var position = scenario switch
            {
                "fury-inactive-alternate" or "fury-active-alternate" => (ushort)122,
                "fury-bag" => (ushort)0,
                _ => (ushort)102,
            };
            runeItems.Add(new PlayerItemSnapshotItem
            {
                Uid = 7_020_901,
                ItemId = DirectRangeFuryStrikeRuneItemId,
                Position = position,
            });
        }

        var worldData = CreateMagicCatalogWithItems(CreateMonsterFamily(), [], runeTypes);
        var flags = new List<int>();
        if (fury && scenario != "fury-no-flag")
        {
            flags.Add(DirectRangeIronGuardFlag);
        }
        if (neptuneCurse)
        {
            flags.Add(DirectRangeNeptuneCurseFlag);
        }

        var attacker = CreateSession(mapId: 1_000, classCode: classCode,
            strength: 300, spirit: 0, minHitPoints: 100_000,
            reborn: scenario == "fury-low-reborn" ? 1 : 2, spellIds: [],
            playerItems: PlayerItemSnapshot.Empty with { RuneItems = runeItems },
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
                ["StatusFlags"] = BuildStatusFlags(flags.ToArray()),
                ["FuryStrikeAttack"] = "0",
            });
        attacker = attacker with { AlternateEquipmentActive = scenario == "fury-active-alternate" };
        if (scenario == "fury-low-level")
        {
            attacker = attacker with { Character = attacker.Character with { Level = 14 } };
        }

        var gearId = dragonhowl ? 201U : scenario switch
        {
            "stone-cracker" or "stone-cracker-wrong-class" or "stone-cracker-unequipped" => 401U,
            "cold-moon" => 402U,
            "tharn-cutter" => 403U,
            _ => 0U,
        };
        if (gearId != 0)
        {
            attacker = attacker with
            {
                CombatGear = CombatGearSnapshot.Empty with
                {
                    Count = 1,
                    Items =
                    [
                        new CombatGearSnapshotItem(gearId, Level: 1, Exp: 0,
                            Equip: scenario.EndsWith("unequipped", StringComparison.Ordinal) ? 0U : 1U,
                            Transform: 0, MasteryScore: 0, Animas: [], BlackJades: []),
                    ],
                },
            };
        }

        var target = CreateSession(uid: TargetPlayerUid, name: "DirectRangeTarget", mapId: 1_000,
            x: 830 + distance, y: 584, minHitPoints: DirectRangeTargetHitpoints,
            strength: 0, spirit: 0, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = DirectRangeTargetHitpoints.ToString(CultureInfo.InvariantCulture),
            });
        var sessions = new WorldSessionStore();
        var attackerFrames = new ConcurrentQueue<byte[]>();
        var targetFrames = new ConcurrentQueue<byte[]>();
        var attackerConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                attackerFrames.Enqueue(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
            sendAsync: (frame, _) =>
            {
                targetFrames.Enqueue(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        sessions.Set(attackerConnection, attacker);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(attackerConnection, TextWriter.Synchronized(log), attacker,
            WorldData: worldData, Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var currentTarget));
        Assert.NotNull(currentTarget);
        var hasClassEquippedGear = !scenario.EndsWith("unequipped", StringComparison.Ordinal) &&
            ((gearId == 201 && classCode / 1000 == 2) ||
                (gearId is >= 401 and <= 403 && classCode / 1000 == 4));
        return new DirectRangeResult(currentTarget, attackerFrames.ToArray(), targetFrames.ToArray(),
            hasClassEquippedGear, log.ToString());
    }

    private static ItemType DirectRangeCreateRuneItemType(uint requiredClass)
    {
        var fields = Enumerable.Repeat("0", ItemType.RuneSpellIdFieldIndex + 1).ToArray();
        fields[0] = DirectRangeFuryStrikeRuneItemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "FuryStrike";
        fields[ItemType.RequiredClassFieldIndex] = requiredClass.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)DirectRangeFuryStrikeRuneItemId), "FuryStrike", fields);
    }

    private static void DirectRangeAssertOutcome(DirectRangeResult result, bool accepted)
    {
        var ordinaryHitExpected = accepted && !result.HasClassEquippedGear;
        if (accepted && result.HasClassEquippedGear)
        {
            Assert.Contains("WorldServer skipped legacy direct physical CombatGear replacement", result.Output);
            Assert.Contains("unavailable", result.Output, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.DoesNotContain("legacy direct physical CombatGear replacement", result.Output);
        }

        foreach (var frames in new[] { result.AttackerFrames, result.TargetFrames })
        {
            var incoming = frames.Where(bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical && packet.Query.Uid == PlayerUid &&
                packet.Query.OpponentUid == TargetPlayerUid).ToArray();
            if (ordinaryHitExpected)
            {
                var bytes = Assert.Single(incoming);
                Assert.Equal((ushort)2135, ReadFrame(bytes).Header.Opcode);
                Assert.True(MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet));
                Assert.True(packet.Query.Damage > 0);
                Assert.Equal((120, 121), ((int)packet.Query.X, (int)packet.Query.Y));
            }
            else
            {
                Assert.Empty(incoming);
            }
        }

        var hitpointUpdates = ParseMsgUpdates(result.TargetFrames)
            .Where(update => update.Uid == TargetPlayerUid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).ToArray();
        if (ordinaryHitExpected)
        {
            Assert.InRange(result.Target.Character.MinHitPoints, 1, DirectRangeTargetHitpoints - 1);
            var hitpoints = Assert.Single(hitpointUpdates);
            Assert.Equal(result.Target.Character.MinHitPoints, checked((long)hitpoints.Value));
        }
        else
        {
            Assert.Equal(DirectRangeTargetHitpoints, result.Target.Character.MinHitPoints);
            Assert.Empty(hitpointUpdates);
        }
    }

    private sealed record DirectRangeResult(
        WorldSession Target,
        byte[][] AttackerFrames,
        byte[][] TargetFrames,
        bool HasClassEquippedGear,
        string Output);
}
