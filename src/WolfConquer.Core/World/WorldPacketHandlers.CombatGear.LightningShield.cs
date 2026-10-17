using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyLightningShieldActivatedFlag = 0xED;

    private const int LegacyLightningShieldFlag = 0xF0;

    private const string RuntimeLegacyLightningShieldRollAttribute = "LegacyLightningShieldRoll";

    private const string RuntimeLegacyLightningShieldLeftAttribute = "LightningShieldLeft";


    private static async ValueTask<LegacyLightningShieldMonsterHitResult?>
        TryApplyLegacyLightningShieldPhysicalMonsterHitAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyMonsterPhysicalDamageResult damageResult,
            CancellationToken cancellationToken)
    {
        var damage = damageResult.Damage;
        var ownerUid = session.Character.Uid;
        if (!TryRefreshLegacyMonsterLightningShieldSubject(context, ownerUid, ref session)) return null;
        if (!damageResult.AllowsLightningShield)
        {
            return new LegacyLightningShieldMonsterHitResult(session, damageResult.Damage);
        }

        var spell = session.PlayerSpells.Spells.FirstOrDefault(spell => spell.Id == 15_750);
        var spellLevel = spell is null ? 0U : spell.Level;
        if (HasLegacyStatusFlag(session.Character, LegacyLightningShieldFlag) &&
            !HasLegacyStatusFlag(session.Character, LegacyLightningShieldActivatedFlag) &&
            spell is not null &&
            CheckLegacyLightningShieldRateRoll(session.Character))
        {
            var shieldLeft = HasLegacyStatusFlag(session.Character, LegacyThunderRampageFlag)
                ? checked((uint)(20_000 + spell.Level + 1_000))
                : 20_000U;
            shieldLeft = checked(shieldLeft + GetLegacyFlashShieldBonus(session.Character));
            session = session with
            {
                Character = WithCharacterAttribute(
                    LegacyLightningShieldActivationLifetime.CaptureFirstInsertion(session.Character,
                        WithLegacySourceAddFlag(session.Character, LegacyLightningShieldActivatedFlag, removeOnDead: true),
                        (context.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime),
                    RuntimeLegacyLightningShieldLeftAttribute,
                    shieldLeft),
            };
            context.Sessions?.Set(context.Connection, session);
            var published = await SendLegacyMonsterLightningShieldInsertionAsync(
                    context, session, ownerUid, cancellationToken)
                .ConfigureAwait(false);
            if (published is null) return null;
            session = published;
            await SendLegacyLightningShieldAzureUpdateAsync(
                    context,
                    session,
                    ReadAttributeUInt32(session.Character.Attributes, RuntimeLegacyLightningShieldLeftAttribute),
                    spellLevel,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyMonsterLightningShieldSubject(context, ownerUid, ref session)) return null;
        }

        var remaining = ReadAttributeUInt32(session.Character.Attributes, RuntimeLegacyLightningShieldLeftAttribute);
        if (HasLegacyStatusFlag(session.Character, LegacyLightningShieldActivatedFlag) && remaining != 0)
        {
            if (damage >= remaining)
            {
                damage -= remaining;
                remaining = 0;
            }
            else
            {
                remaining -= damage;
                damage = 0;
            }

            session = session with
            {
                Character = WithCharacterAttribute(
                    session.Character,
                    RuntimeLegacyLightningShieldLeftAttribute,
                    remaining),
            };
            context.Sessions?.Set(context.Connection, session);

            var currentShieldSpell = session.PlayerSpells.Spells.FirstOrDefault(learned => learned.Id == 15_750);
            var currentShieldLevel = currentShieldSpell is null ? spellLevel : currentShieldSpell.Level;
            await SendLegacyLightningShieldAzureUpdateAsync(
                    context,
                    session,
                    remaining,
                    currentShieldLevel,
                    cancellationToken,
                    wireTime: LegacyLightningShieldActivationLifetime.RemainingSeconds(session.Character,
                        (context.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime))
                .ConfigureAwait(false);
            if (!TryRefreshLegacyMonsterLightningShieldSubject(context, ownerUid, ref session)) return null;
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy LightningShield monster-hit absorption " +
                    $"{FormatSession(context with { Session = session })} shieldLeft={remaining} damage={damage}; " +
                    "source=MsgMonster.ActionHandler.PhysicalAttack LightningShieldActivated.")
                .ConfigureAwait(false);
        }

        return new LegacyLightningShieldMonsterHitResult(session, damage);
    }


    private static async ValueTask SendLegacyLightningShieldAzureUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint shieldLeft,
        uint spellLevel,
        CancellationToken cancellationToken,
        uint wireTime = 10)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AzureShield,
                    Value: 0,
                    Flag: LegacyLightningShieldActivatedFlag,
                    Time: wireTime,
                    Damage: shieldLeft,
                    SpellLevel: spellLevel),
            ]);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
    }


    private static bool CheckLegacyLightningShieldRateRoll(CharacterSnapshot character)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyLightningShieldRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < 20;
    }
}
