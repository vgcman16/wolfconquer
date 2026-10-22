using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort WildPhoenixSpellId = 20_370;
    private const ushort WildPhoenixQualifiedDamageSpellId = 1_001;
    private const ushort WildPhoenixPhysicalSpellsSpellId = 10_490;
    private const uint CombatGearTaoist5ItemId = 0x519;
    private const int LegacyHawksAmbitionFlag = 0x100;
    private const int LegacyWildPhoenixFlag = LegacyOblivionFlag | LegacyHawksAmbitionFlag;
    private const string RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute =
        "LegacyCombatGearWildPhoenixPackedDamage";
    private const string RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute =
        "LegacyCombatGearWildPhoenixChargeCount";

    [Fact]
    public async Task HandlerMagicAttackAgainstWildPhoenixPlayerConsumesDefenderChargeAndDealsOneDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                WildPhoenixQualifiedDamageSpellId,
                sort: 1,
                useMana: 7,
                damage: 120,
                coldTime: 0),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(spellIds: [WildPhoenixQualifiedDamageSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreateWildPhoenixPlayerMagicQuery(targetSession)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(WildPhoenixQualifiedDamageSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage); // Native early WildPhoenix calculator1, then caller1001 identitySoul /2.
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints); // Zero still enters Receive and the Hit=true target.
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
    }

    [Fact]
    public async Task HandlerPhysicalSpellsAgainstWildPhoenixPlayerConsumesDefenderChargesAndScalesDamageToZeroPerHit()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                WildPhoenixPhysicalSpellsSpellId,
                sort: 55,
                useMana: 0,
                damage: 0,
                coldTime: 0),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [WildPhoenixPhysicalSpellsSpellId],
            legacyRandomSpell: WildPhoenixPhysicalSpellsSpellId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "3",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: WildPhoenixPhysicalSpellsSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(WildPhoenixPhysicalSpellsSpellId, spell.SpellId);
            Assert.Equal(3, spell.Targets.Count);
            Assert.All(spell.Targets, target =>
            {
                Assert.Equal(TargetPlayerUid, target.Uid);
                Assert.Equal(0U, target.Damage);
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "0",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntries = ParseMsgUpdates(targetSent)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag)
            .ToArray();
        Assert.Equal(3, archiveEntries.Length);
        Assert.Equal(new uint[] { 2U, 1U, 0U }, archiveEntries.Select(entry => entry.SpellLevel).ToArray());
        Assert.All(archiveEntries, entry =>
        {
            Assert.Equal(32U, entry.Time);
            Assert.Equal(18_007U, entry.Damage);
        });

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=0", output);
        Assert.Contains("WorldServer handled legacy PhysicalSpells player magic", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesWildPhoenixAttackerDamageBonus()
    {
        var baseline = await RunWildPhoenixPlayerDamageAsync(attackerHasWildPhoenix: false);
        var boosted = await RunWildPhoenixPlayerDamageAsync(attackerHasWildPhoenix: true);

        Assert.Equal(ExpectedPlayerThunderDamage, baseline.Damage);
        Assert.Equal(
            baseline.Damage + baseline.Damage * 7 / 100,
            boosted.Damage);
        Assert.Equal(
            boosted.StartingCharges,
            boosted.UpdatedCaster.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.Contains("WorldServer applied legacy WildPhoenix attacker damage bonus", boosted.Output);
    }

    private static async Task<WildPhoenixPlayerDamageResult> RunWildPhoenixPlayerDamageAsync(
        bool attackerHasWildPhoenix)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(
                WildPhoenixQualifiedDamageSpellId,
                sort: 1,
                useMana: 7,
                damage: 541,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterAttributes = attackerHasWildPhoenix
            ? new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            }
            : null;
        var casterSession = CreateSession(
            classCode: 14_002,
            spellIds: [WildPhoenixQualifiedDamageSpellId],
            attributes: casterAttributes) with
        {
            CombatGear = attackerHasWildPhoenix
                ? CreateEquippedWildPhoenixCombatGear()
                : CombatGearSnapshot.Empty,
        };
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584);
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateWildPhoenixPlayerMagicQuery(targetSession)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        var target = Assert.Single(spell.Targets);
        return new WildPhoenixPlayerDamageResult(
            target.Damage,
            updatedCasterSession,
            StartingCharges: "2",
            log.ToString());
    }

    private static MsgAttackQuery CreateWildPhoenixPlayerMagicQuery(WorldSession targetSession)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: TargetPlayerUid,
            X: checked((ushort)targetSession.Character.X),
            Y: checked((ushort)targetSession.Character.Y),
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: WildPhoenixQualifiedDamageSpellId);
    }

    private static CombatGearSnapshot CreateEquippedWildPhoenixCombatGear()
    {
        return new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            Items:
            [
                new CombatGearSnapshotItem(
                    CombatGearTaoist5ItemId,
                    Level: 0,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
    }

    private readonly record struct WildPhoenixPlayerDamageResult(
        uint Damage,
        WorldSession UpdatedCaster,
        string StartingCharges,
        string Output);
}
