namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyIronShieldDamageCompareAndSetAttempts = 4;

    private readonly record struct LegacyIronShieldDamageAbsorptionResult(
        WorldActiveSession Target,
        uint Damage);

    private static async ValueTask<LegacyIronShieldDamageAbsorptionResult>
        ApplyLegacyPlayerPlayerPhysicalSpellIronShieldAsync(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        WorldSession attacker,
        uint incomingDamage,
        CancellationToken cancellationToken)
    {
        var sessions = context.Sessions;
        var currentSession = target.Session;
        if (sessions?.TryGet(target.Connection, out var storedSession) == true && storedSession is not null)
        {
            currentSession = storedSession;
        }

        for (var attempt = 0; attempt < LegacyIronShieldDamageCompareAndSetAttempts; attempt++)
        {
            target = target with { Session = currentSession };
            if (!HasLegacyStatusFlag(currentSession.Character, LegacyIronShieldFlag))
            {
                return new LegacyIronShieldDamageAbsorptionResult(target, incomingDamage);
            }

            var shieldDamage = ReadAttributeUInt32(
                currentSession.Character.Attributes,
                RuntimeLegacyIronShieldDamageAttribute);
            var absorbedDamage = Math.Min(incomingDamage, shieldDamage);
            var shieldBroken = incomingDamage > shieldDamage;
            var shieldDamageLeft = shieldBroken ? 0U : shieldDamage - incomingDamage;
            var outgoingDamage = shieldBroken ? incomingDamage - shieldDamage : 1U;
            var updatedCharacter = shieldBroken
                ? WithoutCharacterAttribute(
                    WithoutCharacterAttribute(
                        WithoutCharacterAttribute(
                            WithoutLegacyStatusFlag(currentSession.Character, LegacyIronShieldFlag),
                            RuntimeLegacyIronShieldExpiresAtUtcAttribute),
                        RuntimeLegacyIronShieldDamageAttribute),
                    RuntimeLegacyIronShieldRemoveOnDeadAttribute)
                : WithCharacterAttribute(
                    currentSession.Character,
                    RuntimeLegacyIronShieldDamageAttribute,
                    shieldDamageLeft);
            var updatedSession = currentSession with { Character = updatedCharacter };
            if (sessions is null ||
                sessions.TryCompareAndSet(target.Connection, currentSession, updatedSession))
            {
                var updatedTarget = target with { Session = updatedSession };
                await SendLegacyIronShieldBlueDamageToVisibleAsync(
                        context,
                        updatedTarget,
                        attacker,
                        absorbedDamage,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (shieldBroken)
                {
                    await SendPlayerStatusFlagVectorToVisibleAsync(
                            context,
                            updatedTarget.Connection,
                            updatedSession,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy IronShield player-hit absorption " +
                        $"attackerUid={attacker.Character.Uid} targetUid={updatedSession.Character.Uid} " +
                        $"incomingDamage={incomingDamage} absorbedDamage={absorbedDamage} " +
                        $"shieldDamageLeft={shieldDamageLeft} shieldBroken={shieldBroken}; " +
                        "source=Physical.OnPlayer IronShield/AzureShield.CreateDmg.")
                    .ConfigureAwait(false);
                return new LegacyIronShieldDamageAbsorptionResult(updatedTarget, outgoingDamage);
            }

            if (!sessions.TryGet(target.Connection, out var latestSession) || latestSession is null)
            {
                break;
            }

            currentSession = latestSession;
        }

        target = target with { Session = currentSession };
        await context.Log.WriteLineAsync(
                "WorldServer skipped legacy IronShield player-hit absorption " +
                $"targetUid={currentSession.Character.Uid}; " +
                "WorldSessionStore.TryCompareAndSet repeatedly rejected the shield transition.")
            .ConfigureAwait(false);
        return new LegacyIronShieldDamageAbsorptionResult(target, incomingDamage);
    }

    private static async ValueTask SendLegacyIronShieldBlueDamageToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        WorldSession attacker,
        uint absorbedDamage,
        CancellationToken cancellationToken)
    {
        var targetCharacter = target.Session.Character;
        var packet = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid)),
            OpponentUid: CheckedUInt32(targetCharacter.Uid, nameof(targetCharacter.Uid)),
            X: CheckedUInt16(targetCharacter.X, nameof(targetCharacter.X)),
            Y: CheckedUInt16(targetCharacter.Y, nameof(targetCharacter.Y)),
            AttackType: MsgAttackType.BlueDamage,
            Damage: CheckedInt32(absorbedDamage, nameof(absorbedDamage)),
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                target.Connection,
                target.Session,
                packet.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
