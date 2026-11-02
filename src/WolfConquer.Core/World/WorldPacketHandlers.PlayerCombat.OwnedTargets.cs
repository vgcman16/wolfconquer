using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct OwnedPhysicalNonPlayerTargets(
        LegacyPhysicalTargetPresence MonsterPresence, LegacyMonsterInstance? Monster, Monster? Family, int MonsterRange,
        LegacyPhysicalTargetPresence SobPresence, SobNpc? Sob)
    {
        internal int NamespaceCount => (MonsterPresence != LegacyPhysicalTargetPresence.Absent ? 1 : 0) +
            (SobPresence != LegacyPhysicalTargetPresence.Absent ? 1 : 0);
    }

    private static OwnedPhysicalNonPlayerTargets ReadOwnedPhysicalNonPlayerTargets(WorldPacketHandlerContext context,
        WorldSession actor, uint uid)
    {
        LegacyPhysicalTargetPresence monsterPresence = LegacyPhysicalTargetPresence.Absent, sobPresence = LegacyPhysicalTargetPresence.Absent;
        LegacyMonsterInstance? monster = null; Monster? family = null; int range = 0; SobNpc? sob = null;
        if (actor.Character.MapId is < int.MinValue or > int.MaxValue || context.WorldData is not { } data)
            return new(LegacyPhysicalTargetPresence.Unavailable, null, null, 0, sobPresence, null);
        LegacyNativeActorScalarState.NativeValueProof location;
        try { location = new(actor.Character); }
        catch (InvalidOperationException)
        { return new(LegacyPhysicalTargetPresence.Unavailable, null, null, 0, sobPresence, null); }
        if (context.MonsterStates is { } monsters)
            monsterPresence = monsters.ReadRegisteredPhysicalTarget(uid, checked((int)actor.Character.MapId),
                location.Value.DynamicID, out monster, out family, out range);
        if (context.SobNpcStates is { } npcs)
        {
            IReadOnlyList<SobNpc> rows = [];
            if (uid <= int.MaxValue && data.TryGetOwnedSobRowsById((int)uid, out var adopted)) rows = adopted;
            sobPresence = npcs.ReadRegisteredPhysicalTarget(uid, checked((int)actor.Character.MapId), rows, out sob);
        }
        return location.IsCurrentPlain() ? new(monsterPresence, monster, family, range, sobPresence, sob) :
            new(LegacyPhysicalTargetPresence.Unavailable, null, null, 0, LegacyPhysicalTargetPresence.Unavailable, null);
    }

    private static bool CanEnterOwnedPhysicalNonPlayer(WorldPacketHandlerContext context, WorldSession actor,
        OwnedPhysicalNonPlayerTargets targets)
    {
        if (HasLegacyStatusFlag(actor.Character, LegacyNeptuneCurseFlag)) return false;
        if (targets.MonsterPresence != LegacyPhysicalTargetPresence.Absent)
        {
            if (targets.MonsterPresence != LegacyPhysicalTargetPresence.Live || targets.Monster is not { } monster ||
                targets.Family is not { } family || !CanSeeMonster(actor.Character, monster) ||
                !CanLegacyPlayerAttackMonster(actor.Character, monster, family, out _,
                    enforceD13UidRange: context.ProtocolProfile == ConquerProtocolProfile.VisibleDx8D13)) return false;
            if (actor.NativeGameClient?.OrdinarySpellBinding is not { IsRetired: false } binding ||
                !LegacyNativeMagicQueryClient.TryReadAttackRangeCache(binding, out var cache) || cache is null) return false;
            long distance = LegacyGetDistance(actor.Character.X, actor.Character.Y, monster.X, monster.Y);
            // FatalStrike still needs its actual status/caster owner. Its raw
            // saved alias cannot authorize a distance bypass or damage branch.
            if (HasLegacyStatusFlag(actor.Character, LegacyFatalStrikeFlag)) return false;
            return distance <= cache.GetAttackRange(targets.MonsterRange) || HasOwnedPhysicalGearDistance(actor, distance);
        }
        return targets.SobPresence == LegacyPhysicalTargetPresence.Live && targets.Sob is { } sob &&
            CanSeeSobNpc(actor.Character, sob) &&
            (LegacyGetDistance(actor.Character.X, actor.Character.Y, sob.X, sob.Y) <= 3 ||
                HasOwnedPhysicalGearDistance(actor, LegacyGetDistance(actor.Character.X, actor.Character.Y, sob.X, sob.Y))) &&
            CanLegacySobNpcMagicDamageTarget(context, actor.Character, sob, out _);
    }

    private static bool HasOwnedPhysicalGearDistance(WorldSession actor, long distance)
    {
        var gear = actor.CombatGear ?? CombatGearSnapshot.Empty;
        return distance <= LegacyDirectPlayerPhysicalDragonhowlRange &&
                HasLegacyCombatGearEquippedForClass(gear, actor.Character.Class, LegacyCombatGearDragonhowlItemId) ||
            distance <= LegacyDirectPlayerPhysicalArcherCombatGearRange &&
                (HasLegacyCombatGearEquippedForClass(gear, actor.Character.Class, LegacyCombatGearStoneCrackerItemId) ||
                 HasLegacyCombatGearEquippedForClass(gear, actor.Character.Class, LegacyCombatGearColdMoonItemId) ||
                 HasLegacyCombatGearEquippedForClass(gear, actor.Character.Class, LegacyCombatGearTharnCutterItemId));
    }
}
