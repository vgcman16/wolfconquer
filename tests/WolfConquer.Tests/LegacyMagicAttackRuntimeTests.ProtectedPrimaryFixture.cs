using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static Task RunProtectedPrimaryAdmissionAsync(byte sort,
        Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance[], Task> exercise)
    {
        var ordinary = CreateHighLifeMonsterFamily();
        var protectedFamily = ordinary with
        {
            Id = 10_000,
            Name = "ProtectedTrainingMonster",
            Attributes = new Dictionary<string, string>(ordinary.Attributes) { ["settings"] = "98" },
        };
        var spawns = new[]
        {
            CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834),
            CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 836,
                monsterTypeId: protectedFamily.Id, sourceMonsterName: protectedFamily.Name),
            CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 838),
        };
        var spellId = sort == 5 ? LightningBombSpellId : ThunderSpellId;
        var data = CreateMagicCatalogCore(ordinary, spawns, [],
            [CreateMagicRow(spellId, sort, useMana: 7, useStamina: 5, range: 12,
                maxTargets: 12, coldTime: 10_000)], monsterRows: [ordinary, protectedFamily]);
        var monsters = spawns.Select((spawn, index) => CreateRuntimeMonsterInstance(
            data, spawn, index == 1 ? protectedFamily : ordinary)).ToArray();
        var session = CreateSession(minHitPoints: 1000, minMana: 500, stamina: 100,
            strength: 300, spellIds: [spellId]);
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        return OwnedEquipmentBehaviorFixture.RunWithEnabledSchedulingAsync(
            new(session.Character, session.PlayerItems, session.PlayerSpells, session.PlayerProficiencies), data,
            new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromHours(1), clock),
            async f =>
            {
                // Existing genuine TCP writer/reader; the owned clock remains
                // fixed because these controls exercise casts, not timer ticks.
                Assert.True(f.HasActualWire(PlayerUid));
                await f.SendAsync(PlayerUid, new MsgActionPacket
                    { ObjId = PlayerUid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
                Assert.True(f.Current(PlayerUid).NativePlayer!.Alive);
                Assert.Equal(500U, f.Current(PlayerUid).NativePlayer!.Mana);
                Assert.Equal((ushort)100, f.Current(PlayerUid).NativePlayer!.Stamina);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Current(PlayerUid).NativeGameClient!.OrdinarySpellBinding!));
                f.ClearRecordedFrames(PlayerUid);
                await exercise(f, monsters);
            });
    }

    private static byte[] ProtectedPrimaryQuery(LegacyMonsterInstance monster, byte sort) =>
        new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false,
            spellId: sort == 5 ? LightningBombSpellId : ThunderSpellId,
            x: sort == 5 ? monster.X : (ushort)838, y: monster.Y)).ToFrameBytes();

    private static void AssertProtectedPrimaryRefusal(OwnedEquipmentBehaviorFixture f, WorldSession before,
        Dictionary<string, byte[]> files, IReadOnlyList<LegacyMonsterInstance> monsters)
    {
        Assert.Same(before, f.Current(PlayerUid));
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(before.NativeGameClient!.OrdinarySpellBinding!));
        Assert.Equal(500L, before.Character.MinMana);
        Assert.Equal(500U, before.NativePlayer!.Mana);
        Assert.Equal("100", before.Character.Attributes["Stamina"]);
        Assert.Equal((ushort)100, before.NativePlayer.Stamina);
        Assert.Null(before.MagicCooldowns);
        Assert.Equal((ushort)0, before.LegacyRandomSpell);
        Assert.Equal(0, Assert.Single(before.PlayerSpells.Spells).Experience);
        Assert.Empty(f.ActorFrames);
        ProcFilesUnchanged(f, files);
        Assert.False(f.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var states = Assert.IsType<MonsterRuntimeStateStore>(f.Handler.CreateMonsterRuntimeContext(
            new(f.Connection(PlayerUid), before)).MonsterStates);
        foreach (var monster in monsters)
        {
            Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visible));
            Assert.Equal(monster.Hitpoints, visible.Hitpoints);
        }
    }
}
