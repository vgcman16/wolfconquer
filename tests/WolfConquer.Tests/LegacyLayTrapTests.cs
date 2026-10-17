using System.Linq;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed class LegacyLayTrapTests
{
    [Fact]
    public void LayTrapCsCase12070TwilightDanceConstantsStaySourceBacked()
    {
        // Source: magictype.txt rows 12070-12076 are sort 21 TwilightDance; LayTrap.cs case 12070 uses item 40 named "trap".
        var definition = LegacyLayTrap.TwilightDance;

        Assert.Equal((ushort)12_070, definition.SpellId);
        Assert.Equal(LegacyMagicSort.LayTrap, definition.Sort);
        Assert.Equal((byte)21, LegacyLayTrap.LayTrapSortValue);
        Assert.Equal("TwilightDance", definition.Name);
        Assert.Equal((uint)40, definition.FloorItemId);
        Assert.Equal((byte)0, definition.FloorColor);
        Assert.Equal(LegacyFloorItemDropType.Effect, definition.DropType);
        Assert.Equal("trap", definition.FloorName);
        Assert.Equal((byte)160, definition.CoordinateMaxDistance);
        Assert.Equal(3, definition.FirstTrapLineIndex);
        Assert.Equal(7, definition.TrapLineIndexStride);
        Assert.Equal(3, definition.MaxTraps);
        Assert.Equal(300, definition.TriggerDelayStepMilliseconds);
        Assert.Equal(0, definition.ElseTriggerDelayMilliseconds);
        Assert.Equal(3, definition.TriggerRadius);
        Assert.Equal(18, definition.VisibilityRadius);
        Assert.Equal((uint)434, definition.RemovalActionType);
        Assert.Equal(70, definition.PlayerDamagePercent);
        Assert.Equal(LegacyFloorItemDropType.RemoveEffect, LegacyLayTrap.TwilightDanceRemovalDropType);
        Assert.Equal(15, LegacyLayTrap.GetTwilightDancePlayerAttackBonus(remainingTrapCount: 3));
        Assert.Equal(25, LegacyLayTrap.GetTwilightDancePlayerAttackBonus(remainingTrapCount: 2));
        Assert.Equal(35, LegacyLayTrap.GetTwilightDancePlayerAttackBonus(remainingTrapCount: 1));
        Assert.Contains(LegacyLayTrap.TwilightDanceSourceEvidence, evidence => evidence.Contains("LayTrap.cs case 12070"));
        Assert.Contains(LegacyLayTrap.TwilightDanceSourceEvidence, evidence => evidence.Contains("ThreadPool.cs case 12070"));
    }

    [Fact]
    public void ThreadPoolCase12070RuntimeMetadataStaysSourceBacked()
    {
        // Source: ThreadPool.cs case 12070 uses radius <= 3, Damage * 70 / 100, ActionQuery Type 434, and removal visibility <= 18.
        var runtime = LegacyLayTrap.TwilightDanceRuntime;

        Assert.Equal(3, runtime.TriggerRadius);
        Assert.Equal(70, runtime.PlayerDamagePercent);
        Assert.Equal(0.70m, runtime.PlayerDamageMultiplier);
        Assert.Equal(863U, runtime.ApplyPlayerDamageMultiplier(1_234U));
        Assert.Equal(863U, LegacyLayTrap.ApplyTwilightDancePlayerDamageMultiplier(1_234U));
        Assert.Equal((uint)434, runtime.RemovalActionType);
        Assert.Equal(LegacyFloorItemDropType.RemoveEffect, runtime.RemovalDropType);
        Assert.Equal(18, runtime.RemovalVisibilityRadius);
        Assert.Equal(15, LegacyLayTrap.GetTwilightDancePlayerAttackBonus(remainingTrapCount: 3));
        Assert.Equal(25, LegacyLayTrap.GetTwilightDancePlayerAttackBonus(remainingTrapCount: 2));
        Assert.Equal(35, LegacyLayTrap.GetTwilightDancePlayerAttackBonus(remainingTrapCount: 1));
    }

    [Fact]
    public void ThreadPoolCase12070TriggerRangeUsesInclusiveLegacyDistanceThree()
    {
        // Source: ThreadPool.cs case 12070 checks Core.GetDistance(target, trap) <= 3 for monsters, players, and SOB NPCs.
        var runtime = LegacyLayTrap.TwilightDanceRuntime;

        Assert.True(runtime.IsTargetInsideTriggerRadius(100, 200, 103, 200));
        Assert.True(runtime.IsTargetInsideTriggerRadius(100, 200, 103, 203));
        Assert.True(LegacyLayTrap.IsTwilightDanceTriggerTargetInRange(100, 200, 100, 197));
        Assert.False(runtime.IsTargetInsideTriggerRadius(100, 200, 104, 200));
        Assert.False(LegacyLayTrap.IsTwilightDanceTriggerTargetInRange(100, 200, 104, 204));
    }

    [Fact]
    public void ThreadPoolCase12070RemovalProjectionCarriesActionAndVisibilityMetadata()
    {
        // Source: ThreadPool.cs case 12070 sends ActionQuery Type 434 with owner/trap coordinates and remove-effect fanout within distance <= 18.
        var action = LegacyLayTrap.BuildTwilightDanceRemovalAction(
            ownerUid: 1_293_600,
            ownerX: 100,
            ownerY: 200,
            trapX: 117,
            trapY: 200);

        Assert.Equal((uint)1_293_600, action.OwnerUid);
        Assert.Equal((ushort)100, action.OwnerX);
        Assert.Equal((ushort)200, action.OwnerY);
        Assert.Equal((ushort)117, action.TrapX);
        Assert.Equal((ushort)200, action.TrapY);
        Assert.Equal((uint)434, action.ActionType);
        Assert.Equal(LegacyFloorItemDropType.RemoveEffect, LegacyLayTrap.TwilightDanceRuntime.RemovalDropType);
        Assert.True(LegacyLayTrap.TwilightDanceRuntime.IsViewerInsideRemovalVisibility(117, 200, 135, 200));
        Assert.True(LegacyLayTrap.IsTwilightDanceRemovalVisibleToViewer(117, 200, 135, 218));
        Assert.False(LegacyLayTrap.TwilightDanceRuntime.IsViewerInsideRemovalVisibility(117, 200, 136, 200));
    }

    [Fact]
    public void LayTrapThreeSourceDdaProjectsHorizontalTrapLineAndFloorEffectFields()
    {
        // Source: LayTrapThree selects DDA line indices 3, 10, and 17; LayTrap.cs stamps delays as 0/300/600.
        var projection = LegacyLayTrap.Project(new LegacyLayTrapCastInput(
            SpellId: 12_070,
            SpellLevel: 4,
            SpellSoulLevel: 2,
            CasterUid: 1_293_600,
            GuildId: 77,
            CasterX: 100,
            CasterY: 200,
            TargetX: 110,
            TargetY: 200,
            FirstFloorUid: 900_092));

        Assert.True(projection.Projected);
        Assert.Equal(LegacyLayTrapProjectionStatus.Projected, projection.Status);
        Assert.NotNull(projection.ClientFloorSpellState);
        Assert.NotNull(projection.CastAnimation);
        Assert.Equal(
            new[]
            {
                (X: (ushort)103, Y: (ushort)200),
                (X: (ushort)110, Y: (ushort)200),
                (X: (ushort)117, Y: (ushort)200),
            },
            projection.FloorEffects.Select(effect => (effect.X, effect.Y)).ToArray());
        Assert.Equal(new[] { 900_092U, 900_093U, 900_094U }, projection.FloorEffects.Select(effect => effect.FloorUid).ToArray());
        Assert.Equal(new[] { 0, 300, 600 }, projection.FloorEffects.Select(effect => effect.InvokeDelayMilliseconds).ToArray());
        Assert.Equal(new[] { 3, 2, 1 }, projection.FloorEffects.Select(effect => effect.RemainingTrapCountAtTrigger).ToArray());
        Assert.Equal(new[] { 15, 25, 35 }, projection.FloorEffects.Select(effect => effect.PlayerAttackBonus).ToArray());

        Assert.All(projection.FloorEffects, effect =>
        {
            Assert.Equal((uint)40, effect.ItemId);
            Assert.Equal((byte)0, effect.Color);
            Assert.Equal(LegacyFloorItemDropType.Effect, effect.DropType);
            Assert.Equal((uint)1_293_600, effect.ItemOwnerUid);
            Assert.Equal((uint)77, effect.GuildId);
            Assert.Equal((ushort)100, effect.OwnerX);
            Assert.Equal((ushort)200, effect.OwnerY);
            Assert.Equal("trap", effect.Name);
            Assert.Equal(0, effect.ElseInvokeDelayMilliseconds);
            Assert.Equal(3, effect.TriggerRadius);
            Assert.Equal(70, effect.PlayerDamagePercent);
            Assert.True(effect.RemovesOnTrigger);
        });

        var state = projection.ClientFloorSpellState!;
        Assert.Equal((uint)1_293_600, state.OwnerUid);
        Assert.Equal((ushort)110, state.X);
        Assert.Equal((ushort)200, state.Y);
        Assert.Equal((byte)2, state.SoulLevel);
        Assert.Equal((ushort)0, state.AttackerX);
        Assert.Equal((ushort)0, state.AttackerY);

        var animation = projection.CastAnimation!;
        Assert.Equal((uint)1_293_600, animation.CasterUid);
        Assert.Equal((uint)1_293_600, animation.TargetUid);
        Assert.Equal((ushort)110, animation.X);
        Assert.Equal((ushort)200, animation.Y);
        Assert.Equal((ushort)12_070, animation.SpellId);
        Assert.Equal((byte)4, animation.SpellLevel);
        Assert.Equal((byte)2, animation.SoulLevel);
        Assert.True(animation.SendsSelfInteraction);
        Assert.Equal((uint)0, animation.InteractionDamage);
        Assert.Equal((uint)0, animation.InteractionAttackType);
    }

    [Fact]
    public void LayTrapThreeSourceDdaProjectsRoundedDiagonalTrapLine()
    {
        // Source: LayTrapThree uses Math.Round over float DDA steps before selecting every seventh trap coordinate.
        var coordinates = LegacyLayTrap.SelectTwilightDanceCoordinates(
            casterX: 100,
            casterY: 100,
            targetX: 112,
            targetY: 105);

        Assert.Equal(
            new[]
            {
                new LegacyLayTrapCoordinate(103, 101),
                new LegacyLayTrapCoordinate(110, 104),
                new LegacyLayTrapCoordinate(117, 107),
            },
            coordinates.ToArray());
    }

    [Fact]
    public void ProjectRejectsUnsupportedLayTrapSpellWithoutRuntimeWiring()
    {
        // Source: this sidecar intentionally covers only LayTrap.cs case 12070; parent wiring can add other cases later.
        var projection = LegacyLayTrap.Project(new LegacyLayTrapCastInput(
            SpellId: 12_345,
            SpellLevel: 0,
            SpellSoulLevel: 0,
            CasterUid: 1,
            GuildId: 0,
            CasterX: 100,
            CasterY: 100,
            TargetX: 110,
            TargetY: 110,
            FirstFloorUid: 900_092));

        Assert.False(projection.Projected);
        Assert.Equal(LegacyLayTrapProjectionStatus.UnsupportedSpell, projection.Status);
        Assert.Null(projection.Definition);
        Assert.Null(projection.ClientFloorSpellState);
        Assert.Null(projection.CastAnimation);
        Assert.Empty(projection.FloorEffects);
        Assert.Contains("source cases 12070, 12550, and 12570", projection.Reason);
        Assert.Contains(projection.PendingSourceBehaviors, behavior => behavior.Contains("Other LayTrap.cs branches"));
    }

    [Fact]
    public void LayTrapCsCase11600DaggerStormConstantsStaySourceBacked()
    {
        Assert.Equal((ushort)11_600, LegacyLayTrap.DaggerStormSpellId);
        Assert.Equal((uint)50, LegacyLayTrap.DaggerStormNormalFloorItemId);
        Assert.Equal((uint)41, LegacyLayTrap.DaggerStormSoulOneFloorItemId);
        Assert.Equal((uint)42, LegacyLayTrap.DaggerStormSoulTwoFloorItemId);
        Assert.Equal((byte)11, LegacyLayTrap.DaggerStormFloorColor);
        Assert.Equal((byte)11, LegacyLayTrap.DaggerStormFlowerType);
        Assert.Equal(4_000, LegacyLayTrap.DaggerStormFloorLifetimeMilliseconds);
        Assert.Equal("trap", LegacyLayTrap.DaggerStormFloorName);
        Assert.Equal(50U, LegacyLayTrap.GetDaggerStormFloorItemId(useSpellSoul: 0));
        Assert.Equal(41U, LegacyLayTrap.GetDaggerStormFloorItemId(useSpellSoul: 1));
        Assert.Equal(42U, LegacyLayTrap.GetDaggerStormFloorItemId(useSpellSoul: 2));
        Assert.Equal(0U, LegacyLayTrap.GetDaggerStormFloorItemId(useSpellSoul: 3));
        Assert.Contains(LegacyLayTrap.DaggerStormSourceEvidence, evidence => evidence.Contains("LayTrap.cs case 11600"));
        Assert.Contains(LegacyLayTrap.DaggerStormSourceEvidence, evidence => evidence.Contains("NormalDaggerStorm=50"));
    }

    [Fact]
    public void LayTrapCsCase12550ConstantsStaySourceBacked()
    {
        // Source: LayTrap.cs case 12550 uses RandomFourLayTraps and FloorSpell(1001390, coord.X/Y, 14, Spell, 4000, 0, user).
        var definition = LegacyLayTrap.SourceCase12550;

        Assert.Equal((ushort)12_550, definition.SpellId);
        Assert.Equal(LegacyMagicSort.LayTrap, definition.Sort);
        Assert.Equal((uint)1_001_390, definition.FloorItemId);
        Assert.Equal((byte)14, definition.FloorColor);
        Assert.Equal(LegacyFloorItemDropType.Effect, definition.DropType);
        Assert.Equal(4, definition.MaxTraps);
        Assert.Equal(4_000, definition.TriggerDelayStepMilliseconds);
        Assert.Equal(0, definition.ElseTriggerDelayMilliseconds);
        Assert.Contains(LegacyLayTrap.SourceCases12550And12570Evidence, evidence => evidence.Contains("case 12550"));
        Assert.Contains(LegacyLayTrap.SourceCases12550And12570Evidence, evidence => evidence.Contains("RandomFourLayTraps.cs"));
    }

    [Fact]
    public void RandomFourLayTrapsUpdatePositionMatchesSourceMovementTable()
    {
        // Source: RandomFourLayTraps.UpdatePosition mutates x/y by the facing-specific deltas below.
        var rolls = new[]
        {
            new LegacyRandomFourLayTrapRoll(LegacyRandomFourLayTrapFacing.North, 3),
            new LegacyRandomFourLayTrapRoll(LegacyRandomFourLayTrapFacing.SouthEast, 4),
            new LegacyRandomFourLayTrapRoll(LegacyRandomFourLayTrapFacing.West, 5),
            new LegacyRandomFourLayTrapRoll(LegacyRandomFourLayTrapFacing.NorthEast, 6),
        };

        var coordinates = LegacyLayTrap.SelectRandomFourLayTrapCoordinates(100, 200, rolls);

        Assert.Equal(
            new[]
            {
                new LegacyLayTrapCoordinate(97, 197),
                new LegacyLayTrapCoordinate(104, 200),
                new LegacyLayTrapCoordinate(95, 205),
                new LegacyLayTrapCoordinate(100, 194),
            },
            coordinates.ToArray());
    }

    [Fact]
    public void LayTrapCsCase12550ProjectsRandomFourFloorEffectsAndAnimationTargets()
    {
        // Source: LayTrap.cs case 12550 queues one SpellObj per created FloorSpell UID with Damage=1, Hit=1, MoveX/Y=caster.
        var projection = LegacyLayTrap.Project(new LegacyLayTrapCastInput(
            SpellId: LegacyLayTrap.SourceCase12550SpellId,
            SpellLevel: 2,
            SpellSoulLevel: 1,
            CasterUid: 1_293_600,
            GuildId: 77,
            CasterX: 100,
            CasterY: 200,
            TargetX: 120,
            TargetY: 220,
            FirstFloorUid: 900_092,
            RandomFourCoordinates:
            [
                new LegacyLayTrapCoordinate(97, 197),
                new LegacyLayTrapCoordinate(104, 200),
                new LegacyLayTrapCoordinate(95, 205),
                new LegacyLayTrapCoordinate(100, 194),
            ]));

        Assert.True(projection.Projected);
        Assert.Equal(LegacyLayTrapProjectionStatus.Projected, projection.Status);
        Assert.NotNull(projection.ClientFloorSpellState);
        Assert.NotNull(projection.CastAnimation);
        Assert.Equal(new[] { 900_092U, 900_093U, 900_094U, 900_095U }, projection.FloorEffects.Select(effect => effect.FloorUid).ToArray());
        Assert.Equal(
            new[]
            {
                (X: (ushort)97, Y: (ushort)197),
                (X: (ushort)104, Y: (ushort)200),
                (X: (ushort)95, Y: (ushort)205),
                (X: (ushort)100, Y: (ushort)194),
            },
            projection.FloorEffects.Select(effect => (effect.X, effect.Y)).ToArray());

        Assert.All(projection.FloorEffects, effect =>
        {
            Assert.Equal((uint)1_001_390, effect.ItemId);
            Assert.Equal((byte)14, effect.Color);
            Assert.Equal(LegacyFloorItemDropType.Effect, effect.DropType);
            Assert.Equal(4_000, effect.InvokeDelayMilliseconds);
            Assert.Equal(0, effect.ElseInvokeDelayMilliseconds);
            Assert.Equal((uint)0, effect.ItemOwnerUid);
            Assert.Equal((uint)0, effect.GuildId);
            Assert.Equal((ushort)0, effect.OwnerX);
            Assert.Equal((ushort)0, effect.OwnerY);
            Assert.Equal((uint)1, effect.AnimationTargetDamage);
            Assert.Equal((uint)1, effect.AnimationTargetHit);
            Assert.Equal((ushort)100, effect.AnimationTargetMoveX);
            Assert.Equal((ushort)200, effect.AnimationTargetMoveY);
        });

        var animation = projection.CastAnimation!;
        Assert.Equal((uint)1_293_600, animation.CasterUid);
        Assert.Equal((uint)1_293_600, animation.TargetUid);
        Assert.Equal((ushort)120, animation.X);
        Assert.Equal((ushort)220, animation.Y);
        Assert.True(animation.SendsSelfInteraction);
        Assert.Equal((uint)0, animation.InteractionDamage);
        Assert.Equal((uint)0, animation.InteractionAttackType);
    }

    [Fact]
    public void LayTrapCsCase12550RequiresCallerSuppliedRandomCoordinates()
    {
        // Source: RandomFourLayTraps depends on Pool.GetRandom, so the pure projection reports the missing coordinate source.
        var projection = LegacyLayTrap.Project(new LegacyLayTrapCastInput(
            SpellId: LegacyLayTrap.SourceCase12550SpellId,
            SpellLevel: 2,
            SpellSoulLevel: 1,
            CasterUid: 1_293_600,
            GuildId: 77,
            CasterX: 100,
            CasterY: 200,
            TargetX: 120,
            TargetY: 220,
            FirstFloorUid: 900_092));

        Assert.False(projection.Projected);
        Assert.Equal(LegacyLayTrapProjectionStatus.MissingRandomCoordinates, projection.Status);
        Assert.Empty(projection.FloorEffects);
        Assert.NotNull(projection.CastAnimation);
        Assert.Contains(projection.PendingSourceBehaviors, behavior => behavior.Contains("does not invent random coordinates"));
    }

    [Fact]
    public void LayTrapCsCase12570ProjectsTargetFloorEffectWhenVisibleTargetResolves()
    {
        // Source: LayTrap.cs case 12570 creates FloorSpell(1001380, obj.X/Y, 14, Spell, 1500, 0, user) for visible players, monsters, or SOBs.
        var projection = LegacyLayTrap.Project(new LegacyLayTrapCastInput(
            SpellId: LegacyLayTrap.SourceCase12570SpellId,
            SpellLevel: 3,
            SpellSoulLevel: 2,
            CasterUid: 1_293_600,
            GuildId: 77,
            CasterX: 100,
            CasterY: 200,
            TargetX: 120,
            TargetY: 220,
            FirstFloorUid: 900_200,
            ResolvedTarget: new LegacyLayTrapResolvedTarget(
                Uid: 400_001,
                LegacyLayTrapTargetKind.Monster,
                X: 111,
                Y: 222)));

        Assert.True(projection.Projected);
        Assert.Single(projection.FloorEffects);
        Assert.NotNull(projection.ClientFloorSpellState);
        Assert.NotNull(projection.CastAnimation);

        var effect = projection.FloorEffects[0];
        Assert.Equal((uint)900_200, effect.FloorUid);
        Assert.Equal((uint)1_001_380, effect.ItemId);
        Assert.Equal((ushort)111, effect.X);
        Assert.Equal((ushort)222, effect.Y);
        Assert.Equal((byte)14, effect.Color);
        Assert.Equal(1_500, effect.InvokeDelayMilliseconds);
        Assert.Equal(0, effect.ElseInvokeDelayMilliseconds);
        Assert.Equal((uint)1, effect.AnimationTargetDamage);
        Assert.Equal((uint)1, effect.AnimationTargetHit);
        Assert.Equal((ushort)100, effect.AnimationTargetMoveX);
        Assert.Equal((ushort)200, effect.AnimationTargetMoveY);

        var animation = projection.CastAnimation!;
        Assert.Equal((uint)1_293_600, animation.CasterUid);
        Assert.Equal((uint)1_293_600, animation.TargetUid);
        Assert.Equal((ushort)120, animation.X);
        Assert.Equal((ushort)220, animation.Y);
        Assert.False(animation.SendsSelfInteraction);
    }

    [Fact]
    public void LayTrapCsCase12570StillProjectsAnimationWhenNoVisibleTargetResolves()
    {
        // Source: LayTrap.cs case 12570 runs UpdateSpell.CheckUpdate and sends MsgSpellAnimation outside the target-resolution branch.
        var projection = LegacyLayTrap.Project(new LegacyLayTrapCastInput(
            SpellId: LegacyLayTrap.SourceCase12570SpellId,
            SpellLevel: 3,
            SpellSoulLevel: 2,
            CasterUid: 1_293_600,
            GuildId: 77,
            CasterX: 100,
            CasterY: 200,
            TargetX: 120,
            TargetY: 220,
            FirstFloorUid: 900_200));

        Assert.True(projection.Projected);
        Assert.Null(projection.ClientFloorSpellState);
        Assert.NotNull(projection.CastAnimation);
        Assert.Empty(projection.FloorEffects);
        Assert.Contains(projection.PendingSourceBehaviors, behavior => behavior.Contains("No floor item or animation target"));
    }
}
