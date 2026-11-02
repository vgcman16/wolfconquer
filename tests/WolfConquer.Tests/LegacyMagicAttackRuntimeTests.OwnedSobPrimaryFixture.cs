using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static Task RunOwnedSobPrimaryAsync(byte sort, string primaryKind,
        Func<OwnedEquipmentBehaviorFixture, SobNpc[], SobNpcRuntimeStateStore, Task> exercise)
    {
        var left = CreateEventSobNpc() with { Id = 65_011, X = 84, SourceLine = 11 };
        var middle = primaryKind == "neutral"
            ? CreateNeutralSobNpc() with { Id = 65_012, X = 85, SourceLine = 12 }
            : CreateEventSobNpc() with { Id = 65_012, X = 85, SourceLine = 12,
                Life = primaryKind == "catalog-dead" ? 0 : 50_000_000 };
        if (primaryKind is "guild-war" or "guild-war-attribute") middle = CreateEventSobNpc() with { Id = LegacySobNpcEventRules.GuildWarPillarUid, X = 85, SourceLine = 12 };
        var right = CreateEventSobNpc() with { Id = 65_013, X = 86, SourceLine = 13 };
        SobNpc[] sobs = [left, middle, right];
        var spellId = sort == 5 ? LightningBombSpellId : ThunderSpellId;
        var catalog = CreateMagicCatalogWithSobNpcs(CreateMonsterFamily(), sobs,
            CreateMagicRow(spellId, sort, useMana: 7, useStamina: 5, damage: 120,
                range: 12, maxTargets: 12, coldTime: 10_000));
        var session = CreateSession(mapId: middle.MapId, x: 82, y: 70, minHitPoints: 1000,
            minMana: 500, stamina: 100, spellIds: [spellId]);
        if (primaryKind is "guild-war" or "guild-war-attribute")
        {
            var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["GuildID"] = "73" };
            if (primaryKind == "guild-war-attribute") attributes["LegacyNewGuildWarProcess"] = "Alive";
            session = session with { Character = session.Character with { Attributes = attributes } };
        }
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        return OwnedEquipmentBehaviorFixture.RunWithEnabledSchedulingAsync(
            new(session.Character, session.PlayerItems, session.PlayerSpells, session.PlayerProficiencies), catalog,
            new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromHours(1), clock), async fixture =>
            {
                Assert.True(fixture.HasActualWire(PlayerUid));
                await fixture.SendAsync(PlayerUid, new MsgActionPacket
                    { ObjId = PlayerUid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
                var owner = fixture.Current(PlayerUid);
                Assert.True(owner.NativePlayer!.Alive);
                Assert.True(fixture.Store.IsBoundSpellOwnerCurrent(owner.NativeGameClient!.OrdinarySpellBinding!));
                var context = fixture.Handler.CreateMonsterRuntimeContext(new(fixture.Connection(PlayerUid), owner));
                var states = Assert.IsType<SobNpcRuntimeStateStore>(context.SobNpcStates);
                if (primaryKind == "runtime-dead")
                {
                    Assert.True(middle.Life > 0);
                    var death = states.ApplyDamage(middle, checked((uint)middle.Life),
                        clock.GetUtcNow(), LegacySobNpcDeathBehavior.PersistentDeath);
                    Assert.True(death.Killed);
                    Assert.Equal(0, states.GetVisible(middle).Life);
                }
                fixture.ClearRecordedFrames(PlayerUid);
                await exercise(fixture, sobs, states);
            });
    }

    private static byte[] OwnedSobQuery(SobNpc selected, byte sort) =>
        new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: checked((uint)selected.Id),
            X: checked((ushort)(sort == 48 ? 86 : selected.X)), Y: checked((ushort)selected.Y),
            AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
            Effect: MsgAttackEffect.None, Unknown: 0,
            SpellId: sort == 5 ? LightningBombSpellId : ThunderSpellId)).ToFrameBytes();
}
