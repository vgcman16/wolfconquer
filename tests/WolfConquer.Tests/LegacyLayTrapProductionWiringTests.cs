using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLayTrapProductionWiringTests
{
    [Fact]
    public void Case12070MaterializesSourceBackedSelfInteractionAndSpellAnimation()
    {
        var projection = LegacyLayTrap.Project(CastInput());

        var wiring = LegacyLayTrapProductionWiringHarness.Materialize(
            projection,
            mapId: 1015,
            now: DateTimeOffset.Parse("2026-06-12T12:00:00Z"));

        Assert.Equal(
            [
                LegacyLayTrapProductionEmissionKind.SelfInteraction,
                LegacyLayTrapProductionEmissionKind.FloorEffect,
                LegacyLayTrapProductionEmissionKind.FloorEffect,
                LegacyLayTrapProductionEmissionKind.FloorEffect,
                LegacyLayTrapProductionEmissionKind.SpellAnimation,
            ],
            wiring.EmissionOrder);

        Assert.Equal((uint)1_293_600, wiring.SelfInteraction.Query.Uid);
        Assert.Equal((uint)1_293_600, wiring.SelfInteraction.Query.OpponentUid);
        Assert.Equal((ushort)110, wiring.SelfInteraction.Query.X);
        Assert.Equal((ushort)200, wiring.SelfInteraction.Query.Y);
        Assert.Equal(MsgAttackType.None, wiring.SelfInteraction.Query.AttackType);
        Assert.Equal(0, wiring.SelfInteraction.Query.Damage);
        Assert.Equal(0, wiring.SelfInteraction.Query.KilledMonster);
        Assert.Equal((uint)0, wiring.SelfInteraction.Query.ResponseDamage);
        Assert.Equal(MsgAttackEffect.None, wiring.SelfInteraction.Query.Effect);
        Assert.Equal((uint)0, wiring.SelfInteraction.Query.Unknown);

        Assert.Equal((uint)1_293_600, wiring.SpellAnimation.AttackerUid);
        Assert.Equal((uint)1_293_600, wiring.SpellAnimation.AttackedUid);
        Assert.Equal((ushort)110, wiring.SpellAnimation.X);
        Assert.Equal((ushort)200, wiring.SpellAnimation.Y);
        Assert.Equal((ushort)12_070, wiring.SpellAnimation.SpellId);
        Assert.Equal((byte)4, wiring.SpellAnimation.SpellLevel);
        Assert.Equal((ushort)0, wiring.SpellAnimation.NextSpell);
        Assert.Equal((byte)2, wiring.SpellAnimation.LevelHu);
        Assert.Equal((byte)0, wiring.SpellAnimation.SpellEffect);
        Assert.Empty(wiring.SpellAnimation.Targets);
    }

    [Fact]
    public void Case12070MaterializesThreeFloorEffectEntriesWithSourceBackedDueTimes()
    {
        var now = DateTimeOffset.Parse("2026-06-12T12:00:00Z");
        var projection = LegacyLayTrap.Project(CastInput());

        var wiring = LegacyLayTrapProductionWiringHarness.Materialize(
            projection,
            mapId: 1015,
            now);

        Assert.Equal(
            new[]
            {
                (Uid: 900_092U, X: (ushort)103, Y: (ushort)200, DueAt: now),
                (Uid: 900_093U, X: (ushort)110, Y: (ushort)200, DueAt: now.AddMilliseconds(300)),
                (Uid: 900_094U, X: (ushort)117, Y: (ushort)200, DueAt: now.AddMilliseconds(600)),
            },
            wiring.FloorEffects
                .Select(entry => (entry.Packet.Uid, entry.Packet.X, entry.Packet.Y, entry.Effect!.DueAt))
                .ToArray());

        Assert.All(wiring.FloorEffects, entry =>
        {
            Assert.Equal(1015, entry.MapId);
            Assert.Equal(FloorItemRuntimePickupKind.Item, entry.Kind);
            Assert.Null(entry.Item);
            Assert.False(entry.OwnerOnly);
            Assert.NotNull(entry.Effect);

            Assert.Equal((uint)40, entry.Packet.ItemId);
            Assert.Equal((ushort)0, entry.Packet.MaxLife);
            Assert.Equal(LegacyFloorItemDropType.Effect, entry.Packet.DropType);
            Assert.Equal((uint)0, entry.Packet.Life);
            Assert.Equal((byte)0, entry.Packet.PlusOrColor);
            Assert.Equal((uint)1_293_600, entry.Packet.ItemOwnerUid);
            Assert.Equal((uint)0, entry.Packet.GuildId);
            Assert.Equal((byte)0, entry.Packet.FlowerType);
            Assert.Equal((ulong)0, entry.Packet.Timer);
            Assert.Equal((ushort)100, entry.Packet.OwnerX);
            Assert.Equal((ushort)200, entry.Packet.OwnerY);
            Assert.Equal("trap", entry.Packet.Name);

            Assert.Equal((ushort)12_070, entry.Effect!.SpellId);
            Assert.Equal((ushort)100, entry.Effect.OriginX);
            Assert.Equal((ushort)200, entry.Effect.OriginY);
            Assert.Equal(3, entry.Effect.Distance);
            Assert.Equal(3.0, entry.Effect.HitRadius);
        });

        var store = new FloorItemRuntimeStateStore();
        foreach (var entry in wiring.FloorEffects)
        {
            store.AddVisible(entry, now);
        }

        Assert.Equal([wiring.FloorEffects[0]], store.ConsumeDueEffects(12_070, now));
        Assert.Empty(store.ConsumeDueEffects(12_070, now.AddMilliseconds(299)));
        Assert.Equal([wiring.FloorEffects[1]], store.ConsumeDueEffects(12_070, now.AddMilliseconds(300)));
        Assert.Equal([wiring.FloorEffects[2]], store.ConsumeDueEffects(12_070, now.AddMilliseconds(600)));
    }

    private static LegacyLayTrapCastInput CastInput()
    {
        return new LegacyLayTrapCastInput(
            SpellId: 12_070,
            SpellLevel: 4,
            SpellSoulLevel: 2,
            CasterUid: 1_293_600,
            GuildId: 0,
            CasterX: 100,
            CasterY: 200,
            TargetX: 110,
            TargetY: 200,
            FirstFloorUid: 900_092);
    }
}

