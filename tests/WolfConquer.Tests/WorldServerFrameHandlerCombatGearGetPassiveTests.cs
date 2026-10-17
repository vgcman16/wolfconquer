using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerPirateGetPassiveRetriesAfterUnlearnedSelectedSpell()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var result = await RunLegacyPirateCombatGearAttackAsync(
            seedPath,
            [21_680U, 21_700U],
            """
            LegacyCombatGearAttackSpellIndex=1
            LegacyCombatGearAttackSpellIndex.0=0
            LegacyCombatGearAttackSpellIndex.1=1
            LegacyCombatGearAttackRateRoll.21680=0
            LegacyCombatGearAttackRateRoll.21700=0
            """,
            LegacyClientCompatibilityOptions.DecompiledSourceCompatible);

        var spellIndex = FindFrameIndexAfter(
            result.Sent,
            result.AttackStart,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
                spell.AttackerUid == 1_293_600U &&
                spell.SpellId == 21_680 &&
                spell.Targets.Any(target => target.Uid == result.Monster.Uid && target.Damage > 0));
        Assert.True(spellIndex >= result.AttackStart);
        Assert.DoesNotContain(
            result.Sent.Skip(result.AttackStart),
            bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var spell) &&
                spell.AttackerUid == 1_293_600U &&
                spell.SpellId == 21_700);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerPirateGetPassiveRetriesAfterSelectedSpellFailsRate()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var result = await RunLegacyPirateCombatGearAttackAsync(
            seedPath,
            [21_670U, 21_680U, 21_700U],
            """
            LegacyCombatGearAttackSpellIndex=1
            LegacyCombatGearAttackSpellIndex.0=0
            LegacyCombatGearAttackSpellIndex.1=1
            LegacyCombatGearAttackRateRoll.21670=99
            LegacyCombatGearAttackRateRoll.21680=0
            LegacyCombatGearAttackRateRoll.21700=0
            """,
            LegacyClientCompatibilityOptions.DecompiledSourceCompatible);

        var spellIndex = FindFrameIndexAfter(
            result.Sent,
            result.AttackStart,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
                spell.AttackerUid == 1_293_600U &&
                spell.SpellId == 21_680 &&
                spell.Targets.Any(target => target.Uid == result.Monster.Uid && target.Damage > 0));
        Assert.True(spellIndex >= result.AttackStart);
        Assert.DoesNotContain(
            result.Sent.Skip(result.AttackStart),
            bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var spell) &&
                spell.AttackerUid == 1_293_600U &&
                spell.SpellId is 21_670 or 21_700);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerPirateGetPassiveUsesNineWithReplacementAttemptsThenFallsBackPhysical()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var result = await RunLegacyPirateCombatGearAttackAsync(
            seedPath,
            [21_680U],
            """
            LegacyCombatGearAttackSpellIndex=1
            LegacyCombatGearAttackSpellIndex.0=0
            LegacyCombatGearAttackSpellIndex.1=0
            LegacyCombatGearAttackSpellIndex.2=0
            LegacyCombatGearAttackSpellIndex.3=0
            LegacyCombatGearAttackSpellIndex.4=0
            LegacyCombatGearAttackSpellIndex.5=0
            LegacyCombatGearAttackSpellIndex.6=0
            LegacyCombatGearAttackSpellIndex.7=0
            LegacyCombatGearAttackSpellIndex.8=0
            LegacyCombatGearAttackSpellIndex.9=1
            LegacyCombatGearAttackRateRoll.21680=0
            """,
            LegacyClientCompatibilityOptions.DecompiledSourceCompatible);

        Assert.DoesNotContain(
            result.Sent.Skip(result.AttackStart),
            bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var spell) &&
                spell.AttackerUid == 1_293_600U &&
                spell.SpellId == 21_680);
        Assert.Contains(
            result.Sent.Skip(result.AttackStart),
            bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var physical) &&
                physical.Query.Uid == 1_293_600U &&
                physical.Query.OpponentUid == result.Monster.Uid &&
                physical.Query.AttackType == MsgAttackType.Physical);
        Assert.DoesNotContain(
            "WorldServer replaced legacy monster physical attack with CombatGear spell",
            result.Log);
    }

    private sealed record LegacyPirateCombatGearAttackResult(
        List<byte[]> Sent,
        int AttackStart,
        LegacyMonsterSpawnPacket Monster,
        string Log);

    private static async Task<LegacyPirateCombatGearAttackResult> RunLegacyPirateCombatGearAttackAsync(
        string seedPath,
        IReadOnlyList<uint> learnedSpellIds,
        string extraCharacterLines,
        LegacyClientCompatibilityOptions clientCompatibility)
    {
        var dbRoot = Path.Combine(
            Path.GetTempPath(),
            "wolf-world-combatgear-pirate-get-passive-attempts-" + Guid.NewGuid());
        var usersRoot = Path.Combine(dbRoot, "Users");
        var combatGearRoot = Path.Combine(dbRoot, "PlayersCombatGear");
        var spellsRoot = Path.Combine(dbRoot, "PlayersSpells");
        try
        {
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(combatGearRoot);
            Directory.CreateDirectory(spellsRoot);
            WriteSnapshot(
                usersRoot,
                1293600,
                uid: 1293600,
                mapId: 1015,
                x: 837,
                y: 573,
                classCode: 7051,
                secondClass: 7051,
                extraCharacterLines: extraCharacterLines);
            File.WriteAllBytes(
                Path.Combine(spellsRoot, "1293600.bin"),
                PlayerSpellSnapshotLoaderTests.BuildPlayerSpells(learnedSpellIds));
            File.WriteAllText(
                Path.Combine(combatGearRoot, "1293600.ini"),
                """
                [CombatGear]
                MasteryPoints=0
                TaoistReiki=0
                PirateExp=0
                Lottery=0
                Count=1
                [AirPowers]
                SenseLevel=9
                ArmedLevel=9
                OverLoadLevel=9
                Points=92305
                SenseAipPower=10000
                ArmedAipPower=10000
                OverLoadAipPower=10000
                [Item0]
                ItemID=701
                Level=4
                Exp=0
                Equip=1
                Transform=0
                MasteryScore=928000
                Animas[0]0=0
                Animas[0]1=0
                Animas[0]2=0
                Animas[0]3=0
                Animas[0]4=0
                Animas[0]5=0
                Animas[0]6=0
                Animas[1]0=0
                Animas[1]1=0
                Animas[1]2=0
                Animas[1]3=0
                Animas[1]4=0
                Animas[1]5=0
                Animas[1]6=0
                BlackJades0=0
                BlackJades1=0
                BlackJades2=0
                BlackJades3=0
                BlackJades4=0
                BlackJades5=0
                [BlackJadeBag]
                Count=0
                """);

            var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
            RaiseSeedMonsterLifeForRetaliationFixture(worldData, "Birdman");
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ticket = handoff.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
            using var log = new StringWriter();
            var handler = new WorldServerFrameHandler(
                new CharacterSnapshotLoader(usersRoot),
                handoff,
                worldData,
                LegacyRuntimeUpdateLoopOptions.Disabled,
                log,
                clientCompatibility: clientCompatibility);
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);

            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgActionPacket
                {
                    ObjId = 1_293_600,
                    Timestamp = 1234,
                    Type = MsgActionType.SetLocation,
                }.ToFrameBytes()));

            var monster = sent.ToArray()
                .Select(bytes => LegacyMonsterSpawnPacket.TryParse(ReadFrame(bytes), out var parsed) ? parsed : null)
                .FirstOrDefault(packet => packet is not null &&
                    packet.Names[0] == "Birdman" &&
                    packet.X == 837 &&
                    packet.Y == 573);
            Assert.NotNull(monster);

            var attackStart = sent.Count;
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: 1_293_600,
                    OpponentUid: monster.Uid,
                    X: monster.X,
                    Y: monster.Y,
                    AttackType: MsgAttackType.Physical,
                    Damage: 1,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0)).ToFrameBytes()));

            return new LegacyPirateCombatGearAttackResult(sent, attackStart, monster, log.ToString());
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }
}
