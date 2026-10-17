using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacySkillTournamentTypeName = "SkillTournament";
    private const string LegacyFreezeWarTypeName = "FreezeWar";
    private const string LegacyFootballTypeName = "FootBall";
    private const string LegacyDragonWarTypeName = "DragonWar";
    private const uint LegacyFreezeWarFreezeSeconds = 2_592_000;
    private const uint LegacyFootballBallSeconds = 2_592_000;
    private const uint LegacyDragonSwingSeconds = 2_592_000;
    private const int LegacyFootballBallFlag = 0xAB;
    private const int LegacyDragonSwingFlag = 0xA0;
    private const string RuntimeLegacyFreezeWarExpiresAtUtcAttribute = "LegacyFreezeWarExpiresAtUtc";
    private const string RuntimeLegacyFootballBallExpiresAtUtcAttribute = "LegacyFootballBallExpiresAtUtc";
    private const string RuntimeLegacyDragonSwingExpiresAtUtcAttribute = "LegacyDragonSwingExpiresAtUtc";
    private const string RuntimeLegacySkillTournamentLifesAttribute = "SkillTournamentLifes";
    private const string RuntimeLegacyTournamentKillsAttribute = "TournamentKills";
    private const string RuntimeLegacyDragonWarKillSystemUidAttribute = "DragonWarKillSystemUid";
    private const string RuntimeLegacyDragonWarKillSystemFlagAttribute = "DragonWarKillSystemFlag";
    private const string RuntimeLegacyDragonWarKillSystemDeadAttribute = "DragonWarKillSystemDead";
    private const long LegacyDragonWarKillSystemConquerPointsReward = 10_000;

    private enum LegacyPlayerReceiveTournamentOutcomeKind
    {
        None = 0,
        SkillTournamentLifeLost = 1,
        SkillTournamentDeath = 2,
        FreezeWarFrozen = 3,
        FreezeWarThawed = 4,
        FreezeWarNoChange = 5,
        FootballNoBall = 6,
        FootballPassedBall = 7,
        DragonWarNoSwing = 8,
        DragonWarTargetHit = 9,
        DragonWarTargetKilled = 10,
        DragonWarAttackerSwingKilled = 11,
    }

    private readonly record struct LegacyPlayerReceiveTournamentOutcome(
        LegacyPlayerReceiveTournamentOutcomeKind Kind,
        uint SkillTournamentLifesLeft = 0,
        LegacyDragonWarKillSystemResult DragonWarKillSystemResult = default)
    {
        public bool IsActive => Kind != LegacyPlayerReceiveTournamentOutcomeKind.None;

        public bool SendsTargetStatusVector =>
            Kind is LegacyPlayerReceiveTournamentOutcomeKind.FreezeWarFrozen
                or LegacyPlayerReceiveTournamentOutcomeKind.FreezeWarThawed
                or LegacyPlayerReceiveTournamentOutcomeKind.FootballPassedBall
                or LegacyPlayerReceiveTournamentOutcomeKind.DragonWarTargetKilled;

        public bool SendsAttackerStatusVector =>
            Kind is LegacyPlayerReceiveTournamentOutcomeKind.FootballPassedBall
                or LegacyPlayerReceiveTournamentOutcomeKind.DragonWarTargetKilled;

        public bool SendsSkillTournamentDeathEffect =>
            Kind == LegacyPlayerReceiveTournamentOutcomeKind.SkillTournamentDeath;
    }

    private enum LegacyDragonWarKillSystemFlag : byte
    {
        None = 0,
        FirstBloodKill = 1,
        DoubleKill = 2,
        TripleKill = 3,
        UnStoppable = 4,
    }

    private readonly record struct LegacyDragonWarKillSystemResult(
        bool RewardGranted,
        bool DeadMarked,
        string FlagName,
        byte FlagValue,
        long ConquerPointsReward,
        long AnnouncedConquerPoints,
        string SoundPath);

    private static bool TryApplyLegacyPlayerReceiveTournamentRuntime(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        WorldSession targetSession,
        WorldSession? attackerSession,
        LegacyPlayerReceiveDamageModifierResult modifierResult,
        uint previousHitpoints,
        out LegacyPlayerMagicDamageResult result)
    {
        result = default;
        if (previousHitpoints != 0 &&
            IsLegacyPlayerReceiveFootballAttackerActive(modifierResult.Attacker))
        {
            result = ApplyLegacyPlayerReceiveFootballRuntime(
                context,
                target,
                targetSession,
                attackerSession,
                modifierResult,
                previousHitpoints);
            return true;
        }

        if (previousHitpoints == 0 ||
            !IsLegacyPlayerReceiveTournamentAttackerActive(modifierResult.Attacker, out var tournamentType))
        {
            return false;
        }

        if (IsLegacyPlayerReceiveTournamentType(tournamentType, LegacySkillTournamentTypeName))
        {
            result = ApplyLegacyPlayerReceiveSkillTournamentRuntime(
                context,
                target,
                targetSession,
                attackerSession,
                modifierResult,
                previousHitpoints);
            return true;
        }

        if (IsLegacyPlayerReceiveTournamentType(tournamentType, LegacyFreezeWarTypeName))
        {
            result = ApplyLegacyPlayerReceiveFreezeWarRuntime(
                context,
                target,
                targetSession,
                attackerSession,
                modifierResult,
                previousHitpoints);
            return true;
        }

        if (IsLegacyPlayerReceiveTournamentType(tournamentType, LegacyDragonWarTypeName))
        {
            result = ApplyLegacyPlayerReceiveDragonWarRuntime(
                context,
                target,
                targetSession,
                attackerSession,
                modifierResult,
                previousHitpoints);
            return true;
        }

        return false;
    }

    private static LegacyPlayerMagicDamageResult ApplyLegacyPlayerReceiveSkillTournamentRuntime(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        WorldSession targetSession,
        WorldSession? attackerSession,
        LegacyPlayerReceiveDamageModifierResult modifierResult,
        uint previousHitpoints)
    {
        var targetCharacter = targetSession.Character;
        var lifes = ReadLegacyPlayerReceiveTournamentUInt32(
            targetCharacter.Attributes,
            out var lifesKey,
            RuntimeLegacySkillTournamentLifesAttribute,
            "LegacySkillTournamentLifes",
            "SkillTournamentLives",
            "LegacySkillTournamentLives");
        if (lifes != 0)
        {
            var updatedTarget = WithCharacterAttribute(targetCharacter, lifesKey, lifes - 1);
            var updatedTargetSession = targetSession with { Character = updatedTarget };
            context.Sessions?.Set(target.Connection, updatedTargetSession);

            return CreateLegacyPlayerMagicDamageResult(
                target with { Session = updatedTargetSession },
                updatedTargetSession,
                previousHitpoints,
                damageApplied: 0,
                killed: false,
                modifierResult,
                attackerSession,
                new LegacyPlayerReceiveTournamentOutcome(
                    LegacyPlayerReceiveTournamentOutcomeKind.SkillTournamentLifeLost,
                    lifes - 1));
        }

        var updatedAttacker = IncrementLegacyPlayerReceiveTournamentKills(
            context,
            attackerSession,
            modifierResult.Attacker,
            out var updatedAttackerSession);
        var deadTarget = ApplyLegacyPlayerPlayerDeath(targetCharacter, updatedAttacker);
        var deadTargetSession = targetSession with { Character = deadTarget };
        context.Sessions?.Set(target.Connection, deadTargetSession);

        return CreateLegacyPlayerMagicDamageResult(
            target with { Session = deadTargetSession },
            deadTargetSession,
            previousHitpoints,
            previousHitpoints,
            killed: true,
            modifierResult,
            updatedAttackerSession,
            new LegacyPlayerReceiveTournamentOutcome(
                LegacyPlayerReceiveTournamentOutcomeKind.SkillTournamentDeath));
    }

    private static LegacyPlayerMagicDamageResult ApplyLegacyPlayerReceiveFreezeWarRuntime(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        WorldSession targetSession,
        WorldSession? attackerSession,
        LegacyPlayerReceiveDamageModifierResult modifierResult,
        uint previousHitpoints)
    {
        var attackerGarmentId = GetLegacyPlayerReceiveTournamentGarmentId(
            attackerSession,
            modifierResult.Attacker);
        var targetGarmentId = GetLegacyPlayerMagicGarmentId(targetSession);
        var targetFrozen = HasLegacyStatusFlag(targetSession.Character, LegacyFreezeFlag);
        var outcomeKind = LegacyPlayerReceiveTournamentOutcomeKind.FreezeWarNoChange;
        var updatedTarget = targetSession.Character;

        if (attackerGarmentId != targetGarmentId && !targetFrozen)
        {
            updatedTarget = WithCharacterAttribute(
                WithLegacySourceAddFlag(updatedTarget, LegacyFreezeFlag, removeOnDead: true),
                RuntimeLegacyFreezeWarExpiresAtUtcAttribute,
                (DateTimeOffset.UtcNow.ToUniversalTime() + TimeSpan.FromSeconds(LegacyFreezeWarFreezeSeconds))
                    .ToString("O", CultureInfo.InvariantCulture));
            outcomeKind = LegacyPlayerReceiveTournamentOutcomeKind.FreezeWarFrozen;
        }
        else if (attackerGarmentId == targetGarmentId && targetFrozen)
        {
            updatedTarget = WithoutCharacterAttribute(
                WithoutLegacyStatusFlag(updatedTarget, LegacyFreezeFlag),
                RuntimeLegacyFreezeWarExpiresAtUtcAttribute);
            outcomeKind = LegacyPlayerReceiveTournamentOutcomeKind.FreezeWarThawed;
        }

        var updatedTargetSession = targetSession with { Character = updatedTarget };
        context.Sessions?.Set(target.Connection, updatedTargetSession);

        return CreateLegacyPlayerMagicDamageResult(
            target with { Session = updatedTargetSession },
            updatedTargetSession,
            previousHitpoints,
            damageApplied: 0,
            killed: false,
            modifierResult,
            attackerSession,
            new LegacyPlayerReceiveTournamentOutcome(outcomeKind));
    }

    private static LegacyPlayerMagicDamageResult ApplyLegacyPlayerReceiveFootballRuntime(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        WorldSession targetSession,
        WorldSession? attackerSession,
        LegacyPlayerReceiveDamageModifierResult modifierResult,
        uint previousHitpoints)
    {
        var targetHasBall = HasLegacyStatusFlag(targetSession.Character, LegacyFootballBallFlag);
        var outcomeKind = targetHasBall
            ? LegacyPlayerReceiveTournamentOutcomeKind.FootballPassedBall
            : LegacyPlayerReceiveTournamentOutcomeKind.FootballNoBall;
        var updatedTarget = targetHasBall
            ? WithoutCharacterAttribute(
                WithoutLegacyStatusFlag(targetSession.Character, LegacyFootballBallFlag),
                RuntimeLegacyFootballBallExpiresAtUtcAttribute)
            : targetSession.Character;
        var updatedTargetSession = targetSession with { Character = updatedTarget };
        context.Sessions?.Set(target.Connection, updatedTargetSession);

        var updatedAttackerSession = attackerSession;
        if (targetHasBall && attackerSession is not null)
        {
            var expiresAt = (DateTimeOffset.UtcNow.ToUniversalTime() +
                    TimeSpan.FromSeconds(LegacyFootballBallSeconds))
                .ToString("O", CultureInfo.InvariantCulture);
            var updatedAttacker = WithCharacterAttribute(
                WithLegacyStatusFlag(modifierResult.Attacker, LegacyFootballBallFlag),
                RuntimeLegacyFootballBallExpiresAtUtcAttribute,
                expiresAt);
            updatedAttackerSession = attackerSession with { Character = updatedAttacker };
            context.Sessions?.Set(context.Connection, updatedAttackerSession);
        }

        return CreateLegacyPlayerMagicDamageResult(
            target with { Session = updatedTargetSession },
            updatedTargetSession,
            previousHitpoints,
            damageApplied: 0,
            killed: false,
            modifierResult,
            updatedAttackerSession,
            new LegacyPlayerReceiveTournamentOutcome(outcomeKind));
    }

    private static LegacyPlayerMagicDamageResult ApplyLegacyPlayerReceiveDragonWarRuntime(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        WorldSession targetSession,
        WorldSession? attackerSession,
        LegacyPlayerReceiveDamageModifierResult modifierResult,
        uint previousHitpoints)
    {
        var targetCharacter = targetSession.Character;
        var attackerCharacter = modifierResult.Attacker;
        var targetHasDragonSwing = HasLegacyStatusFlag(targetCharacter, LegacyDragonSwingFlag);
        var attackerHasDragonSwing = HasLegacyStatusFlag(attackerCharacter, LegacyDragonSwingFlag);
        var outcomeKind = LegacyPlayerReceiveTournamentOutcomeKind.DragonWarNoSwing;
        var dragonWarKillSystemResult = default(LegacyDragonWarKillSystemResult);
        var damageApplied = 0u;
        var killed = false;

        if (targetHasDragonSwing)
        {
            var targetHits = ReadLegacyPlayerReceiveTournamentUInt32(
                targetCharacter.Attributes,
                out var hitsKey,
                "DragonWarHits",
                "LegacyDragonWarHits");
            if (targetHits == 0)
            {
                attackerCharacter = ApplyLegacyDragonWarKillSystemCheckDead(
                    attackerCharacter,
                    out dragonWarKillSystemResult);
                _ = ReadLegacyPlayerReceiveTournamentUInt32(
                    attackerCharacter.Attributes,
                    out var attackerHitsKey,
                    "DragonWarHits",
                    "LegacyDragonWarHits");
                attackerCharacter = AddLegacyPlayerReceiveTournamentScore(attackerCharacter, 20);
                var hadDragonSwing = HasLegacyStatusFlag(attackerCharacter, LegacyDragonSwingFlag);
                attackerCharacter = WithLegacySourceAddFlag(attackerCharacter, LegacyDragonSwingFlag, removeOnDead: true);
                if (!hadDragonSwing)
                    attackerCharacter = WithCharacterAttribute(attackerCharacter,
                        RuntimeLegacyDragonSwingExpiresAtUtcAttribute,
                        (DateTimeOffset.UtcNow.ToUniversalTime() + TimeSpan.FromSeconds(LegacyDragonSwingSeconds))
                            .ToString("O", CultureInfo.InvariantCulture));
                attackerCharacter = WithCharacterAttribute(attackerCharacter, attackerHitsKey, 5u);
                targetCharacter = WithoutLegacyStatusFlag(
                    ApplyLegacyPlayerPlayerDeath(targetCharacter, attackerCharacter),
                    LegacyDragonSwingFlag);
                targetCharacter = WithoutCharacterAttribute(
                    targetCharacter,
                    RuntimeLegacyDragonSwingExpiresAtUtcAttribute);
                outcomeKind = LegacyPlayerReceiveTournamentOutcomeKind.DragonWarTargetKilled;
                damageApplied = previousHitpoints;
                killed = true;
            }
            else
            {
                attackerCharacter = AddLegacyPlayerReceiveTournamentScore(attackerCharacter, 5);
                targetCharacter = WithCharacterAttribute(targetCharacter, hitsKey, targetHits - 1);
                outcomeKind = LegacyPlayerReceiveTournamentOutcomeKind.DragonWarTargetHit;
            }
        }
        else if (attackerHasDragonSwing)
        {
            attackerCharacter = ApplyLegacyDragonWarKillSystemUpdate(
                attackerCharacter,
                out dragonWarKillSystemResult);
            attackerCharacter = AddLegacyPlayerReceiveTournamentScore(attackerCharacter, 15);
            targetCharacter = ApplyLegacyPlayerPlayerDeath(targetCharacter, attackerCharacter);
            outcomeKind = LegacyPlayerReceiveTournamentOutcomeKind.DragonWarAttackerSwingKilled;
            damageApplied = previousHitpoints;
            killed = true;
        }

        var updatedTargetSession = targetSession with { Character = targetCharacter };
        context.Sessions?.Set(target.Connection, updatedTargetSession);

        var updatedAttackerSession = attackerSession;
        if (attackerSession is not null && !attackerCharacter.Equals(attackerSession.Character))
        {
            updatedAttackerSession = attackerSession with { Character = attackerCharacter };
            context.Sessions?.Set(context.Connection, updatedAttackerSession);
        }

        return CreateLegacyPlayerMagicDamageResult(
            target with { Session = updatedTargetSession },
            updatedTargetSession,
            previousHitpoints,
            damageApplied,
            killed,
            modifierResult with { Attacker = attackerCharacter },
            updatedAttackerSession,
            new LegacyPlayerReceiveTournamentOutcome(
                outcomeKind,
                DragonWarKillSystemResult: dragonWarKillSystemResult));
    }

    private static CharacterSnapshot ApplyLegacyDragonWarKillSystemCheckDead(
        CharacterSnapshot attacker,
        out LegacyDragonWarKillSystemResult result)
    {
        result = default;
        var uid = ReadLegacyPlayerReceiveTournamentUInt64(
            attacker.Attributes,
            out _,
            RuntimeLegacyDragonWarKillSystemUidAttribute,
            "LegacyDragonWarKillSystemUid");
        if (uid != checked((ulong)attacker.Uid))
        {
            return attacker;
        }

        var updated = WithCharacterAttribute(
            attacker,
            ResolveLegacyDragonWarKillSystemAttributeKey(
                attacker.Attributes,
                RuntimeLegacyDragonWarKillSystemDeadAttribute,
                "LegacyDragonWarKillSystemDead"),
            "true");
        result = new LegacyDragonWarKillSystemResult(
            RewardGranted: false,
            DeadMarked: true,
            FlagName: string.Empty,
            FlagValue: 0,
            ConquerPointsReward: 0,
            AnnouncedConquerPoints: 0,
            SoundPath: string.Empty);
        return updated;
    }

    private static CharacterSnapshot ApplyLegacyDragonWarKillSystemUpdate(
        CharacterSnapshot attacker,
        out LegacyDragonWarKillSystemResult result)
    {
        result = default;
        var uid = ReadLegacyPlayerReceiveTournamentUInt64(
            attacker.Attributes,
            out var uidKey,
            RuntimeLegacyDragonWarKillSystemUidAttribute,
            "LegacyDragonWarKillSystemUid");
        var updated = attacker;
        if (uid == 0)
        {
            uid = checked((ulong)attacker.Uid);
            updated = WithCharacterAttribute(updated, uidKey, uid);
        }

        var dead = ReadAttributeBool(
            updated.Attributes,
            RuntimeLegacyDragonWarKillSystemDeadAttribute,
            "LegacyDragonWarKillSystemDead");
        var flag = ReadLegacyDragonWarKillSystemFlag(updated.Attributes, out var flagKey);
        if (uid != checked((ulong)attacker.Uid) ||
            dead ||
            flag >= LegacyDragonWarKillSystemFlag.UnStoppable)
        {
            return updated;
        }

        var nextFlag = (LegacyDragonWarKillSystemFlag)((byte)flag + 1);
        var reward = LegacyDragonWarKillSystemConquerPointsReward;
        var announced = reward * (byte)nextFlag;
        updated = updated with { ConquerPoints = checked(updated.ConquerPoints + reward) };
        updated = WithCharacterAttribute(updated, flagKey, nextFlag.ToString());
        result = new LegacyDragonWarKillSystemResult(
            RewardGranted: true,
            DeadMarked: false,
            FlagName: nextFlag.ToString(),
            FlagValue: (byte)nextFlag,
            ConquerPointsReward: reward,
            AnnouncedConquerPoints: announced,
            SoundPath: GetLegacyDragonWarKillSystemSound(nextFlag));
        return updated;
    }

    private static LegacyDragonWarKillSystemFlag ReadLegacyDragonWarKillSystemFlag(
        IReadOnlyDictionary<string, string> attributes,
        out string key)
    {
        key = ResolveLegacyDragonWarKillSystemAttributeKey(
            attributes,
            RuntimeLegacyDragonWarKillSystemFlagAttribute,
            "LegacyDragonWarKillSystemFlag");
        if (!attributes.TryGetValue(key, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return LegacyDragonWarKillSystemFlag.None;
        }

        if (Enum.TryParse<LegacyDragonWarKillSystemFlag>(raw, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) &&
            Enum.IsDefined(typeof(LegacyDragonWarKillSystemFlag), numeric)
            ? (LegacyDragonWarKillSystemFlag)numeric
            : LegacyDragonWarKillSystemFlag.None;
    }

    private static string ResolveLegacyDragonWarKillSystemAttributeKey(
        IReadOnlyDictionary<string, string> attributes,
        string primary,
        string legacy)
    {
        if (attributes.ContainsKey(primary))
        {
            return primary;
        }

        return attributes.ContainsKey(legacy) ? legacy : primary;
    }

    private static string GetLegacyDragonWarKillSystemSound(LegacyDragonWarKillSystemFlag flag)
    {
        return flag switch
        {
            LegacyDragonWarKillSystemFlag.FirstBloodKill => "sound/fc1.wav",
            LegacyDragonWarKillSystemFlag.DoubleKill => "sound/fc2.wav",
            LegacyDragonWarKillSystemFlag.TripleKill => "sound/fc3.wav",
            LegacyDragonWarKillSystemFlag.UnStoppable => "sound/fc4.wav",
            _ => "none",
        };
    }

    private static async ValueTask SendLegacyDragonWarKillSystemRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession attackerSession,
        LegacyDragonWarKillSystemResult result,
        CancellationToken cancellationToken)
    {
        if (!result.RewardGranted)
        {
            return;
        }

        var soundFrame = new LegacyStringPacket(
                Uid: 0,
                LegacyStringId.Sound,
                [result.SoundPath, "1"])
            .ToFrameBytes();
        var messageFrame = LegacyMsgMessagePacket
            .SystemTalk(
                $"[{result.FlagName}]{attackerSession.Character.Name} received " +
                $"{result.AnnouncedConquerPoints.ToString(CultureInfo.InvariantCulture)} ConquerPoints.")
            .ToFrameBytes();

        await SendLegacyDragonWarTournamentMapFrameAsync(
                context,
                attackerSession,
                soundFrame,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyDragonWarTournamentMapFrameAsync(
                context,
                attackerSession,
                messageFrame,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyDragonWarTournamentMapFrameAsync(
        WorldPacketHandlerContext context,
        WorldSession subjectSession,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        var subject = subjectSession.Character;
        var subjectDynamicId = ReadCharacterDynamicId(subject);
        foreach (var active in sessions.Snapshot())
        {
            var character = active.Session.Character;
            if (character.MapId != subject.MapId ||
                ReadCharacterDynamicId(character) != subjectDynamicId)
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static LegacyPlayerMagicDamageResult CreateLegacyPlayerMagicDamageResult(
        WorldActiveSession activeSession,
        WorldSession updatedSession,
        uint previousHitpoints,
        uint damageApplied,
        bool killed,
        LegacyPlayerReceiveDamageModifierResult modifierResult,
        WorldSession? updatedAttackerSession,
        LegacyPlayerReceiveTournamentOutcome tournamentOutcome = default,
        LegacyPlayerReceiveMirrorOfSinResult mirrorOfSinResult = default,
        LegacyPlayerReceiveLightOfStaminaResult lightOfStaminaResult = default,
        LegacyPlayerReceiveCalmWindResult calmWindResult = default,
        LegacyPlayerReceiveDivineGuardResult divineGuardResult = default,
        LegacyPlayerReceiveKillingFlashResult killingFlashResult = default,
        LegacyPlayerReceiveDrainingTouchResult drainingTouchResult = default,
        LegacyPlayerReceiveBloodSpawnResult bloodSpawnResult = default,
        bool attackerTransformFinished = false,
        LegacyPlayerReceiveResponseEffectsResult responseEffectsResult = default,
        LegacyPlayerReceiveInfiniteMistResult infiniteMistResult = default)
    {
        return new LegacyPlayerMagicDamageResult(
            activeSession,
            updatedSession,
            previousHitpoints,
            damageApplied,
            killed,
            modifierResult.HawksAmbitionConsumed,
            modifierResult.LightningShieldActivationLeft,
            modifierResult.LightningShieldDrainLeft,
            modifierResult.LightningShieldSpellLevel,
            modifierResult.DuelCleanupApplied,
            modifierResult.AttackerAbsolutionConsumed,
            modifierResult.TargetAbsolutionConsumed,
            updatedAttackerSession,
            modifierResult.BloodTideActivated,
            modifierResult.BloodTideHitpoints,
            modifierResult.BloodTideDuration,
            modifierResult.BloodTideSpellLevel,
            modifierResult.ReverseMagicApplied,
            modifierResult.CapriceStaminaTransferred,
            modifierResult.CapriceXpListRemoved,
            modifierResult.XpBoosterApplied,
            modifierResult.XpBoosterDepleted,
            modifierResult.XpKillerApplied,
            modifierResult.TidalWaveApplied,
            modifierResult.TidalWaveValue,
            modifierResult.QuenchApplied,
            modifierResult.QuenchValue,
            modifierResult.AttackHitApplied,
            modifierResult.TargetNeptuneCurseBlocked,
            modifierResult.AttackerNeptuneCurseCapped,
            attackerTransformFinished,
            tournamentOutcome,
            mirrorOfSinResult,
            lightOfStaminaResult,
            calmWindResult,
            divineGuardResult,
            killingFlashResult,
            drainingTouchResult,
            bloodSpawnResult,
            responseEffectsResult,
            infiniteMistResult);
    }

    private static bool IsLegacyPlayerReceiveTournamentAttackerActive(
        CharacterSnapshot attacker,
        out string tournamentType)
    {
        tournamentType = string.Empty;
        if (!ReadAttributeBool(
                attacker.Attributes,
                "LegacyTournamentInTournament",
                "TournamentInTournament",
                "InTournament"))
        {
            return false;
        }

        tournamentType = ReadAttributeString(
            attacker.Attributes,
            "LegacyTournamentType",
            "TournamentType",
            "CurrentTournamentType");
        var process = ReadAttributeString(
            attacker.Attributes,
            "LegacyTournamentProcess",
            "TournamentProcess",
            "CurrentTournamentProcess");

        return IsLegacyPlayerReceiveTournamentProcessAlive(process);
    }

    private static bool IsLegacyPlayerReceiveTournamentProcessAlive(string process)
    {
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("TournamentProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process.EndsWith(".Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static bool IsLegacyPlayerReceiveFootballAttackerActive(CharacterSnapshot attacker)
    {
        var tournamentType = ReadAttributeString(
            attacker.Attributes,
            "LegacyTournamentType",
            "TournamentType",
            "CurrentTournamentType");
        return attacker.MapId == 3_844 &&
            IsLegacyPlayerReceiveTournamentType(tournamentType, LegacyFootballTypeName);
    }

    private static bool IsLegacyPlayerReceiveTournamentType(string actual, string expected)
    {
        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
            actual.EndsWith("." + expected, StringComparison.OrdinalIgnoreCase);
    }

    private static uint ReadLegacyPlayerReceiveTournamentUInt32(
        IReadOnlyDictionary<string, string> attributes,
        out string key,
        params string[] keys)
    {
        var value = ReadLegacyPlayerReceiveTournamentUInt64(attributes, out key, keys);
        return value > uint.MaxValue ? 0 : (uint)value;
    }

    private static ulong ReadLegacyPlayerReceiveTournamentUInt64(
        IReadOnlyDictionary<string, string> attributes,
        out string key,
        params string[] keys)
    {
        foreach (var candidate in keys)
        {
            if (attributes.TryGetValue(candidate, out var raw) &&
                ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                key = candidate;
                return parsed;
            }
        }

        key = keys.Length == 0 ? string.Empty : keys[0];
        return 0;
    }

    private static CharacterSnapshot AddLegacyPlayerReceiveTournamentScore(
        CharacterSnapshot character,
        ulong value)
    {
        var score = ReadLegacyPlayerReceiveTournamentUInt64(
            character.Attributes,
            out var scoreKey,
            "DragonWarScore",
            "LegacyDragonWarScore");
        var updatedScore = ulong.MaxValue - score < value ? ulong.MaxValue : score + value;
        return WithCharacterAttribute(character, scoreKey, updatedScore);
    }

    private static CharacterSnapshot IncrementLegacyPlayerReceiveTournamentKills(
        WorldPacketHandlerContext context,
        WorldSession? attackerSession,
        CharacterSnapshot attacker,
        out WorldSession? updatedAttackerSession)
    {
        var kills = ReadLegacyPlayerReceiveTournamentUInt64(
            attacker.Attributes,
            out var killsKey,
            RuntimeLegacyTournamentKillsAttribute,
            "LegacyTournamentKills");
        var updatedKills = kills == ulong.MaxValue ? ulong.MaxValue : kills + 1;
        var updatedAttacker = WithCharacterAttribute(attacker, killsKey, updatedKills);

        updatedAttackerSession = null;
        if (attackerSession is not null)
        {
            updatedAttackerSession = attackerSession with { Character = updatedAttacker };
            context.Sessions?.Set(context.Connection, updatedAttackerSession);
        }

        return updatedAttacker;
    }

    private static uint GetLegacyPlayerReceiveTournamentGarmentId(
        WorldSession? session,
        CharacterSnapshot character)
    {
        if (session is not null)
        {
            return GetLegacyPlayerMagicGarmentId(session with { Character = character });
        }

        var specialGarment = ReadAttributeUInt64(character.Attributes, "SpecialGarment", "GarmentId", "GarmentID");
        return checked((uint)Math.Min(specialGarment, uint.MaxValue));
    }
}
