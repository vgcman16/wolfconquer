namespace WolfConquer.Core.World;

public static partial class LegacyMagicSupportRuntime
{
    private static LegacyMagicSupportOutcome CreateReviveOutcome(
        LegacyMagicSupportSpell spell,
        LegacyMagicSupportActor target,
        string reason)
    {
        return CreateOutcome(
            spell,
            LegacyMagicSupportKind.Revive,
            reason,
            DetachStatusEvidence,
            spellExperienceDamage: spell.Duration,
            targets:
            [
                CreateReviveTargetOutcome(target, reason),
            ],
            pendingRuntimeEffects:
            [
                "Call Player.Revive, including hitpoint, stamina, XPCount, map-status, spawn, and optional QuickMount side effects.",
                "Send MsgSpellAnimation and spell experience update from UpdateSpell.CheckUpdate.",
            ]);
    }

    private static LegacyMagicSupportTargetOutcome CreateReviveTargetOutcome(
        LegacyMagicSupportActor target,
        string reason)
    {
        var newXpCount = target.KeepsXpCountOnRevive ? target.XpCount : (ushort)0;
        return new LegacyMagicSupportTargetOutcome(
            target.Uid,
            target.Kind,
            target.Uid,
            HitpointDelta: target.MaxHitpoints > target.Hitpoints ? target.MaxHitpoints - target.Hitpoints : 0,
            NewHitpoints: target.MaxHitpoints,
            StaminaDelta: target.Stamina < 100 ? checked((ushort)(100 - target.Stamina)) : (ushort)0,
            NewStamina: 100,
            XpCountDelta: 0,
            NewXpCount: newXpCount,
            Damage: 0,
            Hit: true,
            Revived: true,
            RemovedFly: false,
            reason);
    }

    private static LegacyMagicSupportTargetOutcome CreateHitpointOutcome(
        LegacyMagicSupportActor actor,
        uint animationUid,
        uint delta,
        string reason)
    {
        return new LegacyMagicSupportTargetOutcome(
            actor.Uid,
            actor.Kind,
            animationUid,
            delta,
            AddCapped(actor.Hitpoints, delta, actor.MaxHitpoints),
            StaminaDelta: 0,
            NewStamina: actor.Stamina,
            XpCountDelta: 0,
            NewXpCount: actor.XpCount,
            Damage: delta,
            Hit: true,
            Revived: false,
            RemovedFly: false,
            reason);
    }

    private static LegacyMagicSupportTargetOutcome CreateXpOutcome(
        LegacyMagicSupportActor actor,
        string reason)
    {
        var newXpCount = AddSaturating(actor.XpCount, 20);
        return new LegacyMagicSupportTargetOutcome(
            actor.Uid,
            actor.Kind,
            actor.Uid,
            HitpointDelta: 0,
            NewHitpoints: actor.Hitpoints,
            StaminaDelta: 0,
            NewStamina: actor.Stamina,
            XpCountDelta: SubtractFloorZero(newXpCount, actor.XpCount),
            NewXpCount: newXpCount,
            Damage: 0,
            Hit: true,
            Revived: false,
            RemovedFly: false,
            reason);
    }

    private static LegacyMagicSupportOutcome CreateOutcome(
        LegacyMagicSupportSpell spell,
        LegacyMagicSupportKind kind,
        string reason,
        string sourceEvidence,
        uint spellExperienceDamage,
        IReadOnlyList<LegacyMagicSupportTargetOutcome> targets,
        bool consumesXpList = false,
        int removedCloneCount = 0,
        IReadOnlyList<LegacyMagicSupportSummonOutcome>? summons = null,
        IReadOnlyList<string>? pendingRuntimeEffects = null)
    {
        return new LegacyMagicSupportOutcome(
            kind,
            LegacyMagicSupportModelingStatus.Modeled,
            spell.Id,
            spell.Level,
            reason,
            sourceEvidence,
            spellExperienceDamage,
            consumesXpList,
            removedCloneCount,
            targets,
            summons ?? Array.Empty<LegacyMagicSupportSummonOutcome>(),
            pendingRuntimeEffects ?? Array.Empty<string>());
    }

    private static LegacyMagicSupportOutcome UnsupportedOutcome(LegacyMagicSupportSpell spell, string reason)
    {
        return new LegacyMagicSupportOutcome(
            LegacyMagicSupportKind.Unsupported,
            LegacyMagicSupportModelingStatus.Unsupported,
            spell.Id,
            spell.Level,
            reason,
            "No support-helper source branch was selected.",
            SpellExperienceDamage: 0,
            ConsumesXpList: false,
            RemovedCloneCount: 0,
            Targets: Array.Empty<LegacyMagicSupportTargetOutcome>(),
            Summons: Array.Empty<LegacyMagicSupportSummonOutcome>(),
            PendingRuntimeEffects: Array.Empty<string>());
    }
}
