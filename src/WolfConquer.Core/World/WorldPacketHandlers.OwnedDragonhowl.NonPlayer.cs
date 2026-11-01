using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Called only by the private complete proc preparation. Calculations and
    // bytes remain DATA; mutation requires the retained registered Store token.
    private static bool PrepareDragonhowlMonsterPhases(WorldSessionStore store, WorldSessionStore.OwnedDragonhowlOwner owner,
        WorldPacketHandlerContext context, LegacyMagicRuntimeSpell spell, DragonhowlGeometry geometry,
        LegacyOwnedPhysicalMetadataWitness metadata, ref WorldSession source, List<DragonhowlPhase> phases,
        Dictionary<ConquerTcpConnection, WorldSession> finals, List<LegacySpellAnimationTarget> animation,
        Dictionary<ConquerTcpConnection, WorldSession> calculated,
        Dictionary<ConquerTcpConnection, DragonhowlLevelExperienceData> recipientData, out string reason)
    {
        reason = string.Empty;
        var selected = new HashSet<uint>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, source.Character).ToArray())
        {
            var monster = candidate.Monster;
            if (!geometry.Contains(monster.X, monster.Y) || !CanLegacyPlayerAttackMonster(source.Character, monster, candidate.MonsterFamily, out _)) continue;
            if (!selected.Add(monster.Uid)) { reason = "ownedDragonhowlDuplicateMonsterTarget"; return false; }
            if (IsLegacyGuardMonsterFamily(monster, candidate.MonsterFamily) &&
                !HasLegacyStatusFlag(source.Character, LegacyFlashingNameStatusFlag))
            { reason = "ownedDragonhowlUnjoined:guardFlashingName"; return false; }
            if (HasApplicableDragonhowlNinjaSage(source, context.WorldData!))
            { reason = "ownedDragonhowlUnjoined:ninjaSage"; return false; }
            var scalar = GetLegacyCombatGearTriple17490NonPlayerScalar(spell);
            var damage = CalculateLegacyPlayerMonsterPhysicalSpellDamage(source, context.WorldData, monster, candidate.MonsterFamily, scalar);
            damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(source, spell, damage, LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
            AppendDragonhowlSource(context, ref source, source with { Character = WithCharacterAttribute(source.Character,
                RuntimeLegacyThundercloudSightAttribute, monster.Uid) }, DragonhowlAudience.Owner, [], phases, finals);
            PrepareDragonhowlReceiveFlags(context, metadata, ref source, phases, finals);
            damage = ApplyLegacySobNpcReceiveDamageModifiers(source, damage);
            AppendDragonhowlSource(context, ref source, source with { Character = WithCharacterAttribute(source.Character,
                RuntimeLegacyAttackHitAttribute, "true") }, DragonhowlAudience.Owner, [], phases, finals);
            var target = store.PrepareDragonhowlMonster(owner, source, monster, candidate.MonsterFamily, damage);
            if (target is null) { reason = "ownedDragonhowlMonsterIncarnationUnavailable"; return false; }
            if (!DragonhowlMonsterOutput.TryPrepare(source, monster, candidate.MonsterFamily, damage, out var output))
            { reason = "ownedDragonhowlMonsterOutputPreparationFailed"; return false; }
            calculated[context.Connection] = source;
            if (output.RequiresDeathContinuation)
            {
                if (!AppendDragonhowlMonsterRewardPhase(store, owner, context, target, output, ref source,
                    phases, finals, calculated, recipientData, out reason)) return false;
            }
            else phases.Add(new(context.Connection, source, source, DragonhowlAudience.Owner, [], nonPlayer: target, monsterOutput: output));
            animation.Add(new(monster.Uid, damage, true, MsgAttackEffect.None));
        }
        return true;
    }

    private static bool PrepareDragonhowlSobPhases(WorldSessionStore store, WorldSessionStore.OwnedDragonhowlOwner owner,
        WorldPacketHandlerContext context, LegacyMagicRuntimeSpell spell, DragonhowlGeometry geometry,
        LegacyOwnedPhysicalMetadataWitness metadata, ref WorldSession source, List<DragonhowlPhase> phases,
        Dictionary<ConquerTcpConnection, WorldSession> finals, List<LegacySpellAnimationTarget> animation, out string reason)
    {
        reason = string.Empty;
        var selected = new HashSet<(long Map, long Uid)>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, source.Character).ToArray())
        {
            var birth = candidate.SobNpc;
            if (!geometry.Contains(birth.X, birth.Y)) continue;
            if (!store.TrySelectDragonhowlSob(owner, birth, out var selection) || selection is null)
            { reason = "ownedDragonhowlSobRegisteredBirthUnavailable"; return false; }
            var sob = selection.Current;
            if (!CanLegacySobNpcMagicDamageTarget(context, source.Character, sob, out _)) continue;
            if (!selected.Add((sob.MapId, sob.Id))) { reason = "ownedDragonhowlDuplicateSobTarget"; return false; }
            var gear = source.CombatGear ?? CombatGearSnapshot.Empty;
            if (HasLegacyCombatGearEquippedForClass(gear, source.Character.Class, LegacyCombatGearStoneCrackerItemId) ||
                HasLegacyCombatGearEquippedForClass(gear, source.Character.Class, LegacyCombatGearBloodlustItemId) ||
                HasLegacyCombatGearEquippedForClass(gear, source.Character.Class, LegacyCombatGearTaoist1ItemId) ||
                HasLegacyPirateCombatGearEquipped(source) || HasApplicableDragonhowlNinjaSage(source, context.WorldData!))
            { reason = "ownedDragonhowlUnjoined:sobReceiveGearOrSageOwner"; return false; }
            var scalar = GetLegacyCombatGearTriple17490NonPlayerScalar(spell);
            var damage = CalculateLegacySobNpcPhysicalSpellDamage(source, scalar, context.WorldData, damageMultiplier: 1);
            damage = ApplyLegacyCombatGearWildPhoenixAttackEffect(source, spell, damage, LegacyWildPhoenixDamageTargetKind.MonsterOrSobNpc);
            PrepareDragonhowlReceiveFlags(context, metadata, ref source, phases, finals);
            var hit = WithCharacterAttribute(WithCharacterAttribute(source.Character, RuntimeLegacyThundercloudSightAttribute,
                CheckedUInt32(sob.Id, nameof(sob.Id))), RuntimeLegacyAttackHitAttribute, "true");
            AppendDragonhowlSource(context, ref source, source with { Character = hit }, DragonhowlAudience.Owner, [], phases, finals);
            damage = ApplyLegacySobNpcReceiveDamageModifiers(source, damage);
            var behavior = LegacySobNpcEventRules.GetDeathBehavior(sob, context.WorldData?.StaticStatues);
            var target = store.PrepareDragonhowlSob(owner, source, selection, damage, behavior);
            if (target is null || !target.TryProjectSob(out var after, out var result))
            { reason = "ownedDragonhowlSobIncarnationOrOutputUnavailable"; return false; }
            if (CanPrepareOwnedDragonhowlSobOutput(source, sob))
            {
                if (PrepareOwnedDragonhowlSobOutput(source, sob, after, result) is not { } output)
                { reason = "ownedDragonhowlSobOutputUnavailable"; return false; }
                phases.Add(new(context.Connection, source, source, DragonhowlAudience.Owner, [], nonPlayer: target, sobOutput: output));
            }
            else
            {
                if (!TryPrepareOwnedDragonhowlSobEvent(target, source, sob, after, result, out var change) || change is null ||
                    !store.BindPreparedDragonhowlSobEvent(target, change))
                { reason = "ownedDragonhowlUnjoined:sobEventScoreAndResetOwner"; return false; }
                var eventAfter = change.ActorAfter with { Character = new BoundHitpointsInputs(source.Character, change.ActorAfter.Character).After };
                phases.Add(new(context.Connection, source, eventAfter, DragonhowlAudience.Owner, [], nonPlayer: target, sobEvent: change));
                finals[context.Connection] = source = eventAfter;
            }
            animation.Add(new(CheckedUInt32(sob.Id, nameof(sob.Id)), damage, true, MsgAttackEffect.None));
        }
        return true;
    }

    private static bool HasApplicableDragonhowlNinjaSage(WorldSession source, WorldDataCatalog catalog) =>
        IsLegacyNinjaClass(source.Character.Class) && !HasLegacyStatusFlag(source.Character, LegacyNinjaSageSpeedModeFlag) &&
        TryGetLegacyNinjaEffectiveLevels(source, out var levels) && levels > 0 &&
        TryGetLegacyGouyuImmortalSageMode(catalog, levels, LegacyGouyuImmortalChanceValueIndex, out var chance, out _) && chance > 0;

    private static void AppendDragonhowlSource(WorldPacketHandlerContext context, ref WorldSession source,
        WorldSession after, DragonhowlAudience audience, byte[][] frames, List<DragonhowlPhase> phases,
        Dictionary<ConquerTcpConnection, WorldSession> finals)
    {
        after = after with { Character = new BoundHitpointsInputs(source.Character, after.Character).After };
        phases.Add(new(context.Connection, source, after, audience, frames));
        finals[context.Connection] = source = after;
    }

    private static void PrepareDragonhowlReceiveFlags(WorldPacketHandlerContext context,
        LegacyOwnedPhysicalMetadataWitness metadata, ref WorldSession source, List<DragonhowlPhase> phases,
        Dictionary<ConquerTcpConnection, WorldSession> finals)
    {
        if (context.WorldData is not { } catalog || HasLegacyStatusFlag(source.Character, LegacyCombatGearDragonPierceCritFlag) ||
            !TryResolveLegacyPlayerMagicRuntimeSpell(source, catalog, LegacyCombatGearDragonhowlReceiveSpellId, out var learned, out var spell)) return;
        metadata.RequireRuntimeSpell(spell);
        var character = WithCharacterAttribute(WithCharacterAttribute(WithCharacterAttribute(source.Character,
            RuntimeLegacyCombatGearDragonPierceTortoiseAttribute, spell.DamageOnHuman),
            RuntimeLegacyCombatGearDragonPierceBreakAttribute, spell.Damage2), RuntimeLegacyCombatGearDragonPierceStaminaAttribute, spell.Damage3);
        character = WithLegacySourceAddFlag(character, LegacyCombatGearDragonPierceCritFlag, removeOnDead: true);
        var tortoise = CheckLegacyCombatGearDragonhowlRoll(character, RuntimeLegacyCombatGearDragonhowlTortoiseRollAttribute, "Tortoise");
        character = WithLegacySourceAddFlag(character, tortoise ? LegacyCombatGearDragonPierceTortoiseFlag : LegacyCombatGearDragonPierceStaminaFlag, removeOnDead: true);
        var breakthrough = CheckLegacyCombatGearDragonhowlRoll(character, RuntimeLegacyCombatGearDragonhowlBreakRollAttribute, "Break");
        character = WithLegacySourceAddFlag(character, breakthrough || HasLegacyStatusFlag(character, LegacyCombatGearDragonPierceStaminaFlag)
            ? LegacyCombatGearDragonPierceBreakFlag : LegacyCombatGearDragonPierceStaminaFlag, removeOnDead: true);
        uint uid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var vector = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(uid,
            LegacyPlayerDeathState.CreateStatusFlagVector(character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
        AppendDragonhowlSource(context, ref source, source with { Character = character }, DragonhowlAudience.OwnerVisible, [vector], phases, finals);
        var archive = new List<byte[]>();
        foreach (var (flag, amount) in new[] { (LegacyCombatGearDragonPierceCritFlag, 0U),
            (LegacyCombatGearDragonPierceTortoiseFlag, spell.DamageOnHuman), (LegacyCombatGearDragonPierceBreakFlag, spell.Damage2),
            (LegacyCombatGearDragonPierceStaminaFlag, spell.Damage3) })
            if (HasLegacyStatusFlag(character, flag)) archive.Add(new LegacyMsgUpdatePacket(uid,
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ArchiveSkill, 0, Flag: checked((uint)flag),
                    Time: LegacyCombatGearDragonhowlStatusDuration, Damage: amount, SpellLevel: 0)]).ToFrameBytes());
        AppendDragonhowlSource(context, ref source, source, DragonhowlAudience.Owner, archive.ToArray(), phases, finals);
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(uid, 0, 0, spell.Id,
            (byte)Math.Min(learned.Level, byte.MaxValue), learned.UseSpellSoul, [new(uid, 0, true, MsgAttackEffect.None)]);
        AppendDragonhowlSource(context, ref source, source, DragonhowlAudience.OwnerVisible, animation.ToFrames().ToArray(), phases, finals);
    }
}
