using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyBladeFlurrySpellId = 11_610;
    private const ushort LegacyChainBoltSpellId = 10_309;
    private const ushort LegacyDragonCycloneSpellId = 12_290;
    private const ushort LegacyOmnipotenceSpellId = 12_860;
    private const ushort LegacyThunderRampageSpellId = 15_780;
    private static readonly TimeSpan LegacySpecialXpSkillDefaultDuration = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan LegacyOmnipotenceDuration = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan LegacyThunderRampageDuration = TimeSpan.FromSeconds(40);

    private enum LegacySpecialXpSkillActivationAnimationKind
    {
        None,
        SelfTarget,
        Empty,
    }

    private static async ValueTask<bool> HandleLegacyPlayerSpecialXpSkillMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacySpecialXpSkillDefinition(spell, out var definition))
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        if (definition.RequiresThunderstrikerClass &&
            !IsLegacyThunderstrikerClass(resourceSession.Character.Class))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer interrupted legacy {definition.Name} XP skill after accepted admission " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                    $"class={resourceSession.Character.Class}; required Thunderstriker class changed; " +
                    "accepted accounting retained, no refund or retry authorization.")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyStatusFlag(resourceSession.Character, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            if (await TryHandleLegacyThunderRampageNonXpMagicAsync(
                    context with { Session = resourceSession },
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return true;
            }

            if (await TryHandleLegacySpecialXpSkillNonXpAreaDamageAsync(
                    context with { Session = resourceSession },
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    definition.Name,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return true;
            }

            if (await TryHandleLegacyChainBoltNonXpMagicAsync(
                    context with { Session = resourceSession },
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return true;
            }

            if (await TryHandleLegacyDragonCycloneActiveMagicAsync(
                    context with { Session = resourceSession },
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return true;
            }

            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedMagic(
                        spell.Id,
                        sort: (int)spell.Sort,
                        skillName: definition.Name,
                        reason:
                            $"{definition.Name} XPList activation is modeled, but the non-XPList monster/player/SOB damage branch is not ported yet."))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped legacy {definition.Name} damage branch " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort}; " +
                    "XPList status flag is not active.")
                .ConfigureAwait(false);
            return true;
        }

        if (definition.RestoreStaminaToMaximum)
        {
            if (!TryReadLegacyRuntimeStamina(resourceSession.Character, out var stamina))
            {
                await context.Log.WriteLineAsync(
                        $"WorldServer interrupted legacy {definition.Name} XP restoration after accepted admission: " +
                        "requiredStaminaInvalid; accepted accounting retained, no refund or retry authorization.")
                    .ConfigureAwait(false);
                return true;
            }
            var maximumStamina = HasLegacyFatalCrossHeavenBlessing(resourceSession.Character)
                ? LegacyFatalCrossHeavenBlessingStaminaThreshold
                : LegacyFatalCrossNormalStaminaThreshold;
            if (stamina < maximumStamina)
            {
                resourceSession = await ApplyLegacyStaminaToPlayerAsync(
                        context,
                        context.Connection,
                        resourceSession,
                        maximumStamina,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var hadStatus = HasLegacyStatusFlag(resourceSession.Character, definition.StatusFlag);
        var xpSkillNow = DateTimeOffset.UtcNow;
        var xpListSession = await ApplyLegacyOpenXpSkillAsync(
                context with { Session = resourceSession },
                resourceSession,
                definition.StatusFlag,
                definition.ExpiresAtAttribute,
                xpSkillNow,
                cancellationToken,
                definition.Duration(spell))
            .ConfigureAwait(false);

        if (definition.ActivationAnimationKind != LegacySpecialXpSkillActivationAnimationKind.None)
        {
            var casterUid = CheckedUInt32(xpListSession.Character.Uid, nameof(xpListSession.Character.Uid));
            IReadOnlyList<LegacySpellAnimationTarget> animationTargets =
                definition.ActivationAnimationKind == LegacySpecialXpSkillActivationAnimationKind.SelfTarget
                    ? [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]
                    : [];
            var animation = LegacySpellAnimationPacket.ForPlayerMagic(
                casterUid,
                query.X,
                query.Y,
                spell.Id,
                (byte)Math.Min(playerSpell.Level, byte.MaxValue),
                playerSpell.UseSpellSoul,
                animationTargets);
            await SendPlayerSpellAnimationToVisibleAsync(
                    context with { Session = xpListSession },
                    xpListSession,
                    animation,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                $"WorldServer handled legacy {definition.Name} XPList transition " +
                $"{FormatSession(context with { Session = xpListSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"consumedXpList=True opened{definition.Name}=True refreshed{definition.Name}={hadStatus} " +
                $"expiresAtUtc={xpListSession.Character.Attributes[definition.ExpiresAtAttribute]}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryGetLegacySpecialXpSkillDefinition(
        LegacyMagicRuntimeSpell spell,
        out LegacySpecialXpSkillDefinition definition)
    {
        definition = spell.Sort switch
        {
            LegacyMagicSort.BladeFlurry when spell.Id == LegacyBladeFlurrySpellId =>
                new LegacySpecialXpSkillDefinition(
                    "BladeFlurry",
                    LegacyBladeFlurryFlag,
                    RuntimeLegacyBladeFlurryExpiresAtUtcAttribute,
                    static _ => LegacySpecialXpSkillDefaultDuration,
                    LegacySpecialXpSkillActivationAnimationKind.None,
                    RestoreStaminaToMaximum: false,
                    RequiresThunderstrikerClass: false),
            LegacyMagicSort.ChainBolt when spell.Id == LegacyChainBoltSpellId =>
                new LegacySpecialXpSkillDefinition(
                    "ChainBolt",
                    LegacyChaintBoltFlag,
                    RuntimeLegacyChainBoltExpiresAtUtcAttribute,
                    static _ => LegacySpecialXpSkillDefaultDuration,
                    LegacySpecialXpSkillActivationAnimationKind.None,
                    RestoreStaminaToMaximum: false,
                    RequiresThunderstrikerClass: false),
            LegacyMagicSort.DragonCyclone when spell.Id == LegacyDragonCycloneSpellId =>
                new LegacySpecialXpSkillDefinition(
                    "DragonCyclone",
                    LegacyDragonCycloneFlag,
                    RuntimeLegacyDragonCycloneExpiresAtUtcAttribute,
                    static spell => TimeSpan.FromSeconds(spell.Duration),
                    LegacySpecialXpSkillActivationAnimationKind.SelfTarget,
                    RestoreStaminaToMaximum: false,
                    RequiresThunderstrikerClass: false),
            LegacyMagicSort.Omnipotence when spell.Id == LegacyOmnipotenceSpellId =>
                new LegacySpecialXpSkillDefinition(
                    "Omnipotence",
                    LegacyOmnipotenceFlag,
                    RuntimeLegacyOmnipotenceExpiresAtUtcAttribute,
                    static _ => LegacyOmnipotenceDuration,
                    LegacySpecialXpSkillActivationAnimationKind.None,
                    RestoreStaminaToMaximum: true,
                    RequiresThunderstrikerClass: false),
            LegacyMagicSort.ThunderRampage when spell.Id == LegacyThunderRampageSpellId =>
                new LegacySpecialXpSkillDefinition(
                    "ThunderRampage",
                    LegacyThunderRampageFlag,
                    RuntimeLegacyThunderRampageExpiresAtUtcAttribute,
                    static _ => LegacyThunderRampageDuration,
                    LegacySpecialXpSkillActivationAnimationKind.Empty,
                    RestoreStaminaToMaximum: true,
                    RequiresThunderstrikerClass: true),
            _ => default,
        };

        return !string.IsNullOrEmpty(definition.Name);
    }

    private readonly record struct LegacySpecialXpSkillDefinition(
        string Name,
        int StatusFlag,
        string ExpiresAtAttribute,
        Func<LegacyMagicRuntimeSpell, TimeSpan> Duration,
        LegacySpecialXpSkillActivationAnimationKind ActivationAnimationKind,
        bool RestoreStaminaToMaximum,
        bool RequiresThunderstrikerClass);
}
