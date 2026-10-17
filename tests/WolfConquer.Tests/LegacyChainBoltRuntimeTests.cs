using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task ChainBoltWithoutXpListMonsterPrimaryChainsFromPrimaryCenterToSameTypeInsideStrictHalfRange()
    {
        var monsterFamily = CreateMonsterFamily();
        var primarySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 584);
        var insideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 840, boundY: 584);
        var edgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 841, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_309, x: 840, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [primarySpawn, insideSpawn, edgeSpawn],
            [sobNpc],
            [CreateMatchingDecompileChainBoltLevelZeroRow()]);
        var primaryMonster = CreateRuntimeMonsterInstance(worldData, primarySpawn, monsterFamily);
        var insideMonster = CreateRuntimeMonsterInstance(worldData, insideSpawn, monsterFamily);
        var edgeMonster = CreateRuntimeMonsterInstance(worldData, edgeSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 50,
            strength: 300,
            spellIds: [TestChainBoltSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacySupermanFlag),
                ["XPCount"] = "23",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 840,
            y: 584,
            minHitPoints: 5_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                primaryMonster,
                legacyEncodedQuery: false,
                spellId: TestChainBoltSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(primaryMonster.X, spell.X);
        Assert.Equal(primaryMonster.Y, spell.Y);
        Assert.Equal(TestChainBoltSpellId, spell.SpellId);

        var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { primaryMonster.Uid, insideMonster.Uid }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.DoesNotContain(edgeMonster.Uid, damageByUid.Keys);
        Assert.DoesNotContain(TargetPlayerUid, damageByUid.Keys);
        Assert.DoesNotContain(checked((uint)sobNpc.Id), damageByUid.Keys);
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        Assert.True(monsterStates.TryGetVisibleInstance(primaryMonster, DateTimeOffset.UtcNow, out var damagedPrimary));
        Assert.Equal(primaryMonster.MaxLife - damageByUid[primaryMonster.Uid], damagedPrimary.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(insideMonster, DateTimeOffset.UtcNow, out var damagedInside));
        Assert.Equal(insideMonster.MaxLife - damageByUid[insideMonster.Uid], damagedInside.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(edgeMonster, DateTimeOffset.UtcNow, out var untouchedEdge));
        Assert.Equal(edgeMonster.MaxLife, untouchedEdge.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life, sobNpcStates.GetVisible(sobNpc).Life);

        AssertChainBoltNonXpBranchDidNotOpenXpSkill(sessions, casterConnection, casterSent);
        AssertChainBoltNonXpBranchDidNotLogUnsupportedGap(log.ToString());
        AssertChainBoltMatchingDecompileEvidence(log.ToString());
    }

    [Fact]
    public async Task ChainBoltWithoutXpListSobPrimaryChainsFromPrimaryCenterToSameTypeInsideStrictHalfRange()
    {
        var monsterFamily = CreateMonsterFamily();
        var monsterSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 840, boundY: 584);
        var primarySobNpc = CreateMoveLineSobNpc(id: 65_310, x: 836, y: 584);
        var insideSobNpc = CreateMoveLineSobNpc(id: 65_311, x: 840, y: 584);
        var edgeSobNpc = CreateMoveLineSobNpc(id: 65_312, x: 841, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [monsterSpawn],
            [primarySobNpc, insideSobNpc, edgeSobNpc],
            [CreateMatchingDecompileChainBoltLevelZeroRow()]);
        var nearMonster = CreateRuntimeMonsterInstance(worldData, monsterSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 50,
            strength: 300,
            spellIds: [TestChainBoltSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacySupermanFlag),
                ["XPCount"] = "23",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 840,
            y: 584,
            minHitPoints: 5_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                nearMonster,
                legacyEncodedQuery: false,
                spellId: TestChainBoltSpellId,
                x: 830,
                y: 584,
                opponentUid: checked((uint)primarySobNpc.Id))).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(primarySobNpc.X, spell.X);
        Assert.Equal(primarySobNpc.Y, spell.Y);
        Assert.Equal(TestChainBoltSpellId, spell.SpellId);

        var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { checked((uint)primarySobNpc.Id), checked((uint)insideSobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.DoesNotContain(checked((uint)edgeSobNpc.Id), damageByUid.Keys);
        Assert.DoesNotContain(nearMonster.Uid, damageByUid.Keys);
        Assert.DoesNotContain(TargetPlayerUid, damageByUid.Keys);
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        Assert.Equal(primarySobNpc.Life - (int)damageByUid[checked((uint)primarySobNpc.Id)], sobNpcStates.GetVisible(primarySobNpc).Life);
        Assert.Equal(insideSobNpc.Life - (int)damageByUid[checked((uint)insideSobNpc.Id)], sobNpcStates.GetVisible(insideSobNpc).Life);
        Assert.Equal(edgeSobNpc.Life, sobNpcStates.GetVisible(edgeSobNpc).Life);
        Assert.True(monsterStates.TryGetVisibleInstance(nearMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(nearMonster.MaxLife, untouchedMonster.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);

        AssertChainBoltNonXpBranchDidNotOpenXpSkill(sessions, casterConnection, casterSent);
        AssertChainBoltNonXpBranchDidNotLogUnsupportedGap(log.ToString());
        AssertChainBoltMatchingDecompileEvidence(log.ToString());
    }

    [Fact]
    public async Task ChainBoltWithoutXpListPlayerPrimaryChainsOnlyInsideStrictOneThirdRange()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            [CreateMatchingDecompileChainBoltLevelZeroRow()]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection([]);
        var insideConnection = CreateSendingConnection([]);
        var edgeConnection = CreateSendingConnection([]);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 50,
            strength: 300,
            spellIds: [TestChainBoltSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
                ["StatusFlags"] = BuildStatusFlags(LegacySupermanFlag),
                ["XPCount"] = "23",
            });
        var primarySession = CreateSession(
            uid: TargetPlayerUid,
            name: "Primary",
            x: 836,
            y: 584,
            minHitPoints: 100_000);
        var insideSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "Inside",
            x: 838,
            y: 584,
            minHitPoints: 100_000);
        var edgeSession = CreateSession(
            uid: TargetPlayerUid + 2,
            name: "Edge",
            x: 839,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);
        sessions.Set(insideConnection, insideSession);
        sessions.Set(edgeConnection, edgeSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)primarySession.Character.X),
                Y: checked((ushort)primarySession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: TestChainBoltSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(checked((ushort)primarySession.Character.X), spell.X);
        Assert.Equal(checked((ushort)primarySession.Character.Y), spell.Y);
        Assert.Equal(TestChainBoltSpellId, spell.SpellId);
        var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { TargetPlayerUid, TargetPlayerUid + 1 }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.DoesNotContain(TargetPlayerUid + 2, damageByUid.Keys);
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        Assert.True(sessions.TryGet(primaryConnection, out var updatedPrimarySession));
        Assert.NotNull(updatedPrimarySession);
        Assert.Equal(100_000 - (long)damageByUid[TargetPlayerUid], updatedPrimarySession.Character.MinHitPoints);
        Assert.True(sessions.TryGet(insideConnection, out var updatedInsideSession));
        Assert.NotNull(updatedInsideSession);
        Assert.Equal(100_000 - (long)damageByUid[TargetPlayerUid + 1], updatedInsideSession.Character.MinHitPoints);
        Assert.True(sessions.TryGet(edgeConnection, out var updatedEdgeSession));
        Assert.NotNull(updatedEdgeSession);
        Assert.Equal(100_000, updatedEdgeSession.Character.MinHitPoints);

        AssertChainBoltNonXpBranchDidNotOpenXpSkill(sessions, casterConnection, casterSent);
        AssertChainBoltNonXpBranchDidNotLogUnsupportedGap(log.ToString());
        AssertChainBoltMatchingDecompileEvidence(log.ToString());
    }

    private static MagicType CreateMatchingDecompileChainBoltLevelZeroRow()
    {
        return CreateMagicRow(
            TestChainBoltSpellId,
            sort: 37,
            useMana: 0,
            damage: 275,
            experience: 628_464,
            maxTargets: 4,
            range: 10,
            needLevel: 40,
            coolDown: 0,
            coldTime: 0,
            duration: 60,
            damageOnMonster: 80,
            gDamage: 100);
    }

    private static void AssertChainBoltNonXpBranchDidNotOpenXpSkill(
        WorldSessionStore sessions,
        ConquerTcpConnection casterConnection,
        IReadOnlyList<byte[]> sent)
    {
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(50, updatedCasterSession.Character.MinMana);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacySupermanFlag));
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, TestChainBoltFlag));
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyChainBoltExpiresAtUtcAttribute));
        Assert.Equal("23", updatedCasterSession.Character.Attributes["XPCount"]);
        Assert.DoesNotContain(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.XPList or LegacyMsgUpdateDataType.XPCircle));
    }

    private static void AssertChainBoltNonXpBranchDidNotLogUnsupportedGap(string output)
    {
        Assert.DoesNotContain("unsupported-magic", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
    }

    private static void AssertChainBoltMatchingDecompileEvidence(string output)
    {
        Assert.Contains(
            @"gameplayEvidence=matching-decompile=DecompiledSource\GameServer\MrRayzo.Game.MsgServer.AttackHandler\ChainBolt.cs",
            output);
        Assert.Contains(
            "dispatch=DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgAttackPacket.cs " +
            "MagicSort.ChainBolt->ChainBolt.Execute",
            output);
        Assert.Contains(
            "magic=GameServer\\DBServer\\magictype.txt rows 10309/10343-10346 " +
            "id=10309 sort=37 ranges=10/12/14/15/15 duration=60",
            output);
        Assert.Contains(
            "status=DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgUpdate.cs XPList=4 ChaintBolt=0x5C",
            output);
        Assert.DoesNotMatch(@"[A-Za-z]:[\\/]", output);
    }
}
