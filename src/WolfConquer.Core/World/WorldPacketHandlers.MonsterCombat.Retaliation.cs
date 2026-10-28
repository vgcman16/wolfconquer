using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyMonsterMagicTailedBeastRollAttribute = "LegacyMonsterMagicTailedBeastRoll";

    internal static async ValueTask TrySendLegacyMonsterRetaliationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (IsLegacyAttackingTargetExtraBoss(monster))
        {
            await TrySendLegacyMagicMonsterRetaliationAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    GetLegacyExtraBossSpellId(monster),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (monster.Boss &&
            TryGetLegacyBossSpellTable(monster, monsterFamily, out var bossSpellTable))
        {
            await TrySendLegacyBossSpellRetaliationAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    bossSpellTable,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var spellId = ReadAttributeUInt32(monsterFamily.Attributes, "magic_type");
        if (spellId != 0)
        {
            if (spellId > ushort.MaxValue)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped legacy monster retaliation " +
                        $"{FormatSession(context)} monsterUid={monster.Uid}; magic_type={spellId} exceeds UInt16.")
                    .ConfigureAwait(false);
                return;
            }

            if (monster.Boss)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer ignored legacy boss magic-type retaliation source no-op " +
                        $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                        $"spellId={spellId}; source MsgMonster.ActionHandler.AttackingTarget only dispatches " +
                        "explicit boss family switch cases.")
                    .ConfigureAwait(false);
                return;
            }

            await TrySendLegacyMagicMonsterRetaliationAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    (ushort)spellId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var attackRange = ResolveLegacyAttackingTargetRange(monster, monsterFamily);
        if (attackRange == 0 ||
            LegacyGetDistance(session.Character.X, session.Character.Y, monster.X, monster.Y) > attackRange)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} " +
                    $"distance={LegacyGetDistance(session.Character.X, session.Character.Y, monster.X, monster.Y)} " +
                    $"attackRange={attackRange}.")
                .ConfigureAwait(false);
            return;
        }

        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        if (currentHitpoints == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; target hitpoints are zero.")
                .ConfigureAwait(false);
            return;
        }

        var responseDamage = await TrySendLegacyMonsterResponseDamageAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        session = responseDamage.Session;
        if (responseDamage.InterruptsRetaliation)
        {
            return;
        }

        var damageResult = CalculateLegacyMonsterPhysicalDamage(
            monster,
            monsterFamily,
            session,
            context.WorldData,
            melee: true,
            allowDodge: !monster.Boss);
        var damage = damageResult.Damage;
        if (damage == 0 && !damageResult.SendZeroDamage)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; calculated damage is zero.")
                .ConfigureAwait(false);
            return;
        }

        session = await TryConsumeLegacyHawksAmbitionAsync(
                context,
                session,
                damageResult.ConsumesHawksAmbition,
                cancellationToken)
            .ConfigureAwait(false);
        var shieldResult = await TryApplyLegacyLightningShieldPhysicalMonsterHitAsync(
                context,
                session,
                damageResult,
                cancellationToken)
            .ConfigureAwait(false);
        if (shieldResult is not { } shield) return;
        session = shield.Session;
        damage = damageResult.Dodged ? 0 : shield.Damage;
        var attack = new MsgAttackPacket(new MsgAttackQuery(
            Uid: monster.Uid,
            OpponentUid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            X: CheckedUInt16(session.Character.X, nameof(session.Character.X)),
            Y: CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
            AttackType: MsgAttackType.Physical,
            Damage: checked((int)damage),
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        var published = await SendLegacyPhysicalMonsterInteractionAsync(
                context with { Session = session }, session, session.Character.Uid,
                monster, attack, aroundMonster: true, cancellationToken)
            .ConfigureAwait(false);
        if (published is null) return;
        var receive = await ApplyLegacyPhysicalMonsterHitAsync(
                context with { Session = published }, published, monster, damage, cancellationToken)
            .ConfigureAwait(false);
        if (receive.Session is not { } updatedSession || !receive.Entered) return;
        currentHitpoints = receive.PreviousHitpoints;
        var applied = receive.DamageApplied;
        var killedPlayer = receive.Killed;
        updatedSession = await TrySendLegacyMonsterCounterPunchResponseAsync(
                context with { Session = updatedSession }, updatedSession, monster, monsterFamily, cancellationToken)
            .ConfigureAwait(false);

        if (killedPlayer)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy monster player death " +
                    $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} damage={damage} remainingHitpoints=0 " +
                    FormatLegacyMonsterPhysicalDamageInputs(monster, monsterFamily, damageResult))
                .ConfigureAwait(false);
            return;
        }

        if (damageResult.Dodged)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy monster dodge retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} damage=0 " +
                    $"remainingHitpoints={currentHitpoints} " +
                    FormatLegacyMonsterPhysicalDamageInputs(monster, monsterFamily, damageResult))
                .ConfigureAwait(false);
            return;
        }

        if (damageResult.SendZeroDamage)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy monster zero-damage retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} damage=0 " +
                    $"remainingHitpoints={currentHitpoints} " +
                    FormatLegacyMonsterPhysicalDamageInputs(monster, monsterFamily, damageResult))
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster retaliation " +
                $"{FormatSession(context)} monsterUid={monster.Uid} damage={applied} " +
                $"remainingHitpoints={updatedSession.Character.MinHitPoints} " +
                FormatLegacyMonsterPhysicalDamageInputs(monster, monsterFamily, damageResult))
            .ConfigureAwait(false);
    }

    private static bool IsLegacyAttackingTargetExtraBoss(LegacyMonsterInstance monster)
    {
        return monster.MaxLife > 300_000 &&
            monster.MaxLife < 7_000_000;
    }

    private static uint ResolveLegacyAttackingTargetRange(
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        var attackRange = LegacyMonsterFamilyRuntimeRules.ResolveAttackRange(monsterFamily);
        return monster.Boss && monster.Hitpoints >= 2_000_000U
            ? Math.Max(attackRange, 18U)
            : attackRange;
    }

    private static async ValueTask TrySendLegacyMagicMonsterRetaliationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        ushort spellId,
        CancellationToken cancellationToken,
        LegacyShockCasterOwner? capturedOwner = null)
    {
        if (capturedOwner is not null && !capturedOwner.IsCurrent(context, session)) return;
        var attackRange = ResolveLegacyAttackingTargetRange(monster, monsterFamily);
        if (attackRange == 0 ||
            LegacyGetDistance(session.Character.X, session.Character.Y, monster.X, monster.Y) > attackRange)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} " +
                    $"distance={LegacyGetDistance(session.Character.X, session.Character.Y, monster.X, monster.Y)} " +
                    $"attackRange={attackRange}.")
                .ConfigureAwait(false);
            return;
        }

        var currentHitpoints = CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        if (currentHitpoints == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; target hitpoints are zero.")
                .ConfigureAwait(false);
            return;
        }

        var responseDamage = await TrySendLegacyMonsterResponseDamageAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        session = responseDamage.Session;
        if (capturedOwner is not null && !capturedOwner.IsCurrent(context, session)) return;
        if (responseDamage.InterruptsRetaliation)
        {
            return;
        }

        var damageResult = CalculateLegacyMonsterMagicDamage(monster, monsterFamily, session, context.WorldData);
        var damage = damageResult.Damage;
        if (damage == 0 && !damageResult.SendZeroDamage)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster retaliation " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; calculated damage is zero.")
                .ConfigureAwait(false);
            return;
        }

        session = await TryConsumeLegacyHawksAmbitionAsync(
                context,
                session,
                damageResult.ConsumesHawksAmbition,
                cancellationToken)
            .ConfigureAwait(false);
        if (capturedOwner is not null && !capturedOwner.IsCurrent(context, session)) return;

        var applied = Math.Min(damage, currentHitpoints);
        var killedPlayer = applied >= currentHitpoints;
        var updatedCharacter = applied == 0
            ? session.Character
            : session.Character with
            {
                MinHitPoints = currentHitpoints - applied,
            };
        if (killedPlayer)
        {
            updatedCharacter = ApplyLegacyMonsterPlayerDeath(updatedCharacter, monster);
        }

        var updatedSession = session with { Character = updatedCharacter };
        if (capturedOwner is null) context.Sessions?.Set(context.Connection, updatedSession);
        else if (!capturedOwner.TryPublish(context, session, updatedSession, out updatedSession)) return;

        var effect = LegacyMonsterMagicRetaliation.ResolveTailedBeastEffect(
            session.Character.Attributes,
            context.WorldData?.BeastAttributesByLevel,
            TryReadUInt32Attribute(
                session.Character.Attributes,
                out var forcedTailedBeastRoll,
                RuntimeLegacyMonsterMagicTailedBeastRollAttribute)
                ? forcedTailedBeastRoll
                : null);
        var spell = LegacySpellAnimationPacket.ForMonsterMagic(
            monster.Uid,
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            CheckedUInt16(session.Character.X, nameof(session.Character.X)),
            CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
            spellId,
            damage,
            effect);
        if (capturedOwner is null)
            await SendMonsterSpellAnimationToVisibleAsync(context, updatedSession, monster, spell, cancellationToken)
                .ConfigureAwait(false);
        else if (!await SendLegacyShockRetaliationAnimationAsync(context, updatedSession, monster, spell,
                     capturedOwner, cancellationToken).ConfigureAwait(false)) return;
        if (capturedOwner is not null && !capturedOwner.TryCurrent(context, updatedSession, out updatedSession)) return;

        updatedSession = await TryApplyLegacyMonsterHitActivePickRemovalAsync(
                context,
                updatedSession,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
        if (capturedOwner is not null && !capturedOwner.IsCurrent(context, updatedSession)) return;
        updatedCharacter = updatedSession.Character;

        updatedSession = await TryApplyLegacyMonsterHitSitInterruptionAsync(
                context,
                updatedSession,
                damage,
                killedPlayer,
                cancellationToken)
            .ConfigureAwait(false);
        if (capturedOwner is not null && !capturedOwner.IsCurrent(context, updatedSession)) return;
        updatedCharacter = updatedSession.Character;

        if (applied > 0)
        {
            var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
                updatedCharacter,
                currentHitpoints,
                updatedSession.PlayerItems,
                updatedSession.CombatGear ?? CombatGearSnapshot.Empty,
                context.WorldData,
                updatedSession.AlternateEquipmentActive);
            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                maxHitpoints,
                currentHitpoints - applied))
            {
                if (capturedOwner is not null && !capturedOwner.IsCurrent(context, updatedSession)) return;
                await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                if (capturedOwner is not null && !capturedOwner.TryCurrent(context, updatedSession, out updatedSession)) return;
            }
        }

        updatedSession = await TrySendLegacyMonsterCounterPunchResponseAsync(
                context,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        if (capturedOwner is not null && !capturedOwner.IsCurrent(context, updatedSession)) return;

        if (killedPlayer)
        {
            updatedSession = await SendLegacyOwnedThundercloudDeathCleanupAsync(
                    context,
                    context.Connection,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedCharacter = updatedSession.Character;

            await SendLegacyPlayerDeadTalkAsync(context.Connection, cancellationToken)
                .ConfigureAwait(false);

            var statusUpdate = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                CreateLegacyRuntimeStatusFlagVector(updatedCharacter));
            var statusFrame = statusUpdate.ToFrameBytes();
            await context.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
            if (context.Sessions is { } sessions)
            {
                foreach (var active in sessions.Snapshot())
                {
                    if (ReferenceEquals(active.Connection, context.Connection) ||
                        active.Session.Character.Uid == updatedCharacter.Uid ||
                        !CanSeePlayer(active.Session.Character, updatedCharacter))
                    {
                        continue;
                    }

                    await active.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
                }
            }

            var death = new MsgAttackPacket(new MsgAttackQuery(
                Uid: monster.Uid,
                OpponentUid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                X: CheckedUInt16(session.Character.X, nameof(session.Character.X)),
                Y: CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
                AttackType: MsgAttackType.Death,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0));
            await SendInteractionToVisibleAsync(context, updatedSession, death, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy monster player death " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} damage={damage} remainingHitpoints=0.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy magic monster retaliation " +
                $"{FormatSession(context)} monsterUid={monster.Uid} spellId={spellId} damage={applied} " +
                $"remainingHitpoints={currentHitpoints - applied}.")
            .ConfigureAwait(false);
    }
}
