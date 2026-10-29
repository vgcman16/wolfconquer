using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LivePetProjection(
        uint Uid,
        uint Mesh,
        uint PetFlag,
        uint MonsterId,
        byte Level,
        uint Hitpoints,
        uint MaxLife,
        string Name,
        ushort? X = null,
        ushort? Y = null,
        DateTimeOffset? NextAttackAtUtc = null,
        DateTimeOffset? ThunderboltExpiresAtUtc = null);

    private sealed record RaceStaticRoleProjection(
        uint Uid,
        ushort X,
        ushort Y,
        uint Mesh,
        ushort Level,
        uint SetBy,
        bool Viable);

    private sealed record RacePotionSlot(
        int Location,
        LegacyRaceItemType Type,
        ushort Count);

    private sealed record RaceItemPickupResult(
        uint StaticRoleUid,
        LegacyRaceItemType Type,
        LegacyRacePotionPacket? Potion,
        LegacyGameUpdatePacket? GameUpdate);

    private sealed record NewGuildWarItemProjection(
        uint Uid,
        ushort X,
        ushort Y,
        uint ItemId,
        bool Viable,
        string Name);

    private sealed record NewGuildWarItemPickupResult(
        uint Uid,
        ushort X,
        ushort Y,
        uint ItemId,
        uint Points,
        ulong TotalPoints,
        string Name);

    private readonly record struct LegacyNpcDialogOption(string Text, byte OptionId);
    private readonly record struct LegacyNpcTeleportDestination(long MapId, long X, long Y, string SourceMethod);
    private readonly record struct LegacyMonsterDeathDropSelection(
        uint ItemId,
        FloorItemRuntimePickupKind Kind,
        long Amount);
    private readonly record struct LegacyQueenOfEvilClassRewardItem(
        uint ItemId,
        ushort StackSize);
    private readonly record struct LegacyMonsterGeneratedItemBase(ushort Sort, uint Level);
    private readonly record struct LegacyPlayerMonsterMagicTarget(
        LegacyMonsterInstance Monster,
        Monster MonsterFamily);
    private readonly record struct LegacyPlayerSobNpcMagicTarget(SobNpc SobNpc);
    private readonly record struct LegacyPlayerMagicTarget(WorldActiveSession ActiveSession);
    private readonly record struct LegacyPlayerMagicDamageResult(
        WorldActiveSession ActiveSession,
        WorldSession UpdatedSession,
        uint PreviousHitpoints,
        uint DamageApplied,
        bool Killed,
        bool HawksAmbitionConsumed = false,
        uint? LightningShieldActivationLeft = null,
        uint? LightningShieldDrainLeft = null,
        uint LightningShieldSpellLevel = 0,
        bool DuelCleanupApplied = false,
        bool AttackerAbsolutionConsumed = false,
        bool TargetAbsolutionConsumed = false,
        WorldSession? UpdatedAttackerSession = null,
        bool BloodTideActivated = false,
        uint BloodTideHitpoints = 0,
        uint BloodTideDuration = 0,
        byte BloodTideSpellLevel = 0,
        bool ReverseMagicApplied = false,
        bool CapriceStaminaTransferred = false,
        bool CapriceXpListRemoved = false,
        bool XpBoosterApplied = false,
        bool XpBoosterDepleted = false,
        bool XpKillerApplied = false,
        bool TidalWaveApplied = false,
        byte TidalWaveValue = 0,
        bool QuenchApplied = false,
        byte QuenchValue = 0,
        bool AttackHitApplied = false,
        bool TargetNeptuneCurseBlocked = false,
        bool AttackerNeptuneCurseCapped = false,
        bool AttackerTransformFinished = false,
        LegacyPlayerReceiveTournamentOutcome TournamentOutcome = default,
        LegacyPlayerReceiveMirrorOfSinResult MirrorOfSinResult = default,
        LegacyPlayerReceiveLightOfStaminaResult LightOfStaminaResult = default,
        LegacyPlayerReceiveCalmWindResult CalmWindResult = default,
        LegacyPlayerReceiveDivineGuardResult DivineGuardResult = default,
        LegacyPlayerReceiveKillingFlashResult KillingFlashResult = default,
        LegacyPlayerReceiveDrainingTouchResult DrainingTouchResult = default,
        LegacyPlayerReceiveBloodSpawnResult BloodSpawnResult = default,
        LegacyPlayerReceiveResponseEffectsResult ResponseEffectsResult = default,
        LegacyPlayerReceiveInfiniteMistResult InfiniteMistResult = default,
        uint? ReceiveDamage = null,
        bool NormalDeathPhasesPublished = false,
        bool EarlyModifierPhasesPublished = false,
        WorldSessionStore.CapricePairReceipt? CapriceReceipt = null);
    private readonly record struct LegacyBossPlayerDamageResult(
        WorldActiveSession ActiveSession,
        WorldSession UpdatedSession,
        uint PreviousHitpoints,
        uint Damage,
        uint DamageApplied,
        bool Killed);
    private readonly record struct LegacyBossStatusApplyResult(
        WorldSession Session,
        bool Applied);
    private readonly record struct LegacyBossAoeSpellTargeting(
        ushort SelectionOriginX,
        ushort SelectionOriginY,
        ushort AnimationX,
        ushort AnimationY,
        long Radius);
    private readonly record struct LegacyMoveLineGuildWarEndpointCheck(
        bool Applies,
        bool Allowed,
        int CurrentTerrainMask,
        int NextTerrainMask,
        int FloorType,
        bool LeftGateOpen,
        bool RightGateOpen,
        ushort X,
        ushort Y);

    private readonly record struct LegacyMonsterResponseDamageResult(
        WorldSession Session,
        bool Matched,
        bool InterruptsRetaliation);

    private static MonsterDamageResult ApplyLegacyPlayerMonsterDamage(
        MonsterRuntimeStateStore monsterStates,
        LegacyMonsterInstance monster,
        uint requestedDamage,
        DateTimeOffset now,
        WorldSession attacker)
    {
        var attackerName = !string.IsNullOrWhiteSpace(attacker.Character.Name)
            ? attacker.Character.Name
            : attacker.Username;
        if (string.IsNullOrWhiteSpace(attackerName))
        {
            return monsterStates.ApplyDamage(monster, requestedDamage, now);
        }

        return monsterStates.ApplyDamage(
            monster,
            requestedDamage,
            now,
            CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid)),
            attackerName);
    }

}