internal sealed record LegacyLayTrapProductionWiring(
    MsgAttackPacket SelfInteraction,
    LegacySpellAnimationPacket SpellAnimation,
    IReadOnlyList<FloorItemRuntimeEntry> FloorEffects,
    IReadOnlyList<LegacyLayTrapProductionEmissionKind> EmissionOrder);

internal enum LegacyLayTrapProductionEmissionKind
{
    SelfInteraction,
    FloorEffect,
    SpellAnimation,
}

internal static class LegacyLayTrapProductionWiringHarness
{
    public static LegacyLayTrapProductionWiring Materialize(
        LegacyLayTrapProjection projection,
        long mapId,
        DateTimeOffset now)
    {
        Assert.True(projection.Projected);
        Assert.NotNull(projection.CastAnimation);

        var animation = projection.CastAnimation!;
        Assert.True(animation.SendsSelfInteraction);
        var selfInteraction = new MsgAttackPacket(new MsgAttackQuery(
            animation.CasterUid,
            animation.CasterUid,
            animation.X,
            animation.Y,
            MsgAttackType.None,
            checked((int)animation.InteractionDamage),
            KilledMonster: 0,
            ResponseDamage: 0,
            MsgAttackEffect.None,
            Unknown: 0));

        var spellAnimation = new LegacySpellAnimationPacket(
            animation.CasterUid,
            animation.TargetUid,
            animation.X,
            animation.Y,
            animation.SpellId,
            animation.SpellLevel,
            NextSpell: 0,
            animation.SoulLevel,
            SpellEffect: 0,
            Targets: []);

        var floorEffects = projection.FloorEffects
            .Select(effect =>
            {
                var packet = new LegacyFloorItemPacket(
                    effect.FloorUid,
                    effect.ItemId,
                    effect.X,
                    effect.Y,
                    MaxLife: 0,
                    effect.DropType,
                    Life: 0,
                    effect.Color,
                    effect.ItemOwnerUid,
                    effect.GuildId,
                    FlowerType: 0,
                    Timer: 0,
                    effect.OwnerX,
                    effect.OwnerY,
                    effect.Name);
                var runtimeEffect = new FloorItemRuntimeEffect(
                    animation.SpellId,
                    effect.OwnerX,
                    effect.OwnerY,
                    now.AddMilliseconds(effect.InvokeDelayMilliseconds),
                    effect.TriggerRadius,
                    effect.TriggerRadius);

                return FloorItemRuntimeEntry.ForEffect(mapId, packet, runtimeEffect);
            })
            .ToArray();

        var emissionOrder = new List<LegacyLayTrapProductionEmissionKind>(floorEffects.Length + 2)
        {
            LegacyLayTrapProductionEmissionKind.SelfInteraction,
        };
        emissionOrder.AddRange(Enumerable.Repeat(
            LegacyLayTrapProductionEmissionKind.FloorEffect,
            floorEffects.Length));
        emissionOrder.Add(LegacyLayTrapProductionEmissionKind.SpellAnimation);

        return new LegacyLayTrapProductionWiring(selfInteraction, spellAnimation, floorEffects, emissionOrder);
    }
}
